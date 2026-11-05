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

An isolated local clone of the 01F release checkpoint contained no local `bin`, `obj`, `.venv`, model binary, generated dataset, or User Secrets. Locked NuGet restore, creation of a new CPython 3.14.6 environment, installation from the release lock, Release build, 18 Core, 58 API, 9 Web, 21 model-independent Python, and 15 browser tests all succeeded. The first run exposed four initially implicit Python transitives; these were pinned before the final validation clone.

## Live regression

Pending the fresh 2026-11-05 execution. This section must record readiness, HANA integration count, live-browser outcome, model/retrieval identifiers, and any provider limitation from the current run. Older ARI-UI-01 evidence is reference material only and does not satisfy this fresh gate.

## Definition of Done

| 01F criterion | State | Evidence |
| --- | --- | --- |
| Documented .NET/Python environment builds from a fresh checkout | PASS | pinned toolchains, locks, [reproduction guide](reproducibility-and-ci.md), and isolated clone run |
| Deterministic tests and GitHub CI succeed | PASS locally / hosted pending 01G | 121/121 from the public checkout; workflow has read-only permissions and no live secrets |
| Fresh live E2E and final regression are documented | IN PROGRESS | fresh live execution pending |

No unavailable or skipped live test is counted as a pass.
