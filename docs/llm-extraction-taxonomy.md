# ARI-LLM-02 – Komplikationstaxonomie

| Code | Positiv, wenn explizit dokumentiert | Nicht verwenden für | Gleichzeitige Codes |
| --- | --- | --- | --- |
| `SEIZED_FASTENER` | Schraube/Befestiger/Montagehardware sitzt fest, ist korrodiert, widersteht dem Ausbau oder benötigt kontrolliertes Lösen | bloß sichtbare Korrosion vor Reparatur; gebrochene Schraube ohne Festsetzen | `BROKEN_BOLT`, wenn ein getrennt belegter Bruch folgt |
| `BROKEN_BOLT` | Schraube/Befestiger bricht, reißt ab, versagt oder muss wegen Versagens extrahiert und ersetzt werden | schwergängige, aber intakte Schraube | `SEIZED_FASTENER`, wenn beide Ereignisse belegt sind |
| `DAMAGED_BUSHING_SEAT` | Buchsen-/Montagesitz oder Buchsenaufnahme ist verschlissen/beschädigt oder benötigt korrigierende Reinigung/Vorbereitung | routinemäßige Reinigung ohne problematischen Sitz | andere Codes bei getrennten Belegen |
| `ALIGNMENT_CORRECTION` | Endvermessung/Radgeometrie benötigt zusätzliche Korrektur oder Nachstellung | routinemäßige Endkontrolle ohne Korrektur | andere Codes bei getrennten Belegen |

## Übergreifende Regeln

- Ein Code benötigt einen wörtlichen Beleg aus dem übergebenen Dokument.
- Negierte Aussagen erzeugen keinen Code.
- Möglichkeitsformen und unbestätigte Vermutungen werden nicht als beobachtetes Ereignis übernommen.
- Mehrere Codes sind zulässig, wenn getrennte Textstellen mehrere Ereignisse belegen.
- Abschlussereignisse sind `DURING_REPAIR`; eine abschließende Geometriekorrektur ist
  `AFTER_REPAIR`.
- Vorabtexte können bekannte Bedingungen wie `VISIBLE_CORROSION` enthalten, aber keine bereits
  beobachtete Reparaturkomplikation.
- `VISIBLE_CORROSION` ist kein Synonym für `SEIZED_FASTENER`.

Die Taxonomie entspricht dem bestehenden Katalog. Es wurden keine neuen Fachcodes eingeführt.
