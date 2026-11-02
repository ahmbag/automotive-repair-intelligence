# Repair Extraction Prompt V2

Version: `repair-extraction-v2`  
Zweck: domänen- und zeitbewusste Extraktion; Standardkonfiguration.

Die kanonische, ausführbare Version liegt in
[`RepairExtractionPrompts.cs`](../../src/ARI.Infrastructure/AI/RepairExtractionPrompts.cs).

Zusätzlich zu V1 enthält V2:

- verbindliche Reparatur-, Komplikations- und Vorbedingungen-Kataloge;
- `PRE_REPAIR_DESCRIPTION` und `REPAIR_COMPLETION_REPORT` als anwendungseigene Kontexte;
- Verbot, während/nach der Reparatur beobachtete Ereignisse als `BEFORE_REPAIR` auszugeben;
- Ist-Arbeitszeit nur bei expliziter Angabe in einem Abschlussbericht;
- keine Erfindung von Fahrzeug, Einbauseite, Dauer oder Zustand;
- `NEEDS_REVIEW`, wenn keine eindeutige Katalogzuordnung möglich ist;
- explizite Abwehr von Anweisungen und Rollenwechseln innerhalb des Werkstatttextes.

Das Prompt ist nur eine erste Schutzschicht. Katalog, Evidence und zeitliche Regeln werden nach der
Deserialisierung nochmals deterministisch in `ARI.Core` geprüft.
