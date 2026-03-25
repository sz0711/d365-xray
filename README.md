# 🔬 d365-xray

> Deep analysis & comparison tool for Microsoft Dynamics 365 / Dataverse environments.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#license)
[![Status](https://img.shields.io/badge/status-V1%20Sprint%204%20Complete-blue)]()

---

## Overview

CLI tool that connects to one or more Dataverse environments, captures read-only snapshots of **21 artifact types**, compares them via **16 cross-env analyzers**, scores risk with **44+ rules**, and exports reports in JSON, Markdown, and an **interactive HTML dashboard**.

### Key Capabilities

- **Multi-Environment Connect** — Azure Identity auth (ClientSecret, Interactive, DeviceCode, Default)
- **21 Artifact Collectors** — Solutions, plugins, SDK steps, workflows, business rules, security roles, entity metadata, environment variables, connection references, web resources, forms, views, charts, app modules, field security profiles, and more
- **16 Cross-Env Analyzers + Single-Env Checks** — Baseline or AllToAll comparison modes
- **44+ Risk Rules** — Severity levels: Low / Medium / High / Critical
- **AI Enrichment** — Optional pluggable adapter with provenance markers
- **CI/CD Exit Codes** — `0` OK, `2` Critical Risk, `3` Config Error

### HTML Report Features

The self-contained HTML report includes:

- **Dashboard** with SVG risk gauge and 6 clickable KPI cards
- **ALM Maturity Score** — 6-dimension composite (solution management, plugin architecture, audit coverage, env configuration, settings compliance, security governance)
- **Executive Summary** — Auto-generated insights from scan data
- **15 deep-dive sections** — Environment inventory, solution breakdown, custom artifacts, settings audit, plugin registration map, security posture, environment variables, entity governance, severity analysis, coverage matrix, findings with deep links
- **Action Items & Remediation Roadmap** — Prioritized P1–P3 recommendations with section cross-links
- **Sticky TOC sidebar** with scroll-spy navigation
- **Dark mode**, **print CSS**, Chart.js charts, interactive findings filter, Microsoft artifact filtering

---

## Architecture

```
src/
├── D365Xray.Core           # Domain model, service contracts (0 deps)
├── D365Xray.Connectors     # Dataverse Web API client, auth, 21 collectors
├── D365Xray.Diff           # Snapshot diff engine, 16 analyzers
├── D365Xray.Risk           # 44+ risk rules, rule engine
├── D365Xray.Reporting      # JSON / Markdown / HTML exporters
└── D365Xray.Cli            # System.CommandLine 2.0.5 entry point
```

**103 source files** · **160 tests** (150 unit + 10 integration) · **0 warnings**

### Pipeline

```
Connect → Snapshot → Diff → Risk Score → (AI Enrich) → Export
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.201+)
- Azure App Registration with Dataverse permissions, registered as **Application User** in Power Platform

### Build & Test

```bash
dotnet build d365-xray.sln
dotnet test  d365-xray.sln --filter "Category!=Integration"
```

### CLI Usage

```bash
# Single environment — ClientSecret
dotnet run --project src/D365Xray.Cli -- scan \
  --env https://orgXXXXXX.crm4.dynamics.com \
  --name Dev \
  --auth ClientSecret \
  --tenant-id <TENANT_ID> --client-id <APP_ID> --client-secret <SECRET>

# Two-environment comparison (Baseline mode)
dotnet run --project src/D365Xray.Cli -- scan \
  --env https://dev.crm4.dynamics.com https://prod.crm4.dynamics.com \
  --name Dev Prod \
  --auth ClientSecret \
  --tenant-id <TENANT_ID> --client-id <APP_ID> --client-secret <SECRET>

# Interactive browser auth
dotnet run --project src/D365Xray.Cli -- scan \
  --env https://orgXXXXXX.crm4.dynamics.com --name Prod --auth Interactive --client-id <APP_ID>

# AllToAll comparison (every pair)
dotnet run --project src/D365Xray.Cli -- scan \
  --env https://dev.crm4.dynamics.com https://test.crm4.dynamics.com https://prod.crm4.dynamics.com \
  --name Dev Test Prod --auth ClientSecret \
  --tenant-id <TENANT_ID> --client-id <APP_ID> --client-secret <SECRET> \
  --comparison-mode AllToAll
```

### CLI Options

| Option | Required | Description |
|--------|----------|-------------|
| `--env` / `-e` | ✅ | Dataverse environment URL(s) |
| `--name` / `-n` | | Display name(s) for environments |
| `--auth` / `-a` | | `Default`, `ClientSecret`, `Interactive`, `DeviceCode` |
| `--tenant-id` | | Entra ID tenant |
| `--client-id` | | App registration client ID |
| `--client-secret` | | Client secret |
| `--output` / `-o` | | Output directory (default: `./output`) |
| `--comparison-mode` / `-m` | | `Baseline` (default) or `AllToAll` |

### Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success (Low/Medium risk) |
| `2` | Critical risk (score > 75) |
| `3` | Config error |

---

## Authentication

| Method | Use Case | Required |
|--------|----------|----------|
| `Default` | Auto-detect (env vars → MI → VS/CLI → browser) | — |
| `ClientSecret` | CI/CD, service principal | `--tenant-id`, `--client-id`, `--client-secret` |
| `Interactive` | Developer workstation | `--client-id` |
| `DeviceCode` | Headless / SSH | `--client-id` |

**Setup:** Create App Registration in Entra ID → add `Dynamics CRM` / `user_impersonation` permission → register as Application User in Power Platform Admin Center.

---

## Integration Tests

```bash
cd tests/D365Xray.IntegrationTests
dotnet user-secrets set "Dataverse:EnvironmentUrl" "https://yourorg.crm4.dynamics.com"
dotnet user-secrets set "Dataverse:TenantId" "<TENANT_ID>"
dotnet user-secrets set "Dataverse:ClientId" "<APP_ID>"
dotnet user-secrets set "Dataverse:ClientSecret" "<SECRET>"

dotnet test --filter "Category=Integration"
```

Tests are **auto-skipped** when credentials are not configured.

---

## Output

Reports are written to `--output` (default `./output`):

| File | Description |
|------|-------------|
| `report.json` | Machine-readable full data |
| `report.md` | Human-readable Markdown |
| `report.html` | Interactive dashboard (15 sections, charts, dark mode, TOC, print-ready) |

---

## Technology Stack

| Component | Version |
|-----------|---------|
| .NET | 10.0 LTS |
| System.CommandLine | 2.0.5 GA |
| Azure.Identity | 1.19.0 |
| Microsoft.Extensions.Hosting | 10.0.5 |
| xUnit | 2.x / 3.x |

---

## License

MIT

## Disclaimer

Private project. Provided without warranty. Use at your own risk.

---

> Built with ❤️ for Dynamics 365 administrators and architects.
