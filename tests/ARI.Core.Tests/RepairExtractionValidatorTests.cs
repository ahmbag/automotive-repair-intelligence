using ARI.Core.Application;
using ARI.Core.Contracts;

namespace ARI.Core.Tests;

public sealed class RepairExtractionValidatorTests
{
    [Fact]
    public void Valid_completion_report_preserves_during_repair_information_time()
    {
        const string document = "A mounting bolt broke during removal and was replaced.";
        var result = Result(
            RepairDocumentTypes.RepairCompletionReport,
            observed:
            [
                new(
                    "BROKEN_BOLT",
                    RepairInformationTimes.DuringRepair,
                    "A mounting bolt broke during removal")
            ]);

        var validation = RepairExtractionValidator.Validate(
            new(document, RepairDocumentTypes.RepairCompletionReport), result, 2026);

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public void Hallucinated_evidence_is_rejected()
    {
        var result = Result(
            RepairDocumentTypes.RepairCompletionReport,
            observed:
            [
                new(
                    "BROKEN_BOLT",
                    RepairInformationTimes.DuringRepair,
                    "A bolt broke")
            ]);

        var validation = RepairExtractionValidator.Validate(
            new("Repair completed normally.", RepairDocumentTypes.RepairCompletionReport),
            result,
            2026);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("not a source substring"));
    }

    [Fact]
    public void Post_start_fact_cannot_be_relabelled_as_known_before_repair()
    {
        const string document = "The bolt broke during removal.";
        var result = Result(
            RepairDocumentTypes.RepairCompletionReport,
            observed:
            [
                new("BROKEN_BOLT", RepairInformationTimes.BeforeRepair, "bolt broke during removal")
            ]);

        var validation = RepairExtractionValidator.Validate(
            new(document, RepairDocumentTypes.RepairCompletionReport), result, 2026);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("known before repair"));
    }

    [Fact]
    public void Pre_repair_document_cannot_supply_actual_labor_or_observed_complication()
    {
        const string document = "A bolt may be difficult to remove. Actual work took 90 minutes.";
        var result = Result(
            RepairDocumentTypes.PreRepairDescription,
            observed:
            [
                new("SEIZED_FASTENER", RepairInformationTimes.Unknown, "bolt may be difficult")
            ],
            actualLaborMinutes: 90);

        var validation = RepairExtractionValidator.Validate(
            new(document, RepairDocumentTypes.PreRepairDescription), result, 2026);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("cannot assert observed"));
        Assert.Contains(validation.Errors, error => error.Contains("cannot contain actual"));
    }

    [Fact]
    public void Unknown_supported_information_remains_null()
    {
        var result = Result(RepairDocumentTypes.RepairCompletionReport, repairCode: null);

        var validation = RepairExtractionValidator.Validate(
            new("Repair completed normally.", RepairDocumentTypes.RepairCompletionReport),
            result,
            2026);

        Assert.True(validation.IsValid);
        Assert.Null(result.Vehicle.Manufacturer);
        Assert.Null(result.RepairCode);
    }

    private static RepairExtractionResult Result(
        string documentType,
        string? repairCode = "CONTROL_ARM_FRONT_LEFT_REPLACE",
        IReadOnlyCollection<ExtractedRepairFact>? observed = null,
        int? actualLaborMinutes = null) => new(
            documentType,
            new ExtractedVehicle(null, null, null, null),
            repairCode,
            observed ?? [],
            [],
            actualLaborMinutes,
            RepairExtractionStatuses.Extracted,
            new ExtractionTelemetry("test", "test", 1, null, null, null));
}
