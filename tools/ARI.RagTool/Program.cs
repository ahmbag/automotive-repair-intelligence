using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ARI.Core.Contracts;
using ARI.Core.Domain;
using ARI.Infrastructure.AI.Embeddings;
using ARI.Infrastructure.Persistence.Hana;
using ARI.Infrastructure.Persistence.Hana.Import;
using ARI.Infrastructure.Persistence.Hana.Vector;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
if (args.Length < 1)
{
    Usage();
    return 2;
}

if (args[0] == "prepare-evaluation" && args.Length == 3)
{
    var dataset = await HistoricalDatasetJsonReader.ReadAsync(args[1]);
    var evaluation = EvaluationBuilder.Build(dataset);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
    await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(evaluation, json) + Environment.NewLine);
    Console.WriteLine($"Prepared {evaluation.Cases.Count} retrieval queries.");
    Console.WriteLine($"Checksum: {evaluation.Checksum}");
    return 0;
}

if (args[0] == "prepare-quality-v2" && args.Length == 5)
{
    var dataset = await HistoricalDatasetJsonReader.ReadAsync(args[1]);
    var frozen = JsonSerializer.Deserialize<RetrievalEvaluationSet>(await File.ReadAllTextAsync(args[2]), json)
                 ?? throw new InvalidDataException("Frozen V1 evaluation set is empty.");
    var (development, holdout) = RetrievalQualityV2Builder.Build(dataset, frozen);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);
    await File.WriteAllTextAsync(args[3], JsonSerializer.Serialize(development, json) + Environment.NewLine);
    await File.WriteAllTextAsync(args[4], JsonSerializer.Serialize(holdout, json) + Environment.NewLine);
    Console.WriteLine($"Prepared development={development.Cases.Count}, holdout={holdout.Cases.Count}.");
    Console.WriteLine($"Development checksum: {development.Checksum}");
    Console.WriteLine($"Holdout checksum: {holdout.Checksum}");
    return 0;
}

var embeddings = LoadEmbeddings();
if (args[0] == "embed-smoke" && args.Length == 1)
{
    using var smokeClient = new HttpClient { Timeout = embeddings.Timeout };
    var smokeProvider = new OpenAiRepairEmbeddingProvider(smokeClient, embeddings);
    var batch = await smokeProvider.EmbedAsync(["front suspension control arm repair"]);
    Console.WriteLine($"Model: {batch.ModelId}");
    Console.WriteLine($"Dimensions: {batch.Dimensions}");
    Console.WriteLine($"Vectors: {batch.Vectors.Count}");
    Console.WriteLine($"Input tokens: {batch.InputTokens?.ToString() ?? "unavailable"}");
    return 0;
}

var hana = LoadHana();
var factory = new HanaConnectionFactory(hana);

