using ARI.Core.Contracts;
using ARI.Infrastructure.Persistence.Hana;
using ARI.Infrastructure.Persistence.Hana.Import;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: ARI.HistoricalDataImporter <dataset.json> [batch-size]");
    return 2;
}

var batchSize = args.Length == 2 && int.TryParse(args[1], out var configuredBatchSize)
    ? configuredBatchSize
    : 100;
var hanaOptions = new HanaOptions
{
    Host = Environment.GetEnvironmentVariable("Hana__Host") ?? string.Empty,
    Port = int.TryParse(Environment.GetEnvironmentVariable("Hana__Port"), out var port) ? port : 443,
    User = Environment.GetEnvironmentVariable("Hana__User") ?? string.Empty,
    Password = Environment.GetEnvironmentVariable("Hana__Password") ?? string.Empty,
    ConnectionTimeoutSeconds = 60
};

var dataset = await HistoricalDatasetJsonReader.ReadAsync(Path.GetFullPath(args[0]));
var connectionFactory = new HanaConnectionFactory(hanaOptions);
var vehicles = new HanaVehicleSnapshotRepository(connectionFactory, hanaOptions);
var orders = new HanaRepairOrderRepository(connectionFactory, hanaOptions);
var importer = new HanaHistoricalRepairImportService(
    connectionFactory, hanaOptions, vehicles, orders);
importer.BatchCommitted += progress => Console.WriteLine(
    $"Committed progress: processed={progress.Processed}, imported={progress.Imported}, skipped={progress.Skipped}");
var result = await importer.ImportAsync(dataset, new HistoricalImportOptions(batchSize));

Console.WriteLine($"Dataset: {result.DatasetId}");
Console.WriteLine($"Imported this run: {result.ImportedCount}");
Console.WriteLine($"Skipped existing: {result.SkippedCount}");
Console.WriteLine($"Already complete: {result.AlreadyComplete}");
return 0;
