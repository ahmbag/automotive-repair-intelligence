using ARI.Core.Domain;

namespace ARI.Core.Abstractions;

public interface IRepairOrderRepository
{
    Task AddAsync(RepairOrder order, CancellationToken cancellationToken = default);

    Task<RepairOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RepairOrder?> GetWithVehicleAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
