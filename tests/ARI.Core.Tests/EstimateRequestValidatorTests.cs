using ARI.Core.Application;
using ARI.Core.Contracts;

namespace ARI.Core.Tests;

public sealed class EstimateRequestValidatorTests
{
    [Fact]
    public void Valid_reference_request_has_no_errors()
    {
        var request = CreateValidRequest();

        var errors = EstimateRequestValidator.Validate(request, currentYear: 2026);

        Assert.Empty(errors);
    }

    [Fact]
    public void Future_vehicle_and_negative_mileage_are_rejected()
    {
        var request = CreateValidRequest() with
        {
            Vehicle = new VehicleSnapshotRequest("Volkswagen", "Golf VII", 2030, -1)
        };

        var errors = EstimateRequestValidator.Validate(request, currentYear: 2026);

        Assert.Contains(errors, error => error.Contains("model year", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("mileage", StringComparison.OrdinalIgnoreCase));
    }

    private static CreateEstimateRequest CreateValidRequest() =>
        new(
            new VehicleSnapshotRequest("Volkswagen", "Golf VII", 2014, 210_000),
            EstimateRequestValidator.ReferenceRepairCode,
            "Replace front left control arm. Visible corrosion.",
            ["CORROSION_VISIBLE"]);
}