if (args[0] == "smoke" && args.Length == 1)
{
    await using var connection = await factory.OpenConnectionAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT COSINE_SIMILARITY(TO_REAL_VECTOR('[1,0,0]'), TO_REAL_VECTOR('[1,0,0]')),
               CARDINALITY(TO_REAL_VECTOR('[0.1,0.2,0.3]'))
        FROM DUMMY
        """;
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    Console.WriteLine($"Cosine identity: {Convert.ToDouble(reader.GetValue(0)):F6}");
    Console.WriteLine($"Dimensions: {reader.GetInt32(1)}");
    return 0;
}

using var httpClient = new HttpClient { Timeout = embeddings.Timeout };
var provider = new OpenAiRepairEmbeddingProvider(httpClient, embeddings);
var repository = new HanaVectorRepository(factory, hana);
var v2Repository = new HanaRepairSearchV2Repository(factory, hana);

if (args[0] == "index" && args.Length == 2)
{
    var dataset = await HistoricalDatasetJsonReader.ReadAsync(args[1]);
    var indexer = new RepairEmbeddingIndexer(provider, repository, embeddings);
    var result = await indexer.IndexAsync(dataset);
    Console.WriteLine($"Sources: {result.SourceCount}");
    Console.WriteLine($"Embedded: {result.EmbeddedCount}");
    Console.WriteLine($"Unchanged: {result.UnchangedCount}");
    Console.WriteLine($"Provider batches: {result.ProviderBatchCount}");
    Console.WriteLine($"Input tokens: {result.InputTokens?.ToString() ?? "unavailable"}");
    return 0;
}

if (args[0] == "index-v2" && args.Length == 2)
{
    var dataset = await HistoricalDatasetJsonReader.ReadAsync(args[1]);
    var indexer = new RepairSearchV2Indexer(provider, v2Repository, embeddings);
    var result = await indexer.IndexAsync(dataset);
    Console.WriteLine($"Repair cases: {result.RepairCaseCount}");
    Console.WriteLine($"Documents: {result.DocumentCount}");
    Console.WriteLine($"Embedded: {result.EmbeddedCount}");
    Console.WriteLine($"Unchanged: {result.UnchangedCount}");
    Console.WriteLine($"Provider batches: {result.ProviderBatchCount}");
    Console.WriteLine($"Input tokens: {result.InputTokens?.ToString() ?? "unavailable"}");
    return 0;
}

if (args[0] == "analyze-v1" && args.Length == 4)
{
    var evaluation = JsonSerializer.Deserialize<RetrievalEvaluationSet>(await File.ReadAllTextAsync(args[1]), json)
                     ?? throw new InvalidDataException("Evaluation set is empty.");
    var frozenReport = JsonSerializer.Deserialize<RetrievalEvaluationReport>(await File.ReadAllTextAsync(args[2]), json)
                       ?? throw new InvalidDataException("Evaluation report is empty.");
    EvaluationBuilder.Verify(evaluation);
    var service = new HanaRepairRetrievalService(provider, repository, v2Repository, embeddings);
    var analysis = await RetrievalFailureAnalyzer.AnalyzeAsync(service, evaluation, frozenReport);
    await File.WriteAllTextAsync(args[3], JsonSerializer.Serialize(analysis, json) + Environment.NewLine);
    Console.WriteLine($"Analyzed {analysis.Cases.Count} frozen V1 cases; baseline checksum preserved.");
    return 0;
}

if (args[0] == "evaluate-v2" && args.Length == 3)
{
    var evaluation = JsonSerializer.Deserialize<RetrievalEvaluationSet>(await File.ReadAllTextAsync(args[1]), json)
                     ?? throw new InvalidDataException("Evaluation set is empty.");
    RetrievalQualityV2Builder.Verify(evaluation);
    var service = new HanaRepairRetrievalService(provider, repository, v2Repository, embeddings);
    var report = await RetrievalQualityV2Runner.RunAsync(service, evaluation, embeddings);
    await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(report, json) + Environment.NewLine);
    foreach (var experiment in report.Experiments)
        Console.WriteLine($"{experiment.Strategy}: recall@5={experiment.Metrics.RecallAt5:F3}, " +
                          $"precision@5={experiment.Metrics.PrecisionAt5:F3}, MRR={experiment.Metrics.Mrr:F3}, " +
                          $"nDCG@5={experiment.Metrics.NdcgAt5:F3}, candidate={experiment.Metrics.CandidateRecall:F3}, " +
                          $"HANA p95={experiment.Metrics.P95HanaLatencyMilliseconds} ms, full p95={experiment.Metrics.P95FullLatencyMilliseconds} ms");
    return 0;
}

if (args[0] == "evaluate" && args.Length == 3)
{
    var evaluation = JsonSerializer.Deserialize<RetrievalEvaluationSet>(
        await File.ReadAllTextAsync(args[1]), json)
        ?? throw new InvalidDataException("Evaluation set is empty.");
    EvaluationBuilder.Verify(evaluation);
    var service = new HanaRepairRetrievalService(provider, repository, v2Repository, embeddings);
    var experiments = new List<RetrievalExperiment>();
    foreach (var strategy in new[]
             {
                 RepairRetrievalStrategies.Structured,
                 RepairRetrievalStrategies.Semantic,
                 RepairRetrievalStrategies.Hybrid
             })
    {
        Console.WriteLine($"Evaluating {strategy} on {evaluation.Cases.Count} queries...");
        experiments.Add(await EvaluationRunner.RunAsync(service, evaluation.Cases, strategy));
    }

    var report = new RetrievalEvaluationReport(
        "ari-rag-evaluation-report-v1",
        evaluation.DatasetId,
        evaluation.Checksum,
        DateTimeOffset.UtcNow,
        embeddings.Model,
        embeddings.ModelVersion,
        experiments);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
    await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(report, json) + Environment.NewLine);
    foreach (var experiment in experiments)
    {
        Console.WriteLine(
            $"{experiment.Strategy}: recall@5={experiment.Metrics.RecallAt5:F3}, " +
            $"precision@5={experiment.Metrics.PrecisionAt5:F3}, MRR={experiment.Metrics.Mrr:F3}, " +
            $"nDCG@5={experiment.Metrics.NdcgAt5:F3}, p95={experiment.Metrics.P95LatencyMilliseconds} ms, " +
            $"no-result={experiment.Metrics.NoResultAccuracy:F3}");
    }

    return 0;
}

Usage();
return 2;

static void Usage() => Console.Error.WriteLine("""
    Usage:
      ARI.RagTool smoke
      ARI.RagTool embed-smoke
      ARI.RagTool index <historical-dataset.json>
      ARI.RagTool index-v2 <historical-dataset.json>
      ARI.RagTool prepare-evaluation <historical-dataset.json> <evaluation-set.json>
      ARI.RagTool prepare-quality-v2 <historical-dataset.json> <v1-set.json> <dev-set.json> <holdout-set.json>
      ARI.RagTool evaluate <evaluation-set.json> <evaluation-report.json>
      ARI.RagTool evaluate-v2 <evaluation-set.json> <evaluation-report.json>
      ARI.RagTool analyze-v1 <v1-set.json> <v1-report.json> <analysis.json>
    """);

static HanaOptions LoadHana() => new()
{
    Host = Environment.GetEnvironmentVariable("Hana__Host") ?? string.Empty,
    Port = int.TryParse(Environment.GetEnvironmentVariable("Hana__Port"), out var port) ? port : 443,
    User = Environment.GetEnvironmentVariable("Hana__User") ?? string.Empty,
    Password = Environment.GetEnvironmentVariable("Hana__Password") ?? string.Empty,
    ConnectionTimeoutSeconds = 60
};

static EmbeddingProviderOptions LoadEmbeddings() => new()
{
    ApiKey = Environment.GetEnvironmentVariable("Embeddings__ApiKey")
             ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    Endpoint = new Uri(Environment.GetEnvironmentVariable("Embeddings__Endpoint")
                       ?? "https://api.openai.com/v1/embeddings"),
    Model = Environment.GetEnvironmentVariable("Embeddings__Model") ?? "text-embedding-3-small",
    ModelVersion = Environment.GetEnvironmentVariable("Embeddings__ModelVersion")
                   ?? "repair-report-embedding-v1",
    V2ModelVersion = Environment.GetEnvironmentVariable("Embeddings__V2ModelVersion")
                     ?? "repair-search-embedding-v2",
    V2ProfileVersion = Environment.GetEnvironmentVariable("Embeddings__V2ProfileVersion")
                       ?? "canonical-repair-search-profile-v2",
    V2DocumentVersion = Environment.GetEnvironmentVariable("Embeddings__V2DocumentVersion") ?? "2.0.0",
    Dimensions = int.TryParse(Environment.GetEnvironmentVariable("Embeddings__Dimensions"), out var dimensions)
        ? dimensions : 1536,
    BatchSize = int.TryParse(Environment.GetEnvironmentVariable("Embeddings__BatchSize"), out var batchSize)
        ? batchSize : 100,
    MaxRetries = 2,
    Timeout = TimeSpan.FromSeconds(60)
};

public sealed record RetrievalEvaluationSet(
    string DatasetId,
    string SourceDatasetId,
    string SelectionVersion,
    int CaseCount,
    string Checksum,
    IReadOnlyList<RetrievalEvaluationCase> Cases);

public sealed record RetrievalEvaluationCase(
    string Id,
    string Description,
    string RepairCode,
    string? Manufacturer,
    string? Model,
    int? VehicleAgeYears,
    int? MileageKm,
    IReadOnlyCollection<string> KnownConditions,
    DateTimeOffset AsOfUtc,
    IReadOnlyDictionary<Guid, int> Relevance,
    bool ExpectsNoResult,
    bool ManuallyReviewed);

public static class EvaluationBuilder
{
    private const string SelectionVersion = "ari-rag-eval-selection-v1";

    public static RetrievalEvaluationSet Build(HistoricalRepairDataset dataset)
    {
        var positive = dataset.Records
            .GroupBy(item => item.Order.RepairCode, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .SelectMany(group => group.OrderBy(item => item.Order.Id).Take(7))
            .Take(35)
            .Select((item, index) => BuildPositive(item, dataset.Records, index < 10))
            .ToList();
        for (var index = 0; index < 5; index++)
        {
            positive.Add(new RetrievalEvaluationCase(
                $"no-result-{index + 1:00}",
                $"Brake disc and pad replacement request variant {index + 1} with no suspension work.",
                "BRAKE_DISC_FRONT_REPLACE",
                index % 2 == 0 ? "Volkswagen" : "BMW",
                null,
                8 + index,
                80_000 + index * 20_000,
                [],
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new Dictionary<Guid, int>(),
                true,
                false));
        }

        var cases = positive.ToArray();
        var checksum = Checksum(cases);
        return new RetrievalEvaluationSet(
            "ari-rag-retrieval-evaluation-v1",
            dataset.Manifest.DatasetId,
            SelectionVersion,
            cases.Length,
            checksum,
            cases);
    }

    public static void Verify(RetrievalEvaluationSet set)
    {
        if (set.CaseCount != set.Cases.Count || set.Cases.Count != 40 ||
            set.Checksum != Checksum(set.Cases))
        {
            throw new InvalidDataException("Retrieval evaluation set count or checksum is invalid.");
        }
    }

    private static RetrievalEvaluationCase BuildPositive(
        HistoricalRepairCase item,
        IReadOnlyList<HistoricalRepairCase> all,
        bool manuallyReviewed)
    {
        var asOf = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var age = asOf.Year - item.Vehicle.ModelYear;
        var conditions = item.KnownConditions.Select(value => value.ConditionCode).Order().ToArray();
        var relevant = all.Where(candidate => candidate.Order.RepairCode == item.Order.RepairCode)
            .OrderByDescending(candidate => LabelScore(item, candidate))
            .ThenBy(candidate => candidate.Order.Id)
            .Take(5)
            .Select((candidate, rank) => new
            {
                candidate.Order.Id,
                Grade = rank == 0 ? 3 : rank < 3 ? 2 : 1
            })
            .ToDictionary(entry => entry.Id, entry => entry.Grade);
        var conditionText = conditions.Length == 0
            ? "No pre-existing condition was recorded."
            : $"Known before work: {string.Join(", ", conditions.Select(Humanize))}.";
        return new RetrievalEvaluationCase(
            $"query-{item.Order.Id:D}",
            $"Find completed comparisons for planned {Humanize(item.Order.RepairCode)} on a " +
            $"{age}-year-old {item.Vehicle.Manufacturer} {item.Vehicle.Model} at " +
            $"{item.Vehicle.MileageKm} km. {conditionText}",
            item.Order.RepairCode,
            item.Vehicle.Manufacturer,
            item.Vehicle.Model,
            age,
            item.Vehicle.MileageKm,
            conditions,
            asOf,
            relevant,
            false,
            manuallyReviewed);
    }

    private static double LabelScore(HistoricalRepairCase query, HistoricalRepairCase candidate)
    {
        var score = 0d;
        if (query.Vehicle.Model == candidate.Vehicle.Model) score += 4;
        if (query.Vehicle.Manufacturer == candidate.Vehicle.Manufacturer) score += 2;
        score += Math.Max(0, 1 - Math.Abs(query.Vehicle.MileageKm - candidate.Vehicle.MileageKm) / 200_000d);
        score += Math.Max(0, 1 - Math.Abs(query.Vehicle.ModelYear - candidate.Vehicle.ModelYear) / 10d);
        var queryConditions = query.KnownConditions.Select(item => item.ConditionCode).ToHashSet();
        if (candidate.KnownConditions.Any(item => queryConditions.Contains(item.ConditionCode))) score += 2;
        return score;
    }

    private static string Humanize(string value) => value.Replace('_', ' ').ToLowerInvariant();

    private static string Checksum(IReadOnlyList<RetrievalEvaluationCase> cases) =>
        Convert.ToHexStringLower(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(cases, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
}

public sealed record RetrievalMetrics(
    double RecallAt5,
    double PrecisionAt5,
    double Mrr,
    double NdcgAt5,
    double NoResultAccuracy,
    long P50LatencyMilliseconds,
    long P95LatencyMilliseconds,
    int QueryCount);

public sealed record RetrievalOutcome(
    string CaseId,
    IReadOnlyList<Guid> RetrievedIds,
    long LatencyMilliseconds,
    string? ErrorType);

public sealed record RetrievalExperiment(
    string Strategy,
    RetrievalMetrics Metrics,
    IReadOnlyDictionary<string, int> ErrorTypes,
    IReadOnlyList<RetrievalOutcome> Outcomes);

public sealed record RetrievalEvaluationReport(
    string ReportVersion,
    string DatasetId,
    string DatasetChecksum,
    DateTimeOffset EvaluatedAtUtc,
    string EmbeddingModel,
    string EmbeddingVersion,
    IReadOnlyList<RetrievalExperiment> Experiments);

public static class EvaluationRunner
{
    public static async Task<RetrievalExperiment> RunAsync(
        HanaRepairRetrievalService service,
        IReadOnlyList<RetrievalEvaluationCase> cases,
        string strategy)
    {
        var outcomes = new List<RetrievalOutcome>();
        var errors = new Dictionary<string, int>(StringComparer.Ordinal);
        var recalls = new List<double>();
        var precisions = new List<double>();
        var reciprocalRanks = new List<double>();
        var ndcgs = new List<double>();
        var noResultCorrect = 0;
        foreach (var item in cases)
        {
            try
            {
                var result = await service.SearchAsync(new RepairRetrievalQuery(
                    item.Description,
                    item.RepairCode,
                    item.Manufacturer,
                    item.Model,
                    item.VehicleAgeYears,
                    item.MileageKm,
                    item.KnownConditions,
                    item.AsOfUtc,
                    strategy,
                    5,
                    0.55));
                var ids = result.Cases.Select(value => value.RepairOrderId).ToArray();
                if (item.ExpectsNoResult)
                {
                    noResultCorrect += ids.Length == 0 ? 1 : 0;
                }
                else
                {
                    var relevant = item.Relevance.Keys.ToHashSet();
                    var hits = ids.Count(relevant.Contains);
                    recalls.Add((double)hits / relevant.Count);
                    precisions.Add((double)hits / 5);
                    var first = Array.FindIndex(ids, relevant.Contains);
                    reciprocalRanks.Add(first < 0 ? 0 : 1d / (first + 1));
                    ndcgs.Add(Ndcg(ids, item.Relevance));
                }

                outcomes.Add(new RetrievalOutcome(item.Id, ids, result.LatencyMilliseconds, null));
            }
            catch (Exception exception)
            {
                var type = exception.GetType().Name;
                errors[type] = errors.TryGetValue(type, out var count) ? count + 1 : 1;
                outcomes.Add(new RetrievalOutcome(item.Id, [], 0, type));
            }
        }

        var latencies = outcomes.Where(item => item.ErrorType is null)
            .Select(item => item.LatencyMilliseconds).Order().ToArray();
        return new RetrievalExperiment(
            strategy,
            new RetrievalMetrics(
                recalls.Average(),
                precisions.Average(),
                reciprocalRanks.Average(),
                ndcgs.Average(),
                noResultCorrect / 5d,
                Percentile(latencies, 0.50),
                Percentile(latencies, 0.95),
                cases.Count),
            errors,
            outcomes);
    }

    private static double Ndcg(IReadOnlyList<Guid> ids, IReadOnlyDictionary<Guid, int> relevance)
    {
        var dcg = ids.Select((id, index) =>
                relevance.TryGetValue(id, out var grade)
                    ? (Math.Pow(2, grade) - 1) / Math.Log2(index + 2)
                    : 0)
            .Sum();
        var ideal = relevance.Values.OrderDescending().Take(5)
            .Select((grade, index) => (Math.Pow(2, grade) - 1) / Math.Log2(index + 2)).Sum();
        return ideal == 0 ? 0 : dcg / ideal;
    }

    private static long Percentile(IReadOnlyList<long> values, double percentile) =>
        values.Count == 0 ? 0 : values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * percentile) - 1)];
}
