## Plan: D365-Xray Vollanalyse Ausbau

Wir erweitern d365-xray auf einen umfassenden Dataverse-Analyzer mit API plus optionalem TDS/SQL-readonly Pfad, ergänzen vollwertige 2:n und n:n Vergleichsmodi per CLI, bauen Deep Links für Power Automate und klassische Workflows sauber aus, modernisieren das HTML-Reporting deutlich, und sichern den Rollout über Unit- und Integration-Tests. README wird explizit als letzter inhaltlicher Schritt aktualisiert; danach erfolgt Commit und Push.

**Steps**
1. Phase 1: Scope- und Architektur-Freeze
1.1 Definiere den funktionalen Zielumfang für v1 als Pflichtdomänen: Solutions, Components, Layers, Dependencies, Plugins/SDK Steps, Workflows/Flows, Business Rules, Environment Variables, Settings, Connections/Endpoints, Forms/Views/Charts/App Modules, Security/RBAC, Entity/Attribute-Metadaten.
1.2 Lege Datenquellen-Strategie fest: Dataverse Web API bleibt primär; TDS/SQL-readonly wird als optionale Quelle hinter Feature Flag ergänzt (kein Hard-Fail, graceful fallback auf API).
1.3 Definiere explizite Out-of-Scope Grenzen für v1, um Lieferbarkeit zu sichern: tenantweite Governance-Artefakte außerhalb Environment-Kontext, Schreiboperationen, nicht unterstützte SQL-Provider.

2. Phase 2: Connector-Erweiterung für Vollanalyse
2.1 Erweitere das Snapshot-Modell in Core um neue Artefaktlisten für Forms, Views, Charts, App Modules, Roles/Privileges/Field Security, Entity/Attribute-Metadaten und SQL-Telemetrie-Herkunft je Datensatz.
2.2 Implementiere neue Collector in Connectors für API-basierte Erfassung der neuen Domänen mit Paging, Retry und graceful degradation analog bestehendem Collector-Muster.
2.3 Ergänze optionalen SQL-readonly Collector-Pfad (TDS) als ergänzende Quelle mit Konfiguration pro Lauf; mergen der Daten in ein konsistentes Snapshot-Objekt mit Provenance-Marker.
2.4 Ergänze Validierungs- und Normalisierungslogik (stabile IDs, Schlüssel, Null-Handling), damit spätere Diffs deterministisch bleiben.

3. Phase 3: Diff-Engine auf Baseline plus All-to-All umstellen
3.1 Erweitere CLI um auswählbaren Vergleichsmodus: Baseline-gegen-alle und All-to-All Paarvergleich.
3.2 Erweitere Diff-Engine und Ergebnisstruktur für Pair-Kontext, ohne bestehende Konsumenten zu brechen (kompatible Evolution des Result-Modells).
3.3 Migriere baseline-zentrierte Analyzer schrittweise auf kontextfähige Analyzer; zuerst domänenkritische Analyzer (Missing Components, Workflows, Env Vars, Plugins), danach restliche.
3.4 Stelle sicher, dass Single-Environment Checks auch bei Multi-Environment-Läufen je Environment erfasst und im Report separat markiert werden.

4. Phase 4: Deep Links für Workflows und zusätzliche Artefakte
4.1 Erweitere Finding-Details für Workflow-Typkontext (Classic, ModernFlow, BPF) sowie notwendige Environment-Parameter.
4.2 Implementiere DeepLinkBuilder-Logik für Power Automate Cloud Flows plus robuste Fallbacks auf klassische Dataverse Links.
4.3 Ergänze Deep-Link-Unterstützung für neu erfasste Domänen (Forms, Views, App Modules, Security-Artefakte soweit im UI adressierbar).
4.4 Definiere degradierte Linkausgabe, wenn einzelne IDs/URL-Teile fehlen (kein Fehler, stattdessen erklärender Hinweis im Details-Bereich).

5. Phase 5: Reporting visuell und funktional aufwerten
5.1 Erarbeite ein neues HTML-Design mit klarer Informationshierarchie (Executive Summary, Risiko-KPIs, Vergleichsmatrix, Findings, Inventar).
5.2 Füge interaktive Funktionen hinzu: Volltextsuche, Filter nach Severity/Category/Environment-Paar, sortierbare Abschnitte und persistente UI-States.
5.3 Ergänze Chart-Set für Mehrumgebungs-Szenarien: Pairwise-Diff-Matrix, Category-Heatmap, Risikoentwicklung je Environment.
5.4 Führe Theming/Branding-Struktur ein (CSS-Variablen, typografische Linie, responsives Verhalten Desktop/Mobile), ohne bestehende Report-Artefakte zu brechen.

6. Phase 6: Teststrategie und Qualitätssicherung
6.1 Ergänze Unit-Tests für neue Collector, Normalisierung, Vergleichsmodi, DeepLinkBuilder und Reporting-Filterlogik.
6.2 Ergänze Diff-Tests für Baseline und All-to-All mit deterministischen Fixture-Snapshots inklusive Edge Cases (fehlende Daten, Teilmengen, asymmetrische Unterschiede).
6.3 Aktualisiere/ergänze Integrationstests für echte Dataverse-Läufe mit optionalem SQL-readonly Pfad und Fallback-Verhalten.
6.4 Führe vollständige Testausführung über die Solution aus; Zielkriterium: alle Unit- und Integration-Tests grün.

