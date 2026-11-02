# ARI-ML-01: Vom HANA-Export zum evaluierten Modell

Dieses Dokument erklärt den ML-Weg nicht nur als Bedienungsanleitung, sondern auch die Gründe
hinter den Entscheidungen.

## 1. Reproduzierbare Datenbasis

`ARI.MlDatasetExporter` ist die einzige HANA-nahe Komponente des ML-Exports. Sie läuft mit dem
eingeschränkten Benutzer `ARI_APP`, liefert genau eine Zeile pro abgeschlossener Reparaturposition
und schreibt ein kanonisch prüfbares JSON. Python erhält keine HANA-Zugangsdaten.

Der abgenommene Lauf hatte folgende Identität:

| Merkmal | Wert |
| --- | --- |
| Dataset-ID | `ari-suspension-synthetic-v1` |
| Klassifikation | `SYNTHETIC` |
| Generatorversion | `1.0.0` |
| Zeilen | 1.000 |
| Quelldataset SHA-256 | `960588c090ad9092dc98f59721e08ad8df3718d899012ace0b344fb238ff1384` |
| Export SHA-256 | `325f6a9fd2d63efc7829a0d8a4a9b031c7e162267f80c13c96f975694a401def` |

Der Quell-Hash identifiziert den bereits importierten historischen Datensatz. Der Export-Hash
identifiziert die für ML aufbereitete Darstellung. Beides ist nötig: Dieselben Quelldaten könnten
durch einen geänderten Export andere Spalten oder Einheiten erhalten.

```powershell
$env:Hana__Host = "<HANA-host>"
$env:Hana__Port = "443"
$env:Hana__User = "ARI_APP"
$env:Hana__Password = "<secret>"
dotnet run --project tools/ARI.MlDatasetExporter -- `
  ari-suspension-synthetic-v1 artifacts/ml/training-export.json
