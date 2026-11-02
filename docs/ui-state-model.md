# ARI-UI-01 – State model

## Analysis states

| State | Entry | Allowed next states | User meaning |
|---|---|---|---|
| IDLE | new/reset workspace | VALIDATING | no analysis in progress |
| VALIDATING | valid form submit | RUNNING, FAILED | local/domain checks |
| RUNNING | API call started | all terminal states | real backend work; no fake percentage |
| COMPLETED | accepted extraction + full result | VALIDATING, reset | complete result |
| PARTIAL_RESULT | valid prediction/evidence but partial quality/explanation | retry/reset | usable subset with warning |
| MANUAL_REVIEW_REQUIRED | HTTP 422 or NEEDS_REVIEW | corrected submit/reset | mapping required; no bypass |
| FAILED | controlled timeout/network/provider/API failure | retry/reset | no result committed |
| CANCELLED | user cancellation | retry/reset | no result committed |

## Quality state mapping

| API extraction state | UI treatment |
|---|---|
| ACCEPTED | complete unless explanation is absent |
| PARTIAL | partial result warning |
| NEEDS_REVIEW | manual review required |
| REJECTED / 422 | manual review/rejected message; no prediction commit |

The API remains authoritative. Web displays but does not redefine the quality policy.

## Concurrency invariant

Every submit gets a monotonically increasing generation and unique GUID. A second request cancels the active token and advances the generation. Completion may commit only when completedGeneration equals currentGeneration. A slow old response cannot overwrite a newer response.

The submit button is disabled during VALIDATING and RUNNING. This is a usability guard; the generation comparison is the correctness guard.

## Snapshot invariants

- RepairAnalysisSnapshot copies the intake and stores accepted result and baseline features.
- Scenario creation uses record copying.
- RepairWorkspace exposes a read-only scenario list.
- A maximum of five scenarios is enforced twice.
- A scenario never replaces the baseline.
- Reload starts a new circuit and a safe empty session. Persistent history is outside the MVP.

## Accessible state communication

State messages use role=status and aria-live=polite. Errors use text and border semantics, never color alone. Route changes focus the h1; controls have labels; the page has a skip link; dialogs have accessible names; focus outlines remain visible. Reduced-motion preference disables movement.