7. Phase 7: Abschluss, Dokumentation, Commit und Push
7.1 Aktualisiere als letzten inhaltlichen Schritt README mit neuem Funktionsumfang, Vergleichsmodi, Datenquellen, Deep-Link-Abdeckung, Reporting-Features und Testhinweisen.
7.2 Validiere README-Konsistenz gegen CLI-Optionen und tatsächliches Verhalten.
7.3 Erstelle einen klaren Commit mit zusammenfassender Message und pushe auf den vorgesehenen Branch.

**Relevant files**
- c:/_dev/d365-xray/src/D365Xray.Core/Model/EnvironmentSnapshot.cs — Snapshot-Domänen und Provenance erweitern.
- c:/_dev/d365-xray/src/D365Xray.Core/Model/ComparisonResult.cs — Pairwise-Ergebnisstruktur und Kompatibilität.
- c:/_dev/d365-xray/src/D365Xray.Core/ServiceContracts.cs — Service-Verträge für Modus/Ergebnis ggf. erweitern.
- c:/_dev/d365-xray/src/D365Xray.Connectors/DataverseConnector.cs — Orchestrierung neuer Collector und optionaler SQL-Pfad.
- c:/_dev/d365-xray/src/D365Xray.Connectors/Collectors/ — neue Collector für Forms/Views/Charts/App Modules/RBAC/Metadata.
- c:/_dev/d365-xray/src/D365Xray.Diff/SnapshotDiffEngine.cs — Vergleichsmodus Baseline vs All-to-All.
- c:/_dev/d365-xray/src/D365Xray.Diff/*Analyzer.cs — Kontextfähige Analyzer-Migration.
- c:/_dev/d365-xray/src/D365Xray.Reporting/DeepLinkBuilder.cs — Cloud Flow und Workflow-Typ-spezifische Links.
- c:/_dev/d365-xray/src/D365Xray.Reporting/HtmlReportExporter.cs — neues Layout, Filter, Matrix, UX.
- c:/_dev/d365-xray/src/D365Xray.Reporting/MarkdownReportExporter.cs — ergänzte Strukturen und Linkqualität.
- c:/_dev/d365-xray/src/D365Xray.Reporting/JsonReportExporter.cs — erweitertes Ergebnisobjekt.
- c:/_dev/d365-xray/src/D365Xray.Cli/ScanCommand.cs — CLI Optionen für Vergleichsmodus und SQL-readonly.
- c:/_dev/d365-xray/tests/D365Xray.Connectors.Tests/ — Collector- und Fallback-Tests.
- c:/_dev/d365-xray/tests/D365Xray.Diff.Tests/ — Baseline/All-to-All und Determinismus.
- c:/_dev/d365-xray/tests/D365Xray.Reporting.Tests/ — Deep Links, Filter, HTML-Ausgabe.
- c:/_dev/d365-xray/tests/D365Xray.IntegrationTests/ — End-to-End gegen Dataverse inkl. optionalem SQL-Pfad.
- c:/_dev/d365-xray/README.md — letzter inhaltlicher Schritt vor Commit/Push.

**Verification**
1. Build und statische Validierung: dotnet build d365-xray.sln
2. Unit-Tests: dotnet test d365-xray.sln --filter Category!=Integration
3. Integrationstests: dotnet test tests/D365Xray.IntegrationTests --filter Category=Integration
4. Ergebnisvalidierung Baseline-Modus mit 2 Environments: korrekte Findings, Deep Links, Report-Layout.
5. Ergebnisvalidierung All-to-All mit mindestens 3 Environments: Pairwise-Matrix, Filterbarkeit, deterministische JSON-Struktur.
6. Regression-Check: bestehende Output-Dateien report.json/report.md/report.html werden weiterhin erzeugt.

**Decisions**
- SQL wird als optionaler readonly TDS-Pfad integriert, nicht als Ersatz der API.
- Vergleichsmodus wird per CLI wählbar: Baseline und All-to-All.
- Vollanalyse-v1 umfasst die von dir bestätigten zusätzlichen Domänen inkl. RBAC und Metadaten.
- README-Update erfolgt als finaler inhaltlicher Schritt unmittelbar vor Commit und Push.
- Integrationstests nutzen lokale Secret-Verwaltung (dotnet user-secrets oder CI Secret Store); keine Zugangsdaten im Repository oder in Dokumentation.
- Für Integrationstest-Ausführung wird zusätzlich eine konkrete Dataverse Environment-URL benötigt; ohne diese bleibt die Testausführung blockiert.
- Integrationstests werden gegen https://orgac796185.crm4.dynamics.com geplant; nach Testdurchlauf ist Secret-Rotation als Pflichtschritt vorgesehen.

**Further Considerations**
1. SQL-TDS Feature Flag Empfehlung: standardmäßig aus, aktivierbar über explizite CLI-Option zur Risikominimierung.
2. Für große n:n Läufe Empfehlung: optionales Pair-Limit oder Sampling-Modus als späterer Performance-Hebel.
3. Für visuelles Redesign Empfehlung: zuerst Informationsarchitektur und Datendichte fixieren, danach Design-Politur.
