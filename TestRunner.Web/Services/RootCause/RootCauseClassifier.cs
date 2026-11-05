namespace TestRunner.Web.Services.RootCause;

public sealed record RootCauseResult(
    bool Classified,
    string? Label,
    double Probability,
    IReadOnlyDictionary<string, double> Probabilities,
    string? Reason,
    string? Note);

/// <summary>
/// Classifies a failed scenario as slow response, failed request or overload.
/// A failure is classified only if it has timeout or failed request evidence;
/// otherwise the result is "not classified", because the model has only been
/// validated on faults injected by the PRAT harness.
/// </summary>
public sealed class RootCauseClassifier
{
    public const string OverloadNote =
        "A page timeout with no failed request can also come from slow responses; " +
        "testing could not tell these apart on Documents pages.";

    private static readonly Lazy<RootCauseClassifier?> DefaultInstance = new(LoadDefault);

    private readonly RootCauseModel _model;

    public RootCauseClassifier(RootCauseModel model) => _model = model;

    /// <summary>The classifier loaded from the model file shipped with the app, or null if it is missing or unreadable.</summary>
    public static RootCauseClassifier? Default => DefaultInstance.Value;

    private static RootCauseClassifier? LoadDefault()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "RootCause", "root-cause-frozen-model.json");
            return File.Exists(path) ? new RootCauseClassifier(RootCauseModel.Load(path)) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>True if the features show timeout or failed request evidence and all values are present.</summary>
    public static bool InScope(double[] f) =>
        (f[0] == 1 || f[2] > 0 || f[9] > 0) && !double.IsNaN(f[7]) && !double.IsNaN(f[8]);

    public RootCauseResult Classify(RootCauseEvidence evidence)
    {
        var f = RootCauseFeatures.Build(evidence);
        if (!InScope(f))
        {
            return new RootCauseResult(false, null, 0, new Dictionary<string, double>(),
                "No timeout or failed request evidence, so no cause is suggested.", null);
        }

        var probs = _model.Probabilities(f);
        string? best = null;
        var bestP = double.NegativeInfinity;
        foreach (var c in _model.Classes)
        {
            if (probs[c] > bestP)
            {
                best = c;
                bestP = probs[c];
            }
        }
        return new RootCauseResult(true, best, bestP, probs, null, best == "overload" ? OverloadNote : null);
    }
}
