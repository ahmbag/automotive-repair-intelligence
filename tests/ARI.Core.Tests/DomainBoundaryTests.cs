using ARI.Core.Domain;

namespace ARI.Core.Tests;

public sealed class DomainBoundaryTests
{
    [Fact]
    public void Estimate_captures_required_traceability_fields()
    {
        var estimate = new RepairEstimate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1.8m,
            2.6m,
            null,
            "demo-v1",
            ["CORROSION_VISIBLE"],
            EstimateDataSource.Synthetic,
            EstimateStatus.Estimated,
            DateTimeOffset.Parse("2026-10-22T08:00:00Z"),
            "synthetic-v1",
            "ML_REGRESSION");

        Assert.Equal("demo-v1", estimate.ModelVersion);
        Assert.Equal("synthetic-v1", estimate.InputDataVersion);
        Assert.Equal(EstimateDataSource.Synthetic, estimate.DataSource);
        Assert.Null(estimate.PredictionRangeHours);
    }
}
