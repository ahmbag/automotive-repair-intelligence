# ARI-UI-01 – UI workflows

## 1. Repair intake

Open **New Repair Estimate**. Manufacturer and model are optional because LLM extraction may obtain them from text. Model year, mileage and workshop baseline have plausible domain ranges. The repair code comes from the supported catalog.

Only facts known before repair may be entered: vehicle identity and mileage, requested operation, visible corrosion or prior damage, the pre-repair description and the point in time (AsOfUtc) at which the information was known. Actual labor, completion outcomes and complications discovered during/after repair are deliberately absent.

### Conflict handling

IntakeConflictDetector compares a structured model year or mileage with recognizable values in the description. A mismatch becomes a validation error. The application neither silently prefers one value nor merges both. Unknown optional fields remain unknown until server extraction.

### Demo cases

| Case | Learning purpose |
|---|---|
| Standard Repair | complete accepted vertical path |
| Corrosion Risk | permitted pre-repair risk feature |
| High Mileage | domain-valid large numerical feature |
| Ambiguous Description | server quality gate and MANUAL_MAPPING_REQUIRED |
| Missing Information | client-side validation without API request |

The cases populate the real form. Analyze still invokes the configured client. Fixture mode has a prominent banner, so test data cannot be mistaken for live results.

## 2. Analysis

The coordinator:

1. cancels any previous request;
2. allocates a new generation and correlation ID;
3. transitions VALIDATING → RUNNING;
4. calls /api/estimates/with-evidence;
5. commits only if its generation is still current;
6. maps extraction quality and explanation availability to a terminal state.

The API endpoint is synchronous, so the UI shows activity and stage text but never invents a percentage.

On HTTP 422, Web shows manual mapping and API-supplied missing fields. There is no client bypass. HTTP 429, 503, timeout, unreadable response and network failure are translated into actionable messages. A partial response retains its numerical prediction and evidence while clearly warning that explanation is unavailable.

## 3. Repair Case 360°

The page separates:

- **prediction**: Ridge hours and model version;
- **validated input**: vehicle, repair code, mileage and before-repair facts;
- **technical trace**: prompt/model/retrieval/embedding/data versions, feature categories and correlation ID;
- **evidence and explanation**: five historical cases plus source-bound observations.

An RRF score is a ranking score, not a probability. No confidence interval is fabricated. Historical actual labor is marked “documented” and separated from the current prediction.

Evidence detail distinguishes the historical pre-repair profile, during/after completion evidence and actual labor. Source references are plain text, never arbitrary hyperlinks. With no cases, the prediction remains but the UI explicitly says there is no historical support.

## 4. What-if

A scenario requires a successful baseline. The baseline stores a copied LaborPredictionInput and cannot be edited.

Allowed changes are mileage, vehicle age, supported repair code and hypothetical visible corrosion. Each click creates a new ScenarioSnapshot; up to five coexist. Absolute and relative deltas are against the original prediction and are labelled non-causal.

- **ML-only** calls /api/predictions for rapid sensitivity checks.
- **Full Evidence Refresh** calls extraction, ML and retrieval again.

Actual labor and post-repair complications are not fields in the scenario DTO. A new scenario never mutates another scenario or the baseline.

## 5. Recovery

| Situation | Behaviour |
|---|---|
| API unavailable | controlled error; retry remains; no snapshot commit |
| HANA paused | readiness unavailable; full analysis returns controlled 503 |
| OpenAI rate limit | wait/retry guidance; completed snapshots retained |
| manual mapping | missing fields shown; ML does not run |
| Python down | no browser fallback; controlled unavailable state |
| no RAG matches | prediction retained, evidence explicitly empty |
| cancellation | token cancelled; no partial commit |
| rapid second request | prior request cancelled; generation blocks stale overwrite |
| Blazor disconnect | reconnect modal; reload returns to a safe empty session |
