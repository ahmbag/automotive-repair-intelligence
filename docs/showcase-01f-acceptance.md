# ARI-SHOWCASE-01F acceptance record

Candidate: `ARI-RC1` / `0.1.0-rc.1`  
Execution date: 2026-11-05  
Scope: reproducibility, deterministic CI path, and fresh live end-to-end regression

## Release environment

| Item | Verified value |
| --- | --- |
| .NET SDK | 10.0.401 |
| Python | 3.14.6 |
| NuGet resolution | 15 committed `packages.lock.json` files; locked restore |
| Python resolution | committed `ml/requirements.lock` |
| Build mode | Release, deterministic, warnings as errors |

## Deterministic regression

The same commands encoded by `.github/workflows/ci.yml` were executed locally from the release candidate.

| Gate | Result |
| --- | ---: |
| Locked NuGet restore | PASS |
| Solution build | PASS — 0 warnings, 0 errors |
| ARI.Core.Tests | PASS — 18/18 |
| ARI.Api.Tests | PASS — 58/58 |
| ARI.Web.Tests | PASS — 9/9 |
| Python CI-safe tests | PASS — 21/21; 3 trained-artifact tests deliberately outside the public-checkout gate; one upstream Starlette/httpx deprecation warning |
| Deterministic Playwright | PASS — 15/15 |
| Deterministic fresh-checkout total | **PASS — 121/121** |
| Publication/security audit | PASS |

The workflow uses no provider secrets and makes no HANA/OpenAI request. Its jobs are independently reproducible with [`tools/validate-release.ps1`](../tools/validate-release.ps1). A hosted GitHub run can only exist after the repository is connected/pushed in 01G; the workflow definition and its exact local command path are complete here, without claiming a hosted run that does not yet exist.

## Fresh-checkout proof

An isolated local clone of commit `6d64d10` contained no local `bin`, `obj`, `.venv`, model binary, generated dataset, or User Secrets. Locked NuGet restore, creation of a new CPython 3.14.6 environment, installation from the release lock, Release build, 18 Core, 58 API, 9 Web, 21 model-independent Python, and 15 browser tests all succeeded. The first diagnostic clone exposed four initially implicit Python transitives and an implicit build backend; all five were pinned before this final validation clone.

## Live regression

The fresh 2026-11-05 execution produced:

| Live gate | Result |
| --- | --- |
| Python `/health/ready` | PASS — HTTP 200, `ari-repair-duration-v1`, schema `ari-labor-features-v1` |
| API `/health` | PASS — HTTP 200 |
| HANA `/health/ready` | PASS — HTTP 200, authenticated `ARI_APP` |
| API ML readiness | PASS — HTTP 200 |
| ARI.Web `/health-ui` | PASS — HTTP 200, fixture mode disabled |
| HANA integration and data acceptance | PASS — 16/16, no skips |
| Trained-model acceptance | PASS — 3/3, no skips |
| Live browser | **BLOCKED — 0/1; OpenAI Responses API returned HTTP 401** |

The browser failure is not a product assertion failure: API logs show that the configured OpenAI credential was rejected before extraction. No key value was logged or copied into the repository. The older green browser report is not used as a substitute. A valid replacement key and one rerun are required to close 01F.

Current fresh regression: **140 tests passed** (121 public-checkout + 3 trained-model + 16 HANA), **1 live browser test failed at provider authentication**. The 141-test release reference is therefore not yet re-established.

## Definition of Done

| 01F criterion | State | Evidence |
| --- | --- | --- |
| Documented .NET/Python environment builds from a fresh checkout | PASS | pinned toolchains, locks, [reproduction guide](reproducibility-and-ci.md), and isolated clone run |
| Deterministic tests and GitHub CI succeed | PASS locally / hosted pending 01G | 121/121 from the public checkout; workflow has read-only permissions and no live secrets |
| Fresh live E2E and final regression are documented | BLOCKED | current evidence recorded above; valid OpenAI credential and one live-browser rerun required |

No unavailable or skipped live test is counted as a pass.
