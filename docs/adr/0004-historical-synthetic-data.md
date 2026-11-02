# ADR 0004: Versionierte synthetische Reparaturhistorien

- Status: Accepted
- Date: 2026-10-23

## Context

ARI benötigt vor dem ersten ML-Modell eine reproduzierbare, fachlich verständliche Datenbasis.
Python soll Daten erzeugen, aber weder HANA-Berechtigungen noch eine zweite Persistenzlogik besitzen.
Vorhersageinformationen und spätere Outcomes müssen dauerhaft unterscheidbar bleiben.

## Decision

Ein deterministischer Python-Generator erzeugt versioniertes JSON mit UUIDv5-IDs und kanonischer
SHA-256-Prüfsumme. Ein .NET-Importer prüft und mappt das Dataset, validiert Core-Aggregate und schreibt
100 Fälle je HANA-Transaktion. `HISTORICAL_DATASETS` verwaltet unveränderliche Metadaten, Fortschritt
und Abschlussstatus. Deterministische Auftrags-IDs und Manifeststatus ermöglichen idempotente
Wiederholung und Wiederaufnahme.

Vorab bekannte Bedingungen werden in `REPAIR_CONDITIONS`, spätere Ereignisse in
`REPAIR_COMPLICATIONS` gespeichert. Ein ausführbarer Featurevertrag verbietet Outcome-Felder als
ML-Eingaben.

## Consequences

Jeder Trainingslauf kann auf Dataset-ID, Generatorversion, Seed und Prüfsumme zurückgeführt werden.
Fehler können keine halben Fälle innerhalb eines Batches hinterlassen. Der zeilenweise Cloud-Import
ist nachvollziehbar, aber langsamer als ein künftiger Bulkpfad. Alle Aussagen betreffen synthetische
Annahmen; reale Vorhersagegüte kann erst mit unabhängigen Realdaten bewertet werden.
