using System.Text.RegularExpressions;

namespace TestRunner.Web.Services.RootCause;

/// <summary>
/// The four pieces of failure evidence the root cause classifier reads, in the
/// same form the harness stored them when the model was trained: text is one
/// line, hosts and emails masked, then truncated (message 400, step 200,
/// network events 300).
/// </summary>
public sealed record RootCauseEvidence(string ErrorMessage, string FailedStep, string StepSeconds, string NetworkEvents)
{
    private static readonly Regex Gherkin = new(@"^(Given|When|Then|And|But) ", RegexOptions.Compiled);
    private static readonly Regex Duration = new(@"\((\d+(?:\.\d+)?)s\)", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex HostRe = new(@"https?://[^/\s'""<>]+", RegexOptions.Compiled);
    private static readonly Regex EmailRe = new(@"[\w.+-]+@[\w-]+(?:\.[\w-]+)+", RegexOptions.Compiled);

    /// <summary>Builds the evidence from a test's standard output and error message (as held in the .trx).</summary>
    public static RootCauseEvidence FromStdOut(string? stdOut, string? errorMessage)
    {
        return new RootCauseEvidence(
            Clean(errorMessage, 400),
            Clean(FailedStepOf(stdOut), 200),
            StepSecondsOf(stdOut),
            Clean(NetworkEventsOf(stdOut), 300));
    }

    /// <summary>One line, addresses masked, then truncated. Idempotent.</summary>
    public static string Clean(string? text, int limit)
    {
        var t = Spaces.Replace(text ?? "", " ").Trim();
        t = HostRe.Replace(t, "***");
        t = EmailRe.Replace(t, "<email>");
        return t.Length > limit ? t[..limit] : t;
    }

    /// <summary>Per step timings as "Keyword=seconds;Keyword=seconds", in order. Skipped steps are omitted.</summary>
    public static string StepSecondsOf(string? stdOut)
    {
        string? keyword = null;
        List<string>? block = null;
        var result = new List<string>();
        foreach (var raw in (stdOut ?? "").Split('\n'))
        {
            var line = raw.Trim();
            if (block is null)
            {
                if (line.StartsWith("STEP START:", StringComparison.Ordinal))
                    block = new List<string>();
                else if (Gherkin.IsMatch(line))
                    keyword = line.Split(' ', 2)[0];
            }
            else if (line.StartsWith("STEP END:", StringComparison.Ordinal))
            {
                var found = Duration.Matches(string.Join(' ', block));
                if (found.Count > 0 && keyword is not null)
                    result.Add($"{keyword}={found[found.Count - 1].Groups[1].Value}");
                block = null;
            }
            else
            {
                block.Add(line);
            }
        }
        return string.Join(';', result);
    }

    /// <summary>Gherkin text of the step that errored, or an empty string.</summary>
    public static string FailedStepOf(string? stdOut)
    {
        var current = "";
        List<string>? block = null;
        foreach (var raw in (stdOut ?? "").Split('\n'))
        {
            var line = raw.Trim();
            if (block is null)
            {
                if (line.StartsWith("STEP START:", StringComparison.Ordinal))
                    block = new List<string>();
                else if (Gherkin.IsMatch(line))
                    current = line;
            }
            else if (line.StartsWith("STEP END:", StringComparison.Ordinal))
            {
                if (block.Any(b => b.StartsWith("-> error:", StringComparison.Ordinal)))
                    return current;
                block = null;
            }
            else
            {
                block.Add(line);
            }
        }
        return "";
    }

    /// <summary>Up to three failed network requests, joined with " | ".</summary>
    public static string NetworkEventsOf(string? stdOut)
    {
        var events = (stdOut ?? "").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("[RequestFailed]", StringComparison.Ordinal))
            .Select(l => l.Replace("[RequestFailed] ", ""))
            .Take(3);
        return string.Join(" | ", events);
    }
}
