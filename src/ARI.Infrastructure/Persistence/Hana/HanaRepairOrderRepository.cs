using System.Data;
using System.Data.Common;
using System.Text.Json;
using ARI.Core.Abstractions;
using ARI.Core.Domain;
using Sap.Data.Hana;

namespace ARI.Infrastructure.Persistence.Hana;

public sealed class HanaRepairOrderRepository(
    IHanaConnectionFactory connectionFactory,
    HanaOptions options) : IRepairOrderRepository
{
    private const string InsertSql = """
        INSERT INTO ARI.REPAIR_ORDERS
            (ID, VEHICLE_SNAPSHOT_ID, REPAIR_CODE, DESCRIPTION, CREATED_AT_UTC,
             KNOWN_CONDITIONS_JSON, DATASET_ID)
        VALUES (?, ?, ?, ?, ?, ?, ?)
        """;

    private const string SelectJoinedSql = """
        SELECT r.ID, r.REPAIR_CODE, r.DESCRIPTION, r.CREATED_AT_UTC, r.KNOWN_CONDITIONS_JSON,
               v.ID, v.MANUFACTURER, v.MODEL, v.MODEL_YEAR, v.MILEAGE_KM, v.CAPTURED_AT_UTC
        FROM ARI.REPAIR_ORDERS r
        INNER JOIN ARI.VEHICLE_SNAPSHOTS v ON v.ID = r.VEHICLE_SNAPSHOT_ID
        WHERE r.ID = ?
        """;

    public async Task AddAsync(RepairOrder order, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await InsertAsync(connection, null, order, cancellationToken, null);
    }

    public Task<RepairOrder?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) => GetWithVehicleAsync(id, cancellationToken);

    public async Task<RepairOrder?> GetWithVehicleAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = HanaCommand.Create(
            connection, SelectJoinedSql, options.ConnectionTimeoutSeconds, null,
            (DbType.String, id.ToString("D")));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var conditions = JsonSerializer.Deserialize<string[]>(reader.GetString(4))
            ?? throw new JsonException("KNOWN_CONDITIONS_JSON must contain a JSON array.");
        var vehicle = HanaVehicleSnapshotRepository.Read(reader, 5);

        return new RepairOrder(
            Guid.Parse(reader.GetString(0)),
            vehicle,
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            HanaValueMapper.FromDatabaseTimestamp(reader.GetDateTime(3)),
            conditions);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM ARI.REPAIR_ORDERS WHERE ID = ?";
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = HanaCommand.Create(
            connection, sql, options.ConnectionTimeoutSeconds, null,
            (DbType.String, id.ToString("D")));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal async Task InsertAsync(
        DbConnection connection,
        DbTransaction? transaction,
        RepairOrder order,
        CancellationToken cancellationToken,
        string? datasetId = null)
    {
        ArgumentNullException.ThrowIfNull(order);

        try
        {
            await using var command = HanaCommand.Create(
                connection, InsertSql, options.ConnectionTimeoutSeconds, transaction,
                (DbType.String, order.Id.ToString("D")),
                (DbType.String, order.Vehicle.Id.ToString("D")),
                (DbType.String, HanaValueMapper.NormalizeRepairCode(order.RepairCode)),
                (DbType.String, order.ProblemDescription),
                (DbType.DateTime, HanaValueMapper.ToDatabaseTimestamp(order.CreatedAt)),
                (DbType.String, JsonSerializer.Serialize(order.KnownConditions)),
                (DbType.String, datasetId));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (HanaException exception)
        {
            throw HanaPersistenceErrors.Translate(exception, options);
        }
    }
}
