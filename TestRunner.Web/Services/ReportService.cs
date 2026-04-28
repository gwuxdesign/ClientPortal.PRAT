using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using TestRunner.Web.Models;

namespace TestRunner.Web.Services;

public class ReportService : IReportService
{
    private readonly string _reportsDirectory;
    private readonly int _maxReports;
    private readonly string _indexPath;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public ReportService(IConfiguration configuration)
    {
        _reportsDirectory = configuration["TestRunner:ReportsDirectory"]
            ?? throw new InvalidOperationException("TestRunner:ReportsDirectory is not configured.");
        _maxReports = int.TryParse(configuration["TestRunner:MaxReports"], out var max) ? max : 20;
        _indexPath = Path.Combine(_reportsDirectory, "reports-index.json");
        Directory.CreateDirectory(_reportsDirectory);
    }

    public string GetReportsDirectory() => _reportsDirectory;

    public async Task<string> GenerateReportAsync(TestRunReport report, string runId)
    {
        var runFolder = Path.Combine(_reportsDirectory, runId);
        Directory.CreateDirectory(runFolder);

        var html = BuildHtml(report, runId);
        var reportPath = Path.Combine(runFolder, report.ReportFileName);
        await File.WriteAllTextAsync(reportPath, html, Encoding.UTF8);
        await UpdateIndexAsync(report, runId);
        await RotateReportsAsync();
        return reportPath;
    }

