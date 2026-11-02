# ARI-RC1 scope and repository audit

**Candidate:** `ARI-RC1`  
**Semantic version:** `0.1.0-rc.1` (see [`VERSION`](../VERSION))  
**Scope freeze date:** 2026-11-02  
**Data classification:** synthetic only

## Release intent

ARI-RC1 is a reviewable portfolio candidate for one narrow, end-to-end repair-effort workflow. The freeze applies to the behavior and evidence listed here. After the candidate commit, changes require a new candidate version; no unassessed functional change may be described as part of ARI-RC1.

The freeze anchor is the Git commit that contains this document and `VERSION`. It can be inspected with `git rev-parse HEAD`. A public tag, packaged release, deployment, license decision, and publication remain outside Showcase 01A–01D.

## Included and completed

| Capability | Evidence |
| --- | --- |
| Repair Case Intake | [`NewEstimate.razor`](../src/ARI.Web/Components/Pages/NewEstimate.razor), validation tests, live screenshot |
| LLM V3 extraction and validation | [`repair-extraction-v3`](prompts/repair-extraction-v3.md), ADR 0007, frozen holdout report |
| Ridge labor prediction | versioned `ari-repair-duration-v1` artifact, metadata and test evaluation |
| HANA Vector / RRF V2 retrieval | migrations 003/004, ADR 0006/0008, frozen holdout report |
| Grounded explanations | source validation, grounding policy, endpoint and adapter tests |
| Repair Case 360° | live UI, evidence modal, source/provenance metadata, browser acceptance |
| What-if simulation | immutable baseline plus ML-only scenarios and deltas |
| Status and metadata | workflow status, correlation ID, latency, model/profile/strategy identifiers |

## Explicitly excluded

- Full workshop management, scheduling, invoicing, parts logistics, or customer lifecycle.
- Commercial or production deployment, service-level commitments, and public release packaging.
- Real customer, vehicle, insurer, or workshop data.
- New ML methods, uncertainty calibration, or model re-training for this candidate.
- Further LLM prompt optimization, taxonomy expansion, or RAG ranking experiments.
- Claims of real-world accuracy, causal effects, pricing approval, or safety authorization.

## Repository inventory

Audit excludes generated `bin`, `obj`, virtual-environment, and cache directories.

| Area | Audited state |
| --- | --- |
| Solution | `AutomotiveRepairIntelligence.slnx`; 15 .NET projects |
| Runtime projects | `ARI.Web`, `ARI.Api`, `ARI.Core`, `ARI.Infrastructure`, Python `ari_ml` |
| Tools | database migrator, historical importer, data profiler, ML exporter, LLM evaluator, RAG tool |
| Database | four forward-only HANA migrations (`001`–`004`) |
| Tests | 33 C#/Python test source files across unit, contract, integration, E2E, and live categories |
| Prompts | three versioned extraction prompts (`v1`–`v3`) |
| Evaluation data | 13 committed JSON set/report/analysis files under `artifacts/llm` and `artifacts/rag` |
| ML evidence | versioned metadata/evaluation under `artifacts/ml`; generated training export/model remain local and ignored, with hashes recorded in metadata |
| Documentation | 47 Markdown files before Showcase additions; architecture, API, ADRs, methods, acceptance |
| Visual evidence | dashboard, intake, Repair Case 360°, and what-if live captures |

## Open-item audit

An exact repository search for `TODO`, `FIXME`, `HACK`, `NotImplementedException`, and `NotSupportedException` found no product-code TODOs. Three `NotSupportedException` occurrences are deliberate uncalled members in API unit-test doubles:

- `tests/ARI.Api.Tests/RepairEmbeddingIndexerTests.cs`
- `tests/ARI.Api.Tests/RepairSearchV2Tests.cs`

They do not represent unimplemented runtime paths. Planned endpoints in `docs/api-contract.md` are explicitly labelled planned and are not part of ARI-RC1.

## Stable versus experimental

**Candidate-complete:** the included workflow, current contracts, deterministic fixture mode, frozen synthetic evaluations, and the live acceptance path.

**Experimental:** all metric values as predictors of real-workshop performance; the current closed complication taxonomy; exact vector search at 1,000 records; Ridge point estimates without calibrated intervals; OpenAI latency/cost; synthetic what-if sensitivity.

## Known limitations and open boundaries

- Every training/evaluation case is synthetic and comes from a controlled generator family.
- The LLM’s perfect V3 holdout score covers a closed synthetic taxonomy, not free-form workshop language.
- The Ridge artifact is technically integrated, but its own metadata conservatively recommends the statistical baseline for decision policy; the UI presents Ridge as an engineering demonstration.
- What-if deltas show model sensitivity, not causality and not a promise of additional labor.
- Retrieval was evaluated at 1,000 cases; it is not a scale or concurrency benchmark.
- The UI has no user/tenant authorization model. ARI-RC1 must not hold customer data.
- The repository started without Git history. ARI-RC1 is the initial candidate snapshot; earlier acceptance evidence is preserved in files and hashes, not prior commits.

## Freeze rule

The candidate is frozen when the version file, this audit, README, diagrams, demo evidence, evaluation evidence, and acceptance record are committed together and the verification commands pass. Any later functional edit requires a new candidate identifier and a repeated impact-based verification. No Git tag or public release is created by Showcase 01A–01D.
