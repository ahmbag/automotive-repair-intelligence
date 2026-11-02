using System.Net;
using System.Text;
using System.Text.Json;
using ARI.Core.Contracts;
using ARI.Infrastructure.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace ARI.Api.Tests;

public sealed class OpenAiRepairInformationExtractorTests
{
    [Fact]
    public async Task Strict_structured_output_is_requested_and_valid_response_is_mapped()
    {
        string? requestBody = null;
        string? authorization = null;
        using var client = Client(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            authorization = request.Headers.Authorization?.Scheme;
            return ProviderResponse(ValidExtractionJson());
        });
        var extractor = Extractor(client);

        var result = await extractor.ExtractAsync(ValidRequest());

        using var body = JsonDocument.Parse(requestBody!);
        Assert.True(body.RootElement.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
        Assert.Equal("json_schema", body.RootElement.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.Equal("Bearer", authorization);
        Assert.Equal("CONTROL_ARM_FRONT_LEFT_REPLACE", result.RepairCode);
        Assert.Equal("BROKEN_BOLT", Assert.Single(result.ObservedComplications).Code);
        Assert.Equal(12, result.Telemetry.InputTokens);
        Assert.Equal(8, result.Telemetry.OutputTokens);
    }

    [Fact]
    public async Task Extra_schema_property_is_rejected_before_domain_use()
    {
        var invalid = ValidExtractionJson().Replace(
            "\"status\":\"EXTRACTED\"",
            "\"status\":\"EXTRACTED\",\"invented\":true",
            StringComparison.Ordinal);
        using var client = Client(_ => Task.FromResult(ProviderResponse(invalid)));
        var extractor = Extractor(client);

        var exception = await Assert.ThrowsAsync<RepairExtractionValidationException>(
            () => extractor.ExtractAsync(ValidRequest()));

        Assert.Contains(exception.Errors, error => error.Contains("schema deserialization"));
    }

    [Fact]
    public async Task Missing_required_schema_property_is_rejected_controlled()
    {
        var invalid = ValidExtractionJson().Replace(
            "\"observedComplications\":[{\"code\":\"BROKEN_BOLT\",\"informationTime\":\"DURING_REPAIR\",\"evidence\":\"A mounting bolt broke during removal\"}],",
            string.Empty,
            StringComparison.Ordinal);
        using var client = Client(_ => Task.FromResult(ProviderResponse(invalid)));
        var extractor = Extractor(client);

        var exception = await Assert.ThrowsAsync<RepairExtractionValidationException>(
            () => extractor.ExtractAsync(ValidRequest()));

        Assert.Contains(exception.Errors, error => error.Contains("schema deserialization"));
    }

    [Fact]
    public async Task Unsupported_evidence_is_rejected_by_separate_domain_validation()
    {
        var invalid = ValidExtractionJson().Replace(
            "A mounting bolt broke during removal",
            "This evidence was invented",
            StringComparison.Ordinal);
        using var client = Client(_ => Task.FromResult(ProviderResponse(invalid)));
        var extractor = Extractor(client);

        var exception = await Assert.ThrowsAsync<RepairExtractionValidationException>(
            () => extractor.ExtractAsync(ValidRequest()));

        Assert.Contains(exception.Errors, error => error.Contains("not a source substring"));
    }

    [Fact]
    public async Task Rate_limit_is_retried_a_bounded_number_of_times()
    {
        var calls = 0;
        using var client = Client(_ => Task.FromResult(
            ++calls < 3
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : ProviderResponse(ValidExtractionJson())));
        var extractor = Extractor(client, maxRetries: 2);

        var result = await extractor.ExtractAsync(ValidRequest());

        Assert.Equal(3, calls);
        Assert.Equal(RepairExtractionStatuses.Extracted, result.Status);
    }

    [Fact]
    public async Task Provider_timeout_becomes_controlled_unavailability()
    {
        using var client = new HttpClient(new TimeoutHandler())
        {
            Timeout = TimeSpan.FromMilliseconds(25)
        };
        var extractor = Extractor(client, timeout: client.Timeout);

        var exception = await Assert.ThrowsAsync<RepairExtractionUnavailableException>(
            () => extractor.ExtractAsync(ValidRequest()));

        Assert.Contains("timeout", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_hidden_as_provider_failure()
    {
        using var client = new HttpClient(new TimeoutHandler())
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        var extractor = Extractor(client, timeout: client.Timeout);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => extractor.ExtractAsync(ValidRequest(), cancellation.Token));
    }

    [Fact]
    public async Task Missing_secret_is_rejected_without_an_http_call()
    {
        var calls = 0;
        using var client = Client(_ =>
        {
            calls++;
            return Task.FromResult(ProviderResponse(ValidExtractionJson()));
        });
        var extractor = Extractor(client, apiKey: null);

        await Assert.ThrowsAsync<RepairExtractionUnavailableException>(
            () => extractor.ExtractAsync(ValidRequest()));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task V3_second_stage_recovers_indirect_explicit_complications()
    {
        var providerJson = ValidExtractionJson().Replace(
            "[{\"code\":\"BROKEN_BOLT\",\"informationTime\":\"DURING_REPAIR\",\"evidence\":\"A mounting bolt broke during removal\"}]",
            "[]",
            StringComparison.Ordinal);
        using var client = Client(_ => Task.FromResult(ProviderResponse(providerJson)));
        var extractor = Extractor(client);
        var request = ValidRequest() with
        {
            Document = "Volkswagen Golf VII, model year 2014, 215000 km. " +
                "The front left control arm was replaced. " +
                "A failed fastener added an unplanned removal step. Actual labor was 186 minutes.",
            PromptVersion = RepairExtractionPrompts.QualityVersion
        };

        var result = await extractor.ExtractAsync(request);

        var fact = Assert.Single(result.ObservedComplications);
        Assert.Equal("BROKEN_BOLT", fact.Code);
        Assert.Contains("failed fastener", fact.Evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RepairExtractionStatuses.Accepted, result.Status);
    }

    [Fact]
    public async Task V3_classifier_does_not_turn_negation_into_a_complication()
    {
        var providerJson = ValidExtractionJson().Replace(
            "[{\"code\":\"BROKEN_BOLT\",\"informationTime\":\"DURING_REPAIR\",\"evidence\":\"A mounting bolt broke during removal\"}]",
            "[]",
            StringComparison.Ordinal);
        using var client = Client(_ => Task.FromResult(ProviderResponse(providerJson)));
        var extractor = Extractor(client);
        var request = ValidRequest() with
        {
            Document = "Volkswagen Golf VII, model year 2014, 215000 km. " +
                "The front left control arm was replaced. No mounting bolt broke during removal. " +
                "Actual labor was 186 minutes.",
            PromptVersion = RepairExtractionPrompts.QualityVersion
        };

        var result = await extractor.ExtractAsync(request);

        Assert.Empty(result.ObservedComplications);
    }

    private static OpenAiRepairInformationExtractor Extractor(
        HttpClient client,
        int maxRetries = 2,
        TimeSpan? timeout = null,
        string? apiKey = "unit-test-secret") => new(
            client,
            new LlmProviderOptions
            {
                Endpoint = new Uri("https://api.openai.test/v1/responses"),
                Model = "test-model",
                ApiKey = apiKey,
                Timeout = timeout ?? TimeSpan.FromSeconds(2),
                MaxRetries = maxRetries
            },
            NullLogger<OpenAiRepairInformationExtractor>.Instance);

    private static RepairExtractionRequest ValidRequest() => new(
        "Volkswagen Golf VII, model year 2014, 215000 km. " +
        "The front left control arm was replaced. " +
        "A mounting bolt broke during removal and was replaced. " +
        "Actual labor was 186 minutes.",
        RepairDocumentTypes.RepairCompletionReport,
        RepairExtractionPrompts.DomainAwareVersion);

    private static string ValidExtractionJson() => """
        {
          "documentType":"REPAIR_COMPLETION_REPORT",
          "vehicle":{"manufacturer":"Volkswagen","model":"Golf VII","modelYear":2014,"mileageKm":215000},
          "repairCode":"CONTROL_ARM_FRONT_LEFT_REPLACE",
          "observedComplications":[{"code":"BROKEN_BOLT","informationTime":"DURING_REPAIR","evidence":"A mounting bolt broke during removal"}],
          "knownBeforeRepair":[],
          "actualLaborMinutes":186,
          "status":"EXTRACTED"
        }
        """;

    private static HttpResponseMessage ProviderResponse(string extractionJson)
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            status = "completed",
            output = new[]
            {
                new
                {
                    type = "message",
                    content = new[] { new { type = "output_text", text = extractionJson } }
                }
            },
            usage = new { input_tokens = 12, output_tokens = 8 }
        });
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
        response.Headers.Add("x-request-id", "req_unit_test");
        return response;
    }

    private static HttpClient Client(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) =>
        new(new StubHandler(responder)) { Timeout = TimeSpan.FromSeconds(2) };

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request);
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Cancelled request must not complete.");
        }
    }
}