    public async Task<List<ReportIndexEntry>> GetReportIndexAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_indexPath))
                return new List<ReportIndexEntry>();

            var json = await File.ReadAllTextAsync(_indexPath);
            return JsonSerializer.Deserialize<List<ReportIndexEntry>>(json)
                   ?? new List<ReportIndexEntry>();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task UpdateIndexAsync(TestRunReport report, string runId)
    {
        await _lock.WaitAsync();
        try
        {
            var index = new List<ReportIndexEntry>();
            if (File.Exists(_indexPath))
            {
                var json = await File.ReadAllTextAsync(_indexPath);
                index = JsonSerializer.Deserialize<List<ReportIndexEntry>>(json)
                        ?? new List<ReportIndexEntry>();
            }

            index.Insert(0, new ReportIndexEntry
            {
                Id = report.Id,
                RunId = runId,
                RunAt = report.RunAt,
                RunBy = report.RunBy,
                Browser = report.Browser,
                Device = report.Device,
                Environment = report.Environment,
                Suite = report.Suite,
                TotalPassed = report.TotalPassed,
                TotalFailed = report.TotalFailed,
                TotalSkipped = report.TotalSkipped,
                DurationDisplay = FormatDuration(report.TotalDuration),
                ReportFileName = report.ReportFileName,
                RunFolder = runId
            });

            await File.WriteAllTextAsync(
                _indexPath,
                JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task RotateReportsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_indexPath)) return;

            var json = await File.ReadAllTextAsync(_indexPath);
            var index = JsonSerializer.Deserialize<List<ReportIndexEntry>>(json)
                        ?? new List<ReportIndexEntry>();

            while (index.Count > _maxReports)
            {
                var oldest = index[^1];
                index.RemoveAt(index.Count - 1);

                var runFolder = Path.Combine(_reportsDirectory, oldest.RunFolder);
                if (Directory.Exists(runFolder))
                    Directory.Delete(runFolder, recursive: true);
            }

            await File.WriteAllTextAsync(
                _indexPath,
                JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            _lock.Release();
        }
    }

    public static TestRunReport ParseTrx(
        string trxPath,
        string consoleOutput,
        TestRunRequest request,
        string reportFileName,
        string runFolder)
    {
        var id = Path.GetFileNameWithoutExtension(reportFileName);
        var report = new TestRunReport
        {
            Id = id,
            RunAt = DateTime.Now,
            RunBy = request.RunnerName,
            Browser = request.Browser,
            Device = request.Device,
            Environment = request.Environment,
            Suite = request.Suite,
            Headed = request.Headed,
            RecordVideo = request.RecordVideo,
            ReportFileName = reportFileName,
            TrxFileName = Path.GetFileName(trxPath)
        };

        if (!File.Exists(trxPath))
            return report;

        var doc = XDocument.Load(trxPath);
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

        var times = doc.Descendants(ns + "Times").FirstOrDefault();
        if (times != null &&
            DateTime.TryParse(times.Attribute("start")?.Value, out var start) &&
            DateTime.TryParse(times.Attribute("finish")?.Value, out var finish))
        {
            report.RunAt = start;
            report.TotalDuration = finish - start;
        }

        var counters = doc.Descendants(ns + "Counters").FirstOrDefault();
        if (counters != null)
        {
            report.TotalPassed = int.TryParse(counters.Attribute("passed")?.Value, out var p) ? p : 0;
            report.TotalFailed = int.TryParse(counters.Attribute("failed")?.Value, out var f) ? f : 0;
            report.TotalSkipped = int.TryParse(counters.Attribute("notExecuted")?.Value, out var s) ? s : 0;
        }

        var stepsByScenario = ParseStepsFromConsole(consoleOutput);
        var scenarioFiles = ParseScenarioFileNames(consoleOutput);
        var rawResults = doc.Descendants(ns + "UnitTestResult").ToList();
        var executionOrder = BuildExecutionOrder(consoleOutput, rawResults, ns, doc);

        var orderedResults = rawResults
            .OrderBy(r => executionOrder.TryGetValue(
                r.Attribute("testName")?.Value ?? "", out var pos) ? pos : int.MaxValue)
            .ToList();

        for (var i = 0; i < orderedResults.Count; i++)
        {
            var result = orderedResults[i];

            var name = result.Attribute("testName")?.Value ?? "Unknown";
            var outcome = result.Attribute("outcome")?.Value ?? "Unknown";
            var duration = TimeSpan.TryParse(result.Attribute("duration")?.Value, out var d)
                ? d : TimeSpan.Zero;

            var errorInfo = result.Descendants(ns + "ErrorInfo").FirstOrDefault();
            var errorMessage = errorInfo?.Element(ns + "Message")?.Value;
            var stackTrace = errorInfo?.Element(ns + "StackTrace")?.Value;

            var matchedFileName = i < scenarioFiles.Count
                ? scenarioFiles[i]
                : SanitiseFileName(name);

            var traceFile = Path.Combine(runFolder, $"{matchedFileName}.zip");
            var videoFile = Path.Combine(runFolder, $"{matchedFileName}.webm");

            var scenario = new TestScenarioResult
            {
                Name = name,
                Outcome = outcome,
                Duration = duration,
                ErrorMessage = errorMessage,
                StackTrace = stackTrace,
                TraceFile = File.Exists(traceFile) ? $"{matchedFileName}.zip" : null,
                VideoFile = File.Exists(videoFile) ? $"{matchedFileName}.webm" : null,
                Steps = stepsByScenario.TryGetValue(name, out var steps)
                               ? steps : new List<string>()
            };

            report.Scenarios.Add(scenario);
        }

        return report;
    }

    private static Dictionary<string, int> BuildExecutionOrder(
        string consoleOutput,
        List<XElement> results,
        XNamespace ns,
        XDocument doc)
    {
        var executionTitles = new List<string>();
        foreach (var line in consoleOutput.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("SCENARIO NAME: "))
                executionTitles.Add(trimmed["SCENARIO NAME: ".Length..].Trim());
        }

        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var claimed = new HashSet<int>();

        foreach (var result in results)
        {
            var testName = result.Attribute("testName")?.Value ?? "";
            var parenIdx = testName.IndexOf('(');
            var methodName = parenIdx >= 0 ? testName[..parenIdx] : testName;
            var methodNorm = Normalise(methodName);

            for (var i = 0; i < executionTitles.Count; i++)
            {
                if (claimed.Contains(i)) continue;

                var titleNorm = Normalise(executionTitles[i]);
                if (methodNorm.Equals(titleNorm, StringComparison.OrdinalIgnoreCase))
                {
                    if (ArgumentsMatch(testName, executionTitles[i], consoleOutput, i))
                    {
                        order[testName] = i;
                        claimed.Add(i);
                        break;
                    }
                }
            }
        }

        return order;
    }

    private static string Normalise(string input)
    {
        var sb = new StringBuilder();
        foreach (var c in input)
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    private static bool ArgumentsMatch(
        string trxTestName,
        string scenarioTitle,
        string consoleOutput,
        int executionIndex)
    {
        var parenOpen = trxTestName.IndexOf('(');
        if (parenOpen < 0) return true;

        var argsPart = trxTestName[(parenOpen + 1)..].TrimEnd(')');
        var trxArgs = ParseArgs(argsPart);

        if (trxArgs.Count >= 2)
            trxArgs = trxArgs.Take(trxArgs.Count - 2).ToList();

        if (trxArgs.Count == 0) return true;

        var fileLines = new List<string>();
        foreach (var line in consoleOutput.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("SCENARIO FILE: "))
                fileLines.Add(trimmed["SCENARIO FILE: ".Length..].Trim());
        }

        if (executionIndex >= fileLines.Count) return false;

        var fileName = fileLines[executionIndex];
        foreach (var arg in trxArgs)
        {
            var sanitisedArg = SanitiseFileName(arg);
            if (!fileName.Contains(sanitisedArg, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static List<string> ParseArgs(string argsPart)
    {
        var args = new List<string>();
        var depth = 0;
        var current = new StringBuilder();

        foreach (var c in argsPart)
        {
            if (c == '"') depth = depth == 0 ? 1 : 0;
            else if (c == ',' && depth == 0)
            {
                args.Add(current.ToString().Trim().Trim('"'));
                current.Clear();
                continue;
            }
            current.Append(c);
        }

        if (current.Length > 0)
            args.Add(current.ToString().Trim().Trim('"'));

        return args;
    }

    private static List<string> ParseScenarioFileNames(string consoleOutput)
    {
        var result = new List<string>();

        foreach (var line in consoleOutput.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("SCENARIO FILE: ")) continue;

            var fileName = trimmed["SCENARIO FILE: ".Length..].Trim();
            if (result.Count == 0 || result[^1] != fileName)
                result.Add(fileName);
        }

        return result;
    }

    private static Dictionary<string, List<string>> ParseStepsFromConsole(string consoleOutput)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? currentScenario = null;

        foreach (var line in consoleOutput.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("SCENARIO START: "))
            {
                currentScenario = trimmed["SCENARIO START: ".Length..].Trim();
                result[currentScenario] = new List<string>();
            }
            else if (trimmed.StartsWith("STEP START: ") && currentScenario != null)
            {
                result[currentScenario].Add(trimmed["STEP START: ".Length..].Trim());
            }
        }

        return result;
    }

    private static string SanitiseFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        name = name.Replace('"', '_')
                   .Replace('\'', '_')
                   .Replace(',', '_')
                   .Replace('(', '_')
                   .Replace(')', '_')
                   .Replace(' ', '_');

        while (name.Contains("__"))
            name = name.Replace("__", "_");

        name = name.Trim('_');

        return name;
    }

    private static string FormatDuration(TimeSpan d)
    {
        if (d.TotalSeconds < 60) return $"{d.TotalSeconds:F1}s";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes}m {d.Seconds}s";
        return $"{(int)d.TotalHours}h {d.Minutes}m {d.Seconds}s";
    }

    private string BuildHtml(TestRunReport report, string runId)
    {
        var sortedScenarios = report.Scenarios
            .OrderByDescending(s => s.Duration)
            .ToList();
        var slowest = sortedScenarios.FirstOrDefault();
        var sb = new StringBuilder();

        sb.AppendLine($$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<title>Test Report — {{report.Suite}} — {{report.RunAt:dd MMM yyyy HH:mm}}</title>
<script>
  // Inherit theme from localStorage so report matches app theme
  (function() {
    var theme = localStorage.getItem('prat-theme') || 'dark';
    document.documentElement.setAttribute('data-theme', theme);
  })();
</script>
<style>
  :root[data-theme="dark"] {
    --bg: #0f1117; --bg2: #1a1d27; --bg3: #22263a;
    --border: #2e3250; --text: #e8eaf6; --text2: #9096b8;
    --pass: #4caf7d; --fail: #f05454; --skip: #f0a154;
    --pass-bg: rgba(76,175,125,0.12); --fail-bg: rgba(240,84,84,0.12); --skip-bg: rgba(240,161,84,0.12);
    --accent: #7c83ff;
  }
  :root[data-theme="light"] {
    --bg: #f4f6fb; --bg2: #ffffff; --bg3: #edf0fa;
    --border: #d0d5ea; --text: #1a1d2e; --text2: #5a607a;
    --pass: #2e7d52; --fail: #c62828; --skip: #b35c00;
    --pass-bg: rgba(46,125,82,0.1); --fail-bg: rgba(198,40,40,0.1); --skip-bg: rgba(179,92,0,0.1);
    --accent: #4a52d9;
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { background: var(--bg); color: var(--text); font-family: 'Segoe UI', system-ui, sans-serif; font-size: 14px; line-height: 1.6; }
  .container { max-width: 1200px; margin: 0 auto; padding: 24px 20px; }
  header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 28px; }
  .header-title h1 { font-size: 22px; font-weight: 700; color: var(--text); }
  .header-title p { color: var(--text2); margin-top: 4px; font-size: 13px; }
  .back-link { background: var(--bg3); border: 1px solid var(--border); color: var(--text2); padding: 6px 14px; border-radius: 20px; font-size: 13px; text-decoration: none; transition: all 0.2s; }
  .back-link:hover { color: var(--text); border-color: var(--accent); }
  .summary-bar { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 16px; margin-bottom: 24px; }
  .summary-card { background: var(--bg2); border: 1px solid var(--border); border-radius: 10px; padding: 16px 20px; }
  .summary-card .label { font-size: 11px; text-transform: uppercase; letter-spacing: 0.08em; color: var(--text2); margin-bottom: 6px; }
  .summary-card .value { font-size: 26px; font-weight: 700; }
  .summary-card.pass .value { color: var(--pass); }
  .summary-card.fail .value { color: var(--fail); }
  .summary-card.skip .value { color: var(--skip); }
  .summary-card.neutral .value { color: var(--accent); font-size: 20px; }
  .params-section { background: var(--bg2); border: 1px solid var(--border); border-radius: 10px; padding: 20px; margin-bottom: 24px; }
  .params-section h2 { font-size: 14px; font-weight: 600; margin-bottom: 14px; color: var(--text2); text-transform: uppercase; letter-spacing: 0.06em; }
  .params-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 12px; }
  .param-item .param-label { font-size: 11px; color: var(--text2); text-transform: uppercase; letter-spacing: 0.06em; }
  .param-item .param-value { font-size: 15px; font-weight: 600; margin-top: 2px; }
  .badge { display: inline-block; padding: 2px 10px; border-radius: 12px; font-size: 12px; font-weight: 600; }
  .badge-pass { background: var(--pass-bg); color: var(--pass); }
  .badge-fail { background: var(--fail-bg); color: var(--fail); }
  .badge-skip { background: var(--skip-bg); color: var(--skip); }
  .results-section { background: var(--bg2); border: 1px solid var(--border); border-radius: 10px; overflow: hidden; margin-bottom: 24px; }
  .results-section h2 { font-size: 14px; font-weight: 600; color: var(--text2); text-transform: uppercase; letter-spacing: 0.06em; padding: 16px 20px; border-bottom: 1px solid var(--border); }
  table { width: 100%; border-collapse: collapse; }
  thead th { padding: 10px 16px; text-align: left; font-size: 11px; text-transform: uppercase; letter-spacing: 0.06em; color: var(--text2); background: var(--bg3); border-bottom: 1px solid var(--border); }
  tbody tr { border-bottom: 1px solid var(--border); transition: background 0.15s; }
  tbody tr:last-child { border-bottom: none; }
  tbody tr:hover { background: var(--bg3); }
  tbody td { padding: 12px 16px; vertical-align: top; }
  .scenario-name { font-weight: 500; }
  .slowest-tag { background: var(--bg3); color: var(--accent); font-size: 10px; padding: 1px 7px; border-radius: 8px; margin-left: 8px; font-weight: 600; }
  .duration { color: var(--text2); font-variant-numeric: tabular-nums; white-space: nowrap; }
  .expand-btn { background: none; border: 1px solid var(--border); color: var(--text2); padding: 3px 10px; border-radius: 6px; cursor: pointer; font-size: 12px; transition: all 0.15s; }
  .expand-btn:hover { border-color: var(--accent); color: var(--accent); }
  .detail-row td { padding: 0 16px 16px 16px; }
  .detail-row { display: none; }
  .detail-row.open { display: table-row; }
  .detail-inner { background: var(--bg3); border-radius: 8px; padding: 14px 16px; }
  .detail-inner h4 { font-size: 12px; color: var(--text2); text-transform: uppercase; letter-spacing: 0.06em; margin-bottom: 8px; margin-top: 12px; }
  .detail-inner h4:first-child { margin-top: 0; }
  .error-msg { color: var(--fail); font-size: 13px; white-space: pre-wrap; }
  .stack-trace { color: var(--text2); font-family: 'Cascadia Code', 'Fira Code', monospace; font-size: 12px; white-space: pre-wrap; word-break: break-all; max-height: 300px; overflow-y: auto; }
  .steps-list { list-style: none; }
  .steps-list li { padding: 3px 0; font-size: 13px; color: var(--text2); }
  .steps-list li::before { content: '→ '; color: var(--accent); }
  .artifact-link { display: inline-flex; align-items: center; gap: 5px; background: var(--bg2); border: 1px solid var(--border); color: var(--accent); padding: 4px 12px; border-radius: 6px; font-size: 12px; text-decoration: none; transition: all 0.15s; }
  .artifact-link:hover { border-color: var(--accent); background: var(--bg3); }
  video { width: 100%; border-radius: 6px; margin-top: 8px; background: #000; max-height: 400px; }
  footer { text-align: center; color: var(--text2); font-size: 12px; padding-top: 16px; border-top: 1px solid var(--border); }
</style>
</head>
<body>
<div class="container">
  <header>
    <div class="header-title">
      <h1>Test Report — {{report.Suite}} Suite</h1>
      <p>{{report.RunAt:dddd dd MMMM yyyy}} at {{report.RunAt:HH:mm:ss}} &nbsp;·&nbsp; Run by {{report.RunBy}}</p>
    </div>
    <div style="display:flex;gap:10px;align-items:center;">
      <a class="back-link" href="/reports">← All Reports</a>
      <button class="back-link" onclick="toggleTheme()" style="cursor:pointer;border:1px solid var(--border);">☀ / ◑</button>
    </div>
  </header>

  <div class="summary-bar">
    <div class="summary-card pass">
      <div class="label">✅ Passed</div>
      <div class="value">{{report.TotalPassed}}</div>
    </div>
    <div class="summary-card fail">
      <div class="label">❌ Failed</div>
      <div class="value">{{report.TotalFailed}}</div>
    </div>
    <div class="summary-card skip">
      <div class="label">⏭ Skipped</div>
      <div class="value">{{report.TotalSkipped}}</div>
    </div>
    <div class="summary-card neutral">
      <div class="label">⏱ Duration</div>
      <div class="value">{{FormatDuration(report.TotalDuration)}}</div>
    </div>
    {{(slowest != null ? $"""
    <div class="summary-card neutral">
      <div class="label">🏆 Slowest Test</div>
      <div class="value">{FormatDuration(slowest.Duration)}</div>
    </div>
    """ : "")}}
  </div>

  <div class="params-section">
    <h2>Run Parameters</h2>
    <div class="params-grid">
      <div class="param-item"><div class="param-label">Browser</div><div class="param-value">{{report.Browser}}</div></div>
      <div class="param-item"><div class="param-label">Device</div><div class="param-value">{{report.Device}}</div></div>
      <div class="param-item"><div class="param-label">Environment</div><div class="param-value">{{report.Environment}}</div></div>
      <div class="param-item"><div class="param-label">Suite</div><div class="param-value">{{report.Suite}}</div></div>
      <div class="param-item"><div class="param-label">Mode</div><div class="param-value">{{(report.Headed ? "Headed" : "Headless")}}</div></div>
      <div class="param-item"><div class="param-label">Video</div><div class="param-value">{{(report.RecordVideo ? "Enabled" : "Disabled")}}</div></div>
    </div>
  </div>

  <div class="results-section">
    <h2>Scenario Results ({{sortedScenarios.Count}} total)</h2>
    <table>
      <thead>
        <tr>
          <th>#</th>
          <th>Scenario</th>
          <th>Duration</th>
          <th>Status</th>
          <th>Detail</th>
        </tr>
      </thead>
      <tbody>
""");

        for (var i = 0; i < sortedScenarios.Count; i++)
        {
            var s = sortedScenarios[i];
            var isSlowest = slowest != null && s.Name == slowest.Name;
            var badgeClass = s.Outcome == "Passed" ? "badge-pass" : s.Outcome == "Failed" ? "badge-fail" : "badge-skip";
            var badgeIcon = s.Outcome == "Passed" ? "✅ Passed" : s.Outcome == "Failed" ? "❌ Failed" : "⏭ Skipped";
            var rowId = $"detail-{i}";
            var hasDetail = s.ErrorMessage != null || s.Steps.Count > 0 || s.TraceFile != null || s.VideoFile != null;

            sb.AppendLine($"""
        <tr>
          <td style="color:var(--text2)">{i + 1}</td>
          <td class="scenario-name">{HtmlEncode(s.Name)}{(isSlowest ? "<span class=\"slowest-tag\">SLOWEST</span>" : "")}</td>
          <td class="duration">{FormatDuration(s.Duration)}</td>
          <td><span class="badge {badgeClass}">{badgeIcon}</span></td>
          <td>{(hasDetail ? $"<button class=\"expand-btn\" onclick=\"toggleDetail('{rowId}')\">▶ Details</button>" : "—")}</td>
        </tr>
""");

            if (hasDetail)
            {
                sb.AppendLine($"""
        <tr class="detail-row" id="{rowId}">
          <td colspan="5">
            <div class="detail-inner">
""");
                if (s.Steps.Count > 0)
                {
                    sb.AppendLine("              <h4>Steps</h4><ul class=\"steps-list\">");
                    foreach (var step in s.Steps)
                        sb.AppendLine($"                <li>{HtmlEncode(step)}</li>");
                    sb.AppendLine("              </ul>");
                }
                if (s.ErrorMessage != null)
                    sb.AppendLine($"              <h4>Error</h4><div class=\"error-msg\">{HtmlEncode(s.ErrorMessage)}</div>");
                if (s.StackTrace != null)
                    sb.AppendLine($"              <h4>Stack Trace</h4><div class=\"stack-trace\">{HtmlEncode(s.StackTrace)}</div>");
                if (s.TraceFile != null || s.VideoFile != null)
                {
                    sb.AppendLine("              <h4>Artifacts</h4>");
                    if (s.TraceFile != null)
                        sb.AppendLine($"              <div><a class=\"artifact-link\" href=\"/reports/artifact/{runId}/{s.TraceFile}\" download>📦 Download Trace</a></div>");
                    if (s.VideoFile != null)
                    {
                        sb.AppendLine($"""
              <video controls preload="metadata">
                <source src="/reports/artifact/{runId}/{s.VideoFile}" type="video/webm" />
                Your browser does not support the video tag.
              </video>
""");
                    }
                }
                sb.AppendLine("""
            </div>
          </td>
        </tr>
""");
            }
        }

        sb.AppendLine($$"""
      </tbody>
    </table>
  </div>

  <footer>
    Report generated {{DateTime.Now:dd MMM yyyy HH:mm:ss}} &nbsp;·&nbsp; TestRunner &nbsp;·&nbsp; Run ID: {{runId}}
  </footer>
</div>
<script>
  function toggleDetail(id) {
    const row = document.getElementById(id);
    const btn = row.previousElementSibling.querySelector('.expand-btn');
    row.classList.toggle('open');
    btn.textContent = row.classList.contains('open') ? '▼ Details' : '▶ Details';
  }

  function toggleTheme() {
    const current = document.documentElement.getAttribute('data-theme') || 'dark';
    const next = current === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', next);
    localStorage.setItem('prat-theme', next);
  }
</script>
</body>
</html>
""");

        return sb.ToString();
    }

    private static string HtmlEncode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}