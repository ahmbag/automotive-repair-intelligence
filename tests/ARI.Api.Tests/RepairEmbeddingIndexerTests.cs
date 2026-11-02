using ARI.Core.Abstractions;
using ARI.Core.Contracts;
using ARI.Core.Domain;
using ARI.Infrastructure.AI.Embeddings;
using ARI.Infrastructure.Persistence.Hana.Vector;

namespace ARI.Api.Tests;

public sealed class RepairEmbeddingIndexerTests
{
    [Fact]
    public async Task Unchanged_second_run_makes_no_provider_call_or_duplicate()
    {
        var provider = new StubProvider();
        var repository = new MemoryRepository();
        var indexer = new RepairEmbeddingIndexer(provider, repository, Options());
        var dataset = Dataset();

        var first = await indexer.IndexAsync(dataset);
        var second = await indexer.IndexAsync(dataset);

        Assert.Equal(1, first.EmbeddedCount);
        Assert.Equal(0, second.EmbeddedCount);
        Assert.Equal(1, second.UnchangedCount);
        Assert.Equal(1, provider.Calls);
        Assert.Single(repository.Records);
    }

    private static EmbeddingProviderOptions Options() => new()
    {
        ApiKey = "test",
        Model = "text-embedding-3-small",
        ModelVersion = "repair-report-embedding-v1",
        Dimensions = 1536,
        BatchSize = 100
    };

    private static HistoricalRepairDataset Dataset()
    {
        var vehicle = new VehicleSnapshot(Guid.NewGuid(), "Volkswagen", "Golf VII", 2014, 200_000,
            new DateTimeOffset(2024, 1, 1, 8, 0, 0, TimeSpan.Zero));
        var order = new RepairOrder(Guid.NewGuid(), vehicle, "CONTROL_ARM_FRONT_LEFT_REPLACE",
            "Noise", vehicle.CapturedAtUtc.AddHours(1), []);
        var operation = new RepairOperation(Guid.NewGuid(), order.Id, "FRONT_SUSPENSION",
            order.RepairCode, 1.8m);
        var execution = new RepairExecution(Guid.NewGuid(), order.Id, 120,
            order.CreatedAt.AddDays(1), order.CreatedAt.AddDays(1).AddHours(2),
            [order.RepairCode], "A mounting bolt required additional removal work.");
        var repairCase = new HistoricalRepairCase(vehicle, order, [operation], [], execution, []);
        return new HistoricalRepairDataset(
            new HistoricalDatasetManifest("test", "1", 1, 1, "SYNTHETIC", 2, new string('a', 64)),
            [repairCase]);
    }

    private sealed class StubProvider : IRepairEmbeddingProvider
    {
        public int Calls { get; private set; }

        public Task<RepairEmbeddingBatch> EmbedAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            IReadOnlyList<IReadOnlyList<float>> vectors = texts
                .Select(_ => (IReadOnlyList<float>)Enumerable.Repeat(0.01f, 1536).ToArray())
                .ToArray();
            return Task.FromResult(new RepairEmbeddingBatch(
                "text-embedding-3-small", 1536, vectors, texts.Count));
        }
    }

    private sealed class MemoryRepository : IHanaVectorRepository
    {
        public List<RepairEmbeddingRecord> Records { get; } = [];

        public Task<IReadOnlyDictionary<Guid, string>> GetSourceHashesAsync(
            string embeddingModel,
            string modelVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                Records.ToDictionary(item => item.RepairOrderId, item => item.SourceSha256));

        public Task StoreAsync(
            IReadOnlyList<RepairEmbeddingRecord> records,
            CancellationToken cancellationToken = default)
        {
            foreach (var record in records)
            {
                Records.RemoveAll(item => item.RepairOrderId == record.RepairOrderId &&
                                          item.EmbeddingModel == record.EmbeddingModel &&
                                          item.ModelVersion == record.ModelVersion);
                Records.Add(record);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SimilarRepairCase>> SearchAsync(
            RepairRetrievalQuery query,
            IReadOnlyList<float>? queryVector,
            EmbeddingProviderOptions embeddingOptions,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
