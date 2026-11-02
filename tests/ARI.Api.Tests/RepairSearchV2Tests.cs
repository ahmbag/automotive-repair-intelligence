using ARI.Core.Abstractions;
using ARI.Core.Contracts;
using ARI.Core.Domain;
using ARI.Infrastructure.AI.Embeddings;
using ARI.Infrastructure.Persistence.Hana.Vector;

namespace ARI.Api.Tests;

public sealed class RepairSearchV2Tests
{
    [Fact]
    public void Pre_repair_profile_contains_domain_signals_but_no_outcomes()
    {
        var repairCase = Case();
        var profile = CanonicalRepairSearchProfile.BuildPreRepairProfile(repairCase);
        var evidence = CanonicalRepairSearchProfile.BuildCompletionEvidence(repairCase);

        Assert.Contains("PRE_REPAIR_PROFILE", profile);
        Assert.Contains("visible corrosion", profile);
        Assert.DoesNotContain("seized bolt", profile, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(repairCase.Execution.ActualLaborMinutes.ToString(), profile);
        Assert.Contains("COMPLETION_EVIDENCE", evidence);
        Assert.Contains("seized bolt", evidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unchanged_v2_index_reuses_both_documents_without_provider_call()
    {
        var provider = new StubProvider();
        var repository = new MemoryV2Repository();
        var indexer = new RepairSearchV2Indexer(provider, repository, Options());
        var dataset = new HistoricalRepairDataset(
            new HistoricalDatasetManifest("test", "1", 1, 1, "SYNTHETIC", 2, new string('a', 64)),
            [Case()]);

        var first = await indexer.IndexAsync(dataset);
        var second = await indexer.IndexAsync(dataset);

        Assert.Equal(2, first.EmbeddedCount);
        Assert.Equal(0, second.EmbeddedCount);
        Assert.Equal(2, second.UnchangedCount);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(2, repository.Records.Count);
    }

    [Fact]
    public async Task Hybrid_falls_back_to_structured_when_query_embedding_is_unavailable()
    {
        var v2 = new MemoryV2Repository();
        var service = new HanaRepairRetrievalService(new FailingProvider(), new UnusedV1Repository(), v2, Options());
        var result = await service.SearchAsync(Query());

        Assert.True(result.UsedStructuredFallback);
        Assert.Equal("QUERY_EMBEDDING_UNAVAILABLE", result.FallbackReason);
        Assert.Equal(RepairRetrievalStrategies.Structured, v2.LastStrategy);
    }

    [Fact]
    public async Task V2_index_rejects_provider_dimension_mismatch()
    {
        var repository = new MemoryV2Repository();
        var indexer = new RepairSearchV2Indexer(new WrongDimensionProvider(), repository, Options());
        var dataset = new HistoricalRepairDataset(
            new HistoricalDatasetManifest("test", "1", 1, 1, "SYNTHETIC", 2, new string('a', 64)),
            [Case()]);

        await Assert.ThrowsAsync<RepairEmbeddingUnavailableException>(() => indexer.IndexAsync(dataset));
        Assert.Empty(repository.Records);
    }

    private static EmbeddingProviderOptions Options() => new()
    {
        ApiKey = "test", Dimensions = 1536, BatchSize = 100,
        Model = "text-embedding-3-small", ModelVersion = "repair-report-embedding-v1",
        V2ModelVersion = "repair-search-embedding-v2",
        V2ProfileVersion = CanonicalRepairSearchProfile.ProfileVersion,
        V2DocumentVersion = CanonicalRepairSearchProfile.DocumentVersion
    };

    private static RepairRetrievalQuery Query() => new(
        "visible corrosion", "CONTROL_ARM_FRONT_LEFT_REPLACE", "Volkswagen", "Golf VII",
        12, 215_000, ["VISIBLE_CORROSION"], DateTimeOffset.UtcNow,
        RepairRetrievalStrategies.StructuredFirstHybrid, 5, -1,
        RepairEmbeddingProfiles.RepairProfileV2);

    private static HistoricalRepairCase Case()
    {
        var vehicle = new VehicleSnapshot(Guid.NewGuid(), "Volkswagen", "Golf VII", 2014, 215_000,
            new DateTimeOffset(2025, 1, 1, 8, 0, 0, TimeSpan.Zero));
        var order = new RepairOrder(Guid.NewGuid(), vehicle, "CONTROL_ARM_FRONT_LEFT_REPLACE",
            "Noise", vehicle.CapturedAtUtc.AddHours(1), ["VISIBLE_CORROSION"]);
        var operation = new RepairOperation(Guid.NewGuid(), order.Id, "FRONT_SUSPENSION",
            order.RepairCode, 1.8m);
        var condition = new KnownRepairCondition("VISIBLE_CORROSION", "HIGH", order.CreatedAt);
        var execution = new RepairExecution(Guid.NewGuid(), order.Id, 180, order.CreatedAt.AddDays(1),
            order.CreatedAt.AddDays(1).AddHours(3), [order.RepairCode],
            "A seized bolt required additional extraction work.");
        return new HistoricalRepairCase(vehicle, order, [operation], [condition], execution, []);
    }

    private sealed class StubProvider : IRepairEmbeddingProvider
    {
        public int Calls { get; private set; }
        public Task<RepairEmbeddingBatch> EmbedAsync(IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new RepairEmbeddingBatch("text-embedding-3-small", 1536,
                texts.Select(_ => (IReadOnlyList<float>)new float[1536]).ToArray(), texts.Count));
        }
    }

    private sealed class FailingProvider : IRepairEmbeddingProvider
    {
        public Task<RepairEmbeddingBatch> EmbedAsync(IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default) =>
            Task.FromException<RepairEmbeddingBatch>(new RepairEmbeddingUnavailableException("offline"));
    }

    private sealed class WrongDimensionProvider : IRepairEmbeddingProvider
    {
        public Task<RepairEmbeddingBatch> EmbedAsync(IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default) => Task.FromResult(new RepairEmbeddingBatch(
                "text-embedding-3-small", 3,
                texts.Select(_ => (IReadOnlyList<float>)new float[3]).ToArray(), texts.Count));
    }

    private sealed class MemoryV2Repository : IHanaRepairSearchV2Repository
    {
        public List<RepairSearchEmbeddingRecord> Records { get; } = [];
        public string? LastStrategy { get; private set; }
        public Task<IReadOnlyDictionary<string, string>> GetSourceHashesAsync(string embeddingModel,
            string modelVersion, string profileVersion, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(Records.ToDictionary(
                value => $"{value.RepairOrderId:D}|{value.ProfileType}", value => value.SourceSha256));
        public Task StoreAsync(IReadOnlyList<RepairSearchEmbeddingRecord> records,
            CancellationToken cancellationToken = default)
        {
            foreach (var value in records)
            {
                Records.RemoveAll(item => item.Id == value.Id);
                Records.Add(value);
            }
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<SimilarRepairCase>> SearchAsync(RepairRetrievalQuery query,
            IReadOnlyList<float>? queryVector, EmbeddingProviderOptions options,
            CancellationToken cancellationToken = default)
        {
            LastStrategy = query.Strategy;
            return Task.FromResult<IReadOnlyList<SimilarRepairCase>>([]);
        }
    }

    private sealed class UnusedV1Repository : IHanaVectorRepository
    {
        public Task<IReadOnlyDictionary<Guid, string>> GetSourceHashesAsync(string embeddingModel,
            string modelVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task StoreAsync(IReadOnlyList<RepairEmbeddingRecord> records,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SimilarRepairCase>> SearchAsync(RepairRetrievalQuery query,
            IReadOnlyList<float>? queryVector, EmbeddingProviderOptions options,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
