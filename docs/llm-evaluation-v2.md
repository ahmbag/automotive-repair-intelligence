# ARI-LLM-02 – Evaluation V2

Stand: 28. Oktober 2026

## Datentrennung und Ground Truth

| Bestand | Fälle | 0 / 1 / mehrere Komplikationen | Verwendung | SHA-256 |
| --- | ---: | ---: | --- | --- |
| `ari-llm-evaluation-v1` | 100 | 40 / 40 / 20 | unveränderte historische Referenz | `b59c6863…bbb0ff9` |
| `llm-quality-dev-v2` | 50 | 15 / 25 / 10 | V3-Entwicklung | `dd6557df…cc800` |
| `llm-quality-test-v2` | 60 | 20 / 25 / 15 | einmalige finale Abnahme | `51d92fc6…a146d` |

Die IDs überschneiden sich weder untereinander noch mit dem V1-Bestand. Ground Truth enthält nur
`DOCUMENT_SUPPORTED`-Komplikationen, die im tatsächlich übergebenen Bericht vorkommen. Pro neuem
Bestand wurden zehn über Null-, Einzel- und Mehrfachfälle verteilte Fälle zusätzlich auf Textbeleg
und Code geprüft. Strukturierte, aber nicht erwähnte Ereignisse sind `STRUCTURED_ONLY` und werden
nicht als erwartete Extraktion gezählt. Unklare oder negierte Aussagen sind keine positiven Labels.

Primärmetrik ist Multi-Label Micro-F1. Zusätzlich werden Macro-F1, Samples-F1, Precision, Recall,
TP/FP/FN je Klasse, Schema- und Reparaturcodegenauigkeit, Abdeckung, unbelegte Claims, Temporal
Leaks, Tokens und Latenz gemessen.

## Entwicklungsergebnis

| Version | Precision | Recall | Micro-F1 | Macro-F1 | Samples-F1 | Mittel | p95 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| V1 | 1,000 | 0,500 | 0,667 | 0,548 | 0,663 | 2.887 ms | 4.010 ms |
| V2 | 1,000 | 0,417 | 0,588 | 0,517 | 0,596 | 3.597 ms | 6.248 ms |
| V3 | 1,000 | 1,000 | 1,000 | 1,000 | 1,000 | 3.527 ms | 5.139 ms |

V3 wurde nach diesem Lauf eingefroren. Prompt, Klassifikator und Holdout-Labels wurden danach nicht
angepasst.

## Finale unabhängige Holdout-Evaluation

| Version | Precision | Recall | Micro-F1 | Macro-F1 | Samples-F1 | Schema | Repair-Code | Abdeckung |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| V1 | 1,000 | 0,526 | 0,690 | 0,624 | 0,711 | 100 % | 100 % | 100 % |
| V3 | 1,000 | 1,000 | **1,000** | **1,000** | **1,000** | 100 % | 100 % | 100 % |

V3 je Klasse:

| Code | TP | FP | FN | F1 |
| --- | ---: | ---: | ---: | ---: |
| `ALIGNMENT_CORRECTION` | 7 | 0 | 0 | 1,000 |
| `BROKEN_BOLT` | 8 | 0 | 0 | 1,000 |
| `DAMAGED_BUSHING_SEAT` | 15 | 0 | 0 | 1,000 |
| `SEIZED_FASTENER` | 27 | 0 | 0 | 1,000 |

Beide Holdout-Experimente hatten null Provider-/Schemafehler, null unbelegte Claims und null
Temporal Leaks. V3 benötigte im Mittel 3.785 ms (p95 5.737 ms), V1 2.772 ms (p95 4.362 ms).

## Tokenverbrauch und Kosten

| Lauf | Input | Output | Kosten bei 0,10/0,50 USD je MTok |
| --- | ---: | ---: | ---: |
| Dev V1 | 24.803 | 10.946 | 0,007953 USD |
| Dev V2 | 38.603 | 13.706 | 0,010713 USD |
| Dev V3 | 40.803 | 12.955 | 0,010558 USD |
| Holdout V1 | 29.792 | 12.549 | 0,009254 USD |
| Holdout V3 | 48.992 | 16.740 | 0,013269 USD |
| Gesamt | 182.993 | 66.896 | rund 0,051747 USD |

Die Preise entsprechen der zum Abnahmezeitpunkt geöffneten
[offiziellen OpenAI-Preisseite](https://developers.openai.com/api/docs/pricing) für Standard-
Verarbeitung mit `gpt-6-luna`. Die Werte sind aus Provider-Telemetrie berechnet, aber keine Rechnung.

## Grenzen

- Die Fälle sind synthetisch und stammen weiterhin aus derselben Generatorfamilie. Der Holdout ist
  auftragsseitig unabhängig, aber kein externer, sprachlich frei annotierter Werkstattkorpus.
- Der perfekte Holdout-Wert belegt die geschlossene MVP-Taxonomie, nicht allgemeine automobile
  Sprachkompetenz. Neue Formulierungen benötigen kontinuierliche Evaluation.
- Der deterministische V3-Klassifikator ist absichtlich konservativ. Unbekannte Ereignisse werden
  nicht in bestehende Codes gezwungen.
- Anbieterpreise und Latenzen können sich ändern.

Die lokale Evaluation folgt damit dem Grundgedanken der
[offiziellen OpenAI-Evaluationsleitlinien](https://developers.openai.com/api/docs/guides/evaluation-best-practices):
klare Kriterien, getrennte Datensätze, kontinuierliche Messung und keine Optimierung auf dem finalen
Testset. Strict Structured Outputs sichern das Format; die inhaltliche Qualität wird separat gemessen.
