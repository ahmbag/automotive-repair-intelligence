using ARI.Core.Domain;
using ARI.Core.Persistence;
using ARI.Infrastructure.Persistence.Hana;
using Xunit.Sdk;

namespace ARI.Infrastructure.IntegrationTests;

[Trait("Category", "HanaIntegration")]
public sealed class HanaPersistenceTests
{
    [Fact]
    public async Task Vehicle_snapshot_round_trips()
    {
        var sut = CreateContextOrSkip();
        var snapshot = CreateSnapshot();

        try
        {
            await sut.Vehicles.AddAsync(snapshot);
            Assert.Equal(snapshot, await sut.Vehicles.GetByIdAsync(snapshot.Id));
        }
        finally
        {
            await sut.Vehicles.DeleteAsync(snapshot.Id);
        }
    }

    [Fact]
    public async Task Repair_order_round_trips_with_normalized_code_and_null_description()
    {
        var sut = CreateContextOrSkip();
        var snapshot = CreateSnapshot();
        var order = CreateOrder(snapshot, description: null, repairCode: "  brake.front  ");

        try
        {
            await sut.Persistence.CreateAsync(snapshot, order);
            var loaded = await sut.Orders.GetByIdAsync(order.Id);

            Assert.NotNull(loaded);
            Assert.Equal("BRAKE.FRONT", loaded.RepairCode);
            Assert.Null(loaded.ProblemDescription);
            Assert.Equal(order.KnownConditions, loaded.KnownConditions);
        }
        finally
        {
            await CleanupAsync(sut, order.Id, snapshot.Id);
        }
    }

    [Fact]
    public async Task Joined_load_returns_order_and_vehicle_together()
    {
        var sut = CreateContextOrSkip();
        var snapshot = CreateSnapshot();
        var order = CreateOrder(snapshot);

        try
        {
            await sut.Persistence.CreateAsync(snapshot, order);
            var loaded = await sut.Orders.GetWithVehicleAsync(order.Id);

            Assert.NotNull(loaded);
            Assert.Equal(snapshot, loaded.Vehicle);
            Assert.Equal(order.Id, loaded.Id);
        }
        finally
        {
            await CleanupAsync(sut, order.Id, snapshot.Id);
        }
    }

    [Fact]
    public async Task Foreign_key_rejects_unknown_vehicle()
    {
        var sut = CreateContextOrSkip();
        var snapshot = CreateSnapshot();
        var order = CreateOrder(snapshot);

        var exception = await Record.ExceptionAsync(() => sut.Orders.AddAsync(order));

        Assert.IsType<PersistenceIntegrityException>(exception);
        Assert.Null(await sut.Orders.GetByIdAsync(order.Id));
    }

    [Fact]
    public async Task Duplicate_order_id_is_reported_as_conflict()
    {
        var sut = CreateContextOrSkip();
        var snapshot = CreateSnapshot();
        var order = CreateOrder(snapshot);

        try
        {
            await sut.Persistence.CreateAsync(snapshot, order);
            var exception = await Record.ExceptionAsync(() => sut.Orders.AddAsync(order));
            Assert.IsType<PersistenceConflictException>(exception);
        }
        finally
        {
            await CleanupAsync(sut, order.Id, snapshot.Id);
        }
    }

    [Fact]
    public async Task Failed_second_insert_rolls_back_first_insert()
    {
        var sut = CreateContextOrSkip();
        var existingSnapshot = CreateSnapshot();
        var duplicateOrder = CreateOrder(existingSnapshot);
        var rolledBackSnapshot = CreateSnapshot();
        var attemptedOrder = duplicateOrder with { Vehicle = rolledBackSnapshot };

        try
        {
            await sut.Persistence.CreateAsync(existingSnapshot, duplicateOrder);

            var exception = await Record.ExceptionAsync(
                () => sut.Persistence.CreateAsync(rolledBackSnapshot, attemptedOrder));

            Assert.IsType<PersistenceConflictException>(exception);
            Assert.Null(await sut.Vehicles.GetByIdAsync(rolledBackSnapshot.Id));
            Assert.Equal(existingSnapshot, (await sut.Orders.GetByIdAsync(duplicateOrder.Id))?.Vehicle);
        }
        finally
        {
            await CleanupAsync(sut, duplicateOrder.Id, existingSnapshot.Id);
            await sut.Vehicles.DeleteAsync(rolledBackSnapshot.Id);
        }
    }

