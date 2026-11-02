using ARI.Core.Domain;

namespace ARI.Core.Abstractions;

public interface IVehicleSnapshotRepository
{
    Task AddAsync(VehicleSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<VehicleSnapshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
