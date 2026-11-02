using System.Diagnostics;
using ARI.Core.Abstractions;
using ARI.Core.Contracts;
using ARI.Infrastructure.AI.Embeddings;

namespace ARI.Infrastructure.Persistence.Hana.Vector;

public sealed class HanaRepairRetrievalService(
    IRepairEmbeddingProvider embeddingProvider,
    IHanaVectorRepository repository,
    IHanaRepairSearchV2Repository v2Repository,
    EmbeddingProviderOptions options) : IRepairRetrievalService
{
    public async Task<RepairRetrievalResult> SearchAsync(
        RepairRetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var hanaStopwatch = new Stopwatch();
        var fallback = false;
        string? fallbackReason = null;
        try
        {
            ValidateProfile(query);
            IReadOnlyList<float>? vector = null;
            if (query.Strategy != RepairRetrievalStrategies.Structured)
            {
                try
                {
                    var text = query.EmbeddingProfile == RepairEmbeddingProfiles.RepairProfileV2
                        ? CanonicalRepairSearchProfile.BuildQuery(query)
                        : query.Description;
                    var batch = await embeddingProvider.EmbedAsync([text], cancellationToken).ConfigureAwait(false);
                    vector = batch.Vectors.Single();
                }
                catch (RepairEmbeddingUnavailableException) when (
                    query.Strategy is RepairRetrievalStrategies.StructuredFirstHybrid or RepairRetrievalStrategies.RankFusion)
                {
                    fallback = true;
                    fallbackReason = "QUERY_EMBEDDING_UNAVAILABLE";
                }
            }

            var effectiveQuery = fallback ? query with { Strategy = RepairRetrievalStrategies.Structured } : query;
            hanaStopwatch.Start();
            var cases = query.EmbeddingProfile == RepairEmbeddingProfiles.RepairProfileV2
                ? await v2Repository.SearchAsync(effectiveQuery, vector, options, cancellationToken).ConfigureAwait(false)
                : await repository.SearchAsync(effectiveQuery, vector, options, cancellationToken).ConfigureAwait(false);
            hanaStopwatch.Stop();
            stopwatch.Stop();
            return new RepairRetrievalResult(
                query.Strategy,
                options.Model,
                query.EmbeddingProfile == RepairEmbeddingProfiles.RepairProfileV2
                    ? options.V2ModelVersion : options.ModelVersion,
                stopwatch.ElapsedMilliseconds,
                cases,
                query.EmbeddingProfile,
                hanaStopwatch.ElapsedMilliseconds,
                fallback,
                fallbackReason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (RepairEmbeddingUnavailableException exception)
        {
            throw new RepairRetrievalUnavailableException(
                "Retrieval query embedding could not be produced.", exception);
        }
        catch (Exception exception)
        {
            throw new RepairRetrievalUnavailableException(
                "Historical repair retrieval is unavailable.", exception);
        }
    }

    private static void ValidateProfile(RepairRetrievalQuery query)
    {
        if (!RepairEmbeddingProfiles.IsSupported(query.EmbeddingProfile) ||
            (RepairRetrievalStrategies.UsesV2(query.Strategy) &&
             query.EmbeddingProfile != RepairEmbeddingProfiles.RepairProfileV2) ||
            (query.Strategy is RepairRetrievalStrategies.Semantic or RepairRetrievalStrategies.Hybrid &&
             query.EmbeddingProfile != RepairEmbeddingProfiles.ReportV1))
            throw new ArgumentException("Retrieval strategy and embedding profile are not compatible.", nameof(query));
    }
}
