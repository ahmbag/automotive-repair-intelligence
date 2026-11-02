using ARI.Core.Contracts;

namespace ARI.Core.Application;

/// <summary>
/// Domain validation runs after JSON/schema validation. A schema-correct model response can still
/// contain an unsupported code, invented evidence, or a temporally unsafe fact.
/// </summary>
public static class RepairExtractionValidator
{
    public static readonly IReadOnlySet<string> RepairCatalog = new HashSet<string>(
        StringComparer.Ordinal)
    {
        "CONTROL_ARM_FRONT_LEFT_REPLACE",
        "CONTROL_ARM_FRONT_PAIR_REPLACE",
        "CONTROL_ARM_FRONT_RIGHT_REPLACE",
        "CONTROL_ARM_REAR_LEFT_REPLACE",
        "CONTROL_ARM_REAR_RIGHT_REPLACE"
    };

    public static readonly IReadOnlySet<string> ComplicationCatalog = new HashSet<string>(
        StringComparer.Ordinal)
    {
        "ALIGNMENT_CORRECTION",
        "BROKEN_BOLT",
        "DAMAGED_BUSHING_SEAT",
        "SEIZED_FASTENER"
    };

    public static readonly IReadOnlySet<string> KnownConditionCatalog = new HashSet<string>(
        StringComparer.Ordinal)
    {
        "VISIBLE_CORROSION",
        "VISIBLE_PRIOR_DAMAGE"
    };

    public static RepairExtractionValidationResult Validate(
        RepairExtractionRequest request,
        RepairExtractionResult result,
        int currentYear)
    {
        var errors = new List<string>();

        if (!RepairDocumentTypes.IsSupported(request.DocumentType))
        {
            errors.Add("The application document type is unsupported.");
        }

        if (!string.Equals(request.DocumentType, result.DocumentType, StringComparison.Ordinal))
        {
            errors.Add("The model changed the application-owned document type.");
        }

        if (result.RepairCode is not null && !RepairCatalog.Contains(result.RepairCode))
        {
            errors.Add($"Unsupported repair code: {result.RepairCode}.");
        }

        ValidateVehicle(result.Vehicle, currentYear, errors);
        ValidateFacts(request.Document, result.ObservedComplications, ComplicationCatalog, errors);
        ValidateFacts(request.Document, result.KnownBeforeRepair, KnownConditionCatalog, errors);

        if (result.ObservedComplications.Any(fact =>
                fact.InformationTime == RepairInformationTimes.BeforeRepair))
        {
            errors.Add("An observed complication cannot be classified as known before repair.");
        }

        if (result.KnownBeforeRepair.Any(fact =>
                fact.InformationTime != RepairInformationTimes.BeforeRepair))
        {
            errors.Add("A known-before-repair fact must use BEFORE_REPAIR.");
        }

        if (request.DocumentType == RepairDocumentTypes.PreRepairDescription)
        {
            if (result.ObservedComplications.Count > 0)
            {
                errors.Add("A pre-repair description cannot assert observed repair complications.");
            }

            if (result.ActualLaborMinutes is not null)
            {
                errors.Add("A pre-repair description cannot contain actual completed labor.");
            }
        }

        if (result.ActualLaborMinutes is <= 0 or > 10_000)
        {
            errors.Add("Actual labor minutes are outside the supported range.");
        }

        if (!RepairExtractionStatuses.IsSupported(result.Status))
        {
            errors.Add("Extraction status is unsupported.");
        }

        return new RepairExtractionValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateVehicle(
        ExtractedVehicle vehicle,
        int currentYear,
        ICollection<string> errors)
    {
        if (vehicle.ModelYear is not null &&
            (vehicle.ModelYear < 1886 || vehicle.ModelYear > currentYear + 1))
        {
            errors.Add("Extracted model year is outside the supported range.");
        }

        if (vehicle.MileageKm is < 0 or > 1_000_000)
        {
            errors.Add("Extracted mileage is outside the supported range.");
        }
    }

    private static void ValidateFacts(
        string source,
        IReadOnlyCollection<ExtractedRepairFact> facts,
        IReadOnlySet<string> catalog,
        ICollection<string> errors)
    {
        foreach (var fact in facts)
        {
            if (!catalog.Contains(fact.Code))
            {
                errors.Add($"Unsupported extracted code: {fact.Code}.");
            }

            if (string.IsNullOrWhiteSpace(fact.Evidence) ||
                !source.Contains(fact.Evidence, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Evidence for {fact.Code} is not a source substring.");
            }

            if (fact.InformationTime is not (
                    RepairInformationTimes.BeforeRepair or
                    RepairInformationTimes.DuringRepair or
                    RepairInformationTimes.AfterRepair or
                    RepairInformationTimes.Unknown))
            {
                errors.Add($"Unsupported information time for {fact.Code}.");
            }
        }
    }
}
