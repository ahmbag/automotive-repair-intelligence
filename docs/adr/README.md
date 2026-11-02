# Architecture decision record index

This index consolidates the accepted decisions used by `ARI-RC1`; it does not replace or rewrite their original records.

| Decision | Record | Candidate consequence |
| --- | --- | --- |
| Keep Core independent and Python ML separate | [ADR 0001](0001-architecture.md) | `ARI.Core` has no SAP/OpenAI/Python dependency; FastAPI has an explicit REST boundary |
| Centralize TLS-validated HANA access | [ADR 0002](0002-hana-connectivity.md) | HANA provider and credentials remain in Infrastructure |
| Persist repair aggregates transactionally | [ADR 0003](0003-hana-persistence.md) | forward-only schema and atomic writes |
| Preserve temporal knowledge boundaries in synthetic history | [ADR 0004](0004-historical-synthetic-data.md) | pre-repair conditions are separate from later outcomes |
| Use a versioned, leakage-safe ML lifecycle | [ADR 0005](0005-ml-model-lifecycle.md) | chronological 70/15/15 split, offline training, immutable model metadata |
| Use SAP HANA as the vector store | [ADR 0006](0006-rag-hana-vector.md) | relational filters and exact cosine retrieval remain in one system |
| Add deterministic second-stage LLM classification | [ADR 0007](0007-llm-quality-v3.md) | evidence-backed V3 closes synthetic-taxonomy recall without lowering precision |
| Use structured-first retrieval and RRF V2 | [ADR 0008](0008-retrieval-quality-v2.md) | pre-repair profiles drive candidates; completion text is evidence, not a search leak |

The current component and sequence diagrams are in [Architecture](../architecture.md), evaluation consequences in [Evaluation evidence](../evaluation-evidence.md), and the precise candidate boundary in [Release scope and audit](../release-scope-and-audit.md).

