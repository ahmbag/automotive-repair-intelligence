# ARI-ML-01: Feature-Schema und Leakage-Schutz

Das erste produktionsnahe Schema heißt `ari-labor-features-v1`. Seine Reihenfolge ist Teil des
Vertrags und wird beim Laden eines Modells geprüft. Training und Inferenz erzeugen exakt dieselben
sieben Spalten.

| Feature | Typ / Aufbereitung | Warum zum Prognosezeitpunkt bekannt? |
| --- | --- | --- |
| `vehicle_age_years` | numerisch, Median-Imputation | aus Modelljahr und historischem Auftragszeitpunkt |
| `mileage_km` | numerisch, Median-Imputation | Fahrzeug-Snapshot bei Auftragserstellung |
| `baseline_labor_minutes` | numerisch, Median-Imputation | geplante Referenzzeit der Reparaturposition |
| `known_corrosion` | 0/1, numerisch | nur vorab dokumentierte sichtbare Korrosion |
| `repair_code` | One-Hot-Kodierung | geplanter, normalisierter Reparaturtyp |
| `manufacturer` | One-Hot-Kodierung | Fahrzeug-Snapshot |
| `model` | One-Hot-Kodierung | Fahrzeug-Snapshot |

Unbekannte Hersteller und Modelle sind durch `OneHotEncoder(handle_unknown="ignore")` zulässig.
Ein unbekannter Reparaturcode wird dagegen bewusst abgelehnt, weil für eine fachlich neue Arbeit
keine belastbare Modellgrundlage vorliegt.

## Was ausdrücklich kein Feature ist

- `actual_labor_minutes`: das vorherzusagende Label;
- Komplikationscodes: erst während oder nach der Arbeit bekannt;
- Werkstattbericht: nachträgliche Dokumentation;
- Abschlusszeit: Split- und Auditinformation, keine Ursache der Schätzung;
- Auftrags-/Positions-ID: Identität statt verallgemeinerbarem Signal.

[`features.py`](../ml/src/ari_ml/datasets/features.py) enthält die kanonische Positiv- und
Sperrliste. [`schema.py`](../ml/src/ari_ml/training/schema.py) sowie automatisierte Negativtests
brechen ab, sobald Ziel-, Ergebnis- oder Identitätsdaten in der Feature-Liste auftauchen.

## Warum die Pipeline Leakage verhindert

`ColumnTransformer` und Modell sind in einer scikit-learn-`Pipeline` verbunden. `fit` wird bei der
Modellauswahl nur auf den ersten 700 Fällen aufgerufen. Imputation, Skalierung und One-Hot-
Kategorien lernen deshalb keine Statistik aus Validierung oder Test. Nach der Auswahl wird auf
Training plus Validierung neu gelernt; die letzten 150 Fälle bleiben bis zur Schlussmessung
unangetastet.

Der Split wird stabil nach `completed_at_utc`, anschließend nach Auftrags-ID sortiert. Nur bereits
abgeschlossene Aufträge dürfen in eine frühere Teilmenge gelangen. Exakte Zwillinge anhand der
sieben Featurewerte dürfen keine Teilmengengrenze überschreiten. Gemessene Aufteilung:

| Teilmenge | Fälle | zeitliches Ende |
| --- | ---: | --- |
| Training | 700 | 2024-06-18 13:48 +02:00 |
| Validierung | 150 | 2025-03-19 19:14 +01:00 |
| unabhängiger Test | 150 | alle späteren Fälle |

Wichtig: Ein sauberer Split verhindert methodische Selbsttäuschung, macht synthetische Daten aber
nicht automatisch realitätsgetreu. Die Messwerte gelten ausschließlich für diesen Datensatz.
