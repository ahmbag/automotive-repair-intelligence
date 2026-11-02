using System.Data;
using System.Data.Common;
using System.Text.Json;
using ARI.Core.Contracts;
using ARI.Core.Domain;

namespace ARI.Infrastructure.Persistence.Hana.Import;

public sealed class HanaHistoricalRepairImportService(
    IHanaConnectionFactory connectionFactory,
    HanaOptions hanaOptions,
    HanaVehicleSnapshotRepository vehicleSnapshots,
    HanaRepairOrderRepository repairOrders) : IHistoricalRepairImportService
{
    public event Action<HistoricalImportProgress>? BatchCommitted;

    public async Task<HistoricalImportResult> ImportAsync(
        HistoricalRepairDataset dataset,
        HistoricalImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateDataset(dataset, options);
        var state = await EnsureDatasetAsync(dataset.Manifest, cancellationToken);

        if (state.Status == "COMPLETE")
        {
            if (state.ImportedCount != dataset.Manifest.RecordCount)
            {
                throw new InvalidOperationException(
                    "A completed dataset has an inconsistent imported-record count.");
            }

            return new HistoricalImportResult(
                dataset.Manifest.DatasetId, 0, dataset.Manifest.RecordCount, true);
        }

        var imported = 0;
        var skipped = 0;

        foreach (var batch in dataset.Records.Chunk(options.BatchSize))
        {
            await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var importedInBatch = 0;

            try
            {
                foreach (var repairCase in batch)
                {
                    repairCase.Validate();
                    var existingDataset = await FindOrderDatasetAsync(
                        connection, transaction, repairCase.Order.Id, cancellationToken);

                    if (existingDataset is not null)
                    {
                        if (!string.Equals(
                                existingDataset,
                                dataset.Manifest.DatasetId,
                                StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "A deterministic order identifier belongs to another dataset.");
                        }

                        skipped++;
                        continue;
                    }

                    await InsertCaseAsync(
                        connection,
                        transaction,
                        dataset.Manifest.DatasetId,
                        repairCase,
                        cancellationToken);
                    importedInBatch++;
                }

                if (importedInBatch > 0)
                {
                    await UpdateImportedCountAsync(
                        connection, transaction, dataset.Manifest.DatasetId, importedInBatch,
                        cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                imported += importedInBatch;
                BatchCommitted?.Invoke(
                    new HistoricalImportProgress(imported + skipped, imported, skipped));
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        await MarkCompleteAsync(dataset.Manifest, cancellationToken);
        return new HistoricalImportResult(dataset.Manifest.DatasetId, imported, skipped, false);
    }

    private async Task InsertCaseAsync(
        DbConnection connection,
        DbTransaction transaction,
        string datasetId,
        HistoricalRepairCase repairCase,
        CancellationToken cancellationToken)
    {
        await vehicleSnapshots.InsertAsync(
            connection, transaction, repairCase.Vehicle, cancellationToken);
        await repairOrders.InsertAsync(
            connection, transaction, repairCase.Order, cancellationToken, datasetId);

        foreach (var condition in repairCase.KnownConditions)
        {
            const string sql = """
                INSERT INTO ARI.REPAIR_CONDITIONS
                    (REPAIR_ORDER_ID, CONDITION_CODE, SEVERITY, DOCUMENTED_AT_UTC)
                VALUES (?, ?, ?, ?)
                """;
            await ExecuteAsync(
                connection, transaction, sql, cancellationToken,
                (DbType.String, repairCase.Order.Id.ToString("D")),
                (DbType.String, condition.ConditionCode),
                (DbType.String, condition.Severity),
                (DbType.DateTime, HanaValueMapper.ToDatabaseTimestamp(condition.DocumentedAt)));
        }

        var sequence = 0;
        foreach (var operation in repairCase.Operations)
        {
            const string sql = """
                INSERT INTO ARI.REPAIR_OPERATIONS
                    (ID, REPAIR_ORDER_ID, ASSEMBLY_CODE, OPERATION_CODE,
                     REFERENCE_LABOR_HOURS, SEQUENCE_NUMBER)
                VALUES (?, ?, ?, ?, ?, ?)
                """;
            await ExecuteAsync(
                connection, transaction, sql, cancellationToken,
                (DbType.String, operation.Id.ToString("D")),
                (DbType.String, operation.RepairOrderId.ToString("D")),
                (DbType.String, operation.AssemblyCode),
                (DbType.String, operation.OperationCode),
                (DbType.Decimal, operation.ReferenceLaborHours),
                (DbType.Int32, ++sequence));
        }

        await InsertExecutionAsync(connection, transaction, repairCase.Execution, cancellationToken);

        foreach (var complication in repairCase.Complications)
        {
            const string sql = """
                INSERT INTO ARI.REPAIR_COMPLICATIONS
                    (ID, REPAIR_EXECUTION_ID, COMPLICATION_CODE, ADDITIONAL_LABOR_MINUTES,
                     OCCURRED_AT_UTC, ADDITIONAL_PART_CODES_JSON)
                VALUES (?, ?, ?, ?, ?, ?)
                """;
            await ExecuteAsync(
                connection, transaction, sql, cancellationToken,
                (DbType.String, complication.Id.ToString("D")),
                (DbType.String, complication.RepairExecutionId.ToString("D")),
                (DbType.String, complication.ComplicationCode),
                (DbType.Int32, complication.AdditionalLaborMinutes),
                (DbType.DateTime, HanaValueMapper.ToDatabaseTimestamp(complication.OccurredAt)),
                (DbType.String, JsonSerializer.Serialize(complication.AdditionalPartCodes)));
        }
    }

    private async Task InsertExecutionAsync(
        DbConnection connection,
        DbTransaction transaction,
        RepairExecution execution,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO ARI.REPAIR_EXECUTIONS
                (ID, REPAIR_ORDER_ID, ACTUAL_LABOR_MINUTES, STARTED_AT_UTC, COMPLETED_AT_UTC,
                 PERFORMED_OPERATION_CODES_JSON, REPAIR_REPORT)
            VALUES (?, ?, ?, ?, ?, ?, ?)
            """;
        await ExecuteAsync(
            connection, transaction, sql, cancellationToken,
            (DbType.String, execution.Id.ToString("D")),
            (DbType.String, execution.RepairOrderId.ToString("D")),
            (DbType.Int32, execution.ActualLaborMinutes),
            (DbType.DateTime, HanaValueMapper.ToDatabaseTimestamp(execution.StartedAt)),
            (DbType.DateTime, HanaValueMapper.ToDatabaseTimestamp(execution.CompletedAt)),
            (DbType.String, JsonSerializer.Serialize(execution.PerformedOperationCodes)),
            (DbType.String, execution.RepairReport));
    }

    private async Task<DatasetState> EnsureDatasetAsync(
        HistoricalDatasetManifest manifest,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        const string selectSql = """
            SELECT GENERATOR_VERSION, SEED, EXPECTED_RECORD_COUNT, DATA_CLASSIFICATION,
                   SCHEMA_VERSION, CHECKSUM, IMPORT_STATUS, IMPORTED_RECORD_COUNT
            FROM ARI.HISTORICAL_DATASETS
            WHERE DATASET_ID = ?
            """;
        await using var select = HanaCommand.Create(
            connection, selectSql, hanaOptions.ConnectionTimeoutSeconds, null,
            (DbType.String, manifest.DatasetId));
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            var matches = reader.GetString(0) == manifest.GeneratorVersion
                && reader.GetInt32(1) == manifest.Seed
                && reader.GetInt32(2) == manifest.RecordCount
                && reader.GetString(3) == manifest.DataClassification
                && reader.GetInt32(4) == manifest.SchemaVersion
                && reader.GetString(5) == manifest.Checksum;
            if (!matches)
            {
                throw new InvalidOperationException(
                    "Dataset ID already exists with different immutable metadata.");
            }

            return new DatasetState(reader.GetString(6), reader.GetInt32(7));
        }

        await reader.DisposeAsync();
        const string insertSql = """
            INSERT INTO ARI.HISTORICAL_DATASETS
                (DATASET_ID, GENERATOR_VERSION, SEED, EXPECTED_RECORD_COUNT,
                 IMPORTED_RECORD_COUNT, DATA_CLASSIFICATION, SCHEMA_VERSION,
                 CHECKSUM, IMPORT_STATUS, IMPORTED_AT_UTC)
            VALUES (?, ?, ?, ?, 0, ?, ?, ?, 'IMPORTING', NULL)
            """;
        await using var insert = HanaCommand.Create(
            connection, insertSql, hanaOptions.ConnectionTimeoutSeconds, null,
            (DbType.String, manifest.DatasetId),
            (DbType.String, manifest.GeneratorVersion),
            (DbType.Int32, manifest.Seed),
            (DbType.Int32, manifest.RecordCount),
            (DbType.String, manifest.DataClassification),
            (DbType.Int32, manifest.SchemaVersion),
            (DbType.String, manifest.Checksum));
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return new DatasetState("IMPORTING", 0);
    }

    private async Task MarkCompleteAsync(
        HistoricalDatasetManifest manifest,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE ARI.HISTORICAL_DATASETS
            SET IMPORT_STATUS = 'COMPLETE', IMPORTED_AT_UTC = CURRENT_UTCTIMESTAMP
            WHERE DATASET_ID = ? AND IMPORTED_RECORD_COUNT = EXPECTED_RECORD_COUNT
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = HanaCommand.Create(
            connection, sql, hanaOptions.ConnectionTimeoutSeconds, null,
            (DbType.String, manifest.DatasetId));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException(
                "Dataset could not be completed because its imported count is inconsistent.");
        }
    }

    private async Task<string?> FindOrderDatasetAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT DATASET_ID FROM ARI.REPAIR_ORDERS WHERE ID = ?";
        await using var command = HanaCommand.Create(
            connection, sql, hanaOptions.ConnectionTimeoutSeconds, transaction,
            (DbType.String, orderId.ToString("D")));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private async Task UpdateImportedCountAsync(
        DbConnection connection,
        DbTransaction transaction,
        string datasetId,
        int amount,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE ARI.HISTORICAL_DATASETS
            SET IMPORTED_RECORD_COUNT = IMPORTED_RECORD_COUNT + ?
            WHERE DATASET_ID = ? AND IMPORT_STATUS = 'IMPORTING'
            """;
        var affected = await ExecuteAsync(
            connection, transaction, sql, cancellationToken,
            (DbType.Int32, amount),
            (DbType.String, datasetId));
        if (affected != 1)
        {
            throw new InvalidOperationException("Dataset progress could not be updated.");
        }
    }

    private async Task<int> ExecuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (DbType Type, object? Value)[] parameters)
    {
        await using var command = HanaCommand.Create(
            connection, sql, hanaOptions.ConnectionTimeoutSeconds, transaction, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateDataset(
        HistoricalRepairDataset dataset,
        HistoricalImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        if (options.BatchSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), "Batch size must be between 1 and 500.");
        }

        if (dataset.Manifest.RecordCount != dataset.Records.Count)
        {
            throw new InvalidOperationException("Manifest record count does not match the dataset.");
        }

        if (dataset.Manifest.SchemaVersion != 2
            || dataset.Manifest.DataClassification != "SYNTHETIC")
        {
            throw new InvalidOperationException(
                "Only schema version 2 synthetic datasets are accepted.");
        }

        if (dataset.Records.Select(record => record.Order.Id).Distinct().Count()
            != dataset.Records.Count)
        {
            throw new InvalidOperationException("Dataset contains duplicate repair-order IDs.");
        }
    }

    private sealed record DatasetState(string Status, int ImportedCount);
}

public sealed record HistoricalImportProgress(int Processed, int Imported, int Skipped);
