# Embedding-Strategie

| Eigenschaft | Wert |
| --- | --- |
| Provider | OpenAI Embeddings API |
| Modell | `text-embedding-3-small` |
| Dimensionen | 1.536 |
| Konfigurationsversion | `repair-report-embedding-v1` |
| HANA-Typ | `REAL_VECTOR(1536)` |
| Chunking | ein normalisierter Abschlussbericht pro Auftrag |
| Batchgröße | 100 |
| Datenklassifikation | `SYNTHETIC` |

Das Modell wird hinter `IRepairEmbeddingProvider` verwendet. Laut
[offizieller OpenAI-Modellseite](https://developers.openai.com/api/docs/models/text-embedding-3-small)
unterstützt `text-embedding-3-small` den Embeddings-Endpunkt. Modell, Dimension und Version sind
Konfiguration; der API-Schlüssel liegt ausschließlich in .NET User Secrets beziehungsweise einer
Deployment-Secret-Quelle.

## Idempotenz

Der Indexer berechnet SHA-256 über den normalisierten UTF-8-Quelltext und liest vor jedem Lauf die
vorhandenen Tupel aus Modell, Version, Auftrag und Hash. Bei identischem Tupel erfolgt weder ein
Provideraufruf noch ein Datenbank-Write. Bei geändertem Text oder einer neuen Modellversion wird
ein neues beziehungsweise ersetztes, transaktionales Artefakt geschrieben. Eine Unique-Constraint
auf `(REPAIR_ORDER_ID, CHUNK_INDEX, EMBEDDING_MODEL, MODEL_VERSION)` verhindert Duplikate zusätzlich.

Leere Texte, nicht-finite Werte, eine andere Antwortanzahl und Vektoren ungleich 1.536 Dimensionen
werden vor dem Speichern abgewiesen. Temporäre HTTP-Fehler werden höchstens zweimal mit Backoff
wiederholt; Caller-Cancellation bleibt erhalten.

## Reproduzierbarer Betrieb

```powershell
# Secrets nur in den Prozess übernehmen, nicht ausgeben.
$secrets = dotnet user-secrets list --project src/ARI.Api --json | ConvertFrom-Json
$env:Embeddings__ApiKey = $secrets.'Embeddings:ApiKey'

dotnet run --project tools/ARI.RagTool -- index `
  artifacts/ari-suspension-synthetic-v1.json

Remove-Item Env:Embeddings__ApiKey
```

Die Konsolenausgabe nennt Quellanzahl, neu erzeugte Vektoren, unveränderte Quellen,
Provider-Batches und gemeldete Input-Tokens. Ein zweiter unveränderter Lauf muss
`Embedded: 0` und `Provider batches: 0` ausgeben.

## Reale Abnahme

Der vollständige Index wurde am 27. Oktober 2026 mit `ARI_APP` aufgebaut und anschließend direkt
in HANA geprüft: 1.000 Embedding-Zeilen, 1.000 unterschiedliche Reparaturaufträge und ausschließlich
Vektoren mit 1.536 Dimensionen. Der unveränderte Wiederholungslauf meldete:

```text
Sources: 1000
Embedded: 0
Unchanged: 1000
Provider batches: 0
```

Damit sind sowohl Datenbank-Idempotenz als auch das Vermeiden unnötiger Providerkosten belegt.
Die 80.902 Quellzeichen entsprechen konservativ geschätzt 16.181 bis 26.968 Input-Tokens. Beim
für die Abnahme verwendeten Preis von 0,02 USD je eine Million Tokens ergibt das ungefähr
0,000324 bis 0,000539 USD für die Erstindexierung. Es handelt sich um eine Schätzung, nicht um
eine Providerrechnung; die exakte Tokenzahl des Erstlaufs wurde nicht dauerhaft protokolliert.
