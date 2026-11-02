namespace ARI.Core.Domain;

/// <summary>A normalized, planned operation and its conventional reference time.</summary>
public sealed record RepairOperation(
    Guid Id,
    Guid RepairOrderId,
    string AssemblyCode,
    string OperationCode,
    decimal ReferenceLaborHours);
