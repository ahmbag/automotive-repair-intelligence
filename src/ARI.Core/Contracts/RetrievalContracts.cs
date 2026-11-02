namespace ARI.Core.Contracts;

public static class RepairRetrievalStrategies
{
    public const string Structured = "STRUCTURED";
    public const string Semantic = "SEMANTIC";
    public const string Hybrid = "HYBRID";
    public const string DomainSemantic = "DOMAIN_SEMANTIC_V2";
    public const string StructuredFirstHybrid = "STRUCTURED_FIRST_HYBRID";
    public const string RankFusion = "RANK_FUSION_V2";

    public static bool IsSupported(string value) =>
        value is Structured or Semantic or Hybrid or DomainSemantic or StructuredFirstHybrid or RankFusion;

    public static bool UsesV2(string value) =>
        value is DomainSemantic or StructuredFirstHybrid or RankFusion;
}

public static class RepairEmbeddingProfiles
{
    public const string ReportV1 = "REPAIR_REPORT_V1";
    public const string RepairProfileV2 = "REPAIR_PROFILE_V2";

    public static bool IsSupported(string value) => value is ReportV1 or RepairProfileV2;
}

public static class RepairSearchDocumentTypes
{
    public const string PreRepairProfile = "PRE_REPAIR_PROFILE";
    public const string CompletionEvidence = "COMPLETION_EVIDENCE";
}

public sealed record RepairEmbeddingBatch(
    string ModelId,
    int Dimensions,
    IReadOnlyList<IReadOnlyList<float>> Vectors,
    int? InputTokens);

public sealed record RepairRetrievalQuery(
    string Description,
    string RepairCode,
    string? Manufacturer,
    string? Model,
    int? VehicleAgeYears,
    int? MileageKm,
    IReadOnlyCollection<string> KnownConditions,
    DateTimeOffset AsOfUtc,
    string Strategy = RepairRetrievalStrategies.Hybrid,
    int TopK = 5,
    double MinimumSimilarity = 0.0,
    string EmbeddingProfile = RepairEmbeddingProfiles.ReportV1);

public sealed record SimilarRepairCase(
    Guid RepairOrderId,
    double Score,
    double? VectorSimilarity,
    string RepairCode,
    string Manufacturer,
    string Model,
    int ModelYear,
    int MileageKm,
    decimal BaselineLaborHours,
    int ActualLaborMinutes,
    string SourceType,
    string SourceClassification,
    string SourceReference,
    string SourceExcerpt,
    DateTimeOffset SourceAvailableAtUtc);

public sealed record RepairRetrievalResult(
    string Strategy,
    string EmbeddingModel,
    string EmbeddingVersion,
    long LatencyMilliseconds,
    IReadOnlyList<SimilarRepairCase> Cases,
    string EmbeddingProfile = RepairEmbeddingProfiles.ReportV1,
    long HanaLatencyMilliseconds = 0,
    bool UsedStructuredFallback = false,
    string? FallbackReason = null);

public sealed record GroundedObservation(
    string Statement,
    string EvidenceQuote,
    IReadOnlyCollection<string> SourceReferences);

public sealed record GroundedRepairExplanation(
    string Summary,
    string EvidenceStrength,
    bool InsufficientEvidence,
    IReadOnlyCollection<GroundedObservation> Observations,
    string PromptVersion,
    string ModelId,
    long LatencyMilliseconds,
    int? InputTokens,
    int? OutputTokens);

public sealed record EvidenceEstimateResult(
    RepairExtractionResult Extraction,
    LaborPredictionResult Prediction,
    RepairRetrievalResult Retrieval,
    GroundedRepairExplanation? Explanation,
    string? ExplanationWarning);
