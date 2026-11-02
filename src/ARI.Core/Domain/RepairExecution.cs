namespace ARI.Core.Domain;

/// <summary>Post-repair facts. These are outcomes and must not leak into prediction features.</summary>
public sealed record RepairExecution(
    Guid Id,
    Guid RepairOrderId,
    int ActualLaborMinutes,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    IReadOnlyCollection<string> PerformedOperationCodes,
    string RepairReport);
