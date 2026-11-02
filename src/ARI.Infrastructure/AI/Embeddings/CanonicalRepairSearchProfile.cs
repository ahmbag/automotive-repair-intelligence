using System.Globalization;
using ARI.Core.Contracts;
using ARI.Core.Domain;

namespace ARI.Infrastructure.AI.Embeddings;

/// <summary>
/// Produces deterministic, versioned documents for retrieval. The pre-repair profile deliberately
/// excludes execution outcomes, actual labor and complications so that prediction-time information
/// can never be enriched with knowledge from the future.
/// </summary>
public static class CanonicalRepairSearchProfile
{
    public const string ProfileVersion = "canonical-repair-search-profile-v2";
    public const string DocumentVersion = "2.0.0";

    public static string BuildPreRepairProfile(HistoricalRepairCase repairCase)
    {
        ArgumentNullException.ThrowIfNull(repairCase);
        var age = Math.Max(0, repairCase.Execution.CompletedAt.Year - repairCase.Vehicle.ModelYear);
        var assemblies = repairCase.Operations.Select(value => value.AssemblyCode)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(Humanize);
        var operations = repairCase.Operations.Select(value => value.OperationCode)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(Humanize);
        var conditions = repairCase.KnownConditions
            .OrderBy(value => value.ConditionCode, StringComparer.Ordinal)
            .Select(value => $"{Humanize(value.ConditionCode)} ({value.Severity.ToLowerInvariant()})")
            .ToArray();

        return Normalize($"""
            Document type: {RepairSearchDocumentTypes.PreRepairProfile}
            Profile version: {ProfileVersion}
            Repair category: {string.Join(", ", assemblies)}
            Repair operation: {string.Join(", ", operations)}
            Repair code: {Humanize(repairCase.Order.RepairCode)}
            Vehicle manufacturer: {repairCase.Vehicle.Manufacturer}
            Vehicle model: {repairCase.Vehicle.Model}
            Vehicle model year: {repairCase.Vehicle.ModelYear.ToString(CultureInfo.InvariantCulture)}
            Vehicle mileage: {repairCase.Vehicle.MileageKm.ToString(CultureInfo.InvariantCulture)} km
            Vehicle age at repair: {age.ToString(CultureInfo.InvariantCulture)} years
            Pre-repair conditions: {(conditions.Length == 0 ? "none recorded" : string.Join(", ", conditions))}
            """);
    }

    public static string BuildCompletionEvidence(HistoricalRepairCase repairCase)
    {
        ArgumentNullException.ThrowIfNull(repairCase);
        return Normalize($"""
            Document type: {RepairSearchDocumentTypes.CompletionEvidence}
            Profile version: {ProfileVersion}
            Repair code: {Humanize(repairCase.Order.RepairCode)}
            Historical completion report: {repairCase.Execution.RepairReport}
            """);
    }

    public static string BuildQuery(RepairRetrievalQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var year = query.VehicleAgeYears is null ? "unknown" :
            (query.AsOfUtc.Year - query.VehicleAgeYears.Value).ToString(CultureInfo.InvariantCulture);
        var conditions = query.KnownConditions.Order(StringComparer.Ordinal).Select(Humanize).ToArray();
        return Normalize($"""
            Document type: {RepairSearchDocumentTypes.PreRepairProfile}
            Profile version: {ProfileVersion}
            Repair operation: {Humanize(query.RepairCode)}
            Repair code: {Humanize(query.RepairCode)}
            Vehicle manufacturer: {query.Manufacturer ?? "unknown"}
            Vehicle model: {query.Model ?? "unknown"}
            Vehicle model year: {year}
            Vehicle mileage: {(query.MileageKm?.ToString(CultureInfo.InvariantCulture) ?? "unknown")} km
            Vehicle age at repair: {(query.VehicleAgeYears?.ToString(CultureInfo.InvariantCulture) ?? "unknown")} years
            Pre-repair conditions: {(conditions.Length == 0 ? "none recorded" : string.Join(", ", conditions))}
            User description: {query.Description}
            """);
    }

    private static string Humanize(string value) => value.Replace('_', ' ').ToLowerInvariant();

    private static string Normalize(string value) => string.Join('\n', value
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
