using ARI.Core.Application;

namespace ARI.Core.Tests;

public sealed class RepairExtractionEvaluationTests
{
    [Fact]
    public void Metrics_use_micro_averaged_complication_counts_and_keep_dimensions_separate()
    {
        ExtractionEvaluationObservation[] observations =
        [
            new(true, true, true, Set("A", "B"), Set("A", "C"), 1, 0, 100, 10, 5),
            new(false, false, true, Set("D"), Set("D"), 0, 1, 200, 20, 8)
        ];

        var metrics = RepairExtractionEvaluation.Calculate(observations);

        Assert.Equal(0.5m, metrics.SchemaValidityRate);
        Assert.Equal(0.5m, metrics.VehicleExactMatchAccuracy);
        Assert.Equal(1m, metrics.RepairCodeExactMatchAccuracy);
        Assert.Equal(decimal.Divide(2, 3), metrics.ComplicationPrecision);
        Assert.Equal(decimal.Divide(2, 3), metrics.ComplicationRecall);
        Assert.Equal(decimal.Divide(2, 3), metrics.ComplicationF1);
        Assert.Equal(1, metrics.UnsupportedClaimCount);
        Assert.Equal(1, metrics.TemporalLeakageViolationCount);
        Assert.Equal(30, metrics.TotalInputTokens);
    }

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
