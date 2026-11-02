# ARI-LLM-02 – Fehleranalyse V1/V2

Grundlage ist die unveränderte Evaluation `ari-llm-evaluation-v1` mit 100 Fällen und SHA-256
`b59c6863fad947861df3051d6125abf2d4748f6c53a956100fb3eb231bbb0ff9`.

## Fehler pro Komplikationsklasse

### V1

| Code | TP | FP | FN | Precision | Recall | F1 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `SEIZED_FASTENER` | 25 | 0 | 11 | 1,000 | 0,694 | 0,820 |
| `BROKEN_BOLT` | 3 | 0 | 11 | 1,000 | 0,214 | 0,353 |
| `DAMAGED_BUSHING_SEAT` | 10 | 0 | 14 | 1,000 | 0,417 | 0,588 |
| `ALIGNMENT_CORRECTION` | 8 | 0 | 0 | 1,000 | 1,000 | 1,000 |

### V2

| Code | TP | FP | FN | Precision | Recall | F1 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `SEIZED_FASTENER` | 17 | 0 | 19 | 1,000 | 0,472 | 0,642 |
| `BROKEN_BOLT` | 3 | 0 | 11 | 1,000 | 0,214 | 0,353 |
| `DAMAGED_BUSHING_SEAT` | 0 | 0 | 24 | 1,000 | 0,000 | 0,000 |
| `ALIGNMENT_CORRECTION` | 8 | 0 | 0 | 1,000 | 1,000 | 1,000 |

V1 erzeugte fünf `NEEDS_REVIEW`-Ergebnisse, V2 dagegen 42. Es gab in beiden Versionen keine False
Positives, keine Schemafehler, keine unbelegten Claims und keine Temporal Leaks.

## Priorisierte Ursachen

1. **Indirekte Ereignisformulierungen:** „failed fastener“, „mounting seat needed attention“ oder
   „additional work around the worn bushing mounting area“ werden inhaltlich erkannt, aber nicht
   zuverlässig einem Katalogcode zugeordnet.
2. **Restriktive V2-Regel:** „nur exakt mappbar, sonst weglassen und NEEDS_REVIEW“ schützt die
   Precision, senkt aber den Recall. Der vollständige Verlust von `DAMAGED_BUSHING_SEAT` erklärt
   einen großen Teil der Verschlechterung von F1 0,719 auf 0,509.
3. **Ursache/Folge-Verwechslung:** Zusatzaufwand und Extraktion sind Folgen; entscheidend ist das
   explizit beschriebene Befestiger- oder Sitzereignis.
4. **Kein Validator-Verlust:** Alle 200 historischen Antworten waren schema- und domainvalide. Die
   korrekten Codes wurden bereits vom Provider ausgelassen, nicht nachträglich verworfen.
5. **Ground Truth:** Der V1-Bestand enthält nur Komplikationen, deren definierte Textphrase im
   Bericht vorkommt. Strukturierte, aber nicht erwähnte Ereignisse werden nicht als erwartetes
   Extraktionslabel gewertet.

V3 adressiert genau die ersten drei Ursachen durch klare Klassenabgrenzung und eine deterministische
zweite Klassifikationsstufe. Die Validatoren bleiben unverändert streng.
