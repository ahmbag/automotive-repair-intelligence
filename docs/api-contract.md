# API contracts

ARI-ML-01 exposes the numerical prediction boundary. The richer, persisted estimate workflow
remains the planned next application use case.

## `POST /api/repair-reports/extract`

Implemented by ARI-LLM-01. The caller supplies the untrusted document and the trusted application
context:

```json
{
  "document": "A mounting bolt broke during removal and was replaced.",
  "documentType": "REPAIR_COMPLETION_REPORT",
  "promptVersion": "repair-extraction-v3"
}
```

The response contains nullable vehicle/repair fields, evidence-backed complications,
known-before facts, optional explicitly stated actual labor, status, and provider telemetry.
Invalid requests return 400, schema/domain failures 422, and provider failures 503.

## `POST /api/estimates/from-description`

Implemented by ARI-LLM-01 as the non-persisted combined workflow. It accepts a pre-repair
description and a positive reference duration. Only a complete, validated extraction with status
Only application-owned `ACCEPTED` reaches `ILaborPredictionService`; `PARTIAL`, `NEEDS_REVIEW`,
and `REJECTED` are stopped. Observed complications and actual labor can never be
forwarded as ML features.

## `POST /api/predictions`

Implemented. Accepts only information available before repair work begins:

```json
{
  "repairCode": "CONTROL_ARM_FRONT_LEFT_REPLACE",
  "manufacturer": "Volkswagen",
  "model": "Golf VII",
  "vehicleAgeYears": 12,
  "mileageKm": 215000,
  "baselineLaborHours": 1.8,
  "knownConditions": ["VISIBLE_CORROSION"],
  "extractedConditions": []
}
```

A successful ML response identifies its semantics and provenance:

```json
{
  "predictedLaborHours": 2.4871363700313847,
  "modelVersion": "ari-repair-duration-v1",
  "dataSource": "SYNTHETIC",
  "rangeLowerHours": null,
  "rangeUpperHours": null,
  "predictionMethod": "ML_REGRESSION",
  "status": "PREDICTED"
}
```

Invalid transport/domain values return 400. An unavailable model returns 503 unless the explicit
reference fallback is enabled; that response uses `REFERENCE_BASELINE` and `FALLBACK` and is never
labelled as ML.

## `GET /health/ml/ready`

Implemented. Returns 200 only when the Python service reports a compatible loaded model, and 503
for missing configuration, transport failure, timeout, or model non-readiness.

## `POST /api/estimates` (planned)

Creates an auditable estimate for facts known before repair begins.

```json
{
  "vehicle": {
    "manufacturer": "Volkswagen",
    "model": "Golf VII",
    "modelYear": 2014,
    "mileageKm": 210000
  },
  "repairCode": "SUSPENSION_CONTROL_ARM_REPLACE",
  "description": "Replace front left control arm. Visible corrosion.",
  "knownConditions": [
    "CORROSION_VISIBLE"
  ]
}
```

`knownConditions` are explicit facts supplied by the caller. Conditions later extracted from
`description` are tracked separately inside the pipeline and are not silently represented as
caller assertions.

Planned success response (`200 OK`):

```json
{
  "estimateId": "9df90e89-9ac6-4b86-902b-dcfc413482ed",
  "baselineLaborHours": 1.8,
  "predictedLaborHours": 2.6,
  "predictionRangeHours": null,
  "modelVersion": "demo-v1",
  "riskFactors": [
    "CORROSION_VISIBLE"
  ],
  "dataSource": "SYNTHETIC",
  "status": "ESTIMATED"
}
```

The values are illustrative, not model output. `predictionRangeHours` remains optional until a
validated interval method exists. Validation failures will use `400`; unavailable ML without an
enabled explicit fallback will use `503`.

## `GET /api/estimates/{id}` (planned)

Returns a previously persisted response by estimate identifier. Planned outcomes are `200` and
`404`; the repository boundary is `IRepairEstimateRepository`.

## `GET /health`

Implemented now. Returns `200 OK` when the ASP.NET Core process is reachable:

```json
{
  "status": "Healthy",
  "service": "ARI.Api"
}
```

## `GET /health/ready`

Implemented by ARI-HANA-01B. Opens a TLS-protected, certificate-validated HANA connection and
executes `SELECT CURRENT_USER FROM DUMMY`. HTTP 200 means authentication and the SQL smoke query
succeeded; configuration, network, TLS, authentication, query, or user-mismatch failures return
HTTP 503 with a sanitized body.

```json
{
  "status": "Ready",
  "dependency": "SAP HANA",
  "authenticatedUser": "ARI_APP"
}
```
# RAG endpoints

`POST /api/repair-cases/search` accepts a `RepairRetrievalQuery` with description, normalized
repair code, optional vehicle facts, pre-repair conditions, `asOfUtc`, strategy, `topK`, and a
calibrated minimum similarity. It returns source-backed historical cases including score,
metadata, availability timestamp, excerpt, and `repair-order:{guid}` reference.

`POST /api/repair-cases/explain` performs retrieval itself and therefore never accepts arbitrary
source blocks from the caller. It returns the retrieval result plus a strict, source-validated
explanation.

`POST /api/estimates/with-evidence` orchestrates pre-repair extraction, Ridge prediction,
retrieval, and grounded generation. Only `BEFORE_REPAIR` conditions enter the ML input. An LLM
generation outage leaves prediction and retrieved sources available with an explicit warning;
retrieval or prediction outages return controlled HTTP 503.
# Retrieval-Konfiguration ARI-RAG-02

Die Retrieval-Endpunkte akzeptieren serverseitig validierte Kombinationen aus
`retrievalStrategy`, `embeddingProfile` und `topK`.

- V1: `STRUCTURED`, `SEMANTIC` oder `HYBRID` mit `REPAIR_REPORT_V1`;
- V2: `DOMAIN_SEMANTIC_V2`, `STRUCTURED_FIRST_HYBRID` oder `RANK_FUSION_V2` mit
  `REPAIR_PROFILE_V2`;
- `topK`: 1 bis 20; `minimumSimilarity`: -1 bis 1.

Nach bestandenem V2-Holdout-Gate ist die produktive Voreinstellung des
Explainable-Estimate-Endpunkts `RANK_FUSION_V2` mit `REPAIR_PROFILE_V2`. Das Ergebnis nennt zusätzlich
`embeddingProfile`, `hanaLatencyMilliseconds`, `usedStructuredFallback` und `fallbackReason`.
Ungültige Kombinationen liefern HTTP 400; nicht verfügbare HANA-/Providerpfade HTTP 503. Bei
Hybrid/RRF darf allein ein Query-Embedding-Ausfall kontrolliert auf strukturierte V2-Rangfolge
zurückfallen.
