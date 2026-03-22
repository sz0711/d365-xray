# Plan: HTML Report Visual Overhaul & Feature Enrichment

## TL;DR
Transform the current minimal HTML report into an impressive, dashboard-style report with interactive Chart.js charts, deep links to Dynamics 365 / Power Automate, environment inventory statistics, a dark mode toggle, and full Finding.Details rendering. Requires model changes (new `EnvironmentSummary` record + GUIDs in Finding.Details), analyzer updates, and a complete HTML exporter rewrite.

---

## Phase 1: Model & Data Layer (blocking — all other phases depend on this)

### Step 1.1 — Add `EnvironmentSummary` record to Core model
- New file: `src/D365Xray.Core/Model/EnvironmentSummary.cs`
- Record: `EnvironmentSummary { EnvironmentDisplayName, SolutionCount, ComponentCount, WorkflowCount, PluginAssemblyCount, SdkStepCount, WebResourceCount, ConnectionReferenceCount, EnvironmentVariableCount, BusinessRuleCount, CustomConnectorCount, ServiceEndpointCount }`
- Purpose: Powers the "Environment Inventory" section in the report

### Step 1.2 — Add `EnvironmentSummaries` to `RiskReport`
- File: `src/D365Xray.Core/Model/RiskReport.cs`
- Add property: `IReadOnlyList<EnvironmentSummary> EnvironmentSummaries { get; init; } = [];`
- Non-breaking: defaults to empty list

### Step 1.3 — Populate summaries in `ScanCommand` pipeline
- File: `src/D365Xray.Cli/ScanCommand.cs`
- After capturing snapshots, build `EnvironmentSummary` for each from snapshot counts
- Pass to `RiskReport` construction (currently built by `RiskRuleEngine.Evaluate()`; may need to thread summaries through or build in ScanCommand after scoring)
- **Key decision**: Either extend `IRiskScorer.Evaluate()` to accept snapshots, or let `ScanCommand` create the summary and attach it after `Evaluate()` using `with { EnvironmentSummaries = ... }`. **Recommended**: attach after Evaluate via `with` — simplest, no IRiskScorer contract change.

### Step 1.4 — Enrich Finding.Details with component GUIDs in analyzers
- Files to modify (add GUIDs to Details dict):
  - `src/D365Xray.Diff/WorkflowDriftAnalyzer.cs` — add `WorkflowId` (from `WorkflowDefinition.WorkflowId`)
  - `src/D365Xray.Diff/SingleEnvironmentAnalyzer.cs` — add `SolutionId` for unmanaged solutions, `StepId` for disabled steps, `WorkflowId` for deactivated workflows, `BusinessRuleId` for deactivated rules, `ConnectionReferenceId` for orphaned refs, `DefinitionId` for env vars
  - `src/D365Xray.Diff/ConnectionDriftAnalyzer.cs` — add `ConnectionReferenceId`, `ServiceEndpointId`, `ConnectorId`
  - `src/D365Xray.Diff/PluginAnalyzer.cs` — add `PluginAssemblyId`, `StepId`
  - `src/D365Xray.Diff/WebResourceDriftAnalyzer.cs` — add `WebResourceId`
  - `src/D365Xray.Diff/BusinessRuleDriftAnalyzer.cs` — add `BusinessRuleId`
  - `src/D365Xray.Diff/SolutionDriftAnalyzer.cs` — add `SolutionId`
  - `src/D365Xray.Diff/EnvironmentVariableDriftAnalyzer.cs` — add `DefinitionId`
  - `src/D365Xray.Diff/MissingComponentAnalyzer.cs` — add `ComponentId`
  - `src/D365Xray.Diff/LayerOverrideAnalyzer.cs` — add `ComponentId`
- Pattern: `Details = new Dictionary<string, string> { ... ["ComponentId"] = guid.ToString(), ... }`
- Also add `EnvironmentUrl` to each finding's Details so the HTML renderer can construct links without cross-referencing

### Step 1.5 — Add deep link helper utility
- New file: `src/D365Xray.Reporting/DeepLinkBuilder.cs`
- Static utility class to construct Dynamics 365 and Power Platform URLs from environment URL + entity type + GUID
- URL patterns:
  - **Solution (maker portal)**: `https://make.powerapps.com/environments/{envId}/solutions/{solutionId}` (needs envId mapping) OR fallback: `{orgUrl}/tools/solution/edit.aspx?id={solutionId}`
  - **Workflow/Flow (D365)**: `{orgUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=workflow&id={workflowId}`
  - **Modern Flow (Power Automate)**: `https://make.powerautomate.com/environments/{envId}/flows/{workflowId}/details`
  - **Plugin Step**: `{orgUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=sdkmessageprocessingstep&id={stepId}`
  - **Web Resource**: `{orgUrl}/main.aspx?forceUCI=1&pagetype=webresource&webresourceName={name}`
  - **Environment Variable**: `{orgUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=environmentvariabledefinition&id={defId}`
  - **Connection Reference**: `{orgUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=connectionreference&id={connRefId}`
