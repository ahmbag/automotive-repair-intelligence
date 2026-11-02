using ARI.Core.Abstractions;
using ARI.Core.Domain;

namespace ARI.Infrastructure.Persistence.Hana;

public sealed class HanaRepairOrderPersistence(
    IHanaConnectionFactory connectionFactory,
    HanaVehicleSnapshotRepository vehicleSnapshots,
    HanaRepairOrderRepository repairOrders) : IRepairOrderPersistence
{
    public async Task CreateAsync(
        VehicleSnapshot snapshot,
        RepairOrder order,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(order);

        if (order.Vehicle.Id != snapshot.Id)
        {
            throw new ArgumentException(
                "The repair order must reference the supplied vehicle snapshot.", nameof(order));
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await vehicleSnapshots.InsertAsync(connection, transaction, snapshot, cancellationToken);
            await repairOrders.InsertAsync(connection, transaction, order, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
