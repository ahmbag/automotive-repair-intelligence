# Datenqualität und analytische Validierung

## Was geprüft wird

Der `HanaHistoricalDataProfiler` liest direkt aus SAP HANA und prüft:

- Manifeststatus sowie erwartete, importierte und tatsächlich gespeicherte Anzahl;
- mindestens eine Position und genau eine Ausführung pro Auftrag;
- verwaiste Positionen, Ausführungen und Komplikationen;
- positive Referenz- und Ist-Arbeitszeiten;
- Modelljahr und Kilometerstand;
- Aufnahme-, Auftrags-, Start-, Abschluss-, Bedingungs- und Komplikationszeitpunkte;
- synthetische Datenklassifikation;
- Verteilung der Arbeitszeiten und Komplikationen;
- sichtbaren Zusammenhang zwischen bekannter Korrosion und späterem Verlauf.

Der Profiler liefert Exitcode 1, sobald eine Qualitätsregel verletzt ist. Damit kann dieselbe Prüfung
später in CI oder einer Deployment-Pipeline verwendet werden.

## Gemessenes Profil vom 23. Oktober 2026

Dataset: `ari-suspension-synthetic-v1`

| Qualitätsgröße | Ergebnis |
| --- | ---: |
| Erwartete/importierte/gespeicherte Aufträge | 1.000 / 1.000 / 1.000 |
| Vollständige Fälle | 1.000 |
| Positionen / Ausführungen | 1.000 / 1.000 |
| Komplikationsereignisse | 634 |
| bekannte strukturierte Bedingungen | 466 |
| verwaiste Datensätze | 0 |
| ungültige Arbeitswerte | 0 |
| unplausible Fahrzeugwerte | 0 |
| ungültige Zeitachsen | 0 |
| fehlende oder leere Reparaturberichte | 0 |
| Klassifikation | `SYNTHETIC` |
| Qualitätsfehler insgesamt | 0 |

## Arbeitszeitprofil

| Kennzahl | Referenzzeit | Ist-Zeit |
| --- | ---: | ---: |
| Mittelwert | 128,49 min | 150,64 min |
| Median | 126 min | 142 min |
| P90 | – | 209,10 min |

Die mittlere absolute Abweichung beträgt 27,83 Minuten. Die mittlere relative Abweichung beträgt
+17,75 %. P90 bedeutet: 90 % der Fälle liegen bei höchstens rund 209 Minuten Ist-Zeit; 10 % liegen
darüber. Für schiefe Arbeitszeitverteilungen ist P90 oft aussagekräftiger als nur der Mittelwert.

## Komplikationen

| Code | Ereignisse |
| --- | ---: |
| `SEIZED_FASTENER` | 291 |
| `DAMAGED_BUSHING_SEAT` | 182 |
| `BROKEN_BOLT` | 83 |
| `ALIGNMENT_CORRECTION` | 78 |

Ein Auftrag kann mehrere Ereignisse besitzen, daher ist die Summe keine Auftragsanzahl.

## Interpretation des Korrosionseffekts

| Gruppe | Fälle | mittlere Zeitabweichung | Fälle mit Komplikation |
| --- | ---: | ---: | ---: |
| vorab bekannte Korrosion | 334 | +33,31 min | 64,37 % |
| keine vorab bekannte Korrosion | 666 | +16,56 min | 38,74 % |

Der beabsichtigte Zusammenhang ist klar sichtbar, aber nicht deterministisch. Genau das ist für ein
Lernproblem nützlich. Wichtig: Die Zahlen validieren den Generator, nicht die reale Welt.

## Profiler ausführen

```powershell
dotnet run --project tools/ARI.DataQualityReport -- `
  ari-suspension-synthetic-v1 artifacts/data-profile.json
```

Die vollständige Abnahme wird außerdem als Test ausgeführt:

```powershell
$env:ARI_DATASET_ID = "ari-suspension-synthetic-v1"
dotnet test tests/ARI.Infrastructure.IntegrationTests `
  --filter "Category=DataAcceptance"
```
