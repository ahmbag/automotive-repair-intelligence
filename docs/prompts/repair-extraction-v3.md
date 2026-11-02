# Repair Extraction Prompt V3

Version: `repair-extraction-v3`  
Zweck: evidenzorientierte Ereigniserkennung plus deterministische Domänenklassifikation.

Die ausführbare Promptversion liegt in `RepairExtractionPrompts.cs`. V3 adressiert die in V1 und
V2 gemessenen False Negatives, ohne Evidence-, Schema- oder Zeitregeln abzuschwächen:

1. Das Modell extrahiert weiterhin ausschließlich explizite, wörtlich belegte Fakten über Strict
   Structured Outputs.
2. Der Prompt definiert positive und negative Grenzen für jeden Komplikationscode und trennt
   Ursache, Folgearbeit, Negation und Unsicherheit.
3. Für V3 klassifiziert `RepairComplicationClassifier` die belegten Sätze anschließend mit einer
   deterministischen, negationsbewussten Taxonomie. Der Klassifikator darf keine Information aus
   Fahrzeugzustand, Statistik oder Erfahrungswissen ergänzen.
4. `ExtractionQualityPolicy` bestimmt den finalen Status. Der Providerstatus wird nicht als
   Confidence oder Freigabe verwendet.

Damit bleibt der LLM-Aufruf für Fahrzeug, Auftrag, Vorbedingungen, Zeit und strukturiertes Parsing
zuständig; die kleine geschlossene Komplikationstaxonomie wird reproduzierbar geprüft.
