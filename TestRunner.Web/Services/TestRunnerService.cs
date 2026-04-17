using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using TestRunner.Web.Models;

namespace TestRunner.Web.Services;

public class TestRunnerService : ITestRunnerService
{
    private readonly string _workingDirectory;
    private readonly string _reportsDirectory;
    private readonly IReportService _reportService;

    public TestRunnerService(IConfiguration configuration, IReportService reportService)
    {
        _workingDirectory = configuration["TestRunner:WorkingDirectory"]
            ?? throw new InvalidOperationException("TestRunner:WorkingDirectory is not configured.");
        _reportsDirectory = configuration["TestRunner:ReportsDirectory"]
            ?? throw new InvalidOperationException("TestRunner:ReportsDirectory is not configured.");
        _reportService = reportService;
    }

    public async IAsyncEnumerable<string> RunTestsAsync(
        TestRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var runId       = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var runFolder   = Path.Combine(_reportsDirectory, runId);
        var trxFileName = $"run-{runId}.trx";
        var trxPath     = Path.Combine(runFolder, trxFileName);

        var artifactsBase = Path.Combine(_workingDirectory, "bin", "Debug", "net10.0");

        Directory.CreateDirectory(runFolder);

        var deviceType = request.Device == "Custom" && !string.IsNullOrEmpty(request.CustomResolution)
            ? $"custom:{request.CustomResolution}"
            : request.Device;

        var psi = new ProcessStartInfo
        {
            FileName  = "dotnet",
            Arguments = $"test --filter \"TestCategory={request.Suite}\" " +
                        $"--logger \"console;verbosity=detailed\" " +
                        $"--logger \"trx;LogFileName={trxPath}\"",
            WorkingDirectory       = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        psi.Environment["BROWSER"]         = request.Browser;
        psi.Environment["HEADED"]          = request.Headed ? "1" : "0";
        psi.Environment["DEVICE_TYPE"]     = deviceType;
        psi.Environment["ENVIRON"]         = request.Environment;
        psi.Environment["RECORD_VIDEO"]    = request.RecordVideo ? "1" : "0";
        psi.Environment["RECORD_TRACES"]   = request.RecordTraces ? "1" : "0";
        psi.Environment["ARTIFACTS_BASE"]  = artifactsBase;

        yield return "SERVICE CALLED";
        yield return $"Working directory : {psi.WorkingDirectory}";
        yield return $"Exists            : {Directory.Exists(psi.WorkingDirectory)}";
        yield return $"Command           : {psi.FileName} {psi.Arguments}";
        yield return $"Browser: {request.Browser} | Headed: {request.Headed} | Device: {deviceType} | Environment: {request.Environment} | Suite: {request.Suite} | Video: {request.RecordVideo} | Traces: {request.RecordTraces}";
        yield return $"Run folder        : {runFolder}";
        yield return "";

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var started = process.Start();
        if (!started)
        {
            yield return "[ERROR] Process failed to start";
            yield break;
        }

        var channel       = Channel.CreateUnbounded<string>();
        var consoleBuffer = new StringBuilder();

        var stdoutTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
            {
                consoleBuffer.AppendLine(line);
                await channel.Writer.WriteAsync(line, CancellationToken.None);
            }
        }, CancellationToken.None);

        var stderrTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) is not null)
                await channel.Writer.WriteAsync("[ERR] " + line, CancellationToken.None);
        }, CancellationToken.None);

        _ = Task.WhenAll(stdoutTask, stderrTask)
                .ContinueWith(_ => channel.Writer.Complete(), TaskScheduler.Default);

        var wasCancelled = false;

        try
        {
            await foreach (var line in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return line;
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                wasCancelled = true;
            }
            await process.WaitForExitAsync(CancellationToken.None);
        }

        if (wasCancelled)
        {
            yield return "--- Test run cancelled ---";
            yield break;
        }

        yield return "";
        yield return "DONE — generating report...";

        MoveArtifacts(artifactsBase, runFolder);

        string? reportFileName = null;
        string? reportWarning  = null;

        try
        {
            reportFileName = $"report-{runId}.html";
            var report = ReportService.ParseTrx(
                trxPath,
                consoleBuffer.ToString(),
                request,
                reportFileName,
                runFolder);

            await _reportService.GenerateReportAsync(report, runId);
        }
        catch (Exception ex)
        {
            reportWarning  = $"[WARN] Report generation failed: {ex.Message}";
            reportFileName = null;
        }

        if (reportWarning != null)
            yield return reportWarning;

        if (reportFileName != null)
            yield return $"REPORT:{runId}/{reportFileName}";

        yield return "COMPLETE";
    }

    private static void MoveArtifacts(string artifactsBase, string runFolder)
    {
        Directory.CreateDirectory(runFolder);

        var tracesDir = Path.Combine(artifactsBase, "artifacts", "traces");
        if (Directory.Exists(tracesDir))
        {
            foreach (var file in Directory.GetFiles(tracesDir, "*.zip"))
            {
                var dest = Path.Combine(runFolder, Path.GetFileName(file));
                File.Move(file, dest, overwrite: true);
            }
        }

        var videosDir = Path.Combine(artifactsBase, "artifacts", "videos");
        if (Directory.Exists(videosDir))
        {
            foreach (var file in Directory.GetFiles(videosDir, "*.webm"))
            {
                var dest = Path.Combine(runFolder, Path.GetFileName(file));
                File.Move(file, dest, overwrite: true);
            }
        }
    }
}