- **Note**: For Power Automate links, we'd need the actual Power Platform environment GUID. For V1, use the Dynamics 365 URL pattern which works within the org URL. Can add Power Platform environment ID collection later.

---

## Phase 2: HTML Report Rewrite (depends on Phase 1)

### Step 2.1 — Complete CSS overhaul
- File: `src/D365Xray.Reporting/HtmlReportExporter.cs` → `AppendCss()` method
- New design system:
  - CSS custom properties for full theming (light + dark mode)
  - Card-based layout with `box-shadow`, rounded corners, subtle gradients
  - Dashboard grid layout (CSS Grid) for executive summary KPIs
  - Better typography: larger headings, improved spacing, section separators
  - Animated risk gauge (CSS keyframe for score reveal)
  - Responsive breakpoints for mobile (already partial, needs enhancement)
  - Print stylesheet (`@media print`) for clean printing
  - **Dark mode toggle**: CSS class-based toggle + small inline JS snippet for button click handler and localStorage persistence

### Step 2.2 — Executive summary dashboard section
- Replace the simple risk badge with a full dashboard header:
  - Large circular gauge/ring showing overall risk score (SVG circle with `stroke-dasharray`)
  - KPI cards row: Total Findings | Critical | High | Medium | Low | Info
  - Color-coded severity pill counts
  - Scan timestamp, tool version, duration (if available from `Metadata.CapturedDuration`)

### Step 2.3 — Chart.js integration (via CDN)
- Add `<script src="https://cdn.jsdelivr.net/npm/chart.js@4/dist/chart.umd.min.js"></script>` to `<head>`
- Charts to add:
  1. **Severity Distribution Donut** — doughnut chart showing finding counts by severity using the severity color palette
  2. **Findings by Category Bar Chart** — horizontal bar chart with category names, colored by max severity in that category
  3. **Risk Score Gauge** — using Chart.js doughnut in half-circle mode (or keep as pure SVG — simpler)
- Inline `<script>` block at bottom with Chart.js initialization
- Charts render into `<canvas>` elements placed in the executive summary area

### Step 2.4 — Environment inventory section
- New section after "Compared Environments"
- Table or card layout showing per-environment stats from `EnvironmentSummaries`:
  - Solutions, Components, Workflows, Plugins, SDK Steps, Web Resources, Connections, Env Vars, Business Rules, Custom Connectors, Service Endpoints
  - With icons or badges for each category
  - DataverseVersion from EnvironmentInfo (already available)

### Step 2.5 — Enhanced findings section with deep links and details
- Each finding card gets:
  - **Deep link button/icon**: clickable link to open the component in D365/Power Automate (using `DeepLinkBuilder`)
  - **Details table**: render all key-value pairs from `Finding.Details` in a compact table (currently ignored!)
  - **AI annotation** (when available): show `FindingAnnotation.Commentary` and `SuggestedActions` in a visually distinct AI callout box with provenance disclaimer
  - Better visual hierarchy: severity badge, title, description, details accordion, links row
  - Copy-to-clipboard for finding IDs

### Step 2.6 — AI Enrichment section
- If `AiEnrichment` is not null, render:
  - AI Executive Summary (with provenance banner)
  - Per-finding annotations inline with findings (handled in 2.5)
  - Model/adapter metadata in footer

### Step 2.7 — Footer enhancement
- Tool version, generation timestamp, schema version
- Link to d365-xray documentation/repo
- Print button

---

## Phase 3: Markdown Report Enhancement (parallel with Phase 2)

### Step 3.1 — Add environment inventory table
- Render `EnvironmentSummaries` as a markdown table

### Step 3.2 — Add Finding.Details to markdown output
- Render each finding's Details dict as an indented key–value list below the description

### Step 3.3 — Extract shared helper methods
- Extract `GetCategoryScope()` and `IsCategoryApplicable()` from both HTML and Markdown exporters into a shared static utility class (e.g., `CategoryHelper.cs`) to eliminate duplication
  - Currently duplicated identically in both exporters

---

## Phase 4: Tests (parallel with Phases 2+3)

### Step 4.1 — Update existing tests
- Update `Html_ContainsRiskBadge` and similar tests for new HTML structure
- Add test for Chart.js `<canvas>` elements present in HTML
- Add test for dark mode toggle button present

### Step 4.2 — New tests
- `Html_ContainsDeepLinks_WhenDetailsHaveGuids` — verify `<a href>` with D365 URL pattern
- `Html_RendersFindingDetails` — verify Details dict rendered as table
- `Html_ContainsEnvironmentInventory_WhenSummariesPresent`
- `Html_ContainsChartCanvas` — verify `<canvas id="severityChart">` etc.
- `DeepLinkBuilder_ConstructsValidUrl_ForEachEntityType` — unit tests for each URL pattern
- `Markdown_ContainsFindingDetails`
- `Markdown_ContainsEnvironmentInventory`

