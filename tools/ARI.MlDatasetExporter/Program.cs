using ARI.Infrastructure.MachineLearning;
using ARI.Infrastructure.Persistence.Hana;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: ARI.MlDatasetExporter <dataset-id> <output.json>");
    return 2;
}

var options = new HanaOptions
{
    Host = Environment.GetEnvironmentVariable("Hana__Host") ?? string.Empty,
    Port = int.TryParse(Environment.GetEnvironmentVariable("Hana__Port"), out var port) ? port : 443,
    User = Environment.GetEnvironmentVariable("Hana__User") ?? string.Empty,
    Password = Environment.GetEnvironmentVariable("Hana__Password") ?? string.Empty,
    ConnectionTimeoutSeconds = 60
};
var exporter = new HanaTrainingDatasetExporter(new HanaConnectionFactory(options), options);
var export = await exporter.ExportAsync(args[0]);
await HanaTrainingDatasetExporter.WriteAsync(export, args[1]);

Console.WriteLine($"Dataset: {export.Manifest.DatasetId}");
Console.WriteLine($"Rows: {export.Manifest.RecordCount}");
Console.WriteLine($"Source checksum: {export.Manifest.SourceDatasetChecksum}");
Console.WriteLine($"Export checksum: {export.Manifest.ExportChecksum}");
return 0;
