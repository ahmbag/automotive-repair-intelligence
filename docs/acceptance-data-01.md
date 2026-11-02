# ARI-DATA-01 – Abnahmeprotokoll

Status: **vollständig – 16/16 Punkte erfüllt**  
Abgenommen: 23. Oktober 2026 gegen die konfigurierte SAP-HANA-Cloud-Instanz

| # | Formale Definition of Done | Status | Nachweis |
| ---: | --- | :---: | --- |
| 1 | Domänenobjekte fachlich vervollständigt | Erfüllt | Start-/Abschluss-, Ereignis- und Berichtsfelder; validiertes `HistoricalRepairCase` |
| 2 | Migration 002 mit `ARI_DEPLOY` erfolgreich und wiederholbar | Erfüllt | erster Lauf `Applied`, Wiederholung über `SCHEMA_MIGRATIONS` erkannt |
| 3 | Positionen, Ausführungen, Komplikationen persistierbar | Erfüllt | 1.000/1.000 vollständige Fälle in den neuen HANA-Tabellen |
| 4 | Vorabmerkmale und spätere Erkenntnisse getrennt | Erfüllt | Conditions vor Start, Complications während Ausführung; Core- und DB-Zeitregeln |
| 5 | Generator mit Seed und Version | Erfüllt | Python-CLI, Seed 20261023, Version 1.0.0, UUIDv5 und SHA-256 |
| 6 | 500–1.000 Fälle reproduzierbar | Erfüllt | 1.000 Fälle; deterministischer Test und stabile Prüfsumme |
| 7 | dokumentierte statistische Regeln | Erfüllt | Alter/Laufleistung/Umwelt/Korrosion/Ereignisse/Reststreuung dokumentiert und getestet |
| 8 | strukturierte Ereignisse und Textberichte | Erfüllt | 634 Ereignisse; Bericht für jeden der 1.000 Fälle |
| 9 | Import über .NET-Persistenz | Erfüllt | Python schreibt nur JSON; .NET importiert als `ARI_APP` |
| 10 | Wiederholung ohne Duplikate | Erfüllt | Zweitlauf: 0 importiert, 1.000 übersprungen, bereits vollständig |
| 11 | Transaktions-/Fehlerverhalten geprüft | Erfüllt | echter HANA-Test erzwingt Fehler im zweiten Fall und beweist Batch-Rollback |
| 12 | Datenqualität und Referenzen bestanden | Erfüllt | 0 Waisen, 0 Arbeits-, Fahrzeug- oder Zeitfehler |
| 13 | statistische Auswertung dokumentiert | Erfüllt | Mittelwert, Median, P90, Abweichungen und Ereignishäufigkeiten aus HANA |
| 14 | ML-Features/Label mit Leakage-Schutz | Erfüllt | erlaubte Featureliste, Outcome-Sperrliste, negative Tests, zeitbasierter Split |
| 15 | .NET-, Python- und HANA-Regressionen | Erfüllt | finaler Testnachweis unten |
| 16 | Generator, Modell, Tests, Einschränkungen dokumentiert | Erfüllt | fünf Lernkapitel, ADR 0004, Beispieldataset und dieses Protokoll |

## Identität des abgenommenen Datasets

```text
Dataset-ID:         ari-suspension-synthetic-v1
Generatorversion:  1.0.0
Seed:               20261023
Records:            1000
Schemaversion:      2
Klassifikation:     SYNTHETIC
SHA-256:            960588c090ad9092dc98f59721e08ad8df3718d899012ace0b344fb238ff1384
```

## Live-Ergebnisse

```text
Migration 002: applied successfully
First import:  imported=1000, skipped=0, alreadyComplete=false
Repeat import: imported=0,    skipped=1000, alreadyComplete=true

Data quality:
expected/imported/orders/complete = 1000/1000/1000/1000
orphans/invalid labor/invalid vehicle/invalid timeline = 0/0/0/0
missing reports = 0
validation errors = 0
```

## Finale Testbilanz

```text
.NET build:             0 Warnungen, 0 Fehler
Core unit tests:        6/6 bestanden
API/Infrastructure:     8/8 bestanden
Python tests:          12/12 bestanden
HANA integration:      10/10 bestanden
Data acceptance:        1/1 bestanden
Format verification:    bestanden
```

## Testbefehle

```powershell
dotnet build AutomotiveRepairIntelligence.slnx --no-restore
dotnet test tests/ARI.Core.Tests --no-build
dotnet test tests/ARI.Api.Tests --no-build

$env:PYTHONPATH = (Resolve-Path "ml/src").Path
python -m unittest discover -s ml/tests -v

dotnet test tests/ARI.Infrastructure.IntegrationTests `
  --no-build --filter "Category=HanaIntegration"

$env:ARI_DATASET_ID = "ari-suspension-synthetic-v1"
dotnet test tests/ARI.Infrastructure.IntegrationTests `
  --no-build --filter "Category=DataAcceptance"
```

Zugangsdaten wurden für Migration und Tests nur temporär im Prozess gehalten und anschließend
entfernt. Der vollständige generierte Datensatz liegt nicht im Repository.

## Bewusste Grenze

Der Datensatz beweist Softwarequalität, Reproduzierbarkeit und das Lernen simulierter Beziehungen.
Er beweist keine reale Werkstattprognose. Trainierter Predictor, RAG, Vector Engine, PAL, MLflow und
Benutzeroberfläche bleiben außerhalb von ARI-DATA-01.
