# ARI-RAG-02 – Abnahmeprotokoll

Abnahmedatum: 29. Oktober 2026

## Evidenz und Versionen

| Gegenstand | Wert |
| --- | --- |
| V1 Dataset-Checksum | `2b1e3e8a878b77618c9bda027f15f0e83cb21e58b1493b553d76227e79368f3c` |
| V1 Set-Datei SHA-256 | `feed37c73e09ddc4c3626d7ed28184492286042bd47332976145d8510293ccd4` |
| V1 Report-Datei SHA-256 | `ac0d5cf3c0b38f385adbb8950f34648572bb57e70ee94c28f67de729021f1881` |
| Development-Checksum | `61d5d35f27794a154b0f618d25221a8fe245e75fe8596faea4e6f355ce4f6172` |
| Holdout-Checksum | `37acf2e6aca722f5b1485af0efc966de86758ebedfba43488ebd3a596e217dbb` |
| V1-Analyse-Datei SHA-256 | `543848d77a2ec9a405c78cdfda4627dae012aea6b2ac740aa14f28c784d28ddc` |
| Development-Report SHA-256 | `242f1c3a1a29063d63885a0da75f80e87934cb12592d6e481eeae90210b305b4` |
| Holdout-Report SHA-256 | `f782a2dced4857457ade21d7ca941162b0c4e664bcb820b0b6f7ededc0a0ce14` |
| Embedding | `text-embedding-3-small`, 1.536 Dimensionen |
| Profil / Dokument | `canonical-repair-search-profile-v2` / `2.0.0` |
| V2 Indexversion | `repair-search-embedding-v2` |

## V1 unverändert

| Strategie | Recall@5 | Precision@5 | MRR | nDCG@5 | No-Result |
| --- | ---: | ---: | ---: | ---: | ---: |
| Structured | 0,6114 | 0,6114 | 0,9429 | 0,7935 | 1,0000 |
| Semantic | 0,0171 | 0,0171 | 0,0500 | 0,0302 | 1,0000 |
| Hybrid | 0,1600 | 0,1600 | 0,5381 | 0,1720 | 1,0000 |

Die Originaldateien wurden nicht neu geschrieben. `retrieval-failure-analysis-v1.json` ist ein
separates, ableitbares Artefakt und enthält alle 40 Einzelanalysen.

## Reale V2-Messung

### Development (50 Fälle, davon 5 No-Result)

| Strategie | Recall@5 | Precision@5 | MRR | nDCG@5 | Candidate | No-Result | HANA p50/p95 | Voll p50/p95 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Structured V1 | 0,5911 | 0,5911 | 0,9296 | 0,7571 | 1,0000 | 1,0000 | 211/222 ms | 211/222 ms |
| Domain Semantic V2 | 0,6267 | 0,6267 | 0,9667 | 0,7955 | 1,0000 | 1,0000 | 324/330 ms | 504/620 ms |
| Structured-first Hybrid | 0,9867 | 0,9867 | 1,0000 | 0,9963 | 1,0000 | 1,0000 | 324/331 ms | 501/605 ms |
| RRF V2 | **0,9911** | **0,9911** | **1,0000** | **0,9968** | **1,0000** | **1,0000** | 323/328 ms | 504/583 ms |

RRF wurde anhand des Development-Splits ausgewählt und danach nicht mehr verändert.

### Unabhängiger Holdout (50 Fälle, davon 5 No-Result)

| Strategie | Recall@5 | Precision@5 | MRR | nDCG@5 | Candidate | No-Result | HANA p50/p95 | Voll p50/p95 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Structured V1 | 0,5822 | 0,5822 | 0,9037 | 0,6948 | 1,0000 | 1,0000 | 211/216 ms | 211/216 ms |
| Domain Semantic V2 | 0,6133 | 0,6133 | 0,9741 | 0,7864 | 1,0000 | 1,0000 | 323/330 ms | 501/612 ms |
| Structured-first Hybrid | 0,9822 | 0,9822 | 1,0000 | 0,9961 | 1,0000 | 1,0000 | 323/328 ms | 503/560 ms |
| **RRF V2 (freigegeben)** | **0,9822** | **0,9822** | **1,0000** | **0,9950** | **1,0000** | **1,0000** | 324/329 ms | 495/551 ms |

Das vorab festgelegte Gate `Recall@5 >= 0,80` ist mit 0,9822 klar bestanden. RRF verbessert
die Structured-Baseline um 0,4000 absolute Recall-Punkte. Alle Experimente liefen ohne technischen
Fehler und ohne Fallback.

## Index, Kosten und realer E2E

