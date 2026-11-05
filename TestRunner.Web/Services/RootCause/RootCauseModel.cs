using System.Text.Json;

namespace TestRunner.Web.Services.RootCause;

internal sealed class RootCauseModelSpec
{
    public string Type { get; set; } = "";
    public string[] Features { get; set; } = Array.Empty<string>();
    public string[] Classes { get; set; } = Array.Empty<string>();
    public double[] Mean { get; set; } = Array.Empty<double>();
    public double[] Scale { get; set; } = Array.Empty<double>();
    public double[][] Coef { get; set; } = Array.Empty<double[]>();
    public double[] Intercept { get; set; } = Array.Empty<double>();
}

/// <summary>
/// The frozen logistic regression (tag root-cause-freeze), read from
/// data/root-cause-frozen-model.json. Scoring: standardise, linear scores per
/// class, softmax. Nothing is refitted here.
/// </summary>
public sealed class RootCauseModel
{
    private readonly RootCauseModelSpec _spec;

    private RootCauseModel(RootCauseModelSpec spec) => _spec = spec;

    public IReadOnlyList<string> Classes => _spec.Classes;

    public static RootCauseModel Load(string path)
    {
        var spec = JsonSerializer.Deserialize<RootCauseModelSpec>(File.ReadAllText(path),
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? throw new InvalidDataException("Model file is empty.");
        if (spec.Type != "logistic_regression")
            throw new InvalidDataException($"Unexpected model type '{spec.Type}'.");
        if (!spec.Features.SequenceEqual(RootCauseFeatures.Names))
            throw new InvalidDataException("Model features do not match the classifier's feature order.");
        return new RootCauseModel(spec);
    }

    /// <summary>Class probabilities for one feature vector.</summary>
    public Dictionary<string, double> Probabilities(double[] x)
    {
        var scores = new double[_spec.Classes.Length];
        for (var k = 0; k < scores.Length; k++)
        {
            var s = _spec.Intercept[k];
            for (var j = 0; j < x.Length; j++)
                s += _spec.Coef[k][j] * ((x[j] - _spec.Mean[j]) / _spec.Scale[j]);
            scores[k] = s;
        }
        var max = scores.Max();
        var exps = scores.Select(s => Math.Exp(s - max)).ToArray();
        var sum = exps.Sum();
        var result = new Dictionary<string, double>();
        for (var k = 0; k < scores.Length; k++)
            result[_spec.Classes[k]] = exps[k] / sum;
        return result;
    }
}
