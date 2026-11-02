# Synthetische Datengenerierung verstehen

## Ziel und wichtige Einschränkung

Der Generator erzeugt einen nachvollziehbaren Lern-Datensatz für Software-, Daten- und
ML-Engineering. Seine Zahlen sind **synthetische Modellannahmen und keine Werkstattstatistik**.
Ein späteres Modell kann nachweisen, dass es diese simulierten Zusammenhänge lernt. Daraus folgt
nicht, dass es reale Reparaturzeiten zuverlässig prognostiziert.

## Reproduzierbarkeit

Die Standardkonfiguration lautet:

| Parameter | Wert |
| --- | --- |
| Anzahl | 1.000 Fälle |
| Seed | `20261023` |
| Reparaturfamilie | `SUSPENSION` |
| Zeitraum | 2021-01-01 bis 2025-12-31 |
| Generatorversion | `1.0.0` |
| Dataset-ID | `ari-suspension-synthetic-v1` |

Alle Zufallsentscheidungen verwenden eine eigene, mit dem Seed initialisierte
`random.Random`-Instanz. IDs entstehen deterministisch über UUIDv5 aus Dataset-ID, Datensatzindex
und Entitätstyp. Es gibt keinen Zugriff auf die aktuelle Uhrzeit und keine zufällige UUIDv4.

Die Reihenfolge und der Inhalt der Records werden als kanonisches JSON serialisiert
(`sort_keys=True`, kompakte Trenner, UTF-8). Darüber wird SHA-256 berechnet. Der abgenommene
1.000-Fälle-Lauf besitzt die Prüfsumme:

```text
960588c090ad9092dc98f59721e08ad8df3718d899012ace0b344fb238ff1384
```

Der .NET-Importer kanonisiert die Records unabhängig erneut und lehnt eine abweichende Prüfsumme ab,
bevor eine Datenbankverbindung für den Import verwendet wird.

## Generierungsmodell Schritt für Schritt

1. Ein historisches Reparaturdatum und ein Fahrzeugprofil werden gewählt.
2. Das Modelljahr wird relativ zum historischen Reparaturdatum bestimmt – nicht relativ zu heute.
3. Der Kilometerstand entsteht aus Alter × jährlicher Fahrleistung plus Streuung.
4. Ein latenter Umwelteinfluss, Alter und Kilometerstand fließen gemeinsam in einen Korrosionsscore.
5. Sichtbare Korrosion wird probabilistisch aus diesem Score abgeleitet und ist vor Beginn bekannt.
6. Reparaturcode und Referenzzeit bestimmen die Ausgangsarbeit.
7. Komplikationen werden als einzelne Ereignisse simuliert. Ihre Wahrscheinlichkeiten hängen teilweise
   von den bekannten Faktoren ab, bleiben aber zufällig.
8. Ist-Zeit = gestreute Referenzzeit + Zusatzminuten der Ereignisse + unerklärte Reststreuung.
9. Aus den Ereignissen wird ein variierter Werkstattbericht erzeugt.

Der Generator enthält fünf Operationsvarianten für vordere/hintere beziehungsweise paarweise
Querlenkerarbeiten. Mögliche Ereignisse sind festkorrodierte Befestigung, Bolzenbruch, beschädigter
Buchsensitz und zusätzliche Achsgeometriekorrektur.

## Warum keine starre Formel verwendet wird

Eine Regel wie „Korrosion bedeutet immer 30 % mehr Zeit“ wäre für ML ungeeignet: Das Ziel wäre fast
deterministisch, und ein Modell müsste nur die eingebaute Formel rekonstruieren. Stattdessen erhöht
Korrosion Wahrscheinlichkeiten. Auch Fahrzeuge ohne vorab sichtbare Korrosion können Komplikationen
haben, und bekannte Korrosion muss nicht zwingend zu einem schweren Verlauf führen.

Im abgenommenen Datensatz zeigt sich der gewollte Zusammenhang trotzdem deutlich:

- 334 Fälle mit bekannter Korrosion: mittlere Abweichung **+33,31 Minuten**;
- 666 andere Fälle: mittlere Abweichung **+16,56 Minuten**;
- Komplikationsrate: **64,37 %** gegenüber **38,74 %**.

Das ist eine Eigenschaft dieses Generierungsmodells, keine Aussage über reale Fahrzeuge.

## Berichte und Ground Truth

Jeder Fall enthält sowohl strukturierte Ereignisse als auch einen Textbericht. Textbausteine werden
variiert; bei einem Teil der Fälle ist der Bericht absichtlich weniger vollständig. Damit kann ein
späterer LLM-Schritt gegen die strukturierten Ereignisse als Ground Truth bewertet werden, ohne dass
der Bericht nur eine triviale Kopie der Tabelle ist.

## Ausführung

```powershell
$env:PYTHONPATH = (Resolve-Path "ml/src").Path
python -m ari_ml.synthetic.generator `
  --count 1000 `
  --seed 20261023 `
  --dataset-id ari-suspension-synthetic-v1 `
  --output artifacts/ari-suspension-synthetic-v1.json
```

Massendaten unter `artifacts/` sind ignoriert. Ein kleiner, lesbarer Beispieldatensatz liegt unter
[`ml/examples/synthetic-sample.json`](../ml/examples/synthetic-sample.json).