- Migration 004 wurde einmal angewendet; 001–003 wurden idempotent übersprungen.
- Erstlauf: 1.000 Fälle, 2.000 Dokumente, 2.000 neue Vektoren, 20 Provider-Batches,
  138.195 gemessene Input-Tokens.
- Zweitlauf: 2.000 unverändert, 0 Embeddings, 0 Provider-Batches.
- Bei 0,02 USD pro 1 Mio. Input-Tokens kostete der V2-Dokumentindex rechnerisch
  `138195 / 1000000 * 0,02 = 0,0027639 USD`. Query-Kosten liegen zusätzlich im deutlich
  unter-Cent-Bereich; Query-Tokenzahlen werden derzeit nicht dauerhaft aggregiert.
- HANA-Integration: 16/16 Tests mit `ARI_APP`, einschließlich 2.000 Zeilen, 1.000 Aufträgen,
  zwei Profiltypen, 1.536 Dimensionen und Temporal-Ausschluss.
- Secret-Scan: kein OpenAI-Schlüssel im Repository; Laufzeitgeheimnisse ausschließlich in
  .NET User Secrets beziehungsweise Prozessvariablen.
- E2E: Health Ready als `ARI_APP`; LLM V3 `ACCEPTED`; Ridge `ari-repair-duration-v1` mit
  2,487136 h; `RANK_FUSION_V2`/`REPAIR_PROFILE_V2`; fünf gültige `repair-order:`-Referenzen;
  null Temporal Violations; kein Fallback; fünf validierte Evidenzbeobachtungen.

## Formale Definition of Done

| # | Kriterium | Status / Nachweis |
| ---: | --- | --- |
| 1 | V1-Ergebnisse unverändert gesichert | Erfüllt: drei Prüfsummen und V1-Metriken oben. |
| 2 | Fehleranalyse aller 40 Fälle | Erfüllt: reales Einzelartefakt mit Top 5, Missing, Scores und Fehlerklassen. |
| 3 | Precision/Recall und Labels geprüft | Erfüllt; Formel und 5-von-5-Sonderfall dokumentiert. |
| 4 | Query/Document und Ranking untersucht | Erfüllt in `retrieval-quality-v2.md`. |
| 5 | Canonical Search Profile | Erfüllt, deterministisch und versioniert. |
| 6 | Profil/Evidenz getrennt | Erfüllt in Code, Migration und Tests. |
| 7 | HANA-Persistenz idempotent | Erfüllt: Migration 004 real angewendet, ältere Migrationen übersprungen. |
| 8 | 1.000 Fälle V2-indexiert | Erfüllt: 2.000 Vektoren, zwei Typen, 1.000 Aufträge, 1.536 Dimensionen. |
| 9 | Zweitlauf ohne Provideraufruf | Erfüllt: 2.000 unverändert, 0 Embeddings, 0 Batches. |
| 10 | Structured-first Candidates | Erfüllt, harte und weiche Signale dokumentiert. |
| 11 | Semantic und Fusion verglichen | Erfüllt auf identischem Dev und Holdout für vier Strategien. |
| 12 | Zeit, Quellen, Zugriff | Erfüllt: Candidate Recall 1,0, Temporal-Test, gültige Quellen, `ARI_APP`. |
| 13 | Dev und Holdout | Erfüllt: je 50 Fälle, getrennte Checksums. |
| 14 | Pflichtmetriken und Latenz | Erfüllt; alle Pflichtmetriken sowie HANA-/Voll-p50/p95 oben. |
| 15 | Recall-Gate | Erfüllt: RRF Holdout Recall@5 0,9822 >= 0,80; Gate geschlossen. |
| 16 | Keine Leaks / unbelegten Quellen | Erfüllt durch Profiltrennung, Quality Policy und Quellenvalidator. |
| 17 | Fallback / Fehler | Unit- und API-Tests erfüllt; HANA-Livefehler kontrolliert. |
| 18 | Realer E2E | Erfüllt: .NET–HANA–OpenAI–Python, fünf Quellen, Erklärung validiert. |
| 19 | Regressionen und Build | Erfüllt: Build 0/0, Core 18/18, API 58/58, HANA 16/16, Python 24/24. |
| 20 | Dokumentation | Erfüllt durch ADR, Architektur-, Qualitäts-, API- und Abnahmedokument. |

## Ergebnis

**20/20 Kriterien erfüllt. ARI-RAG-02 ist abgenommen.** `RANK_FUSION_V2` mit
`REPAIR_PROFILE_V2` ist die geprüfte Standardkonfiguration. V1 bleibt unverändert ausführbar.
