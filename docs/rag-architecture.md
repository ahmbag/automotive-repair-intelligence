# ARI-RAG-01 – Architektur

ARI-RAG-01 ergänzt die bestehende Prognose um historische Belege. Die Verantwortlichkeiten
bleiben strikt getrennt:

```text
Vorab-Beschreibung
  -> LLM-Extraktion und Domain Validation
  -> Python/Ridge: einzige Quelle der numerischen Prognose
  -> OpenAI Embedding: Query-Vektor
  -> SAP HANA: zeitlich gefilterte Structured/Semantic/Hybrid-Suche
  -> OpenAI Structured Output: belegte Erklärung
  -> Quellenvalidierung gegen das Retrieval-Ergebnis
```

`ARI.Core` enthält nur providerneutrale Verträge und Ports. OpenAI-HTTP, HANA SQL,
`REAL_VECTOR` und FastAPI liegen in `ARI.Infrastructure`. Die API orchestriert diese Ports, ohne
Providerantworten als Fachwahrheit zu behandeln.

## Datenflüsse

### Historische Indexierung

1. Der versionierte JSON-Reader prüft den SHA-256 des synthetischen Quelldatensatzes.
2. Pro abgeschlossenem Auftrag wird der kurze Reparaturbericht als ein Chunk normalisiert.
3. SHA-256, Modell und Konfigurationsversion werden mit dem vorhandenen Index verglichen.
4. Nur neue oder geänderte Quellen werden in Batches eingebettet.
5. Ein Batch wird transaktional in `ARI.REPAIR_REPORT_EMBEDDINGS` ersetzt.

Die relationale HANA-Quelle bleibt maßgeblich. Der Vector Store enthält keine zweite unkontrollierte
Kopie von Fahrzeug-, Arbeitszeit- oder Komplikationsdaten; Retrieval verbindet den Vektor immer
mit dem existierenden Auftrag, Fahrzeug, Abschlussbericht und Dataset.

### Online-Retrieval

| Strategie | Verhalten |
| --- | --- |
| `STRUCTURED` | gleicher Reparaturcode; Modell, Hersteller, Alter und Laufleistung bilden transparente Ranking-Signale |
| `SEMANTIC` | exakte `COSINE_SIMILARITY` über alle kompatiblen, zum Stichtag verfügbaren Vektoren |
| `HYBRID` | harter Reparaturcode- und Zeitfilter; 75 % Vektorähnlichkeit plus transparente fachliche Signale |

Jede Suche filtert exakt auf Embedding-Modell und Konfigurationsversion. Vektoren verschiedener
Dimension oder Modellversion werden nie verglichen. `SOURCE_AVAILABLE_AT_UTC <= AsOfUtc` verhindert
die Verwendung von Wissen, das zum Prognosezeitpunkt noch nicht vorlag.

## API

- `POST /api/repair-cases/search`: Retrieval ohne Generierung.
- `POST /api/repair-cases/explain`: Retrieval plus belegte Structured-Output-Erklärung.
- `POST /api/estimates/with-evidence`: Extraktion, Validierung, Ridge, Retrieval und Erklärung.

Fällt die Erklärung aus, bleiben die bereits erzeugte ML-Prognose und das Retrieval-Ergebnis
erhalten. Fällt HANA oder der Embedding-Provider aus, wird kontrolliert HTTP 503 geliefert.

## Bewusste Grenzen

- ein Chunk pro kurzem Werkstattbericht;
- exakte Suche statt HNSW bei nur 1.000 Dokumenten;
- synthetische, nicht mandantenfähige MVP-Daten;
- kein RAG-Training und keine Veränderung des Ridge-Modells;
- historische Ereignisse sind Vergleichsevidenz, keine Tatsachen über den neuen Auftrag.

## Qualitätsprofil V2

ARI-RAG-02 ergänzt V1, ersetzt es aber nicht. `PRE_REPAIR_PROFILE` wird ausschließlich aus
vorab verfügbaren relationalen Fakten erzeugt und dient der Kandidatensuche.
`COMPLETION_EVIDENCE` bleibt eine getrennte, erst nach der Auswahl geladene Evidenzperspektive.
Die neue Tabelle `ARI.REPAIR_SEARCH_EMBEDDINGS_V2` besitzt eigene Modell-, Profil- und
Dokumentversionen. Dadurch können V1 und V2 reproduzierbar nebeneinander ausgeführt werden.

Der V2-Onlinepfad lautet: harter Reparatur-/Zeit-/Zugriffsfilter, strukturierte Kandidaten,
Cosine-Ranking über das Vorabprofil, konservatives Hybrid- oder RRF-Re-Ranking, relationale
Abschlussbelege. HANA-Latenz und vollständige Provider-Latenz werden getrennt gemessen. Details,
Messwerte und Qualitätsgate stehen in `retrieval-quality-v2.md` und `acceptance-rag-02.md`.

## Nachgewiesener Live-Datenfluss

Der End-to-End-Test am 27. Oktober 2026 durchlief die reale Kette über
`POST /api/estimates/with-evidence`: OpenAI-Extraktion, Domain Validation, Python/Ridge-Inferenz,
OpenAI-Query-Embedding, Hybrid Retrieval aus SAP HANA Cloud und OpenAI-Structured-Output mit
anschließender Quellenprüfung. Die API antwortete HTTP 200 mit einer Prognose von 2,4871 Stunden,
fünf historischen HANA-Quellen und vier vollständig validierten Beobachtungen.

Keine beobachtete historische Komplikation und keine Ist-Arbeitszeit wurde dabei als Eingabe des
neuen ML-Falls verwendet. Ein Ausfall der abschließenden Generierung verändert weder die bereits
berechnete Prognose noch die gefundenen Quellen, sondern wird als separater Warnhinweis ausgegeben.
