# ARI-ML-01: Inferenz und .NET-Integration

Training und Serving sind getrennte Prozesse. Der FastAPI-Dienst besitzt absichtlich keinen
`/train`-Endpunkt; beim Start lädt er ein fertig versioniertes Artefakt.

```text
Client -> ARI.Api -> ILaborPredictionService (Core-Port)
                    -> PythonLaborPredictionService (Infrastructure/HTTP)
                    -> FastAPI -> verifizierte scikit-learn-Pipeline
```

`ARI.Core` kennt nur `LaborPredictionInput`, `LaborPredictionResult` und den Port. Es referenziert
weder HTTP, Python, FastAPI, pandas noch scikit-learn.

## Starten

```powershell
$env:ARI_ML_ARTIFACT_DIR = "$PWD\artifacts\ml\ari-repair-duration-v1"
python -m ari_ml.inference.api

# zweites Terminal
$env:MachineLearning__BaseAddress = "http://127.0.0.1:8100/"
$env:MachineLearning__Timeout = "00:00:10"
$env:MachineLearning__UseReferenceBaselineFallback = "false"
dotnet run --project src/ARI.Api
```

| Route | Semantik |
| --- | --- |
| Python `GET /health` | Prozess lebt, unabhängig vom Modell |
| Python `GET /health/ready` | Artefakt, Hash, Schema und Version kompatibel |
| Python `POST /predict` | deterministische Einzelprognose |
| .NET `GET /health/ml/ready` | transportiert Modell-Readiness kontrolliert |
| .NET `POST /api/predictions` | validiert und orchestriert die Prognose |

Beispiel für den .NET-Endpunkt:

```json
{
  "repairCode": "CONTROL_ARM_FRONT_LEFT_REPLACE",
  "manufacturer": "Volkswagen",
  "model": "Golf VII",
  "vehicleAgeYears": 12,
  "mileageKm": 215000,
  "baselineLaborHours": 1.8,
  "knownConditions": ["VISIBLE_CORROSION"],
  "extractedConditions": []
}
```

Der echte Abnahmelauf ergab:

```json
{
  "predictedLaborHours": 2.4871363700313847,
  "modelVersion": "ari-repair-duration-v1",
  "dataSource": "SYNTHETIC",
  "rangeLowerHours": null,
  "rangeUpperHours": null,
  "predictionMethod": "ML_REGRESSION",
  "status": "PREDICTED"
}
```

Das ist ein reproduzierbarer technischer Nachweis, keine fachliche Freigabe dieser konkreten Zeit.
Die Intervalle bleiben bewusst `null`, bis Quantil- oder Konformalmodelle statistisch validiert sind.

## Fehler- und Fallbacksemantik

| Fall | Verhalten |
| --- | --- |
| ungültiger Request | 400 in .NET beziehungsweise 422 in FastAPI |
| unbekannter Reparaturcode | explizite Ablehnung, kein geratenes ML-Ergebnis |
| fehlendes/inkompatibles Artefakt | Python lebt, Readiness 503, Predict 503 |
| Python nicht erreichbar / Timeout | provider-neutraler Fehler, .NET 503 |
| nicht positiver Modellwert | Adapter verwirft Antwort, .NET 503 |
| Fallback deaktiviert (Standard) | Fehler bleibt sichtbar |
| Fallback bewusst aktiviert | Referenzzeit, `REFERENCE_BASELINE`, `FALLBACK` |

Der Fallback wird nur mit
`MachineLearning__UseReferenceBaselineFallback=true` aktiviert. Er fängt ausschließlich
Verfügbarkeitsfehler ab, nicht fachlich ungültige Eingaben. Seine Antwort kann niemals mit einem
ML-Ergebnis verwechselt werden.
