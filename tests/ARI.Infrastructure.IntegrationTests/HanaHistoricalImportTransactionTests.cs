using System.Data;
using ARI.Core.Contracts;
using ARI.Core.Domain;
using ARI.Infrastructure.Persistence.Hana;
using ARI.Infrastructure.Persistence.Hana.Import;
using Xunit.Sdk;

namespace ARI.Infrastructure.IntegrationTests;

[Trait("Category", "HanaIntegration")]
public sealed class HanaHistoricalImportTransactionTests
{
    [Fact]
    public async Task Failing_record_rolls_back_the_entire_import_batch()
    {
        var context = CreateContextOrSkip();
        var datasetId = $"ari-import-rollback-{Guid.NewGuid():N}";
        var first = CreateCase();
        var second = CreateCase();
        second = second with
        {
            Operations =
            [second.Operations.Single() with { Id = first.Operations.Single().Id }]
        };
        var dataset = new HistoricalRepairDataset(
            new HistoricalDatasetManifest(
                datasetId, "test", 1, 2, "SYNTHETIC", 2, new string('0', 64)),
            [first, second]);

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(
                () => context.Importer.ImportAsync(dataset, new HistoricalImportOptions(100)));

            Assert.Null(await context.Orders.GetByIdAsync(first.Order.Id));
            Assert.Null(await context.Orders.GetByIdAsync(second.Order.Id));
            Assert.Null(await context.Vehicles.GetByIdAsync(first.Vehicle.Id));
            Assert.Null(await context.Vehicles.GetByIdAsync(second.Vehicle.Id));
        }
        finally
        {
            await DeleteDatasetMetadataAsync(context.Factory, context.Options, datasetId);
        }
    }

    private static Context CreateContextOrSkip()
    {
        var host = Environment.GetEnvironmentVariable("Hana__Host");
        var user = Environment.GetEnvironmentVariable("Hana__User");
        var password = Environment.GetEnvironmentVariable("Hana__Password");
        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(user)
            || string.IsNullOrWhiteSpace(password))
        {
            throw SkipException.ForSkip("SAP HANA integration configuration is absent.");
        }

        var options = new HanaOptions
        {
            Host = host,
            Port = int.TryParse(Environment.GetEnvironmentVariable("Hana__Port"), out var port) ? port : 443,
            User = user,
            Password = password,
            ConnectionTimeoutSeconds = 30
        };
        var factory = new HanaConnectionFactory(options);
        var vehicles = new HanaVehicleSnapshotRepository(factory, options);
        var orders = new HanaRepairOrderRepository(factory, options);
        return new Context(
            factory,
            options,
            vehicles,
            orders,
            new HanaHistoricalRepairImportService(factory, options, vehicles, orders));
    }

    private static HistoricalRepairCase CreateCase()
    {
        var created = new DateTimeOffset(2025, 2, 1, 8, 0, 0, TimeSpan.Zero);
        var started = created.AddDays(1);
        var orderId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var vehicle = new VehicleSnapshot(
            Guid.NewGuid(), "Test", "Rollback", 2018, 90_000, created);
        var order = new RepairOrder(
            orderId, vehicle, "CONTROL_ARM_REPLACE", null, created, []);
        return new HistoricalRepairCase(
            vehicle,
            order,
            [new RepairOperation(Guid.NewGuid(), orderId, "SUSPENSION", "CONTROL_ARM_REPLACE", 1.8m)],
            [],
            new RepairExecution(
                executionId, orderId, 120, started, started.AddHours(3),
                ["CONTROL_ARM_REPLACE"], "Completed."),
            []);
    }

    private static async Task DeleteDatasetMetadataAsync(
        IHanaConnectionFactory factory,
        HanaOptions options,
        string datasetId)
    {
        await using var connection = await factory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ARI.HISTORICAL_DATASETS WHERE DATASET_ID = ?";
        command.CommandTimeout = options.ConnectionTimeoutSeconds;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "p0";
        parameter.DbType = DbType.String;
        parameter.Value = datasetId;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record Context(
        IHanaConnectionFactory Factory,
        HanaOptions Options,
        HanaVehicleSnapshotRepository Vehicles,
        HanaRepairOrderRepository Orders,
        HanaHistoricalRepairImportService Importer);
}
