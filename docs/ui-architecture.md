# ARI-UI-01 – UI architecture

## Purpose and boundary

ARI.Web is a .NET 10 Blazor Web App using Interactive Server rendering. It owns presentation, form validation, transient UI state and the HTTP client boundary. It does **not** own database access, model orchestration, extraction policy, retrieval trust or credentials.

    Browser / Blazor circuit
            |
            v
    ARI.Web
      Components -> RepairWorkspace -> RepairAnalysisCoordinator -> IAriApiClient
                                                                |
                                                                v
    ARI.Api  (quality gates and orchestration)
      |                 |                    |
      v                 v                    v
    OpenAI          Python ML            SAP HANA Cloud
    LLM/embedding   Ridge inference      data + vector retrieval

The only project reference from ARI.Web is ARI.Core. This is safe because Core contains provider-neutral contracts and domain rules. Core has no reference back to Web and no SAP, OpenAI or Blazor package. Web has no reference to ARI.Api or ARI.Infrastructure.

## Main components

| Component | Responsibility |
|---|---|
| MainLayout, NavMenu, HealthBadge | responsive application shell, navigation and compact readiness |
| NewEstimate.razor | EditForm, DataAnnotations, demo cases, cancellation and submit lock |
| RepairCase.razor | prediction, validated facts, trace, top-five evidence and safe detail dialog |
| WhatIf.razor | immutable baseline, new scenario snapshots and delta comparison |
| SystemInfo.razor | truthful readiness, model registry, security boundary and recovery help |
| RepairWorkspace | circuit-scoped editable intake, completed baseline and up to five scenarios |
| RepairAnalysisCoordinator | workflow transitions, generation IDs, cancellation and anti-stale commit |
| LiveAriApiClient | configurable base URL/timeout, correlation header, cancellation and error translation |
| FixtureAriApiClient | deterministic browser automation only; always visibly labelled test data |

## Dependency injection and configuration

Program.cs binds AriApiOptions from AriApi. Options are validated at startup:

- BaseUrl: absolute HTTP(S) URL, default http://127.0.0.1:5012
- TimeoutSeconds: 5–300 seconds
- UseFixture: false by default; only automation sets it to true

LiveAriApiClient is registered through HttpClientFactory. RepairWorkspace and the coordinator are scoped, which means one isolated state instance per Blazor circuit. No secret is configured in Web or sent to the browser.

## Health truthfulness

| Dependency | Probe | UI value |
|---|---|---|
| ARI.Api | /health | online/offline |
| SAP HANA | /health/ready | online/offline |
| Python ML | /health/ml/ready | online/offline |
| LLM | no dedicated probe | **unknown**, never guessed online |
| embeddings | no dedicated probe | **unknown**, never guessed online |

The health pill is deliberately not a full analysis guarantee. Provider availability is proven only by an analysis request.

## Request and trust flow

1. The browser submits a bounded RepairIntake to its Blazor server circuit.
2. Web labels structured values as trusted UI input and free text as untrusted description.
3. Web sends one EvidenceEstimateRequestDto to ARI.Api.
4. API performs LLM V3 extraction and the server-owned quality gate.
5. Only accepted pre-repair features reach Python ML.
6. API performs RRF V2 retrieval in HANA and validates grounded explanation sources.
7. Web receives provider-neutral Core result records and commits one immutable snapshot.

Historical actual labor and post-repair complications are display-only evidence. They never occur in LaborPredictionInput.

## Security and observability

- Secrets stay in API User Secrets/environment configuration.
- A correlation ID is created by Web, sent as X-Correlation-ID, capped at 100 characters by API and echoed in the response.
- Logs contain path, latency, model/profile IDs, error code and correlation ID—not full prompts, secrets or provider response bodies.
- Provider failures are translated into AriApiException; raw bodies and stack traces are not rendered.
- Evidence text uses normal Razor text encoding. No MarkupString or source-derived link is used.
- Description length is 4,000 characters; names are 80; scenario names are 60; numerical domain ranges are enforced both client-side and by API.
- One in-flight analysis per circuit is allowed. Cancellation tokens are disposed and a generation number prevents stale completion.

For a public showcase, authentication, authorization and deployment-level rate limiting remain mandatory. The current acceptance target is a local demonstration.
