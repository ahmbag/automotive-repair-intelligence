# ARI-LLM-01 – Evaluation

## Ground Truth

Der Generator `ARI.LlmEvaluation prepare` baut aus dem versionierten historischen Dataset einen
festen Evaluationsbestand. Die Berichte werden mit den bereits strukturierten Referenzdaten
angereichert, damit Fahrzeug, Reparaturcode, Evidence und Istzeit ausdrücklich im Testdokument
stehen. Ground Truth enthält nur Textinhalte, die tatsächlich sichtbar sind.

| Merkmal | Wert |
| --- | --- |
| Evaluations-ID | `ari-llm-evaluation-v1` |
| Fälle | 100 |
| historische Abschlussberichte | 90 |
| neue Vorab-Beschreibungen | 5 |
| Negativ-/Injection-Fälle | 5 |
| zusätzlich kontrollierte Fälle | 10 |
| Fälle mit 0 / 1 / mehreren Komplikationen | 40 / 40 / 20 |
| Klassifikation | `SYNTHETIC` |
| SHA-256 | `b59c6863fad947861df3051d6125abf2d4748f6c53a956100fb3eb231bbb0ff9` |

```powershell
dotnet run --project tools/ARI.LlmEvaluation -- prepare `
  artifacts/ari-suspension-synthetic-v1.json `
  artifacts/llm/evaluation-set.json
```

Das Artefakt liegt wegen Umfang und abgeleiteter Berichte unter dem ignorierten `artifacts/`.
Generator, Auswahlregel und Hash bleiben versioniert und reproduzierbar.

## Messgrößen

- `SchemaValidityRate`: Anteil strukturell deserialisierbarer Providerantworten.
- `VehicleExactMatchAccuracy`: alle vier Fahrzeugfelder exakt korrekt.
- `RepairCodeExactMatchAccuracy`: normalisierter Reparaturcode exakt korrekt, einschließlich `null`.
- `ComplicationPrecision/Recall/F1`: mikro-gemittelte Codes über alle 100 Fälle.
- `UnsupportedClaimCount`: Katalog- oder Evidence-Verletzungen.
- `TemporalLeakageViolationCount`: unzulässige Vorab-Einstufung oder Ergebniswissen in Vorabtexten.
- Tokenverbrauch und Gesamtlatenz werden separat erfasst; es gibt keinen verschleiernden Gesamtscore.

## Promptvergleich

Der `run`-Befehl sendet dieselben 100 Fälle mit demselben Modell einmal an V1 und einmal an V2.
Jeder Einzelbefund und jede Fehlerkategorie wird im Report gespeichert.

```powershell
# Geheimnisse aus dem API-User-Secrets-Store nur für diesen Prozess übernehmen, ohne sie auszugeben:
$llmSecrets = dotnet user-secrets list --project src/ARI.Api --json | ConvertFrom-Json
$env:Llm__ApiKey = $llmSecrets.'Llm:ApiKey'
$env:Llm__Model = $llmSecrets.'Llm:Model'
dotnet run --project tools/ARI.LlmEvaluation -- run `
  artifacts/llm/evaluation-set.json `
  artifacts/llm/evaluation-report.json
Remove-Item Env:Llm__ApiKey, Env:Llm__Model
```

Die Abnahmeziele sind: 100 % akzeptierte Schemaantworten, mindestens 90 % Reparaturcode-Accuracy,
Komplikations-F1 mindestens 0,85, keine unbelegten Angaben in der kontrollierten Teilmenge und
keine kritischen Temporal-Leakage-Fehler. Bei Nichterreichen wird das Ergebnis dokumentiert und
nicht durch Änderung des Testsets schöngerechnet.