```

Das Passwort wird nie als Argument, Dateiinhalt, Log oder Metadatum gespeichert. `artifacts/` ist
absichtlich in `.gitignore`; exportierte Betriebsdaten und binäre Modelle gehören nicht ins Git.

## 2. Drei klassische Baselines

Ein ML-Wert ist nur sinnvoll, wenn er eine einfache Alternative messbar schlägt.

1. **Referenzarbeitszeit:** übernimmt die geplante Standardzeit.
2. **Historischer Median:** Median der tatsächlichen Zeiten je Reparaturcode. Gruppen unter zehn
   Fällen fallen auf die Referenzzeit zurück.
3. **Medianer Zeitaufschlag:** Referenzzeit plus Median aus `Istzeit - Referenzzeit` je Code. Eine
   zu kleine/unbekannte Gruppe nutzt den globalen historischen Medianaufschlag.

Beim Validieren werden die Baselines ausschließlich mit Training gelernt. Beim abschließenden Test
dürfen Training und Validierung als ältere Historie dienen. Kein Test-Label fließt in eine
Baseline ein. Im vorliegenden Datensatz sind historischer Median und Medianaufschlag gleich gut,
weil die Referenzzeit innerhalb eines Reparaturcodes konstant ist. Das ist ein Dateneffekt, kein
Implementierungsfehler.

## 3. Metriken richtig lesen

- **MAE** ist der durchschnittliche absolute Fehler. 24,84 Minuten heißt: unabhängig von der
  Richtung liegt die Prognose im Mittel rund 25 Minuten neben der Istzeit.
- **RMSE** gewichtet große Fehler über das Quadrieren stärker. Ein deutlicher Abstand zum MAE weist
  auf einzelne größere Fehler hin.
- **Bias** ist hier ausdrücklich `Prognose - Ist`. Ein negativer Wert bedeutet systematische
  Unterschätzung.
- **Median Absolute Error** beschreibt einen typischen Fall robuster gegen Ausreißer.
- **P90 Absolute Error** wird von 90 % der Fälle nicht überschritten.
- **Underestimation Rate** ist für die Werkstatt wirtschaftlich wichtig, weil zu niedrige
  Kalkulationen Marge gefährden können.

## 4. Modelltraining und Auswahl

[`training-v1.toml`](../ml/config/training-v1.toml) friert Seed, Split, Feature-Schema und
Hyperparameter ein. Ridge und Random Forest erhalten dieselben Zeilen und dieselben sieben
Eingabemerkmale. Ridge skaliert numerische Werte; beide Modelle verwenden dieselbe Imputation und
One-Hot-Kodierung.

Die Validierung wählte Ridge nach dem kleinsten MAE. Erst danach wurden die Modelle auf den 850
älteren Entwicklungsfällen neu trainiert und einmalig an den 150 Testfällen bewertet.

### Unabhängiger Test, alle Angaben in Minuten

| Verfahren | MAE | RMSE | Bias | Underestimation |
| --- | ---: | ---: | ---: | ---: |
| Referenzarbeitszeit | 31,927 | 43,707 | -28,380 | 77,33 % |
| Historischer Median | 27,500 | 37,170 | -16,380 | 59,33 % |
| Median Zeitaufschlag | 27,500 | 37,170 | -16,380 | 59,33 % |
| **Ridge** | **24,842** | **31,873** | **-1,609** | **46,00 %** |
| Random Forest | 26,376 | 33,951 | -1,966 | 44,67 % |

Im unabhängigen Test verbessert Ridge den MAE gegenüber der besten Baseline um 2,658 Minuten
beziehungsweise rund 9,7 %. Die Strategieentscheidung darf diesen Testvorteil aber nicht
nachträglich verwenden: Auf der Validierung hatte die beste Baseline 26,493 Minuten MAE und Ridge
26,511 Minuten. Die gespeicherte Empfehlung lautet deshalb konservativ `STATISTICAL_BASELINE`.
Ridge bleibt das beste der beiden ML-Modelle und das technisch integrierte Lernartefakt. Random
Forest unterschätzt im Test minimal seltener, verliert aber beim primären Kriterium MAE.

## 5. Segmentanalyse und ehrliche Grenzen

| Segment | n | MAE | Bias | Unterquote |
| --- | ---: | ---: | ---: | ---: |
| Fahrzeuge ab 12 Jahren | 27 | 28,006 | -1,147 | 55,56 % |
| Laufleistung ab 200.000 km | 29 | 27,288 | -4,954 | 55,17 % |
| bekannte Korrosion | 66 | 31,000 | -2,967 | 46,97 % |
| tatsächlicher Aufschlag ab 60 min | 24 | 54,632 | -54,632 | 100,00 % |

Die letzte Zeile ist die wichtigste Einschränkung: Extrem aufwendige Fälle werden deutlich
unterschätzt. Ihre später eingetretenen Komplikationen dürfen nicht als Eingabe verwendet werden;
das schützt vor Leakage, begrenzt aber die Vorhersagbarkeit. Ein späteres Paket sollte belastbare
Vorab-Risikomerkmale und Prognoseintervalle untersuchen. Der jetzige Einzelwert darf nicht als
Garantie oder automatische Preisfreigabe verstanden werden.

## 6. Permutation Importance

Beim ausgewählten Modell erhöhte das zufällige Vertauschen der Referenzzeit den MAE im Mittel um
9,778 Minuten. Danach folgten bekannte Korrosion (1,067), Fahrzeugalter (0,873), Reparaturcode
(0,621) und Kilometerstand (0,349). Hersteller und Modell lagen leicht unter null.

Permutation Importance misst den Nutzen für dieses Modell und diesen Testbestand. Sie beweist
weder Kausalität noch, dass ein Merkmal allgemein unwichtig ist. Korrelationen können Importance
zwischen Merkmalen verteilen; kleine negative Werte sind Messrauschen beziehungsweise ein Hinweis,
dass das Merkmal in diesem Lauf nicht geholfen hat.

## 7. Artefakt und Reproduktion

```powershell
python -m pip install -e ".\ml[test]"
python -m ari_ml.training.train `
  --config ml/config/training-v1.toml `
  --dataset artifacts/ml/training-export.json `
  --output-dir artifacts/ml/ari-repair-duration-v1
```

Der Ordner enthält `model.joblib`, `metadata.json` und `evaluation.json`. Die abgenommene
Modell-SHA-256 lautet
`d206f94f7c532d23c210ca0300b978ce5253786143ca4f67c0c8b6a3d8b06840`.
Ein zweiter Trainingslauf mit gleicher Konfiguration erzeugte denselben Modell-Hash und dieselben
Metriken.

Abhängigkeiten des Artefakts: Python 3.14.6, NumPy 2.5.4, pandas 3.1.0,
scikit-learn 1.9.1 und joblib 1.6.0. `joblib` basiert auf Pickle und kann beim Laden Code
ausführen. Der Dienst lädt daher nur das lokal trainierte Artefakt, verifiziert vorher dessen Hash,
Feature-Schema und scikit-learn-Version. Fremde Artefakte sind nicht vertrauenswürdig.
