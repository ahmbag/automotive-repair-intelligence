# Historische Daten nach SAP HANA importieren

## Datenfluss

```text
Python-Generator
    │  versioniertes JSON + Manifest + SHA-256
    ▼
.NET JSON Reader
    │  Prüfsumme, Mapping, fachliche Validierung
    ▼
Historical Import Service
    │  100 Fälle je Transaktion, deterministische IDs, Resume
    ▼
SAP HANA Cloud
```

Python schreibt nie direkt in HANA. Dadurch gelten dieselben .NET-Domänenregeln,
Verbindungsrichtlinien, Parameterregeln und Transaktionsgrenzen wie für die Anwendung.

## Manifest und Unveränderlichkeit

`ARI.HISTORICAL_DATASETS` speichert Dataset-ID, Generatorversion, Seed, erwartete/übernommene Anzahl,
Klassifikation, Schemaversion, Prüfsumme und Status. Existiert dieselbe Dataset-ID mit anderen
unveränderlichen Metadaten, wird der Import abgelehnt. Eine abgeschlossene, identische Dataset-ID
wird sofort als vollständig erkannt.

Abnahmenachweis:

```text
Erstimport:   imported=1000, skipped=0, alreadyComplete=false
Zweitimport:  imported=0,    skipped=1000, alreadyComplete=true
```

## Transaktionen und Wiederaufnahme

Die Standardbatchgröße ist 100 und kann beim CLI-Aufruf verändert werden. Für jeden Batch:

1. Verbindung öffnen und Transaktion beginnen.
2. Für jeden noch unbekannten Fall Fahrzeug, Auftrag, Bedingungen, Positionen, Ausführung und
   Komplikationen schreiben.
3. Manifest-Zähler in derselben Transaktion erhöhen.
4. Gesamten Batch committen.
5. Bei jedem Fehler den gesamten Batch zurückrollen.

Dadurch existiert nie ein halber Reparaturfall. Ein Prozessabbruch nach früheren erfolgreichen
Batches lässt das Dataset im Status `IMPORTING`. Der nächste Lauf erkennt deterministische
Auftrags-IDs, überspringt vollständige Fälle und setzt fort. Erst wenn der Manifest-Zähler exakt der
erwarteten Anzahl entspricht, wechselt der Status auf `COMPLETE`.

Ein echter negativer HANA-Test erzeugt im zweiten Fall eines Batches absichtlich eine doppelte
Operations-ID. HANA verwirft den Insert; der Test weist anschließend nach, dass auch Fahrzeug und
Auftrag des ersten Falls nicht existieren. Damit ist der Rollback nicht nur gemockt, sondern in der
realen Datenbank bewiesen.

## Sicherheit

- Migrationen laufen ausschließlich mit `ARI_DEPLOY`.
- Import und Profiling laufen mit `ARI_APP`.
- Kennwörter werden weder in JSON noch in Argumenten, Dateien oder Logs gespeichert.
- SQL-Werte werden vollständig parametrisiert.
- Die JSON-Prüfsumme verhindert einen unbemerkten Inhaltswechsel.

## Ausführung

Nach Konfiguration der `Hana__*`-Umgebungsvariablen:

```powershell
dotnet run --project tools/ARI.HistoricalDataImporter -- `
  artifacts/ari-suspension-synthetic-v1.json 100
```

Das Tool meldet künftig jeden bestätigten Batch mit processed/imported/skipped-Zählern.

## Gemessene Performance und Grenze

Der echte Erstimport der 1.000 Fälle dauerte ungefähr 19 Minuten. Die Batchgröße steuert bereits die
Transaktionsgrenze, die einzelnen Inserts bleiben aber Netzwerk-Roundtrips. Das ist für das
einmalige Sprint-01-Dataset korrekt und ausreichend, jedoch keine Bulk-Import-Architektur.
`HanaBulkCopy`, wiederverwendete vorbereitete Statements oder staging-basierte Mengenoperationen
sind sinnvolle spätere Optimierungen. Sie wurden nicht vorgetäuscht und sind für ARI-DATA-01 nicht
erforderlich.
