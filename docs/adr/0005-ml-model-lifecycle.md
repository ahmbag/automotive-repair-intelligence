# ADR 0005: Separater, versionierter ML-Lebenszyklus

- Status: Accepted
- Datum: 2026-10-24

## Kontext

ARI benötigt eine numerische Arbeitszeitprognose aus tabellarischen historischen Daten. Das
Domänenmodell und die Orchestrierung sind .NET-basiert; scikit-learn bietet die geeignete
Trainings- und Transformationspipeline. Training pro Anfrage, direkter Python-Zugriff auf HANA und
ML-Bibliotheken in `ARI.Core` würden Reproduzierbarkeit, Sicherheit und Architekturgrenzen
verschlechtern.

## Entscheidung

- HANA wird mit `ARI_APP` durch einen .NET-Infrastruktur-Exporter gelesen.
- Python trainiert offline aus einem versionierten, gehashten Export.
- Train/Validation/Test werden chronologisch 70/15/15 getrennt.
- Ein Artefakt bündelt Vorverarbeitung und ausgewähltes Regressionsmodell.
- FastAPI lädt dieses Artefakt read-only und bietet Liveness, Readiness und Prediction.
- `ARI.Infrastructure` adaptiert HTTP an `ILaborPredictionService`; Core bleibt providerneutral.
- Ein statistischer Fallback ist standardmäßig aus und immer als `FALLBACK` gekennzeichnet.
- Binärartefakte und Exporte werden reproduziert, nicht in Git versioniert.

## Konsequenzen

Vorteile sind identische Transformationen in Training und Inferenz, überprüfbare Metadaten, ein
kleiner Core-Vertrag und unabhängige Skalierung. Dafür müssen zwei Prozesse betrieben und
Kompatibilität sowie Erreichbarkeit überwacht werden. `joblib` darf nur aus vertrauenswürdiger
Quelle geladen werden. Retraining, Modellregistry, Prognoseintervalle und Drift-Monitoring bleiben
bewusste Folgearbeiten.
