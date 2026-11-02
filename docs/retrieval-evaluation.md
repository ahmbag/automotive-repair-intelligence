# ARI-RAG-01 – Retrieval-Evaluation

Stand der Messung: 27. Oktober 2026

## Eingefrorener Bestand

`ARI.RagTool prepare-evaluation` erzeugt deterministisch 40 nicht wortgleiche Suchanfragen aus
dem versionierten historischen Datensatz:

- 35 positive Anfragen, geschichtet über die Reparaturcodes;
- 5 No-Result-Fälle aus einer nicht vorhandenen Reparaturfamilie;
- pro positiver Anfrage fünf nach transparenten Fahrzeug-, Reparatur-, Zustands- und
  Laufleistungsregeln gelabelte Referenzfälle;
- keine Anfrage kopiert den historischen Abschlussbericht.

| Eigenschaft | Wert |
| --- | --- |
| Dataset-ID | `ari-rag-retrieval-evaluation-v1` |
| Auswahlversion | `ari-rag-eval-selection-v1` |
| Fälle | 40 |
| Positive / No-Result | 35 / 5 |
| Manuell kontrollierte Stichprobe | 10 Fälle |
| SHA-256 | `2b1e3e8a878b77618c9bda027f15f0e83cb21e58b1493b553d76227e79368f3c` |

Bei der manuellen Stichprobe wurden für alle zehn markierten Fälle jeweils fünf Referenzen und
die Übereinstimmung ihres Reparaturcodes mit der Anfrage bestätigt. Die Relevanzgrade selbst
sind jedoch deterministisch regelbasiert erzeugt. Sie sind nachvollziehbar und reproduzierbar,
aber noch kein unabhängiges Expertenurteil aus einer Werkstatt. Das ist eine bewusste MVP-Grenze.

Das erzeugte Set und der Einzelresultat-Report liegen unter `artifacts/rag/` und sind als
ableitbare Testartefakte ignoriert. Generator, Auswahlregel und Metrikcode bleiben versioniert.

## Metriken

- Recall@5 und Precision@5 sind das Mittel der positiven Einzelanfragen.
- MRR bewertet den Rang des ersten relevanten Treffers.
- nDCG@5 berücksichtigt Relevanzgrade 1 bis 3.
- No-Result Accuracy misst die fünf explizit negativen Anfragen.
- p50 und p95 umfassen die Online-Kette der jeweiligen Strategie; bei Semantic/Hybrid gehört
  die Query-Embedding-Latenz dazu.
- Fehlertypen und alle zurückgegebenen Auftrags-IDs werden pro Fall gespeichert.

## Reproduzierbare Ausführung

```powershell
dotnet run --project tools/ARI.RagTool -- prepare-evaluation `
  artifacts/ari-suspension-synthetic-v1.json `
  artifacts/rag/retrieval-evaluation-set.json

dotnet run --project tools/ARI.RagTool -- evaluate `
  artifacts/rag/retrieval-evaluation-set.json `
  artifacts/rag/retrieval-evaluation-report.json
```

## Reale Messwerte

Gemessen gegen den vollständigen 1.000-Dokumente-Index in SAP HANA Cloud. Jeder Versuch umfasste
40 Anfragen. Es trat bei keiner Strategie ein technischer Fehler auf.

| Strategie | Recall@5 | Precision@5 | MRR | nDCG@5 | No-Result | p50 | p95 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Structured | 0,6114 | 0,6114 | 0,9429 | 0,7935 | 1,0000 | 217 ms | 229 ms |
| Semantic | 0,0171 | 0,0171 | 0,0500 | 0,0302 | 1,0000 | 415 ms | 514 ms |
| Hybrid | 0,1600 | 0,1600 | 0,5381 | 0,1720 | 1,0000 | 430 ms | 538 ms |

Structured lieferte für alle 35 positiven Fälle fünf Treffer. Semantic und Hybrid lieferten bei
34 positiven Fällen fünf Treffer und bei einem positiven Fall keinen Treffer. Alle fünf negativen
Fälle wurden von jeder Strategie korrekt als No-Result erkannt.

## Bewertung und Fehleranalyse

Der Zielwert `Hybrid Recall@5 >= 0,80` wurde mit 0,1600 klar verfehlt. Das ist ein valides
Evaluationsergebnis und wurde nicht durch nachträgliches Umetikettieren oder Anpassen des
eingefrorenen Sets beschönigt.

Die Ursache ist in dieser ersten Datenrepräsentation gut erklärbar: Eingebettet wird nur der kurze
historische Abschlussbericht. Die Suchanfrage beschreibt dagegen überwiegend strukturierte
Merkmale wie Reparaturcode, Fahrzeug, Alter, Laufleistung und bekannte Vorbedingungen. Diese
Merkmale kommen im Bericht häufig gar nicht vor. Structured Retrieval kann genau die Signale
verwenden, auf denen auch die Ground Truth basiert, und gewinnt deshalb deutlich. Hybrid verbessert
die reine semantische Suche, die Vektorkomponente trägt in der aktuellen Repräsentation aber zu
wenig fachliches Signal.

Der nächste saubere Versuch ist eine neue, versionierte Embedding-Repräsentation, die
Reparaturcode, Fahrzeugmetadaten, bekannte Vorbedingungen und Berichtstext gemeinsam abbildet.
Dazu gehört ein neu eingefrorenes V2-Evaluationsset mit unabhängig von Retrievalregeln geprüften
Expertenlabels und eine anhand der Score-Verteilungen kalibrierte Mindestähnlichkeit. V1 bleibt
unverändert als reproduzierbare Baseline erhalten.

## Kosten der Abnahme

Der Provider meldete beim ersten Vollindexlauf keine dauerhaft gespeicherte Tokenzahl. Für die
80.902 Zeichen der 1.000 Berichte ergibt eine konservative Zeichenabschätzung von drei bis fünf
Zeichen pro Token etwa 16.181 bis 26.968 Input-Tokens. Die 80 semantischen Evaluationsanfragen
umfassten 11.800 Zeichen, also geschätzt 2.360 bis 3.934 Tokens. Beim für die Abnahme verwendeten
Preis von 0,02 USD je eine Million Tokens für `text-embedding-3-small` entspricht dies ungefähr:

| Vorgang | Geschätzte Kosten |
| --- | ---: |
| Vollindex | 0,000324–0,000539 USD |
| Retrieval-Evaluation | 0,000047–0,000079 USD |
| Summe Embeddings | unter 0,0007 USD |

Das sind technische Schätzwerte, keine Provider-Rechnung. Der zweite unveränderte Indexlauf rief
den Provider überhaupt nicht auf und verursachte daher keine zusätzlichen Embedding-Kosten.
