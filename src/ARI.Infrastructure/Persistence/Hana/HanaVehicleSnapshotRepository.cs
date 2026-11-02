using System.Data;
using System.Data.Common;
using ARI.Core.Abstractions;
using ARI.Core.Domain;
using Sap.Data.Hana;

namespace ARI.Infrastructure.Persistence.Hana;

public sealed class HanaVehicleSnapshotRepository(
    IHanaConnectionFactory connectionFactory,
    HanaOptions options) : IVehicleSnapshotRepository
{
    private const string InsertSql = """
        INSERT INTO ARI.VEHICLE_SNAPSHOTS
            (ID, MANUFACTURER, MODEL, MODEL_YEAR, MILEAGE_KM, CAPTURED_AT_UTC)
        VALUES (?, ?, ?, ?, ?, ?)
        """;

    public async Task AddAsync(
        VehicleSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await InsertAsync(connection, null, snapshot, cancellationToken);
    }

    public async Task<VehicleSnapshot?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ID, MANUFACTURER, MODEL, MODEL_YEAR, MILEAGE_KM, CAPTURED_AT_UTC
            FROM ARI.VEHICLE_SNAPSHOTS
            WHERE ID = ?
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = HanaCommand.Create(
            connection, sql, options.ConnectionTimeoutSeconds, null,
            (DbType.String, id.ToString("D")));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM ARI.VEHICLE_SNAPSHOTS WHERE ID = ?";
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = HanaCommand.Create(
            connection, sql, options.ConnectionTimeoutSeconds, null,
            (DbType.String, id.ToString("D")));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal async Task InsertAsync(
        DbConnection connection,
        DbTransaction? transaction,
        VehicleSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            await using var command = HanaCommand.Create(
                connection, InsertSql, options.ConnectionTimeoutSeconds, transaction,
                (DbType.String, snapshot.Id.ToString("D")),
                (DbType.String, snapshot.Manufacturer),
                (DbType.String, snapshot.Model),
                (DbType.Int32, snapshot.ModelYear),
                (DbType.Int32, snapshot.MileageKm),
                (DbType.DateTime, HanaValueMapper.ToDatabaseTimestamp(snapshot.CapturedAtUtc)));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (HanaException exception)
        {
            throw HanaPersistenceErrors.Translate(exception, options);
        }
    }

    internal static VehicleSnapshot Read(DbDataReader reader, int offset = 0) =>
        new(
            Guid.Parse(reader.GetString(offset)),
            reader.GetString(offset + 1),
            reader.GetString(offset + 2),
            reader.GetInt32(offset + 3),
            reader.GetInt32(offset + 4),
            HanaValueMapper.FromDatabaseTimestamp(reader.GetDateTime(offset + 5)));
}
