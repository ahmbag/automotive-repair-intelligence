using ARI.Core.Domain;

namespace ARI.Core.Abstractions;

public interface IRepairEstimateRepository
{
    Task SaveAsync(RepairEstimate estimate, CancellationToken cancellationToken = default);

    Task<RepairEstimate?> FindAsync(Guid id, CancellationToken cancellationToken = default);
}
