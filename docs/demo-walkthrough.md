# ARI-RC1 demo walkthrough

This walkthrough covers the complete portfolio story: **Dashboard → Intake → LLM → ML → Evidence → What-if**. The reference case and all displayed history are synthetic.

## Choose a mode

### Deterministic demo

Set `AriApi__UseFixture=true` for `ARI.Web`. The typed client is replaced by the in-process fixture provider. The UI displays **AUTOMATED TEST DATA** at all times, so fixture results cannot be mistaken for live integration. This mode is offline-safe, deterministic, and used by the main Playwright suite.

### Live demo

Keep `AriApi__UseFixture=false` (the default) and start:

1. the checksum-verified Python model service;
2. `ARI.Api` with HANA and OpenAI configuration supplied through secrets/environment;
3. `ARI.Web` pointing at the API.

Live mode invokes OpenAI extraction/embeddings/explanation, Ridge inference, and SAP HANA Cloud retrieval. Readiness cards and returned provenance make unavailable dependencies visible. The live Playwright suite runs only when `ARI_LIVE_UI_URL` is set.

## Review script

1. **Dashboard** — confirm that the system overview names the LLM, ML, HANA retrieval, and UI boundaries. In fixture mode, point out the test-data banner.
2. **Repair intake** — select **Start new estimate**, review the prefilled synthetic control-arm case, and note that only pre-repair facts can be entered. The form has no actual-labor or post-repair-outcome field.
3. **Run analysis** — choose **Analyze repair case**. The status changes through validation and running states. In live mode the API executes LLM V3 extraction, application validation, Ridge prediction, RRF V2 retrieval, and grounded explanation.
4. **Repair Case 360°** — verify `ACCEPTED`, the predicted effort, five evidence rows, `RANK_FUSION_V2`, model/profile identifiers, source references, correlation ID, and latency. Open **Inspect evidence** to see the documented excerpt.
5. **What-if** — select **Explore what-if scenarios**. Run the two reference scenarios below. The original baseline remains unchanged and each result is marked `ML-ONLY` unless a full evidence refresh is explicitly requested.

## Live reference run

The accepted live run produced these values with `ari-repair-duration-v1`:

| State | Prediction | Delta from baseline |
| --- | ---: | ---: |
| Baseline | 2.447 h | — |
| High mileage (240,000 km) | 2.497 h | +0.050 h (rounding may display +0.051) |
| Visible corrosion | 2.655 h | +0.208 h |

These numbers are runtime model outputs, not hard-coded UI values. They are a regression reference for the current synthetic model artifact. The deltas demonstrate **model sensitivity only**: they do not establish that mileage or corrosion causally adds that amount of work.

## Visual evidence

| View | File | Capture path |
| --- | --- | --- |
| Dashboard | [`01-dashboard-live.png`](ui-screenshots/01-dashboard-live.png) | live Playwright test before navigation |
| Repair intake | [`04-repair-intake-live.png`](ui-screenshots/04-repair-intake-live.png) | live Playwright test before analysis |
| Repair Case 360° | [`02-repair-case-360-live.png`](ui-screenshots/02-repair-case-360-live.png) | live pipeline after evidence inspection |
| What-if | [`03-what-if-live.png`](ui-screenshots/03-what-if-live.png) | live run after both scenarios |

The capture implementation is [`LiveUiAcceptanceTests.cs`](../tests/ARI.Web.E2ETests/LiveUiAcceptanceTests.cs). It asserts live-only behavior, accepted extraction, five retrieved cases, RRF V2, evidence details, two what-if cards, and an unchanged baseline before writing the images.

## Failure behavior to demonstrate

- Validation errors remain on the intake page with field-level guidance.
- A domain/LLM review result is not silently promoted to accepted.
- Prediction or retrieval unavailability produces a controlled failure and no fabricated estimate.
- Explanation failure may preserve prediction and evidence as a visibly partial result.
- Cancelling an active request prevents the stale result from replacing the current workspace.

