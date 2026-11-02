using ARI.Core.Domain;

namespace ARI.Core.Abstractions;

/// <summary>Persists the repair-order aggregate as one atomic business operation.</summary>
public interface IRepairOrderPersistence
{
    Task CreateAsync(
        VehicleSnapshot snapshot,
        RepairOrder order,
        CancellationToken cancellationToken = default);
}
