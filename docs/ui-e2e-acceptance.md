# ARI-UI-01 – Browser E2E acceptance

Test date: 2026-10-31  
Runtime: .NET SDK 10.0.401, Playwright .NET 1.55.0, Chromium 140 / build 1187

## Deterministic browser matrix

Automation starts ARI.Web with AriApi:UseFixture=true. A visible **AUTOMATED TEST DATA** banner distinguishes this from live results. The fixture uses real UI contracts and state transitions but no external service.

| ID | Scenario | Automated assertion | Result |
|---|---|---|---|
| 01 | start/navigation | dashboard and System page accessible | PASS |
| 02 | valid input | 360° and ACCEPTED | PASS |
| 03 | invalid mileage | range error, no navigation | PASS |
| 04 | clear analysis | prompt V3 and explanation visible | PASS |
| 05 | ML prediction | 2.487 regression and ridge-v1 visible | PASS |
| 06 | RAG | five rows and RRF V2 | PASS |
| 07 | evidence detail | ARI-SYN-001 quote and 144 minutes match | PASS |
| 08 | ambiguous input | manual mapping; no case result | PASS |
| 09 | what-if | new ML snapshot; baseline remains | PASS |
| 10 | rapid requests | submit lock; anti-stale unit assertion | PASS |
| 11 | backend outage | controlled message and retry | PASS |
| 12 | responsive | 768×1024 and 390×844 no page overflow | PASS (2) |
| 13 | reload | safe empty case and recovery action | PASS |
| 14 | keyboard | focus and Enter submit without mouse | PASS |

Command:

    dotnet test tests/ARI.Web.E2ETests/ARI.Web.E2ETests.csproj --no-build --no-restore --filter 'Category!=Live'

Result: **15 passed, 0 failed, 10 s**.

## Component/client tests

ARI.Web.Tests has nine tests: negative mileage, structured/text conflict, trust labels, full workflow, manual mapping, two scenarios, anti-stale, cancellation and RFC 7807 numeric-status translation.

Result: **9 passed, 0 failed**.

## Live browser test

LiveUiAcceptanceTests is separately selected. It opens live ARI.Web, submits the standard synthetic repair, requires ACCEPTED plus a plausible tolerance around the historic 2.487136 h reference plus RRF V2 plus five sources, opens evidence, runs two real ML scenarios, protects the baseline and saves three screenshots.

Run:

    $env:ARI_LIVE_UI_URL = 'http://127.0.0.1:5280'
    dotnet test tests/ARI.Web.E2ETests/ARI.Web.E2ETests.csproj --no-build --no-restore --filter 'Category=Live'

### Successful live evidence

- ARI.Api /health: HTTP 200.
- HANA /health/ready: HTTP 200, authenticated user ARI_APP.
- Python /health/ml/ready: HTTP 200, ari-repair-duration-v1.
- ARI.Web /: HTTP 200.
- OpenAI LLM V3 extraction: HTTP 200 using gpt-6-luna and repair-extraction-v3.
- OpenAI embeddings: HTTP 200.
- Grounded explanation: HTTP 200 with source-bound observations.
- Browser result: ACCEPTED, 2.447 h, Ridge ari-repair-duration-v1.
- HANA retrieval: RANK_FUSION_V2, REPAIR_PROFILE_V2, five sources.
- Scenario 1: High mileage, 2.497 h (+0.051 h / +2.1%).
- Scenario 2: Visible corrosion, 2.655 h (+0.208 h / +8.5%).
- Baseline after both scenarios: unchanged at 2.447 h.
- Live test result: **1 passed, 0 failed, 16.55 s**.
- Three full-page screenshots exist under docs/ui-screenshots.

## Regression evidence

| Suite | Result |
|---|---:|
| Solution build | 0 warnings, 0 errors |
| Core | 18 passed |
| API | 58 passed |
| HANA integration (real ARI_APP) | 16 passed |
| Python ML | 24 passed; one upstream deprecation warning |
| Web unit/client | 9 passed |
| deterministic browser | 15 passed |
| live browser / real providers | 1 passed |
