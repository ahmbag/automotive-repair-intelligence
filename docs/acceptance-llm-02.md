# ARI-LLM-02 – Abnahmeprotokoll

Stand: 28. Oktober 2026  
Ergebnis: **17/17 erfüllt; Qualitätsgate Micro-F1 ≥ 0,85 bestanden**

| # | Definition of Done | Status / Evidenz |
| ---: | --- | --- |
| 1 | V1-/V2-Fehlerfälle einschließlich FP/FN analysiert | **Erledigt** – 0 FP; V1 46 TP/47 FN, V2 28 TP/65 FN; Status- und Fehlerklassen dokumentiert. |
| 2 | Precision, Recall und F1 pro Komplikationsklasse | **Erledigt** – TP/FP/FN und Klassenmetriken für V1/V2 sowie V3-Holdout dokumentiert. |
| 3 | Ursache der V2-Verschlechterung bestimmt | **Erledigt** – restriktive Mappingregel erhöht `NEEDS_REVIEW` von 5 auf 42 und verliert insbesondere alle 24 `DAMAGED_BUSHING_SEAT`-Fälle. |
| 4 | Ground Truth gegen Berichtstexte abgeglichen | **Erledigt** – nur `DOCUMENT_SUPPORTED`-Labels; strukturierte, nicht erwähnte Ereignisse zählen nicht; je Split zehn geschichtete Fälle zusätzlich geprüft. |
| 5 | V1 unverändert; separates Development-/Testset | **Erledigt** – 100 V1-, 50 Dev- und 60 Holdout-Fälle ohne ID-Überschneidung und mit SHA-256. |
| 6 | Eindeutige Komplikationstaxonomie | **Erledigt** – positive/negative Grenzen, Kombination, Negation und Zeitbezug für alle vier Codes dokumentiert. |
| 7 | V3 implementiert und versioniert | **Erledigt** – `repair-extraction-v3`, Promptdokument und deterministische zweite Klassifikationsstufe. |
| 8 | Änderungen durch Fehlertypen begründet | **Erledigt** – V3 adressiert indirekte Formulierungen, Ursache/Folge und Negation; keine allgemeine Promptverlängerung. |
| 9 | Schema-, Domain-, Evidence-, Temporal-Validatoren wirksam | **Erledigt** – Strict Structured Output plus unveränderte deterministische Validatoren; Negativtests grün. |
| 10 | Unsichere Extraktionen gekennzeichnet | **Erledigt** – Anwendung vergibt `ACCEPTED`, `PARTIAL`, `NEEDS_REVIEW`, `REJECTED`; nur `ACCEPTED` darf ML-Features liefern. |
| 11 | Entwicklungsmetriken, Kosten, Latenz | **Erledigt** – V1/V2/V3 auf denselben 50 Fällen; V3 F1 1,000, Mittel 3.527 ms, p95 5.139 ms, Tokens/Kosten dokumentiert. |
| 12 | Finale echte LLM-Evaluation auf Holdout | **Erledigt** – 120 echte OpenAI-Aufrufe auf 60 zuvor ungenutzten Fällen, V1 und eingefrorenes V3. |
| 13 | F1 mindestens 0,85 oder offen | **Erledigt/bestanden** – V3 Holdout Micro-F1 **1,000**, Ziel 0,85 überschritten. |
| 14 | Keine unbelegten Claims oder Temporal Leaks | **Erledigt** – jeweils 0 in Dev und Holdout; Evidence bleibt wörtlicher Originalsubstring. |
| 15 | Live-E2E OpenAI, .NET, Python ML | **Erledigt** – HTTP 200, V3 `ACCEPTED`, Ridge-Prognose 2,487136 h; mehrdeutiger Fall kontrolliert HTTP 422. |
| 16 | Regressionen und Solution-Build | **Erledigt** – Build 0/0, Core 18/18, API 53/53, HANA/Infrastructure 14/14 und Python 24/24. |
| 17 | Auswertung, Architektur, Grenzen und Abnahme dokumentiert | **Erledigt** – Analyse, Taxonomie, Evaluation, Prompt, ADR, API-Regeln und dieses Protokoll. |

## Finale Qualitätswerte

| Metrik | V1 Holdout | V3 Holdout | Ziel |
| --- | ---: | ---: | ---: |
| Precision | 1,000 | 1,000 | beobachten |
| Recall | 0,526 | 1,000 | beobachten |
| Micro-F1 | 0,690 | **1,000** | ≥ 0,85 |
| Macro-F1 | 0,624 | **1,000** | dokumentieren |
| Samples-F1 | 0,711 | **1,000** | dokumentieren |
| Schema / Repair-Code | 100 % / 100 % | 100 % / 100 % | keine Verschlechterung |
| Abdeckung | 100 % | 100 % | ausweisen |
| Unsupported / Temporal | 0 / 0 | 0 / 0 | 0 / 0 |
| Mittel / p95 | 2.772 / 4.362 ms | 3.785 / 5.737 ms | dokumentieren |

## Live-E2E

```text
ASP.NET Core -> gpt-6-luna -> strict JSON schema -> V3 classification
                 -> domain/evidence/temporal validation -> quality policy
                 -> FastAPI -> ari-repair-duration-v1
```

Der positive Vorabfall ergab `ACCEPTED`, `VISIBLE_CORROSION`, keine beobachtete Komplikation,
keine Istzeit und eine Ridge-Prognose von 2,48713637003138 Stunden. Ein vollständiger, aber
mehrdeutiger Hinweis („Maybe a bolt is broken … not confirmed“) ergab HTTP 422
`ManualMappingRequired`; er erreichte das ML-Modell nicht.

## Verifikation

Aktueller Verifikationsstand:

- Build: erfolgreich, 0 Warnungen, 0 Fehler;
- Core: 18/18 bestanden;
- API/Adapter/Workflow: 53/53 bestanden;
- Python: 24/24 bestanden; ein nicht blockierender Starlette-Deprecation-Hinweis;
- HANA/Infrastructure: 14/14 am 28.10. gegen die gestartete Cloud-Instanz bestanden;
- Secret-Scan: keine bekannten Zugangsdaten- oder OpenAI-Key-Muster in Projektdateien;
- Repository-Hygiene: `git diff --check` ohne Befund.

## Einschränkung

Der Wert 1,000 gilt für den unabhängigen, aber weiterhin synthetischen Holdout aus derselben
Generatorfamilie. Er darf nicht als Beleg für perfekte Leistung auf freien realen Werkstatttexten
verstanden werden. Diese Grenze bleibt offen für einen späteren extern annotierten Korpus.
