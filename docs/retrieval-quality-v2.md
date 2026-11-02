# ARI-RAG-02 – Retrieval-Qualität V2

Stand: 29. Oktober 2026

## Ursache des V1-Problems

Die eingefrorene V1-Evaluation bleibt unverändert. Dataset-Prüfsumme:
`2b1e3e8a878b77618c9bda027f15f0e83cb21e58b1493b553d76227e79368f3c`.
Die Dateiprüfsummen des Sets und Reports sind in `acceptance-rag-02.md` dokumentiert.

Die 35 positiven V1-Anfragen definieren Relevanz über Reparaturcode, Modell, Hersteller,
Laufleistungsnähe, Baujahrsnähe und bekannte Vorbedingungen. Das V1-Embedding enthält dagegen
nur den Abschlussbericht. Dadurch entstehen Representation- und Query/Document-Mismatch.
Der hohe MRR der strukturierten Suche bei geringerem Recall zeigt zusätzlich: Ein guter erster
Treffer ist häufig vorhanden, die groben Schwellenwerte bringen aber nicht alle fünf
regelbasierten Referenzen in die Top 5. Der reproduzierbare Befehl `analyze-v1` erzeugt den
Einzelbericht `artifacts/rag/retrieval-failure-analysis-v1.json` mit Expected, Top 5, Missing,
Score- und Cosine-Verteilungen sowie Fehlerklassen für alle 40 Fälle. Der reale Lauf bestätigte
bei allen 35 positiven Fällen Ranking-, Repräsentations-, Query/Document- und Score-Fusion-Probleme;
die fünf No-Result-Fälle blieben korrekt leer.

Recall und Precision sind in V1 numerisch gleich, weil jede positive Anfrage exakt fünf relevante
Labels besitzt, `topK=5` gilt und beide Metriken nur über positive Anfragen gemittelt werden:
`Recall = Hits / 5` und `Precision = Hits / 5`. Das ist korrekt, aber keine allgemeine Eigenschaft
der Metriken.

## Kanonisches Suchprofil

`canonical-repair-search-profile-v2`, Dokumentversion `2.0.0`, wird deterministisch aus dem
versionierten relationalen Quelldatensatz erzeugt. Das Vorabprofil enthält ausschließlich:

- Baugruppe, geplante Operation und Reparaturcode;
- Hersteller, Modell, Modelljahr, Laufleistung und Alter zum Reparaturzeitpunkt;
- vor Reparaturbeginn dokumentierte Zustände inklusive Schweregrad.

Es enthält niemals Ist-Arbeitszeit, Abschlussbericht oder während der Ausführung erkannte
Komplikationen. `COMPLETION_EVIDENCE` enthält den Abschlussbericht separat und wird erst als
zitierbarer Beleg für bereits ausgewählte Aufträge verwendet. Die Kandidatensuche nutzt allein
`PRE_REPAIR_PROFILE`.

Jeder Text besitzt SHA-256, Profiltyp, Profilversion, Dokumentversion, Embedding-Modell und
Modellversion. `ARI.REPAIR_SEARCH_EMBEDDINGS_V2` ist durch einen Unique Constraint gegen
Duplikate geschützt; V1 verbleibt separat in `ARI.REPAIR_REPORT_EMBEDDINGS`.

## Candidate Generation und Ranking

Harte Regeln:

1. gleicher fachlicher Reparaturcode;
2. synthetischer, zugelassener MVP-Datenbestand;
3. Auftrag abgeschlossen und Quelle spätestens zum `AsOfUtc` verfügbar;
4. exakt passende Modell-, Profil- und Dokumentversion.

Hersteller, Modell, Alter, Kilometerstand und Vorzustände sind weiche Ranking-Signale. Sie
schließen keinen ansonsten vergleichbaren Auftrag aus.

| Strategie | Zweck |
| --- | --- |
| `STRUCTURED` + `REPAIR_REPORT_V1` | unveränderte V1-Baseline |
| `DOMAIN_SEMANTIC_V2` | Cosine-Ranking der V2-Vorabprofile |
| `STRUCTURED_FIRST_HYBRID` | strukturierte Kandidaten, normierter Fachscore plus Cosine-Signal |
| `RANK_FUSION_V2` | gewichtete Reciprocal Rank Fusion (90 % Fachrang, 10 % Vektorrang, Konstante 60) |

Die Gewichtung wurde nur am Development-Split bewertet. Holdout-Fälle werden danach nicht zum
Nachstimmen verwendet. Fällt das Query-Embedding bei Hybrid oder RRF aus, wird innerhalb desselben
V2-Kandidatenbestands kontrolliert auf strukturiertes Ranking zurückgefallen und
`usedStructuredFallback=true` ausgegeben. Ein HANA-Ausfall wird nicht verschleiert und bleibt
HTTP 503.

