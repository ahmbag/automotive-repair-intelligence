namespace ARI.Core.Abstractions;

public interface IRepairBaselineProvider
{
    Task<decimal> GetLaborHoursAsync(
        string repairCode,
        CancellationToken cancellationToken = default);
}
