using System.Globalization;
using System.Text.RegularExpressions;

namespace TestRunner.Web.Services.RootCause;

/// <summary>
/// The 14 scenario agnostic features, built exactly as analysis/build_failure_table.py
/// builds them. Order matters: it is the order the frozen model was fitted in.
/// </summary>
public static class RootCauseFeatures
{
    public static readonly string[] Names =
    {
        "exc_is_timeout", "exc_is_expectation", "timeout_ms",
        "step_is_given", "step_is_when", "step_is_then",
        "failed_step_position", "secs_in_failed_step", "secs_before_failed_step",
        "net_events", "net_aborted", "net_failed", "net_other", "net_first_is_post",
    };

    private static readonly Regex TimeoutRe = new(@"timeout (\d+)\s*ms", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static double[] Build(RootCauseEvidence e)
    {
        var message = e.ErrorMessage ?? "";
        var step = e.FailedStep ?? "";
        var exc = message.Split(" : ")[0].Trim();
        var timeout = TimeoutRe.Match(message);
        var keyword = step.Length > 0 ? step.Split(' ')[0] : "";

        var secs = new List<double>();
        foreach (var part in (e.StepSeconds ?? "").Split(';'))
        {
            if (part.Contains('='))
                secs.Add(double.Parse(part.Split('=')[1], CultureInfo.InvariantCulture));
        }

        var events = (e.NetworkEvents ?? "").Split(" | ").Where(x => x.Trim().Length > 0).ToArray();
        var first = events.Length > 0 ? events[0].Split(' ')[0] : "";

        double before = double.NaN, inStep = double.NaN;
        if (secs.Count > 0)
        {
            inStep = secs[^1];
            before = 0.0;
            for (var i = 0; i < secs.Count - 1; i++) before += secs[i];
        }

        return new double[]
        {
            exc.EndsWith("TimeoutException", StringComparison.Ordinal) ? 1 : 0,
            exc.EndsWith("PlaywrightException", StringComparison.Ordinal) ? 1 : 0,
            timeout.Success ? double.Parse(timeout.Groups[1].Value, CultureInfo.InvariantCulture) : 0,
            keyword == "Given" ? 1 : 0,
            keyword == "When" ? 1 : 0,
            keyword == "Then" ? 1 : 0,
            secs.Count,
            inStep,
            before,
            events.Length,
            events.Count(x => x.Contains("ERR_ABORTED", StringComparison.Ordinal)),
            events.Count(x => x.Contains("ERR_FAILED", StringComparison.Ordinal)),
            events.Count(x => x.Contains("net::", StringComparison.Ordinal)
                              && !x.Contains("ERR_ABORTED", StringComparison.Ordinal)
                              && !x.Contains("ERR_FAILED", StringComparison.Ordinal)),
            first == "POST" ? 1 : 0,
        };
    }
}
