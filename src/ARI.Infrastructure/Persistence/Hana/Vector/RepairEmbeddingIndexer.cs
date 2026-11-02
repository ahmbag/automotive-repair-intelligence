using System.Security.Cryptography;
using System.Text;
using ARI.Core.Abstractions;
using ARI.Core.Contracts;
using ARI.Infrastructure.AI.Embeddings;

namespace ARI.Infrastructure.Persistence.Hana.Vector;

public sealed record RepairEmbeddingIndexResult(
    int SourceCount,
    int EmbeddedCount,
    int UnchangedCount,
    int ProviderBatchCount,
    int? InputTokens);

public sealed class RepairEmbeddingIndexer(
    IRepairEmbeddingProvider embeddingProvider,
    IHanaVectorRepository repository,
    EmbeddingProviderOptions options)
{
    public async Task<RepairEmbeddingIndexResult> IndexAsync(
        HistoricalRepairDataset dataset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        var current = await repository.GetSourceHashesAsync(
            options.Model, options.ModelVersion, cancellationToken).ConfigureAwait(false);
        var sources = dataset.Records.Select(repairCase =>
        {
            var text = Normalize(repairCase.Execution.RepairReport);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidDataException(
                    $"Repair order {repairCase.Order.Id:D} has no embeddable report.");
            }

            return new Source(
                repairCase.Order.Id,
                text,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))),
                repairCase.Execution.CompletedAt);
        }).ToArray();
        var pending = sources.Where(source =>
            !current.TryGetValue(source.RepairOrderId, out var hash) || hash != source.Hash).ToArray();
        var batches = 0;
        var tokens = 0;
        var hasTokens = false;
        foreach (var batch in pending.Chunk(options.BatchSize))
        {
            var embedded = await embeddingProvider.EmbedAsync(
                batch.Select(source => source.Text).ToArray(), cancellationToken).ConfigureAwait(false);
            if (embedded.Vectors.Count != batch.Length)
            {
                throw new RepairEmbeddingUnavailableException(
                    "Embedding provider returned a different number of vectors than requested.");
            }

            var records = batch.Select((source, index) => new RepairEmbeddingRecord(
                DeterministicId(source.RepairOrderId, options.Model, options.ModelVersion),
                source.RepairOrderId,
                0,
                embedded.Vectors[index],
                options.Model,
                options.ModelVersion,
                source.Hash,
                source.AvailableAtUtc)).ToArray();
            await repository.StoreAsync(records, cancellationToken).ConfigureAwait(false);
            batches++;
            if (embedded.InputTokens is not null)
            {
                tokens += embedded.InputTokens.Value;
                hasTokens = true;
            }
        }

        return new RepairEmbeddingIndexResult(
            sources.Length,
            pending.Length,
            sources.Length - pending.Length,
            batches,
            hasTokens ? tokens : null);
    }

    private static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static Guid DeterministicId(Guid orderId, string model, string version)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{orderId:D}|0|{model}|{version}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private sealed record Source(
        Guid RepairOrderId,
        string Text,
        string Hash,
        DateTimeOffset AvailableAtUtc);
}

