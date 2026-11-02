# ARI-UI-01 – Formal acceptance

Date: 2026-11-01  
Status: **32/32 accepted**

Legend: ✅ accepted

## 01A – Foundation (4/4)

- ✅ ARI.Web is in the .NET 10 solution, starts independently and exposes /health-ui.
- ✅ Blazor shell, five navigation targets and typed API client work.
- ✅ options validation, HttpClientFactory, scoped state, DI and truthful health are verified.
- ✅ Web has no Infrastructure/API reference; Core remains free of Blazor/SAP/OpenAI.

## 01B – Repair Intake (4/4)

- ✅ vehicle, supported operation, baseline, as-of time and bounded description are editable.
- ✅ EditForm/DataAnnotations/domain checks plus API server validation are active.
- ✅ model-year/mileage contradictions are visible; optional unknowns are preserved.
- ✅ five reproducible synthetic demos populate the real workflow.

## 01C – Workflow (4/4)

- ✅ UI invokes the complete evidence estimate endpoint.
- ✅ ACCEPTED, PARTIAL, NEEDS_REVIEW and rejection/manual mapping have distinct treatment.
- ✅ cancellation, submit lock, generation and stale-result guard are tested.
- ✅ 422, partial explanation, timeout, 429, 503, network and invalid response are controlled.

## 01D – Repair Case 360° (4/4)

- ✅ Ridge hours and version are prominent and labelled prediction.
- ✅ LLM V3 extraction and only approved pre-repair features are visible.
- ✅ ML, prompt, data, RRF, embedding, latency and correlation trace are displayed.
- ✅ synthetic/experimental caveat is visible; no invented confidence or interval.

## 01E – Evidence Explorer (4/4)

- ✅ top-five RRF V2 cases show rank, score, vehicle, repair and historical labor.
- ✅ source references and exact excerpts are tied to the selected detail.
- ✅ historical actual values are documented and separated from prediction.
- ✅ empty retrieval is explicit; text is encoded and references cannot become links.

## 01F – What-if (4/4)

- ✅ immutable baseline and independent scenario snapshots exist.
- ✅ two scenarios use the real prediction client path; live Python readiness is proven.
- ✅ only mileage, age, supported code and hypothetical corrosion are editable.
- ✅ unit/browser tests prove no baseline or sibling overwrite.

## 01G – UX & Security (4/4)

- ✅ desktop plus 768×1024 and 390×844 are browser-tested.
- ✅ loading, error, empty, partial, review, cancelled and recovery states exist.
- ✅ labels, headings, skip link, live regions, focus, keyboard and reduced motion are covered.
- ✅ no browser secret/direct DB/provider call; safe text, bounds, cancellation and privacy-safe logs.

## 01H – Testing & Documentation (4/4)

- ✅ all 14 browser scenarios pass (15 executions with two viewports).
- ✅ real Browser → OpenAI → Python ML → HANA/RAG completed: ACCEPTED, 2.447 h, five sources and two live ML scenarios.
- ✅ build/regressions: 18 Core + 58 API + 16 HANA + 24 Python + 9 Web + 15 deterministic browser + 1 live browser.
- ✅ architecture, workflows, state, tests, limitations and screenshot location are documented.

## Live acceptance evidence

- Browser result: ACCEPTED, current Ridge prediction 2.447 h.
- Retrieval: RANK_FUSION_V2 with five HANA-backed historical sources.
- Explanation: grounded LLM response with source-bound quotations.
- What-if: High mileage 2.497 h and visible corrosion 2.655 h; baseline remained 2.447 h.
- Screenshots: dashboard, Repair Case 360° and What-if comparison are stored in docs/ui-screenshots.

Because the key was pasted into chat, rotate it after acceptance and keep the replacement only in API User Secrets.
