# ADR 0006: SAP HANA Vector Engine für RAG

- Status: Accepted
- Datum: 27. Oktober 2026

## Kontext

ARI speichert 1.000 relationale historische Reparaturfälle bereits in SAP HANA Cloud. Für
semantische Vergleichssuche werden versionierte Text-Embeddings benötigt.

## Entscheidung

Die Embeddings werden in einer separaten HANA-Tabelle als `REAL_VECTOR(1536)` gespeichert und
mittels exakter `COSINE_SIMILARITY` durchsucht. Retrieval verbindet die Vektoren in derselben SQL-
Abfrage mit den bestehenden relationalen Aufträgen, Fahrzeugen, Arbeitszeiten und Zeitstempeln.
OpenAI erzeugt Embeddings und strikt strukturierte Erklärungen; die Provider liegen hinter
Core-Ports.

Ein HNSW-Index wird für 1.000 Dokumente nicht eingeführt. Exakte Suche ist einfacher zu erklären,
liefert eine verlässliche Evaluationsbasis und vermeidet Approximation als zusätzliche Variable.

## Folgen

- keine zweite Vektordatenbank und kein weiterer Betriebsdienst;
- relationale und semantische Filter lassen sich in einer Abfrage kombinieren;
- Embeddings sind aus Quelltext, Hash, Modell und Migration reproduzierbar;
- ein Modell-/Dimensionswechsel erfordert eine neue Konfigurationsversion und Re-Indexierung;
- die aktuelle MVP-Tabelle besitzt noch keinen Tenant-Schlüssel. Vor Multi-Tenant-Betrieb muss
  Tenant-Isolation in Tabellen, Constraints, Abfragen und Autorisierung eingeführt werden.

