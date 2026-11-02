namespace ARI.Core.Contracts;

/// <summary>Application-owned document contexts. The model is not allowed to redefine them.</summary>
public static class RepairDocumentTypes
{
    public const string PreRepairDescription = "PRE_REPAIR_DESCRIPTION";
    public const string RepairCompletionReport = "REPAIR_COMPLETION_REPORT";

    public static bool IsSupported(string value) =>
        value is PreRepairDescription or RepairCompletionReport;
}

public static class RepairInformationTimes
{
    public const string BeforeRepair = "BEFORE_REPAIR";
    public const string DuringRepair = "DURING_REPAIR";
    public const string AfterRepair = "AFTER_REPAIR";
    public const string Unknown = "UNKNOWN";
}

public static class RepairExtractionStatuses
{
    public const string Accepted = "ACCEPTED";
    public const string Partial = "PARTIAL";
    public const string NeedsReview = "NEEDS_REVIEW";
    public const string Rejected = "REJECTED";

    // Source compatibility for callers compiled against ARI-LLM-01. The application result now
    // exposes ACCEPTED; the provider's legacy EXTRACTED hint is never trusted as a quality gate.
    public const string Extracted = Accepted;

    public static bool IsSupported(string value) =>
        value is Accepted or Partial or NeedsReview or Rejected;
}

/// <summary>The document is untrusted data; DocumentType is trusted application context.</summary>
public sealed record RepairExtractionRequest(
    string Document,
    string DocumentType,
    string PromptVersion = "repair-extraction-v3");

public sealed record ExtractedVehicle(
    string? Manufacturer,
    string? Model,
    int? ModelYear,
    int? MileageKm);

/// <summary>Every normalized fact retains the exact source passage that supports it.</summary>
public sealed record ExtractedRepairFact(
    string Code,
    string InformationTime,
    string Evidence);

public sealed record ExtractionTelemetry(
    string PromptVersion,
    string ModelId,
    long LatencyMilliseconds,
    int? InputTokens,
    int? OutputTokens,
    string? ProviderRequestId);

public sealed record RepairExtractionResult(
    string DocumentType,
    ExtractedVehicle Vehicle,
    string? RepairCode,
    IReadOnlyCollection<ExtractedRepairFact> ObservedComplications,
    IReadOnlyCollection<ExtractedRepairFact> KnownBeforeRepair,
    int? ActualLaborMinutes,
    string Status,
    ExtractionTelemetry Telemetry);

public sealed record RepairExtractionValidationResult(
    bool IsValid,
    IReadOnlyCollection<string> Errors);

public sealed class RepairExtractionUnavailableException : Exception
{
    public RepairExtractionUnavailableException(string message) : base(message) { }

    public RepairExtractionUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }
}

public sealed class RepairExtractionValidationException(
    IReadOnlyCollection<string> errors) : Exception("The extracted repair information is invalid.")
{
    public IReadOnlyCollection<string> Errors { get; } = errors;
}
