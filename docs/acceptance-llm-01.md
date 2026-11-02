# ARI-LLM-01 – Abnahmeprotokoll

Stand: 26. Oktober 2026  
Aktueller Stand: **19/19 technisch und methodisch erfüllt**

| # | Definition of Done | Status / Evidenz |
| ---: | --- | --- |
| 1 | Bestehende Core-Verträge verwendet | Erledigt – `IFeatureExtractor` gezielt zum evidenzbasierten Domain-Vertrag erweitert. |
| 2 | Realer Provider angebunden | Erledigt – authentifizierter Responses-API-Aufruf mit `gpt-6-luna`, HTTP 200 und streng strukturiertem Ergebnis. |
| 3 | Credentials/Modell sicher | Erledigt – User Secrets/Environment, keine Secrets im Repository; Timeout/Retry/Endpoint konfigurierbar. |
| 4 | Verbindliches JSON-Schema | Erledigt – Responses API, `json_schema`, `strict: true`, alle Felder required/null-fähig. |
| 5 | Schema- und Fachvalidierung getrennt | Erledigt – strikte Deserialisierung vor `RepairExtractionValidator`. |
| 6 | Fahrzeug/Reparatur extrahierbar | Erledigt – im realen 100-Fälle-Lauf beider Prompts jeweils 100 % Fahrzeug- und Reparaturcode-Exact-Match. |
| 7 | Komplikationen/Evidence extrahierbar | Erledigt; Evidence muss Original-Substring sein. |
| 8 | Unbekannt bleibt null/UNKNOWN | Erledigt; Negativtest und striktes Schema vorhanden. |
| 9 | Zeitliche Trennung | Erledigt; PRE_REPAIR blockiert Komplikationen/Istzeit, ML erhält nur BEFORE_REPAIR. |
| 10 | Promptversionierung | Erledigt – V1 Baseline, V2 domänenbewusst, beide dokumentiert. |
| 11 | Ground Truth | Erledigt – 100 reproduzierbare Fälle, Hash `b59c6863…bbb0ff9`. |
| 12 | Qualitätsmetriken gemessen | Erledigt – Schema, Exact Match, Precision, Recall, F1, unsupported Claims, Leakage, Tokens und Latenz real gemessen. |
| 13 | Zwei Prompts verglichen | Erledigt – 200 reale Provideraufrufe auf identischen 100 Fällen; V1 F1 0,719, V2 F1 0,509. |
| 14 | Halluzination/Fehler/Leakage | Erledigt – Evidence-, Injection-, unbekannt-, Zeit- und Schemafälle automatisiert. |
| 15 | Timeout/429/Cancellation/Ausfall | Erledigt – begrenzte Retries und kontrollierte Fehler, automatisiert getestet. |
| 16 | ASP.NET-Core-API | Erledigt – beide Routen über echten Testhost geprüft. |
| 17 | Live-E2E LLM → Validation → ML | Erledigt – echter Provider und lokales FastAPI/Ridge-Modell lieferten gemeinsam HTTP 200 und 2,487136 h; nur `BEFORE_REPAIR` wurde weitergegeben. |
| 18 | Regressionen bleiben grün | Erledigt – Build 0/0, Core 12/12, API 25/25, HANA 11/11 und Python 24/24 grün. |
| 19 | Dokumentation | Erledigt – Architektur, Prompts, Evaluation, Live-Nachweise, Einschränkungen und reale Token-/Kostenmessung. |

Der Evaluationsbestand ist als `ari-llm-evaluation-v1` mit SHA-256
`b59c6863fad947861df3051d6125abf2d4748f6c53a956100fb3eb231bbb0ff9` eingefroren. Beide Prompts
erreichten 100 % Schema- und Reparaturcode-Genauigkeit sowie 0 unbelegte Claims und 0 zeitliche
Leaks. Das F1-Ziel von 0,85 wurde mit 0,719 (V1) beziehungsweise 0,509 (V2) nicht erreicht. Diese
Zielverfehlung ist samt False-Negative-Analyse dokumentiert und wird nicht durch nachträgliche
Änderung der Testfälle verborgen. Der sichere Prognoseworkflow akzeptiert weiterhin nur
vollständig validierte Vorabinformationen.

Live-Nachweise: einzelner Structured-Output-Smoke-Test HTTP 200; V1/V2-Evaluation mit 200 realen
Provideraufrufen; kombinierter ASP.NET-Core→LLM→Validation→FastAPI→Ridge-Aufruf HTTP 200.

Abschließender Regressionstand: vollständiger .NET-Build mit 0 Fehlern und 0 Warnungen; 12 Core-,
25 API-/Adapter-/Workflow-, 11 reale HANA-Integrations- und 24 Python-Tests bestanden. Der HANA-Lauf
am 26. Oktober 2026 prüfte unter anderem Roundtrips, Transaktionen, Fremd- und Eindeutigkeitsschlüssel,
Unicode/UTC sowie die Qualitätsregeln des importierten 1.000-Fälle-Datensatzes. Zugangsdaten wurden
ausschließlich als temporäre Prozessvariablen verwendet und nicht protokolliert.