    [Fact]
    public async Task Missing_ids_return_null()
    {
        var sut = CreateContextOrSkip();
        Assert.Null(await sut.Vehicles.GetByIdAsync(Guid.NewGuid()));
        Assert.Null(await sut.Orders.GetWithVehicleAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Unicode_special_characters_and_utc_round_trip()
    {
        var sut = CreateContextOrSkip();
        var snapshot = CreateSnapshot() with
        {
            Manufacturer = "Škoda & Söhne",
            Model = "Enyaq Coupé 'RS'",
            CapturedAtUtc = new DateTimeOffset(2026, 10, 23, 8, 9, 10, TimeSpan.Zero)
        };
        var order = CreateOrder(snapshot, "Klopfen / Ölverlust – prüfen 🔧") with
        {
            CreatedAt = new DateTimeOffset(2026, 10, 23, 9, 10, 11, TimeSpan.Zero),
            KnownConditions = ["ÖL_UNTER_MINIMUM", "KORROSION_≤_10%"]
        };

        try
        {
            await sut.Persistence.CreateAsync(snapshot, order);
            var loaded = await sut.Orders.GetWithVehicleAsync(order.Id);

            Assert.NotNull(loaded);
            Assert.Equal(order.ProblemDescription, loaded.ProblemDescription);
            Assert.Equal(order.KnownConditions, loaded.KnownConditions);
            Assert.Equal(snapshot.CapturedAtUtc, loaded.Vehicle.CapturedAtUtc);
            Assert.Equal(order.CreatedAt, loaded.CreatedAt);
            Assert.Equal(TimeSpan.Zero, loaded.CreatedAt.Offset);
        }
        finally
        {
            await CleanupAsync(sut, order.Id, snapshot.Id);
        }
    }

    [Fact]
    public void Missing_connection_configuration_fails_without_secret_leakage()
    {
        var options = new HanaOptions { User = "ARI_APP", Password = "top-secret" };
        var exception = Assert.Throws<HanaConfigurationException>(
            () => HanaConnectionFactory.CreateConnectionString(options));

        Assert.DoesNotContain(options.Password, exception.Message, StringComparison.Ordinal);
    }

    private static TestContext CreateContextOrSkip()
    {
        var host = Environment.GetEnvironmentVariable("Hana__Host");
        var user = Environment.GetEnvironmentVariable("Hana__User");
        var password = Environment.GetEnvironmentVariable("Hana__Password");
        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(user)
            || string.IsNullOrWhiteSpace(password))
        {
            throw SkipException.ForSkip(
                "SAP HANA integration configuration is absent; no integration assertion was executed.");
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
        var persistence = new HanaRepairOrderPersistence(factory, vehicles, orders);
        return new TestContext(vehicles, orders, persistence);
    }

    private static VehicleSnapshot CreateSnapshot() =>
        new(
            Guid.NewGuid(),
            "Volkswagen",
            "Golf VIII",
            2022,
            48_123,
            new DateTimeOffset(2026, 10, 23, 7, 8, 9, TimeSpan.Zero));

    private static RepairOrder CreateOrder(
        VehicleSnapshot snapshot,
        string? description = "Front brake vibration",
        string repairCode = "BRAKE.FRONT") =>
        new(
            Guid.NewGuid(),
            snapshot,
            repairCode,
            description,
            new DateTimeOffset(2026, 10, 23, 8, 9, 10, TimeSpan.Zero),
            ["CORROSION_VISIBLE", "WEAR_HIGH"]);

    private static async Task CleanupAsync(TestContext sut, Guid orderId, Guid snapshotId)
    {
        await sut.Orders.DeleteAsync(orderId);
        await sut.Vehicles.DeleteAsync(snapshotId);
    }

    private sealed record TestContext(
        HanaVehicleSnapshotRepository Vehicles,
        HanaRepairOrderRepository Orders,
        HanaRepairOrderPersistence Persistence);
}
