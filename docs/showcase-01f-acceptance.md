# ARI-SHOWCASE-01F acceptance record

Candidate: `ARI-RC1` / `0.1.0-rc.3`<br>
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
| Live browser | PASS — 1/1 in 22 s, no skips |

The successful rerun used an explicitly authorized temporary OpenAI key only in the `ARI.Api` process environment. It was not written to User Secrets, configuration, logs, screenshots, Git, or test artifacts; that process was stopped immediately after the run.

Provider evidence from the successful run:

- OpenAI LLM V3 extraction: HTTP 200, model `gpt-6-luna`, prompt `repair-extraction-v3`;
- OpenAI embeddings: HTTP 200;
- grounded explanation: HTTP 200 and restricted to the five retrieved source IDs;
- Python Ridge inference: HTTP 200, `ari-repair-duration-v1`;
- HANA retrieval: five `RANK_FUSION_V2` / `REPAIR_PROFILE_V2` sources;
- baseline: 2.447 h; high mileage: 2.497 h; visible corrosion: 2.655 h;
- baseline remained unchanged after both what-if runs.

Final fresh regression: **141/141 tests passed** (121 public-checkout + 3 trained-model + 16 HANA + 1 live browser), with no skips in the live suites. The refreshed Repair Case 360° and intake screenshots contain synthetic data only and were visually rechecked for credentials and internal access information.

## Definition of Done

| 01F criterion | State | Evidence |
| --- | --- | --- |
| Documented .NET/Python environment builds from a fresh checkout | PASS | pinned toolchains, locks, [reproduction guide](reproducibility-and-ci.md), and isolated clone run |
| Deterministic tests and GitHub CI succeed | PASS locally / hosted pending 01G | 121/121 from the public checkout; workflow has read-only permissions and no live secrets |
| Fresh live E2E and final regression are documented | PASS | all readiness gates HTTP 200; 16 HANA + 3 model + 1 browser acceptance tests passed |

No unavailable or skipped live test is counted as a pass.
