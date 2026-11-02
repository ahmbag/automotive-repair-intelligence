using ARI.Core.Application;
using ARI.Core.Contracts;

namespace ARI.Core.Tests;

public sealed class ExtractionQualityPolicyTests
{
    [Fact]
    public void Complete_valid_result_is_accepted_by_application_policy()
    {
        var request = new RepairExtractionRequest(
            "Volkswagen Golf VII, model year 2014, 215000 km. Planned work: replace the front left control arm.",
            RepairDocumentTypes.PreRepairDescription);
        var result = Result();
        var validation = RepairExtractionValidator.Validate(request, result, 2026);

        Assert.Equal(
            RepairExtractionStatuses.Accepted,
            ExtractionQualityPolicy.Determine(request, result, validation));
        Assert.True(ExtractionQualityPolicy.CanSupplyPredictionFeatures(
            result with { Status = RepairExtractionStatuses.Accepted }));
    }

    [Fact]
    public void Missing_identity_is_partial_and_cannot_supply_ml()
    {
        var request = new RepairExtractionRequest(
            "No vehicle information was supplied.", RepairDocumentTypes.PreRepairDescription);
        var result = Result() with
        {
            Vehicle = new ExtractedVehicle(null, null, null, null),
            RepairCode = null
        };
        var validation = RepairExtractionValidator.Validate(request, result, 2026);
        var status = ExtractionQualityPolicy.Determine(request, result, validation);

        Assert.Equal(RepairExtractionStatuses.Partial, status);
        Assert.False(ExtractionQualityPolicy.CanSupplyPredictionFeatures(result with { Status = status }));
    }

    [Fact]
    public void Explicit_ambiguity_requires_review()
    {
        var request = new RepairExtractionRequest(
            "The note gives both model year 2014 and 2019.", RepairDocumentTypes.PreRepairDescription);
        var result = Result();
        var validation = RepairExtractionValidator.Validate(request, result, 2026);

        Assert.Equal(
            RepairExtractionStatuses.NeedsReview,
            ExtractionQualityPolicy.Determine(request, result, validation));
    }

    private static RepairExtractionResult Result() => new(
        RepairDocumentTypes.PreRepairDescription,
        new ExtractedVehicle("Volkswagen", "Golf VII", 2014, 215_000),
        "CONTROL_ARM_FRONT_LEFT_REPLACE",
        [],
        [],
        null,
        RepairExtractionStatuses.Accepted,
        new ExtractionTelemetry("test", "test", 1, 1, 1, null));
}
