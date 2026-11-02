namespace ARI.Core.Domain;

/// <summary>A planned repair and its pre-repair information.</summary>
public sealed record RepairOrder(
    Guid Id,
    VehicleSnapshot Vehicle,
    string RepairCode,
    string? ProblemDescription,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<string> KnownConditions);
