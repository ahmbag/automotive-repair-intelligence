using System.Security.Cryptography;
using System.Text;
using ARI.Core.Abstractions;
using ARI.Core.Contracts;
using ARI.Infrastructure.AI.Embeddings;

namespace ARI.Infrastructure.Persistence.Hana.Vector;

public sealed record RepairSearchV2IndexResult(
    int RepairCaseCount, int DocumentCount, int EmbeddedCount, int UnchangedCount,
    int ProviderBatchCount, int? InputTokens);

public sealed class RepairSearchV2Indexer(
    IRepairEmbeddingProvider embeddingProvider,
    IHanaRepairSearchV2Repository repository,
    EmbeddingProviderOptions options)
{
    public async Task<RepairSearchV2IndexResult> IndexAsync(
        HistoricalRepairDataset dataset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        var current = await repository.GetSourceHashesAsync(
            options.Model, options.V2ModelVersion, options.V2ProfileVersion, cancellationToken);
        var documents = dataset.Records.SelectMany(repairCase => new[]
        {
            Create(repairCase.Order.Id, RepairSearchDocumentTypes.PreRepairProfile,
                CanonicalRepairSearchProfile.BuildPreRepairProfile(repairCase), repairCase.Execution.CompletedAt),
            Create(repairCase.Order.Id, RepairSearchDocumentTypes.CompletionEvidence,
                CanonicalRepairSearchProfile.BuildCompletionEvidence(repairCase), repairCase.Execution.CompletedAt)
        }).ToArray();
        var pending = documents.Where(value =>
            !current.TryGetValue(value.Key, out var hash) || hash != value.Hash).ToArray();
        var batchCount = 0;
        var tokens = 0;
        var hasTokens = false;
        foreach (var batch in pending.Chunk(options.BatchSize))
        {
            var embedded = await embeddingProvider.EmbedAsync(batch.Select(value => value.Text).ToArray(), cancellationToken);
            if (embedded.Dimensions != options.Dimensions || embedded.Vectors.Count != batch.Length)
                throw new RepairEmbeddingUnavailableException("Embedding provider returned an invalid V2 batch.");
            var records = batch.Select((value, index) => new RepairSearchEmbeddingRecord(
                DeterministicId(value.OrderId, value.ProfileType, options.Model, options.V2ModelVersion,
                    options.V2ProfileVersion), value.OrderId, value.ProfileType, 0,
                embedded.Vectors[index], options.Model, options.V2ModelVersion,
                options.V2ProfileVersion, options.V2DocumentVersion, value.Hash, value.AvailableAt)).ToArray();
            await repository.StoreAsync(records, cancellationToken);
            batchCount++;
            if (embedded.InputTokens is not null)
            {
                hasTokens = true;
                tokens += embedded.InputTokens.Value;
            }
        }
        return new RepairSearchV2IndexResult(dataset.Records.Count, documents.Length, pending.Length,
            documents.Length - pending.Length, batchCount, hasTokens ? tokens : null);
    }

    private static Document Create(Guid orderId, string type, string text, DateTimeOffset availableAt)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("A V2 search document is empty.");
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return new Document(orderId, type, text, hash, availableAt);
    }

    private static Guid DeterministicId(Guid orderId, string type, string model, string modelVersion,
        string profileVersion)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{orderId:D}|{type}|0|{model}|{modelVersion}|{profileVersion}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private sealed record Document(Guid OrderId, string ProfileType, string Text, string Hash,
        DateTimeOffset AvailableAt)
    {
        public string Key => $"{OrderId:D}|{ProfileType}";
    }
}
