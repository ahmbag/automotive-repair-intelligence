# ARI-LLM-01 – Architektur und Betrieb

## Verantwortungsgrenzen

```text
ASP.NET Core endpoint
  -> IFeatureExtractor (providerneutraler ARI.Core-Port)
  -> OpenAiRepairInformationExtractor (ARI.Infrastructure)
  -> OpenAI Responses API / strict Structured Output
  -> strikte JSON-Deserialisierung
  -> RepairExtractionValidator (Katalog, Evidence, Zeit)
  -> optional ILaborPredictionService -> Python/FastAPI -> Ridge
```

Das LLM interpretiert Text, erzeugt aber niemals selbst eine Arbeitszeitprognose. Im kombinierten
Workflow erhält das bestehende ML-Modell nur Fahrzeugdaten, Reparaturcode und validierte
`BEFORE_REPAIR`-Bedingungen. Beobachtete Komplikationen und Ist-Arbeitszeit werden nicht in den
ML-Request übernommen.

## Vier unterschiedliche Schutzebenen

1. **Structured Output:** Der Provider muss das feste JSON-Schema mit `strict: true` erfüllen.
2. **Schema-Deserialisierung:** .NET weist fehlende, falsch typisierte und zusätzliche Felder ab.
3. **Domain Validation:** Codes müssen im Katalog liegen; Evidence muss im Originaltext vorkommen;
   Dokumenttyp und Informationszeit müssen fachlich passen.
4. **V3-Klassifikation:** Explizite, nicht negierte Ereignissätze werden deterministisch der kleinen
   Komplikationstaxonomie zugeordnet; jede Zuordnung behält Original-Evidence.
5. **Workflow-Freigabe:** `ExtractionQualityPolicy` vergibt `ACCEPTED`, `PARTIAL`, `NEEDS_REVIEW`
   oder `REJECTED`. Nur vollständige `ACCEPTED`-Ergebnisse erreichen ML; alle anderen führen im
   Prognosepfad zu HTTP 422.

Schema-Konformität allein beweist keine Wahrheit. Umgekehrt kann ein fachlich plausibles Ergebnis
wegen erfundener Evidence nicht akzeptiert werden.

## Sichere Konfiguration

Im Repository stehen nur Endpoint, Timeout, Retry-Limit und Promptversion. API-Key und Modell
werden lokal als User Secrets gesetzt:

```powershell
dotnet user-secrets set --project src/ARI.Api "Llm:ApiKey" "<OpenAI-API-key>"
dotnet user-secrets set --project src/ARI.Api "Llm:Model" "<pinned-model-id>"
```

Für Deployment können `Llm__ApiKey`, `Llm__Model`, `Llm__Endpoint`, `Llm__Timeout` und
`Llm__MaxRetries` aus einem Secret Store beziehungsweise Umgebungsvariablen kommen. Der Adapter
loggt weder API-Key, Dokument noch Modellausgabe. Erfasst werden nur Modellkennung, Promptversion,
Latenz, Tokenzahlen und Provider-Request-ID.

## Fehlersemantik

| Fall | Ergebnis |
| --- | --- |
| Request leer/zu lang oder Dokumenttyp unbekannt | HTTP 400 |
| Schema, Katalog, Evidence oder Zeitregel verletzt | HTTP 422 mit Validierungsfehlern |
| Key/Modell fehlen, Provider nicht erreichbar, Timeout, finale 429/5xx | HTTP 503 |
| temporäre 408/429/5xx | maximal zwei exponentiell verzögerte Wiederholungen |
| Client bricht ab | Cancellation wird weitergereicht, nicht als Providerfehler versteckt |
| ML im kombinierten Ablauf nicht verfügbar | vorhandene ML-503-/Fallback-Semantik |

## Endpunkte

- `POST /api/repair-reports/extract`: Abschluss- oder Vorabtext strukturiert extrahieren.
- `POST /api/estimates/from-description`: Vorabtext extrahieren, freigeben und an ML senden.

Training, RAG, Embeddings und Persistenz extrahierter Ergebnisse gehören nicht in dieses Paket.

Providerreferenzen: [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs),
[Responses API und Modelle](https://developers.openai.com/api/docs/models) sowie
[API-Authentifizierung](https://platform.openai.com/docs/api-reference/debugging-requests).
