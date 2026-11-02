# ARI-RC1 evaluation evidence

This page is the portfolio-level index for the frozen ML, LLM, and retrieval evidence. It summarizes existing artifacts without recomputing or replacing them.

## Reading the metrics

- **MAE**: mean absolute difference between predicted and actual labor minutes; lower is better.
- **Precision**: fraction of extracted complication labels that are correct.
- **Recall**: fraction of expected complication labels that are recovered.
- **Micro-F1**: global harmonic mean of precision/recall across all labels.
- **Macro-F1**: unweighted mean of per-class F1, exposing weak classes.
- **Recall@5**: fraction of five expected relevant repairs present in the top five.
- **nDCG@5**: rank-sensitive relevance quality in the top five; higher means relevant cases appear earlier.

All comparisons within a table use the same frozen rows and labels. Values across the ML, LLM, and retrieval tables are not directly comparable because they answer different questions.

## ML: labor prediction

Versioned evidence: [`metadata.json`](../artifacts/ml/ari-repair-duration-v1/metadata.json) and [`evaluation.json`](../artifacts/ml/ari-repair-duration-v1/evaluation.json). The generated `model.joblib` stays outside Git; its SHA-256 is frozen in metadata and verified by the inference loader before use.

| Method on the same 150-case test split | MAE (minutes) |
| --- | ---: |
| Historical median (best simple baseline) | 27.500 |
| Ridge | **24.842** |
| Random Forest | 26.376 |
| Reference labor | 31.927 |

Ridge lowers MAE by `(27.500 - 24.842) / 27.500 = 9.7%` relative to the best simple test baseline. The selection itself used validation MAE; the 150 newest cases were evaluated only after model selection. The data is ordered by `completed_at_utc` and split 700/150/150 (70/15/15), preventing future repairs from entering an older training partition.

Feature schema `ari-labor-features-v1` contains only seven prediction-time fields: vehicle age, mileage, baseline labor, known corrosion, repair code, manufacturer, and model. Actual labor, completion text, complications, extra work, and post-repair outcomes are labels/evidence, never inputs. Model version is `ari-repair-duration-v1`; the model SHA-256 is `d206f94f7c532d23c210ca0300b978ce5253786143ca4f67c0c8b6a3d8b06840`.

Full method: [ML training and evaluation](ml-training-and-evaluation.md). Feature/leakage contract: [ML feature schema](ml-feature-schema.md).

## LLM: structured repair extraction

The final 60-case holdout is [`quality-holdout-set.json`](../artifacts/llm/quality-holdout-set.json); both prompt variants are recorded in [`quality-holdout-report.json`](../artifacts/llm/quality-holdout-report.json).

| Same holdout | Precision | Recall | Micro-F1 | Macro-F1 |
| --- | ---: | ---: | ---: | ---: |
| V1 | 1.000 | 0.526 | 0.690 | 0.624 |
| V3 | **1.000** | **1.000** | **1.000** | **1.000** |

V2 attempted a more restrictive prompt mapping and reduced recall on development data. V3 keeps one schema-constrained extraction call, then performs a deterministic, negation-aware classification over explicitly supported sentences. Evidence and temporal validators run after classification. This preserves V1 precision while closing the synthetic-taxonomy recall gap.

Development (50 cases) and holdout (60 cases) have non-overlapping IDs and do not overlap the 100-case V1 reference. Prompt/classifier choices were frozen after development; V1 and V3 ran on the same final holdout. Dataset content checksum: `51d92fc6f86c9021cfa4035bc501cb39cb5636c6a09200a16579991b21aa146d`. Holdout report file SHA-256: `94b140adc5d31ff2380aa5dc1c199a5a06449e5301f701d66c3ddcafa2a53e49`.

Full method and per-class results: [LLM evaluation V2](llm-evaluation-v2.md). Decision rationale: [ADR 0007](adr/0007-llm-quality-v3.md).

## Retrieval: HANA Vector Engine and RRF V2

The frozen V1 report remains in [`retrieval-evaluation-report.json`](../artifacts/rag/retrieval-evaluation-report.json). It shows why short completion reports are poor semantic documents for queries whose relevance is defined mainly by vehicle and pre-repair attributes:

| V1 strategy, same 40 cases | Recall@5 | nDCG@5 | MRR |
| --- | ---: | ---: | ---: |
| Structured | 0.6114 | 0.7935 | 0.9429 |
| Semantic completion-report embedding | 0.0171 | 0.0302 | 0.0500 |
| V1 hybrid | 0.1600 | 0.1720 | 0.5381 |

V2 creates a canonical pre-repair search profile and stores completion evidence separately. On the independent 50-case holdout, all variants below use identical labels and cases:

| V2 holdout strategy | Recall@5 | nDCG@5 |
| --- | ---: | ---: |
| Structured | 0.5822 | 0.6948 |
| Domain Semantic V2 | 0.6133 | 0.7864 |
| Structured-first Hybrid | **0.9822** | **0.9961** |
| RRF V2 | **0.9822** | **0.9950** |

RRF V2 is the operational default because it preserves a structured-first candidate boundary, fuses incompatible score scales by rank, passed the ≥0.80 Recall@5 gate, and has an explicit structured fallback for query-embedding failure. Development tuned the weighting; holdout decided acceptance and was not used for subsequent tuning.

V1 set/report SHA-256: `feed37c73e09ddc4c3626d7ed28184492286042bd47332976145d8510293ccd4` / `ac0d5cf3c0b38f385adbb8950f34648572bb57e70ee94c28f67de729021f1881`. V2 holdout dataset content checksum: `37acf2e6aca722f5b1485af0efc966de86758ebedfba43488ebd3a596e217dbb`; holdout report file SHA-256: `f782a2dced4857457ade21d7ca941162b0c4e664bcb820b0b6f7ededc0a0ce14`.

Full method: [Retrieval quality V2](retrieval-quality-v2.md). Root-cause record: [RAG-02 acceptance](acceptance-rag-02.md). Architecture decision: [ADR 0008](adr/0008-retrieval-quality-v2.md).

## Test-data creation and separation

- Historical repairs are generated deterministically with explicit version, seed, UUIDv5 identities, and canonical SHA-256.
- ML splits by completion time, not random row order.
- LLM and retrieval have separate development and final holdout sets with overlap checks.
- Labels are generated/frozen before evaluation; audited samples are documented in the method reports.
- The final reports preserve case-level predictions/rankings, failures, timings, and aggregate metrics.

## Limits on conclusions

All data, descriptions, and relevance labels are synthetic and share controlled domain assumptions. The results validate reproducibility, architecture, contracts, and behavior on those artifacts. They cannot establish real-world accuracy, fairness, robustness to unseen workshop language, causal effects, commercial fitness, or production scale. A genuine external evaluation needs independently sampled and annotated real-workshop data under an approved data-governance process.
