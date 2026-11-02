using ARI.Core.Domain;

namespace ARI.Core.Tests;

public sealed class HistoricalRepairCaseTests
{
    [Fact]
    public void Valid_case_preserves_prediction_and_outcome_time_boundaries()
    {
        var repairCase = CreateCase();

        repairCase.Validate();

        Assert.All(
            repairCase.KnownConditions,
            condition => Assert.True(condition.DocumentedAt <= repairCase.Execution.StartedAt));
        Assert.All(
            repairCase.Complications,
            complication => Assert.InRange(
                complication.OccurredAt,
                repairCase.Execution.StartedAt,
                repairCase.Execution.CompletedAt));
    }

    [Fact]
    public void Outcome_observed_after_start_cannot_be_declared_as_known_condition()
    {
        var repairCase = CreateCase();
        var leaked = repairCase with
        {
            KnownConditions =
            [
                new KnownRepairCondition(
                    "BROKEN_BOLT",
                    "HIGH",
                    repairCase.Execution.StartedAt.AddMinutes(10))
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(leaked.Validate);

        Assert.Contains("before repair start", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Completion_before_start_is_rejected()
    {
        var repairCase = CreateCase();
        var invalid = repairCase with
        {
            Execution = repairCase.Execution with
            {
                CompletedAt = repairCase.Execution.StartedAt.AddMinutes(-1)
            }
        };

        Assert.Throws<InvalidOperationException>(invalid.Validate);
    }

    private static HistoricalRepairCase CreateCase()
    {
        var orderId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var created = new DateTimeOffset(2025, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var started = created.AddDays(1);
        var completed = started.AddHours(3);
        var vehicle = new VehicleSnapshot(
            Guid.NewGuid(), "Example", "Model", 2015, 150_000, created.AddMinutes(-30));
        var order = new RepairOrder(
            orderId, vehicle, "CONTROL_ARM_REPLACE", null, created,
            ["VISIBLE_CORROSION"]);

        return new HistoricalRepairCase(
            vehicle,
            order,
            [new RepairOperation(Guid.NewGuid(), orderId, "SUSPENSION", "CONTROL_ARM_REPLACE", 1.8m)],
            [new KnownRepairCondition("VISIBLE_CORROSION", "MODERATE", created)],
            new RepairExecution(
                executionId, orderId, 180, started, completed,
                ["CONTROL_ARM_REPLACE"], "Additional extraction work was required."),
            [
                new RepairComplication(
                    Guid.NewGuid(), executionId, "SEIZED_FASTENER", 45,
                    started.AddMinutes(30), ["MOUNTING_HARDWARE_KIT"])
            ]);
    }
}
