namespace ARI.Core.Domain;

/// <summary>A classified complication observed during a repair execution.</summary>
public sealed record RepairComplication(
    Guid Id,
    Guid RepairExecutionId,
    string ComplicationCode,
    int AdditionalLaborMinutes,
    DateTimeOffset OccurredAt,
    IReadOnlyCollection<string> AdditionalPartCodes);
