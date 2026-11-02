# ADR 0008: Structured-first Retrieval mit getrennten V2-Profilen

- Status: Accepted
- Datum: 2026-10-29

## Kontext

RAG V1 bettet kurze Abschlussberichte ein, während die Relevanzlabels überwiegend auf vorab
bekannten Fahrzeug- und Reparaturdaten beruhen. Semantic und Hybrid blieben dadurch deutlich
hinter der strukturierten Baseline.

## Entscheidung

V1 bleibt unverändert. V2 führt eine eigene HANA-Tabelle und zwei Dokumenttypen ein:
`PRE_REPAIR_PROFILE` für Suche und `COMPLETION_EVIDENCE` für nachgelagerte Belege. Kandidaten
werden zuerst durch Reparaturkompatibilität, Zeitgrenze, Datenklassifikation und Indexversion
begrenzt. Danach vergleichen wir Domain Semantic, einen normierten Structured-first Hybridscore
und gewichtete Reciprocal Rank Fusion. Parameter werden nur am Development-Split gewählt; der
Holdout entscheidet über die Freigabe. Ein Embedding-Ausfall fällt bei Hybrid/RRF kontrolliert
auf strukturiertes V2-Ranking zurück.

## Konsequenzen

- Kein Temporal Leak aus Abschlussdaten in Kandidatensuche oder Ridge-Features.
- V1 und V2 bleiben unabhängig reproduzierbar.
- Zwei Vektoren pro Reparaturfall erhöhen Index- und Providerkosten geringfügig.
- RRF vermeidet die direkte Gleichsetzung inkompatibler Score-Skalen.
- Ein HANA-Ausfall bleibt ein echter Abhängigkeitsfehler; nur ein Query-Embedding-Ausfall darf
  den sicheren strukturierten Fallback aktivieren.
