using ARI.Core.Contracts;

namespace ARI.Core.Application;

/// <summary>
/// Application-owned quality decision. Provider confidence or a provider-supplied status never
/// releases a fact for downstream use.
/// </summary>
public static class ExtractionQualityPolicy
{
    private static readonly string[] AmbiguityMarkers =
    [
        "both model year", "either model year", "year is unclear", "mileage is unknown",
        "possibly", "maybe", "unclear whether", "could have"
    ];

    public static string Determine(
        RepairExtractionRequest request,
        RepairExtractionResult candidate,
        RepairExtractionValidationResult validation)
    {
        if (!validation.IsValid)
        {
            return RepairExtractionStatuses.Rejected;
        }

        if (AmbiguityMarkers.Any(marker =>
                request.Document.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return RepairExtractionStatuses.NeedsReview;
        }

        var missingIdentity = candidate.RepairCode is null ||
            candidate.Vehicle.Manufacturer is null ||
            candidate.Vehicle.Model is null ||
            candidate.Vehicle.ModelYear is null ||
            candidate.Vehicle.MileageKm is null;

        return missingIdentity
            ? RepairExtractionStatuses.Partial
            : RepairExtractionStatuses.Accepted;
    }

    public static bool CanSupplyPredictionFeatures(RepairExtractionResult result) =>
        result.Status == RepairExtractionStatuses.Accepted;
}
