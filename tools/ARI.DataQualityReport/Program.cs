using System.Text.Json;
using ARI.Infrastructure.Persistence.Hana;
using ARI.Infrastructure.Persistence.Hana.Analytics;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: ARI.DataQualityReport <dataset-id> [output.json]");
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
var profiler = new HanaHistoricalDataProfiler(new HanaConnectionFactory(options), options);
var profile = await profiler.AnalyzeAsync(args[0]);
var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);

if (args.Length == 2)
{
    await File.WriteAllTextAsync(Path.GetFullPath(args[1]), json + Environment.NewLine);
}

return profile.IsValid ? 0 : 1;
