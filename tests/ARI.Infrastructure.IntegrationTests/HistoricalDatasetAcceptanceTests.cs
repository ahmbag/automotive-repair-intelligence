using ARI.Infrastructure.Persistence.Hana;
using ARI.Infrastructure.Persistence.Hana.Analytics;
using Xunit.Sdk;

namespace ARI.Infrastructure.IntegrationTests;

[Trait("Category", "DataAcceptance")]
public sealed class HistoricalDatasetAcceptanceTests
{
    [Fact]
    public async Task Imported_dataset_passes_all_quality_gates()
    {
        var host = Environment.GetEnvironmentVariable("Hana__Host");
        var user = Environment.GetEnvironmentVariable("Hana__User");
        var password = Environment.GetEnvironmentVariable("Hana__Password");
        var datasetId = Environment.GetEnvironmentVariable("ARI_DATASET_ID");
        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(user)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(datasetId))
        {
            throw SkipException.ForSkip("HANA or ARI_DATASET_ID configuration is absent.");
        }

        var options = new HanaOptions
        {
            Host = host,
            Port = int.TryParse(Environment.GetEnvironmentVariable("Hana__Port"), out var port) ? port : 443,
            User = user,
            Password = password,
            ConnectionTimeoutSeconds = 60
        };
        var profiler = new HanaHistoricalDataProfiler(new HanaConnectionFactory(options), options);

        var profile = await profiler.AnalyzeAsync(datasetId);

        Assert.True(profile.IsValid, string.Join(Environment.NewLine, profile.ValidationErrors));
        Assert.Equal(1000, profile.RepairOrders);
        Assert.Equal(1000, profile.CompleteCases);
        Assert.Equal(0, profile.OrphanCount);
        Assert.Equal(0, profile.InvalidLaborCount);
        Assert.Equal(0, profile.InvalidTimelineCount);
        Assert.Equal(0, profile.MissingReportCount);
        Assert.True(
            profile.Corrosion.KnownCorrosionMeanDeviationMinutes
            > profile.Corrosion.OtherMeanDeviationMinutes);
    }
}
