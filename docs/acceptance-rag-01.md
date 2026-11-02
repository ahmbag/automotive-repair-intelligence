# ARI-RAG-01 – Abnahmeprotokoll

Stand: 27. Oktober 2026  
Ergebnis: **20/20 Kriterien umgesetzt und nachgewiesen**

| # | Definition of Done | Status / Evidenz |
| ---: | --- | --- |
| 1 | HANA Vector Engine mit realem SQL-Smoke-Test | **Erledigt** – reale Cosine-Identität `1,000000`, `CARDINALITY=3`. |
| 2 | Migration 003 mit ARI_DEPLOY angewendet und idempotent geprüft | **Erledigt** – erster Lauf `Applied: 003_repair_report_embeddings`, zweiter Lauf `Already applied`. |
| 3 | Embedding-Speicherung und Datenzugriff über ARI_APP | **Erledigt** – authentifizierter Store/Search/Quellen-Join/Cleanup-Test bestanden. |
| 4 | Embedding-Provider über ARI.Infrastructure integriert | **Erledigt** – OpenAI-Adapter liegt ausschließlich in Infrastructure; echter Smoke-Test mit `text-embedding-3-small`, 1.536 Dimensionen und fünf Input-Tokens. |
| 5 | Embedding-Modell, Dimension und Quelltext-Hash versioniert | **Erledigt** – Modell, `repair-report-embedding-v1`, 1.536 Dimensionen und SHA-256 werden gespeichert und beim Retrieval geprüft. |
| 6 | 1.000 historische Berichte reproduzierbar vektorisiert | **Erledigt** – HANA enthält 1.000 Zeilen für 1.000 verschiedene Aufträge; alle Vektoren haben 1.536 Dimensionen. |
| 7 | Wiederholte Indexierung ohne Duplikate oder unnötige Provideraufrufe | **Erledigt** – zweiter Vollauf: `Sources: 1000`, `Embedded: 0`, `Unchanged: 1000`, `Provider batches: 0`. |
| 8 | Semantische Top-K-Suche in SAP HANA | **Erledigt** – reale 1.536-dimensionale Suche mit Self-Match, Scores, Metadaten und Quellen-Join erfolgreich. |
| 9 | Relationale Filter und historischer Verfügbarkeitszeitpunkt | **Erledigt** – Reparaturcode, Fahrzeug, Alter, Laufleistung, bekannte Bedingung und `SOURCE_AVAILABLE_AT_UTC <= AsOfUtc` berücksichtigt. |
| 10 | Structured, Semantic und Hybrid Retrieval | **Erledigt** – drei explizite Strategien und getrennte Rankingregeln implementiert und real vermessen. |
| 11 | Ground-Truth-Evaluationsfälle erstellt und geprüft | **Erledigt** – 40 eingefrorene Fälle, davon 35 positiv und 5 No-Result; 10-Fall-Stichprobe geprüft; SHA `2b1e3e8a…79368f3c`. |
| 12 | Recall@5, Precision@5, MRR und Laufzeiten gemessen | **Erledigt** – reale 40×3-Evaluation einschließlich nDCG@5, No-Result Accuracy, p50/p95 und Einzelresultaten. |
| 13 | Retrieval-Vergleich und Fehleranalyse dokumentiert | **Erledigt** – Structured ist mit Recall@5 0,6114 bestes Verfahren; Hybrid erreicht 0,1600 und verfehlt den Zielwert 0,80. Ursache und V2-Maßnahmen sind dokumentiert. |
| 14 | LLM-Erklärungen ausschließlich aus echten Retrieval-Quellen | **Erledigt** – echter Live-E2E lieferte vier Beobachtungen aus fünf realen HANA-Treffern; kein ungegroundeter Freitext wird akzeptiert. |
| 15 | Alle referenzierten Quellen-IDs überprüfbar | **Erledigt** – ID-Allowlist plus Prüfung, dass jedes Evidence-Zitat wörtlich im referenzierten Quellausschnitt vorkommt. |
| 16 | Keine historischen Abschlussinformationen in neuen ML-Features | **Erledigt** – nur validierte `BEFORE_REPAIR`-Bedingungen gehen an ML/Retrieval; beobachtete Komplikationen und Ist-Arbeitszeit sind ausgeschlossen und getestet. |
| 17 | LLM-/Embedding-/HANA-Ausfälle kontrolliert behandelt | **Erledigt** – Timeout, begrenzter Retry, falsche Dimension, Modelltrennung, 503, fehlende Quelle, erfundene ID, Prompt Injection, temporale Grenze und Explanation-Fallback getestet. |
| 18 | Vollständiger Live-E2E über .NET, HANA, ML und LLM | **Erledigt** – `POST /api/estimates/with-evidence` antwortete HTTP 200 mit Extraktion, Ridge-Prognose 2,4871 h, fünf HANA-Quellen und vier validierten LLM-Beobachtungen. |
| 19 | Bestehende Core-, API-, HANA- und Python-Regressionen grün | **Erledigt** – finaler Lauf siehe Abschnitt „Verifikation“. |
| 20 | Architektur, Evaluationsmetriken, Kosten und Grenzen dokumentiert | **Erledigt** – Architektur-, Embedding-, Grounding-, ADR- und Evaluationsdokumente enthalten reale Resultate, Kostenschätzung und MVP-Grenzen. |