### Step 4.3 — Mandatory test gate before completion
- Completion criteria: no handoff/finish until `dotnet build` and `dotnet test` are green
- If any test fails: fix first, rerun, and only then proceed to documentation update

---

## Phase 5: Finalization & Documentation (depends on Phase 4)

### Step 5.1 — Regenerate sample outputs
- Run a representative scan to regenerate `output/report.html`, `output/report.md`, and `output/report.json`
- Manually validate deep links and chart rendering in the generated HTML

### Step 5.2 — Update README after everything is done
- File: `README.md`
- Update report feature description to include:
  - dashboard-style HTML with charts
  - dark mode toggle
  - deep links to Dynamics artifacts (and flow links where available)
  - environment inventory section
  - enriched finding details
- Ensure output file names in README match actual exporter outputs

---

## Relevant Files

**Model changes:**
- `src/D365Xray.Core/Model/EnvironmentSummary.cs` — NEW: environment statistics record
- `src/D365Xray.Core/Model/RiskReport.cs` — add `EnvironmentSummaries` property

**Analyzer enrichment (add GUIDs to Details):**
- `src/D365Xray.Diff/SingleEnvironmentAnalyzer.cs` — add SolutionId, StepId, WorkflowId, etc.
- `src/D365Xray.Diff/WorkflowDriftAnalyzer.cs` — add WorkflowId
- `src/D365Xray.Diff/ConnectionDriftAnalyzer.cs` — add ConnectionReferenceId, ServiceEndpointId
- `src/D365Xray.Diff/PluginAnalyzer.cs` — add PluginAssemblyId, StepId
- `src/D365Xray.Diff/WebResourceDriftAnalyzer.cs` — add WebResourceId
- `src/D365Xray.Diff/BusinessRuleDriftAnalyzer.cs` — add BusinessRuleId
- `src/D365Xray.Diff/SolutionDriftAnalyzer.cs` — add SolutionId
- `src/D365Xray.Diff/EnvironmentVariableDriftAnalyzer.cs` — add DefinitionId
- `src/D365Xray.Diff/MissingComponentAnalyzer.cs` — add ComponentId
- `src/D365Xray.Diff/LayerOverrideAnalyzer.cs` — add ComponentId

**Pipeline:**
- `src/D365Xray.Cli/ScanCommand.cs` — build EnvironmentSummary from snapshots, attach to RiskReport

**Reporting:**
- `src/D365Xray.Reporting/DeepLinkBuilder.cs` — NEW: URL construction utility
- `src/D365Xray.Reporting/HtmlReportExporter.cs` — complete rewrite of Build() + AppendCss()
- `src/D365Xray.Reporting/MarkdownReportExporter.cs` — add inventory + details rendering
- `src/D365Xray.Reporting/CategoryHelper.cs` — NEW: extract shared `GetCategoryScope` / `IsCategoryApplicable` (currently duplicated)

**Tests:**
- `tests/D365Xray.Reporting.Tests/UnitTest1.cs` — update + extend

**Documentation:**
- `README.md` — update feature set/output section after all tests pass

---

## Verification

1. `dotnet build` — ensure all projects compile (**mandatory gate**)
2. `dotnet test` — all existing + new tests pass (**mandatory gate**)
3. Run a real scan against the DEV environment → open `output/report.html` in browser → visually verify:
   - Dashboard layout with KPI cards and charts
   - Dark mode toggle works and persists across page reload
   - Deep links open correct D365 pages in new tab
   - Finding details tables show all key-value pairs
   - Environment inventory shows correct counts
   - Charts render (requires internet for CDN)
   - Print preview looks clean
4. Verify `report.md` shows new inventory table and finding details
5. Verify `report.json` includes `environmentSummaries` array
6. After all checks are green, update `README.md` to document the delivered report improvements and final output behavior

---

## Decisions

- **Chart.js via CDN**: User chose CDN over self-contained. Report needs internet on first open (Chart.js is cached after). Fallback: charts degrade gracefully (show tables) when offline.
- **Dark mode**: Manual toggle button with localStorage persistence, not OS auto-detect.
- **Environment inventory**: Requires threading snapshot data to RiskReport. Use `with` pattern after `Evaluate()` to keep `IRiskScorer` contract unchanged.
- **Deep links V1**: Use Dynamics 365 org URL patterns (`{orgUrl}/main.aspx?forceUCI=1&...`). Power Automate maker portal links (`make.powerautomate.com`) deferred — would need Power Platform environment GUID collection from admin API.
- **Scope boundary**: JSON report gets `environmentSummaries` but no visual changes. Markdown gets inventory + details. HTML gets full visual overhaul.
