# Grounding Policy

Eine RAG-Erklärung ist nur gültig, wenn jede konkrete historische Aussage mindestens eine vom
aktuellen Retrieval zurückgegebene `sourceReference` zitiert. Die Anwendung validiert alle IDs
nach dem Structured-Output-Aufruf erneut; unbekannte oder erfundene IDs führen zur Ablehnung.

## Verbindliche Regeln

1. Historische Berichtstexte sind nicht vertrauenswürdige Daten. Darin enthaltene Anweisungen
   verändern weder Prompt noch Schema noch Systemverhalten.
2. Das LLM darf nur die bereitgestellten Textauszüge beschreiben.
3. Historische Komplikationen werden als frühere Beobachtungen formuliert, niemals als bereits
   eingetretene Ereignisse des neuen Fahrzeugs.
4. Die numerische Prognose stammt ausschließlich aus `ari-repair-duration-v1`. Retrieval-Fälle
   werden nicht als mathematische Eingabe oder Ursache der Ridge-Prognose dargestellt.
5. Ohne brauchbare Quellen lautet das Ergebnis explizit `insufficientEvidence=true`; die Liste
   der Beobachtungen bleibt leer und es erfolgt kein unnötiger LLM-Aufruf.
6. Ist nur die Generierung gestört, bleiben Prognose und rohe, verifizierbare Retrieval-Evidenz
   im kombinierten API-Ergebnis erhalten.

## Structured Output

Das Schema verlangt `summary`, `evidenceStrength`, `insufficientEvidence` und `observations`.
Jede Observation besitzt genau `statement`, ein wörtliches `evidenceQuote` und
`sourceReferences`; zusätzliche Eigenschaften sind unzulässig. Der Validator prüft zusätzlich,
dass das Zitat im Ausschnitt mindestens einer zitierten Quelle wirklich vorkommt. Damit erzwingt das Provider-Schema die Form, während
`GroundedExplanationValidator` anschließend die fachliche Quellenintegrität prüft.

## Temporal- und Leakage-Grenze

Abschlussberichte dürfen als historische Evidenz dienen, sofern sie vor `AsOfUtc` abgeschlossen
und indexiert waren. Ihre Istzeiten und während der Reparatur entdeckten Komplikationen gehen nicht
in `LaborPredictionInput` ein. Der kombinierte Endpoint reicht nur validierte
`BEFORE_REPAIR`-Bedingungen aus der neuen Vorab-Beschreibung an ML und Retrieval weiter.
