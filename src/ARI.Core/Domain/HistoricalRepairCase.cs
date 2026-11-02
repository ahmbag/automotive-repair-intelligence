namespace ARI.Core.Domain;

/// <summary>
/// A completed repair aggregate. Pre-repair observations and post-start outcomes remain separate.
/// </summary>
public sealed record HistoricalRepairCase(
    VehicleSnapshot Vehicle,
    RepairOrder Order,
    IReadOnlyCollection<RepairOperation> Operations,
    IReadOnlyCollection<KnownRepairCondition> KnownConditions,
    RepairExecution Execution,
    IReadOnlyCollection<RepairComplication> Complications)
{
    public void Validate()
    {
        if (Order.Vehicle.Id != Vehicle.Id)
        {
            throw new InvalidOperationException("The order must reference the case vehicle.");
        }

        if (Operations.Count == 0)
        {
            throw new InvalidOperationException("A historical case requires at least one operation.");
        }

        if (Operations.Any(operation => operation.RepairOrderId != Order.Id))
        {
            throw new InvalidOperationException("Every operation must reference the case order.");
        }

        if (Execution.RepairOrderId != Order.Id)
        {
            throw new InvalidOperationException("The execution must reference the case order.");
        }

        if (Complications.Any(complication => complication.RepairExecutionId != Execution.Id))
        {
            throw new InvalidOperationException("Every complication must reference the case execution.");
        }

        if (Vehicle.CapturedAtUtc > Order.CreatedAt
            || Order.CreatedAt > Execution.StartedAt
            || Execution.StartedAt > Execution.CompletedAt)
        {
            throw new InvalidOperationException(
                "Timestamps must satisfy capture <= order <= start <= completion.");
        }

        if (KnownConditions.Any(condition => condition.DocumentedAt > Execution.StartedAt))
        {
            throw new InvalidOperationException(
                "Known conditions must have been documented before repair start.");
        }

        if (Complications.Any(complication =>
                complication.OccurredAt < Execution.StartedAt
                || complication.OccurredAt > Execution.CompletedAt))
        {
            throw new InvalidOperationException(
                "Complications must occur during the repair execution.");
        }

        if (Operations.Any(operation => operation.ReferenceLaborHours <= 0)
            || Execution.ActualLaborMinutes <= 0
            || Complications.Any(complication => complication.AdditionalLaborMinutes < 0))
        {
            throw new InvalidOperationException("Labor values must be positive or non-negative.");
        }
    }
}
