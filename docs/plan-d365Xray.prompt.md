## Plan: Sprint 1 Backlog (Execution-Ready)

Sprint objective: deliver a production-shaped CLI baseline that can ingest read-only Dataverse data from multiple environments, compute deterministic diffs, generate baseline risk scoring, and export English-only analysis outputs.

**Phase 1: Foundation**
1. S1.1 Bootstrap .NET 10 CLI solution structure, test projects, and CI quality skeleton.
2. S1.2 Define canonical snapshot domain model and versioned JSON schema.
3. Dependency: S1.2 depends on S1.1.

**Phase 2: Ingestion**
1. S1.3 Build authentication and connector abstraction (secure config, no secret logging).
2. S1.4 Implement snapshot collectors for solutions, components, layers, dependencies, and selected settings with paging/retry/throttling.
3. Dependency: S1.4 depends on S1.2 and S1.3.

**Phase 3: Comparison Core**
1. S1.5 Implement deterministic diff engine for missing components, layer drift, overrides, dependency conflicts, and settings drift.
2. S1.6 Implement rule-based risk scoring with explainable triggered rules.
3. Dependency: S1.6 depends on S1.5.

**Phase 4: Outputs and Pipeline**
1. S1.7 Generate JSON, Markdown, and static HTML outputs from the same findings model.
2. S1.8 Define CLI workflow and CI exit-code policy by severity/risk threshold.
3. S1.9 Define optional AI adapter contract with markdown custom instructions and explicit AI provenance markers.
4. Dependency: S1.8 depends on S1.7. S1.9 depends on S1.7 and can run in parallel with S1.8.

**Acceptance Criteria**
1. Clean build and tests pass from fresh checkout.
2. Snapshot command works for at least two environments in read-only mode.
3. Compare results are deterministic across repeated runs on identical inputs.
4. Risk scores are explainable and rule-traceable.
5. JSON, Markdown, and HTML outputs contain consistent finding counts.
6. All outputs are English-only.
7. CI mode returns deterministic exit codes aligned with configured thresholds.

**Locked Decisions**
1. Primary implementation language: C# on .NET 10 (LTS).
2. Optional future interactive viewer language: TypeScript.
3. Configuration and custom instructions: Markdown and YAML.
4. Packaging target: portable CLI plus local HTML viewer artifacts.
5. Language policy: all comments, findings, and reports are English-only.

Current repo reference: README.md

If you want, next I can break this into a day-by-day 2-week sprint schedule with owner-friendly work packages and risk buffer allocation.
