using ARI.Api.Endpoints;
using ARI.Core.Abstractions;
using ARI.Core.Contracts;
using ARI.Infrastructure.Persistence.Hana;
using ARI.Infrastructure.Persistence.Hana.Import;
using ARI.Infrastructure.MachineLearning;
using ARI.Infrastructure.AI;
using ARI.Infrastructure.AI.Embeddings;
using ARI.Infrastructure.AI.Grounding;
using ARI.Infrastructure.Persistence.Hana.Vector;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);

builder.Services.Configure<HanaOptions>(
    builder.Configuration.GetSection(HanaOptions.SectionName));
builder.Services.AddSingleton(
    serviceProvider => serviceProvider.GetRequiredService<IOptions<HanaOptions>>().Value);
builder.Services.AddSingleton<IHanaConnectionFactory, HanaConnectionFactory>();
builder.Services.AddSingleton<IHanaConnectivityProbe, HanaConnectivityProbe>();
builder.Services.AddScoped<HanaVehicleSnapshotRepository>();
builder.Services.AddScoped<HanaRepairOrderRepository>();
builder.Services.AddScoped<IVehicleSnapshotRepository>(
    serviceProvider => serviceProvider.GetRequiredService<HanaVehicleSnapshotRepository>());
builder.Services.AddScoped<IRepairOrderRepository>(
    serviceProvider => serviceProvider.GetRequiredService<HanaRepairOrderRepository>());
builder.Services.AddScoped<IRepairOrderPersistence, HanaRepairOrderPersistence>();
builder.Services.AddScoped<IHistoricalRepairImportService, HanaHistoricalRepairImportService>();

builder.Services.Configure<MachineLearningOptions>(
    builder.Configuration.GetSection(MachineLearningOptions.SectionName));
builder.Services.AddSingleton(
    serviceProvider => serviceProvider.GetRequiredService<IOptions<MachineLearningOptions>>().Value);
builder.Services.AddHttpClient<PythonLaborPredictionService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<MachineLearningOptions>();
    if (options.BaseAddress is not null)
    {
        client.BaseAddress = options.BaseAddress;
    }

    client.Timeout = options.Timeout;
});
builder.Services.AddTransient<IMachineLearningReadinessProbe>(
    serviceProvider => serviceProvider.GetRequiredService<PythonLaborPredictionService>());
builder.Services.AddTransient<ILaborPredictionService>(serviceProvider =>
{
    var primary = serviceProvider.GetRequiredService<PythonLaborPredictionService>();
    var options = serviceProvider.GetRequiredService<MachineLearningOptions>();
    return options.UseReferenceBaselineFallback
        ? new ReferenceBaselineFallbackService(primary)
        : primary;
});

builder.Services.Configure<LlmProviderOptions>(
    builder.Configuration.GetSection(LlmProviderOptions.SectionName));
builder.Services.AddSingleton(
    serviceProvider => serviceProvider.GetRequiredService<IOptions<LlmProviderOptions>>().Value);
builder.Services.AddHttpClient<OpenAiRepairInformationExtractor>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<LlmProviderOptions>();
    client.Timeout = options.Timeout;
});
builder.Services.AddTransient<IFeatureExtractor>(
    serviceProvider => serviceProvider.GetRequiredService<OpenAiRepairInformationExtractor>());

builder.Services.Configure<EmbeddingProviderOptions>(
    builder.Configuration.GetSection(EmbeddingProviderOptions.SectionName));
builder.Services.AddSingleton(
    serviceProvider => serviceProvider.GetRequiredService<IOptions<EmbeddingProviderOptions>>().Value);
builder.Services.AddHttpClient<OpenAiRepairEmbeddingProvider>((serviceProvider, client) =>
{
    client.Timeout = serviceProvider.GetRequiredService<EmbeddingProviderOptions>().Timeout;
});
builder.Services.AddTransient<IRepairEmbeddingProvider>(
    serviceProvider => serviceProvider.GetRequiredService<OpenAiRepairEmbeddingProvider>());
builder.Services.AddScoped<IHanaVectorRepository, HanaVectorRepository>();
builder.Services.AddScoped<IHanaRepairSearchV2Repository, HanaRepairSearchV2Repository>();
builder.Services.AddScoped<IRepairRetrievalService, HanaRepairRetrievalService>();
builder.Services.AddHttpClient<OpenAiRepairEvidenceExplainer>((serviceProvider, client) =>
{
    client.Timeout = serviceProvider.GetRequiredService<LlmProviderOptions>().Timeout;
});
builder.Services.AddTransient<IRepairEvidenceExplainer>(
    serviceProvider => serviceProvider.GetRequiredService<OpenAiRepairEvidenceExplainer>());

var app = builder.Build();

app.Use(async (context, next) =>
{
    var incoming = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    var correlationId = string.IsNullOrWhiteSpace(incoming)
        ? $"api-{Guid.NewGuid():N}"
        : incoming.Length <= 100 ? incoming : incoming[..100];
    context.TraceIdentifier = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    await next();
});

app.MapFoundationEndpoints();
app.MapPredictionEndpoints();
app.MapRepairExtractionEndpoints();
app.MapRagEndpoints();

app.Run();

// Exposes the entry point to the API test project without moving application logic here.
public partial class Program;