## Evaluation

| Bestand | Fälle | Nutzung | Prüfsumme |
| --- | ---: | --- | --- |
| `rag-eval-v1-frozen` | 40 | unveränderliche Baseline | `2b1e3e8…e79368f3c` |
| `rag-quality-dev-v2` | 50 (45/5) | Ranking-Entwicklung | `61d5d35f…e4f6172` |
| `rag-quality-holdout-v2` | 50 (45/5) | einmalige finale Auswahl | `37acf2e6…17dbb` |

Die V2-Quellen überschneiden sich weder mit den 35 V1-Quellfällen noch untereinander. Je Split
sind 15 positive und zwei No-Result-Fälle als fachlich geprüfte Stichprobe markiert. Labels werden
vor der Auswertung deterministisch erzeugt und anschließend nur per Prüfsumme gelesen.

Metriken: Recall@5, Precision@5, MRR, nDCG@5, No-Result Accuracy, Candidate Recall sowie getrennte
p50/p95 für HANA und die vollständige Kette inklusive Query-Embedding. Candidate Recall ist der
Anteil relevanter Labels, die Reparaturcode-, Zeit-, Klassifikations- und Versionsfilter passieren.

Die Development-Auswertung wählte RRF mit Recall@5 0,9911. Auf dem anschließend einmalig
ausgeführten Holdout erreichte RRF Recall@5 und Precision@5 0,9822, MRR 1,0000, nDCG@5 0,9950,
Candidate Recall und No-Result Accuracy jeweils 1,0000. Structured erreichte auf demselben
Holdout nur 0,5822 Recall@5. Das Qualitätsgate 0,80 ist damit geschlossen. Sämtliche Einzelwerte
und Latenzen stehen in `acceptance-rag-02.md`.

## Kosten und Betrieb

Das Modell bleibt `text-embedding-3-small` mit 1.536 Dimensionen. Laut
[offizieller OpenAI-Dokumentation](https://developers.openai.com/api/docs/guides/embeddings)
sind 1.536 die Standarddimensionen; die API unterstützt eine explizite Dimensionsangabe.
Die [offizielle Modellseite](https://developers.openai.com/api/docs/models/text-embedding-3-small)
nennt 0,02 USD pro eine Million Input-Tokens (Stand der Abnahme). V2 indexiert 2.000 kurze
Dokumente: 1.000 Vorabprofile und 1.000 getrennte Abschlussbelege. Der reale Erstlauf verbrauchte
138.195 Input-Tokens beziehungsweise rechnerisch 0,0027639 USD. Der unveränderte Zweitlauf meldete
2.000 unveränderte Dokumente, null Embeddings und null Provider-Batches und verursachte somit
keine weiteren Dokument-Embedding-Kosten.

## Reproduktion

```powershell
dotnet run --project tools/ARI.DatabaseMigrator -- database/hana

dotnet run --project tools/ARI.RagTool -- index-v2 `
  artifacts/ari-suspension-synthetic-v1.json

dotnet run --project tools/ARI.RagTool -- prepare-quality-v2 `
  artifacts/ari-suspension-synthetic-v1.json `
  artifacts/rag/retrieval-evaluation-set.json `
  artifacts/rag/quality-development-set.json `
  artifacts/rag/quality-holdout-set.json

dotnet run --project tools/ARI.RagTool -- evaluate-v2 `
  artifacts/rag/quality-development-set.json `
  artifacts/rag/quality-development-report.json

dotnet run --project tools/ARI.RagTool -- evaluate-v2 `
  artifacts/rag/quality-holdout-set.json `
  artifacts/rag/quality-holdout-report.json
```

Zugangsdaten werden vorher aus .NET User Secrets in Prozessvariablen geladen und niemals in
Artefakte geschrieben.

## Grenzen

- Die Daten und Labels sind synthetisch; markierte Prüfung ersetzt kein Werkstatt-Expertenpanel.
- Exakte HANA-Cosine-Suche ist bei 1.000 Fällen angemessen, aber kein Lasttest für Millionen Daten.
- Die RRF-Gewichtung ist bewusst konservativ. Das Vektorsignal ergänzt die Fachbaseline; die
  Freigabe beruht auf dem unabhängigen Holdout, nicht auf einer nachträglichen Zielanpassung.
- Die initiale Indexierung benötigt wegen einzelner transaktionaler Delete-/Insert-Roundtrips
  rund 13 Minuten. Idempotente Wiederholung dauert nur Sekunden; Array Binding ist eine spätere
  Performanceoptimierung und keine Abnahmevoraussetzung.
- Mandanten- und Zeilenberechtigungen sind im MVP durch die ausschließlich synthetische
  Datenklassifikation repräsentiert; echte Tenant-Policies gehören in eine spätere Ausbaustufe.