## Retrieval-Ergebnis

| Strategie | Recall@5 | Precision@5 | MRR | nDCG@5 | No-Result | p50 | p95 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Structured | 0,6114 | 0,6114 | 0,9429 | 0,7935 | 1,0000 | 217 ms | 229 ms |
| Semantic | 0,0171 | 0,0171 | 0,0500 | 0,0302 | 1,0000 | 415 ms | 514 ms |
| Hybrid | 0,1600 | 0,1600 | 0,5381 | 0,1720 | 1,0000 | 430 ms | 538 ms |

Der vorgeschlagene Entwicklungszielwert `Hybrid Recall@5 >= 0,80` ist **nicht erreicht**. Die
Definition of Done verlangt den gemessenen Vergleich und die Dokumentation der Ursache, nicht das
Erzwingen eines bestimmten Ergebnisses. Der eingefrorene Datensatz wurde deshalb nicht nachträglich
optimiert. Die kurze Bericht-Only-Repräsentation enthält zu wenig der strukturierten Merkmale aus
den Suchanfragen. Eine angereicherte, neu versionierte Repräsentation ist als nächste Iteration
dokumentiert.

## Live-E2E-Nachweis

Der echte Request verwendete einen Volkswagen Golf VII, Baujahr 2014, 215.000 km,
Querlenker vorne links, sichtbare Korrosion und 1,8 h Referenzzeit. Ergebnis:

- LLM-Extraktion: korrekter Reparaturcode und `VISIBLE_CORROSION` als `BEFORE_REPAIR`;
- Ridge-Prognose: 2,4871363700313847 h, Modell `ari-repair-duration-v1`;
- Hybrid Retrieval: fünf echte HANA-Quellen mit überprüfbaren `repair-order:{guid}`-Referenzen;
- Grounded Generation: vier Beobachtungen, jede ID zulässig und jedes Zitat wörtlich belegt;
- HTTP 200, kein Fallback-Warnhinweis.

Die Extraktion benötigte 756 Input- und 140 Output-Tokens, die Erklärung 734 Input- und
788 Output-Tokens. Beim für die Abnahme dokumentierten Modellpreis ergibt das rund 0,000613 USD
für diese beiden LLM-Aufrufe. Zusammen mit den konservativ geschätzten Embedding-Kosten lag die
Live-Demonstration unter etwa 0,0014 USD. Dies ist eine Kostenschätzung, keine Rechnung.

## Verifikation

Die hier eingetragenen Zähler stammen aus dem abschließenden Lauf nach Fertigstellung der
Dokumentation:

- .NET Build: erfolgreich, 0 Warnungen, 0 Fehler;
- Core: 15/15 bestanden;
- API: 35/35 bestanden;
- HANA Integration: 14/14 bestanden, davon 13/13 echte `HanaIntegration`-Tests;
- Python: 24/24 bestanden; lediglich ein nicht blockierender Deprecation-Hinweis aus Starlette;
- Secret-Scan: keine bekannten Zugangsdaten oder OpenAI-Key-Muster in Projektdateien gefunden.

## Bewusste Grenzen

- synthetischer Einzelmandanten-Datensatz;
- ein kurzer Abschlussbericht je Auftrag und exakte Vektorsuche statt HNSW;
- regelbasierte Ground Truth mit begrenzter manueller Stichprobe statt unabhängiger Expertenlabels;
- keine Behauptung, dass historische Evidenz die Ridge-Prognose mathematisch verursacht;
- keine automatische Übernahme historischer Komplikationen auf einen neuen Auftrag;
- Providerkosten und Antwortzeiten sind umgebungs- und preisabhängig.
