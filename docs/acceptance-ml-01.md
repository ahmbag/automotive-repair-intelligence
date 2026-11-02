# ARI-ML-01 – Abnahmeprotokoll

Abnahmedatum: 24. Oktober 2026  
Ergebnis: **18/18 erfüllt**

Die Abnahme bezieht sich auf das ausdrücklich als `SYNTHETIC` klassifizierte Dataset
`ari-suspension-synthetic-v1`. Sie bestätigt technische und methodische Funktionsfähigkeit, nicht
die Übertragbarkeit auf reale Werkstattaufträge.

| # | Definition of Done | Ergebnis / Evidenz |
| ---: | --- | --- |
| 1 | Historische Daten reproduzierbar aus HANA exportiert | 1.000 Zeilen durch `ARI.MlDatasetExporter`; Python prüft Manifest und kanonischen Export-Hash. |
| 2 | ID, Generatorversion, SHA dokumentiert | ID `ari-suspension-synthetic-v1`, Generator `1.0.0`, Quell-SHA `960588…1384`, Export-SHA `325f6a…1def` in `metadata.json`. |
| 3 | Drei Baselines implementiert | Referenzzeit, historischer Median, medianer Aufschlag; identische Evaluationszeilen. |
| 4 | MAE, RMSE, Bias berechnet | Für alle fünf Verfahren plus Medianfehler, P90 und Unterquote in `evaluation.json`. |
| 5 | Feature-Schema versioniert | `ari-labor-features-v1`, sieben explizite Features und feste Reihenfolge. |
| 6 | Chronologischer Split | 700/150/150 nach Abschlusszeit, stabile ID als Tie-Breaker. |
| 7 | Nur bekannte Features | Istzeit, Komplikationen, Bericht, Abschlusszeit und IDs ausgeschlossen. |
| 8 | Leakage-Tests | Sperrlisten-, Ziel-, Komplikations-, Pipeline-, Split- und Zwillingsprüfungen automatisiert. |
| 9 | Ridge und Random Forest | Beide mit festem Seed/Parametern trainiert. |
| 10 | Vergleich unter gleichen Bedingungen | gemeinsame Features/Splits; Auswahl nur über Validierung, Schlussmessung am Testset. |
| 11 | Segmentanalyse | Alter, Laufleistung, Korrosion, hoher tatsächlicher Aufschlag einschließlich Gruppengrößen. |
| 12 | Artefakt und Metadaten | `model.joblib`, `metadata.json`, `evaluation.json`; Modell-SHA `d206f9…6840`; Runtime-Versionen gespeichert. |
| 13 | Python-Inferenzservice | FastAPI `/health`, `/health/ready`, `/predict`; kein Training im Serving-Prozess. |
| 14 | .NET-Adapter ohne Core-Infrastruktur | Adapter ausschließlich in `ARI.Infrastructure`; Core kennt nur Vertrag und Port. |
| 15 | echter .NET-End-to-End-Nachweis | `.NET -> HTTP -> FastAPI -> Ridge`: 2,487136 h, Modell v1, Status `PREDICTED`. |
| 16 | Fehler, Readiness, Fallback | fehlender Dienst live mit 503 geprüft; Timeout/Antworttests; Fallback explizit konfigurierbar und markiert. |
| 17 | Regressionen erfolgreich | 24 Python-, 14 API-/Adapter- und 6 Core-Tests bestanden; Build 0 Warnungen/0 Fehler. HANA-nahe Exportabnahme erfolgte live mit `ARI_APP`. |
| 18 | Qualität, Grenzen, Ausführung dokumentiert | Feature-, Training-, Segment-, Inferenz-, Sicherheits- und Betriebsdokumente vorhanden. |

## Gemessenes Ergebnis auf dem unabhängigen Testset

| Verfahren | MAE min | RMSE min | Bias min |
| --- | ---: | ---: | ---: |
| Referenzarbeitszeit | 31,927 | 43,707 | -28,380 |
| Historischer Median | 27,500 | 37,170 | -16,380 |
| Median Zeitaufschlag | 27,500 | 37,170 | -16,380 |
| **Ridge** | **24,842** | **31,873** | **-1,609** |
| Random Forest | 26,376 | 33,951 | -1,966 |

Ridge wurde auf der Validierung als bestes ML-Modell ausgewählt und schlägt im unabhängigen Test
die beste Baseline. Die Strategie selbst wird aber ebenfalls ausschließlich anhand der
Validierung gewählt: Dort lag die Baseline mit 26,493 Minuten knapp vor Ridge mit 26,511 Minuten.
Die konservativ gespeicherte Empfehlung lautet daher `STATISTICAL_BASELINE`; der Testvorteil wird
nicht zur nachträglichen Umentscheidung missbraucht.

## Ausgeführte Nachweise

```powershell
dotnet build AutomotiveRepairIntelligence.slnx --no-restore
dotnet test tests/ARI.Core.Tests/ARI.Core.Tests.csproj --no-restore
dotnet test tests/ARI.Api.Tests/ARI.Api.Tests.csproj --no-restore
python -m unittest discover -s ml/tests -v
python -m ari_ml.training.train --config ml/config/training-v1.toml `
  --dataset artifacts/ml/training-export.json `
  --output-dir artifacts/ml/ari-repair-duration-v1
```

Zusätzlich wurden Python und `ARI.Api` als echte lokale Prozesse gestartet. Beide Readiness-Routen
lieferten Modell `ari-repair-duration-v1` und Schema `ari-labor-features-v1`. Nach Abschalten von
Python lieferten die .NET-ML-Readiness und die Prognoseroute jeweils HTTP 503.

## Abnahmeeinschränkung

Das Modell unterschätzt das 24 Fälle große Segment mit tatsächlichem Mehraufwand ab 60 Minuten zu
100 %; Segment-MAE 54,632 Minuten. Vor einer realen Preis- oder Kapazitätsentscheidung sind echte
Daten, fachliche Freigabe, Driftüberwachung und eine Unsicherheitsdarstellung notwendig.

Weiterführend: [Training und Evaluation](ml-training-and-evaluation.md),
[Feature-Schema](ml-feature-schema.md), [Inferenz](ml-inference.md) und
[ADR 0005](adr/0005-ml-model-lifecycle.md).
