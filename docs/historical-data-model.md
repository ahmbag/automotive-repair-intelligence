# Historisches Reparaturdatenmodell

## Warum ein historischer Fall mehr als ein Auftrag ist

Ein `RepairOrder` beschreibt nur die Ausgangssituation: welches Fahrzeug, welche geplante Reparatur
und was zu diesem Zeitpunkt bekannt war. Für Lernverfahren benötigen wir zusätzlich den geplanten
Referenzaufwand und das später beobachtete Ergebnis. ARI fasst diese Bestandteile im
`HistoricalRepairCase` zusammen, ohne ihre Wissenszeitpunkte zu vermischen.

```text
VehicleSnapshot ──1:1── RepairOrder ──1:n── RepairOperation
                         │     │
                         │     └──1:n── KnownRepairCondition   (vor Beginn bekannt)
                         │
                         └──1:1── RepairExecution ──1:n── RepairComplication
                                 (Ergebnis)             (während der Arbeit erkannt)
```

`HistoricalRepairCase` ist eine Aggregatansicht über bestehende Fachobjekte. Es ersetzt weder
`VehicleSnapshot` noch `RepairOrder`, sondern validiert ihre gemeinsame fachliche Konsistenz.

## Entitäten und Tabellen

| Fachobjekt | Tabelle | Bedeutung |
| --- | --- | --- |
| Dataset-Manifest | `ARI.HISTORICAL_DATASETS` | Herkunft, Seed, Version, Prüfsumme und Importfortschritt |
| `VehicleSnapshot` | `ARI.VEHICLE_SNAPSHOTS` | unveränderlicher Fahrzeugzustand am Erfassungszeitpunkt |
| `RepairOrder` | `ARI.REPAIR_ORDERS` | Auftrag und bekannte Ausgangssituation; optionale Dataset-Zuordnung |
| `RepairOperation` | `ARI.REPAIR_OPERATIONS` | geplante Position und positive Referenzarbeitszeit |
| `KnownRepairCondition` | `ARI.REPAIR_CONDITIONS` | strukturiertes, vor Beginn dokumentiertes Merkmal |
| `RepairExecution` | `ARI.REPAIR_EXECUTIONS` | Start, Abschluss, Ist-Zeit, ausgeführte Arbeiten und Bericht |
| `RepairComplication` | `ARI.REPAIR_COMPLICATIONS` | während der Ausführung beobachtetes Ereignis und Zusatzaufwand |

Die textuellen Sammlungen `PerformedOperationCodes` und `AdditionalPartCodes` werden in dieser
ersten Ausbaustufe als JSON-Arrays gespeichert. Die zentralen analytischen Beziehungen bleiben
relational, indiziert und über Fremdschlüssel geschützt.

## Zeitmodell und Wissensgrenze

Für jeden Fall gilt:

```text
Fahrzeugaufnahme ≤ Auftragserstellung ≤ Reparaturbeginn ≤ Reparaturabschluss
                                         │
                 bekannte Bedingungen ───┘
                                         └── Komplikationen nur ab hier
```

Die erfasste Arbeitszeit ist eine Werkstattgröße. Sie wird nicht aus der Kalenderdifferenz zwischen
Start und Abschluss abgeleitet, weil Pausen, Wartezeiten und parallele Arbeit diese Differenz
verfälschen können.

### Weshalb diese Trennung für ML entscheidend ist

Angenommen, ein Bolzen bricht bei der Demontage. `BROKEN_BOLT` erklärt dann einen Teil der
tatsächlichen Arbeitszeit, war aber vor Arbeitsbeginn unbekannt. Würde man dieses Feld als
Vorhersagefeature verwenden, bekäme das Modell Informationen aus der Zukunft. Die Evaluation sähe
künstlich gut aus und wäre in einer echten Vorhersagesituation nicht reproduzierbar.

Deshalb sind folgende Felder potenzielle Features:

- Fahrzeugbaujahr beziehungsweise daraus historisch berechnetes Alter;
- Kilometerstand am damaligen Auftrag;
- Reparaturcode und Referenzarbeitszeit;
- vor Beginn dokumentierte Korrosion oder Vorschäden.

Folgende Felder sind Outcomes oder Labels:

- Ist-Arbeitszeit;
- Komplikationscodes und Zusatzminuten;
- ausgeführte Folgearbeiten;
- Reparaturbericht und Abschlusszeitpunkt.

Die Methode `HistoricalRepairCase.Validate()` erzwingt IDs, Kardinalitäten, positive Arbeitswerte
und Zeitgrenzen bereits vor dem Datenbankzugriff. HANA wiederholt zentrale Regeln mit PK, FK,
UNIQUE- und CHECK-Constraints. Diese doppelte Sicherung ist absichtlich: Core liefert verständliche
fachliche Fehler, die Datenbank schützt die Daten auch vor anderen Clients.

## Migration 002

[`002_historical_repairs.sql`](../database/hana/002_historical_repairs.sql) ist vorwärtsgerichtet
und verändert Migration 001 nicht. Sie wurde am 23. Oktober 2026 als `ARI_DEPLOY` ausgeführt und
anschließend vom Migration-Runner als bereits angewendet erkannt. `ARI_APP` besitzt DML-Rechte,
aber keine Schemaerstellungsrechte.
