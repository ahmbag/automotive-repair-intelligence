namespace ARI.Core.Domain;

/// <summary>
/// A structured condition documented before work started. It may be used as a prediction-time
/// feature because its knowledge timestamp is part of the value.
/// </summary>
public sealed record KnownRepairCondition(
    string ConditionCode,
    string Severity,
    DateTimeOffset DocumentedAt);
