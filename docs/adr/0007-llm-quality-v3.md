# ADR 0007 – Evidenzbasierte zweistufige LLM-Extraktion

Status: Accepted, 28. Oktober 2026

## Entscheidung

ARI verwendet `repair-extraction-v3` als Standard. Ein einzelner OpenAI-Structured-Output-Aufruf
extrahiert die Reparaturdaten. Für die kleine geschlossene Komplikationstaxonomie klassifiziert
anschließend ein deterministischer, negationsbewusster Adapter ausschließlich explizite Sätze.
Eine anwendungseigene Quality Policy vergibt `ACCEPTED`, `PARTIAL`, `NEEDS_REVIEW` oder `REJECTED`.

## Begründung

V1 und V2 hatten perfekte Precision, aber unzureichenden Recall. Ein längerer V2-Prompt erhöhte die
Abweisungen und verschlechterte F1. Die zweistufige Strategie trennt sprachliches Parsing von der
reproduzierbaren Zuordnung weniger Fachcodes. Jeder Code behält wörtliche Evidence und durchläuft
dieselben Domain- und Temporal-Validatoren.

## Folgen

- Holdout Micro-F1 steigt von V1 0,690 auf V3 1,000.
- Durchschnittslatenz steigt im Holdout von 2.772 auf 3.785 ms.
- Neue Codes oder Formulierungen erfordern Taxonomie-, Test- und Versionsänderung.
- Das Ergebnis ist für den synthetischen MVP belegt, nicht für beliebige reale Werkstatttexte.