Für den ersten Lauf ist `gpt-6-luna` eine kostengünstige Ausgangswahl: Laut aktuellem
[offiziellen Modellblatt](https://developers.openai.com/api/docs/models/gpt-6-luna) unterstützt es
Responses und Structured Outputs. Modell-ID und tatsächlich gemessene Tokens werden im Report
gespeichert; daraus lassen sich die realen Kosten anhand der zum Laufzeitpunkt gültigen Preise
berechnen.

## Reale Provider-Evaluation vom 26. Oktober 2026

Der authentifizierte Lauf verwendete `gpt-6-luna`, den unveränderten Datensatz mit dem oben
angegebenen SHA-256 und für beide Experimente dieselben 100 Fälle. Es gab keine Provider-,
Timeout-, Schema- oder Domain-Validation-Fehler. Das vollständige, wegen der enthaltenen
Werkstatttexte ignorierte Ergebnis liegt lokal unter `artifacts/llm/evaluation-report.json`.

| Metrik | V1 Basic | V2 Domain-Aware | Ziel |
| --- | ---: | ---: | ---: |
| akzeptierte schema-konforme Ergebnisse | 100/100 | 100/100 | 100 % |
| Fahrzeug Exact Match | 100,0 % | 100,0 % | separat messen |
| Reparaturcode Exact Match | 100,0 % | 100,0 % | mindestens 90 % |
| Komplikations-Precision | 1,000 | 1,000 | separat messen |
| Komplikations-Recall | 0,561 | 0,341 | separat messen |
| Komplikations-F1 | 0,719 | 0,509 | mindestens 0,85 |
| unbelegte Claims | 0 | 0 | 0 in kontrollierter Teilmenge |
| Temporal-Leakage-Verstöße | 0 | 0 | 0 |
| Istzeit Exact Match | 100,0 % | 100,0 % | separat messen |
| mittlere Latenz | 3.458 ms | 3.501 ms | beobachten |
| P95-Latenz | 5.410 ms | 5.153 ms | beobachten |
| Input-/Output-Tokens | 49.345 / 20.989 | 76.945 / 23.853 | beobachten |

Alle zehn zusätzlich kontrollierten Fälle wurden in beiden Experimenten akzeptiert; es gab
dort weder unbelegte Claims noch zeitliche Leaks. V1 lieferte bei geringerem Tokenverbrauch die
bessere Komplikations-Erkennung. V2 war konservativer: 42 Ergebnisse standen auf `NEEDS_REVIEW`
gegenüber 5 bei V1. Die False Negatives verteilen sich wie folgt:

| ausgelassener Code | V1 | V2 |
| --- | ---: | ---: |
| `SEIZED_FASTENER` | 11 | 19 |
| `BROKEN_BOLT` | 11 | 11 |
| `DAMAGED_BUSHING_SEAT` | 14 | 24 |

### Bewertung und Einschränkungen

Das F1-Entwicklungsziel von 0,85 wurde von keiner Prompt-Version erreicht. Das ist keine
Schema- oder Halluzinationsschwäche: Die Precision beträgt 1,0 und es wurden keine unbelegten
Codes akzeptiert. Die Schwäche liegt im Recall bei indirekt formulierten Komplikationen. V2 legt
die Regel „nur exakt mappbare Fakten“ konservativer aus und setzt bei Unsicherheit häufiger
`NEEDS_REVIEW`. Deshalb wird V2 nicht aufgrund seines Namens zum Qualitätssieger erklärt.

Für den produktionsnahen Prognosepfad bleibt diese Einschränkung sicher: Er verarbeitet nur
`PRE_REPAIR_DESCRIPTION`, akzeptiert ausschließlich vollständige `EXTRACTED`-Ergebnisse und reicht
nur validierte `BEFORE_REPAIR`-Merkmale an ML weiter. Ausgelassene Merkmale vermindern möglicherweise
die Prognoseinformation, erzeugen aber kein Zukunftsdaten-Leak. Bevor Komplikationen automatisch
für weitere fachliche Entscheidungen verwendet werden, sind ein unabhängig manuell annotiertes
Set und eine neue, vorab festgelegte Prompt-/Modelliteration erforderlich. Der eingefrorene
Abnahmebestand wird nicht nachträglich zur Optimierung umetikettiert.

### Kosten

Zum Laufzeitpunkt nennt das
[offizielle Modellblatt](https://developers.openai.com/api/docs/models/gpt-6-luna) 0,10 US-Dollar
pro Million Input-Tokens und 0,50 US-Dollar pro Million Output-Tokens. Daraus ergeben sich für
die reine 200-Request-Evaluation ungefähr:

| Experiment | geschätzte Providerkosten |
| --- | ---: |
| V1 | 0,015429 USD |
| V2 | 0,019621 USD |
| gesamt | 0,035050 USD |

Die Werte sind aus den tatsächlich vom Provider gemeldeten Tokens berechnet, aber keine
Abrechnungsbelege. Separat ausgeführte Smoke- und E2E-Aufrufe sind in dieser Summe nicht enthalten.

## Live-Workflow-Nachweis

Der kombinierte Endpunkt wurde ebenfalls am 26. Oktober 2026 real ausgeführt. Eine
Vorab-Beschreibung für einen Volkswagen Golf VII (2014, 215.000 km), den geplanten Austausch des
vorderen linken Querlenkers und sichtbar dokumentierte Korrosion durchlief:

```text
ASP.NET Core -> gpt-6-luna -> JSON-Schema -> Domain Validation
              -> Python FastAPI -> ari-repair-duration-v1
```

Ergebnis: HTTP 200, Reparaturcode `CONTROL_ARM_FRONT_LEFT_REPLACE`,
`VISIBLE_CORROSION/BEFORE_REPAIR`, keine beobachteten Komplikationen, keine Istzeit und eine
ML-Prognose von 2,487136 h mit `predictionMethod=ML_REGRESSION` und `status=PREDICTED`. Damit ist
nachgewiesen, dass nachträgliche Merkmale nicht in die ML-Anfrage gelangen.
