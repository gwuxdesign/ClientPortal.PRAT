using System.Text.Json;
using TestRunner.Web.Services.RootCause;

namespace TestRunner.Web.Tests;

/// <summary>
/// The C# port must reproduce the Python features, gate and frozen model
/// predictions on all 799 recorded failures (TestRunner.Web.Tests/Golden).
/// </summary>
public class GoldenCaseTests
{
    private sealed record Case(int Id, RootCauseEvidence Evidence, double[] Features, bool Gate,
        string Predicted, Dictionary<string, double> Probabilities);

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "ClientPortal.PRAT.sln")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static readonly Lazy<List<Case>> Cases = new(() =>
    {
        var json = File.ReadAllText(Path.Combine(RepoRoot(), "TestRunner.Web.Tests", "Golden", "root-cause-golden.json"));
        using var doc = JsonDocument.Parse(json);
        var list = new List<Case>();
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var i = c.GetProperty("inputs");
            string S(string n) => i.GetProperty(n).ValueKind == JsonValueKind.Null ? "" : i.GetProperty(n).GetString()!;
            var features = RootCauseFeatures.Names
                .Select(n => c.GetProperty("features").GetProperty(n))
                .Select(v => v.ValueKind == JsonValueKind.Null ? double.NaN : v.GetDouble())
                .ToArray();
            var probs = c.GetProperty("probabilities").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble());
            list.Add(new Case(c.GetProperty("id").GetInt32(),
                new RootCauseEvidence(S("error_message"), S("failed_step"), S("step_seconds"), S("network_events")),
                features, c.GetProperty("gate").GetBoolean(), c.GetProperty("predicted").GetString()!, probs));
        }
        return list;
    });

    private static RootCauseClassifier Classifier() =>
        new(RootCauseModel.Load(Path.Combine(RepoRoot(), "data", "root-cause-frozen-model.json")));

    [Fact]
    public void Golden_file_holds_all_recorded_failures()
    {
        Assert.Equal(799, Cases.Value.Count);
    }

    [Fact]
    public void Features_match_the_python()
    {
        var bad = Cases.Value
            .Where(c => !RootCauseFeatures.Build(c.Evidence).Zip(c.Features, (a, b) => Math.Abs(a - b) < 1e-9).All(x => x))
            .Select(c => c.Id).Take(5).ToList();
        Assert.True(bad.Count == 0, "Feature mismatch in cases: " + string.Join(", ", bad));
    }

    [Fact]
    public void Gate_matches_the_python()
    {
        var bad = Cases.Value
            .Where(c => RootCauseClassifier.InScope(RootCauseFeatures.Build(c.Evidence)) != c.Gate)
            .Select(c => c.Id).Take(5).ToList();
        Assert.True(bad.Count == 0, "Gate mismatch in cases: " + string.Join(", ", bad));
    }

    [Fact]
    public void Predicted_labels_match_the_frozen_model()
    {
        var clf = Classifier();
        var bad = Cases.Value
            .Where(c => clf.Classify(c.Evidence).Label != c.Predicted)
            .Select(c => c.Id).Take(5).ToList();
        Assert.True(bad.Count == 0, "Label mismatch in cases: " + string.Join(", ", bad));
    }

    [Fact]
    public void Probabilities_match_the_frozen_model()
    {
        var clf = Classifier();
        var bad = Cases.Value
            .Where(c => c.Probabilities.Any(p => Math.Abs(clf.Classify(c.Evidence).Probabilities[p.Key] - p.Value) > 1e-9))
            .Select(c => c.Id).Take(5).ToList();
        Assert.True(bad.Count == 0, "Probability mismatch in cases: " + string.Join(", ", bad));
    }
}
