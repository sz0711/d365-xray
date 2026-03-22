# 🔬 d365-xray

> Deep analysis & comparison tool for Microsoft Dynamics 365 / Dataverse environments.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#license)
[![Status](https://img.shields.io/badge/status-V1%20Sprint%201%20Complete-blue)]()

---

## 📋 Overview

**d365-xray** is a CLI tool that connects to one or more Dataverse environments, captures read-only snapshots, compares them, scores risk based on 14 built-in rules, and exports detailed reports in JSON, Markdown, and HTML.

### Key Capabilities

| Feature | Description |
|---------|-------------|
| 🔗 **Multi-Environment Connect** | Authenticate via Azure Identity (ClientSecret, Interactive, DeviceCode, Default) |
| 📸 **Snapshot Capture** | Solutions, components, layers, dependencies, settings, Dataverse version |
| 🔍 **Deterministic Diff** | 5 analyzers compare snapshots and produce reproducible findings |
| ⚠️ **Risk Scoring** | 14 rules assign risk scores; overall level: Low / Medium / High / Critical |
| 🤖 **AI Enrichment (optional)** | Pluggable adapter for AI-powered analysis with provenance markers |
| 📊 **Multi-Format Reports** | JSON (machine-readable), Markdown (docs), HTML (local viewer) |
| 🚦 **CI/CD Exit Codes** | `0` = OK, `2` = Critical Risk, `3` = Config Error |

---

## 🏗️ Architecture

```
d365-xray.sln
├── src/
│   ├── D365Xray.Core           # Domain model, service contracts (0 NuGet deps)
│   ├── D365Xray.Connectors     # Dataverse Web API client, auth, 5 collectors
│   ├── D365Xray.Diff           # Snapshot diff engine, 5 analyzers
│   ├── D365Xray.Risk           # 14 risk rules, rule engine
│   ├── D365Xray.Reporting      # JSON / Markdown / HTML exporters
│   └── D365Xray.Cli            # System.CommandLine 2.0.5 entry point
└── tests/
    ├── D365Xray.Core.Tests           # 13 tests
    ├── D365Xray.Connectors.Tests     #  8 tests
    ├── D365Xray.Diff.Tests           # 12 tests
    ├── D365Xray.Risk.Tests           # 21 tests
    ├── D365Xray.Reporting.Tests      # 16 tests
    └── D365Xray.IntegrationTests     # 10 live Dataverse integration tests
```

**33 source files** · **80 tests** (70 unit + 10 integration) · **0 warnings**

### Pipeline

```
Connect → Snapshot → Diff → Risk Score → (AI Enrich) → Export
```

Each step is behind a clean interface registered via IoC/DI:

| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| `IEnvironmentConnector` | `DataverseConnector` | Capture snapshots |
| `IDiffEngine` | `SnapshotDiffEngine` | Compare environments |
| `IRiskScorer` | `RiskRuleEngine` | Evaluate risk |
| `IReportExporter` | `CompositeReportExporter` | Export reports |
| `IAiAnalysisAdapter` | `NullAiAnalysisAdapter` | AI enrichment (no-op default) |

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.201+)
- An Azure App Registration with Dataverse permissions
- The app registered as an **Application User** in your Power Platform environment

### Build

```bash
dotnet build d365-xray.sln
```

### Run Unit Tests

```bash
dotnet test d365-xray.sln --filter "Category!=Integration"
```

### CLI Usage

> **Important:** The `--name` parameter controls the display name shown in the report header
> (*"Compared Environments"*). Make sure it matches the actual environment type (Dev, Test, Staging, Prod, …).
> If omitted, environments are auto-named `Env1`, `Env2`, etc.

```bash
# ── Single Dev environment – self-analysis ───────────────────
dotnet run --project src/D365Xray.Cli -- scan \
  --env https://orgXXXXXX.crm4.dynamics.com \
  --name Dev \
  --auth ClientSecret \
  --tenant-id <TENANT_ID> \
  --client-id <APP_ID> \
  --client-secret <SECRET>

# ── Single Prod environment – Interactive browser auth ───────
dotnet run --project src/D365Xray.Cli -- scan \
  --env https://orgYYYYYY.crm4.dynamics.com \
  --name Production \
  --auth Interactive \
  --client-id <APP_ID>

# ── Two-environment comparison (Dev vs Prod) ─────────────────
# The first --name maps to the first --env URL, the second to the second, etc.
dotnet run --project src/D365Xray.Cli -- scan \
  --env https://orgXXXXXX.crm4.dynamics.com https://orgYYYYYY.crm4.dynamics.com \
  --name Dev Prod \
  --auth ClientSecret \
  --tenant-id <TENANT_ID> \
  --client-id <APP_ID> \
  --client-secret <SECRET> \
  --output ./reports

# ── Three-environment comparison (Dev → Test → Prod) ─────────
dotnet run --project src/D365Xray.Cli -- scan \
  --env https://dev.crm4.dynamics.com https://test.crm4.dynamics.com https://prod.crm4.dynamics.com \
  --name Dev Test Prod \
  --auth ClientSecret \
  --tenant-id <TENANT_ID> \
  --client-id <APP_ID> \
  --client-secret <SECRET> \
  --output ./reports

# ── DefaultAzureCredential (auto-detect: env vars → MI → VS/CLI → browser)
dotnet run --project src/D365Xray.Cli -- scan \
  --env https://orgXXXXXX.crm4.dynamics.com \
  --name Staging \
  --auth Default

# ── DeviceCode auth (headless / SSH sessions) ────────────────
dotnet run --project src/D365Xray.Cli -- scan \
  --env https://orgXXXXXX.crm4.dynamics.com \
  --name Dev \
  --auth DeviceCode \
  --client-id <APP_ID>

# ── With optional AI enrichment ──────────────────────────────
dotnet run --project src/D365Xray.Cli -- scan \
  --env https://orgXXXXXX.crm4.dynamics.com \
  --name Dev \
  --auth Default \
  --ai-instructions ./prompts/analysis.md

# ── Custom output directory ──────────────────────────────────
dotnet run --project src/D365Xray.Cli -- scan \
  --env https://orgXXXXXX.crm4.dynamics.com \
  --name Dev \
  --auth ClientSecret \
  --tenant-id <TENANT_ID> \
  --client-id <APP_ID> \
  --client-secret <SECRET> \
  --output ./my-reports/2026-03-22
```

### CLI Options

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `--env` | `-e` | ✅ | Dataverse environment URL(s) |
| `--name` | `-n` | | Display name(s) for environments |
| `--auth` | `-a` | | Auth method: `Default`, `ClientSecret`, `Interactive`, `DeviceCode` |
| `--tenant-id` | | | Entra ID tenant ID |
| `--client-id` | | | App registration client ID |
| `--client-secret` | | | Client secret (prefer env vars) |
| `--output` | `-o` | | Output directory (default: `./output`) |
| `--ai-instructions` | | | Path to Markdown file with custom AI instructions |

### Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success – risk level is Low or Medium |
| `1` | General error during execution |
| `2` | Analysis completed but risk is **Critical** (score > 75) |
| `3` | Invalid CLI arguments or configuration |

---

## 🔐 Authentication

d365-xray uses [Azure.Identity](https://learn.microsoft.com/en-us/dotnet/api/azure.identity) for authentication. Supported methods:

| Method | Use Case | Required Parameters |
|--------|----------|-------------------|
| `Default` | Auto-detect (env vars → managed identity → VS/CLI → browser) | — |
| `ClientSecret` | CI/CD, service principal | `--tenant-id`, `--client-id`, `--client-secret` |
| `Interactive` | Developer workstation (browser popup) | `--client-id` |
| `DeviceCode` | Headless environments | `--client-id` |

### App Registration Setup

1. Create an App Registration in **Microsoft Entra ID**
2. Add API permission: **Dynamics CRM** → `user_impersonation`
3. For `ClientSecret` auth: create a client secret and grant **Application** permission
4. Register the app as an **Application User** in **Power Platform Admin Center**:
   - Go to **Environments** → select your environment
   - **Settings** → **Users + permissions** → **Application users**
   - Click **+ New app user** → select your app → assign a Security Role

---

## 🧪 Integration Tests

Integration tests run against a real Dataverse environment. Credentials are stored via `dotnet user-secrets` (never committed to git).

### Setup

```bash
cd tests/D365Xray.IntegrationTests

dotnet user-secrets set "Dataverse:EnvironmentUrl" "https://yourorg.crm4.dynamics.com"
dotnet user-secrets set "Dataverse:TenantId" "<TENANT_ID>"
dotnet user-secrets set "Dataverse:ClientId" "<APP_ID>"
dotnet user-secrets set "Dataverse:ClientSecret" "<SECRET>"
```

### Run

```bash
dotnet test tests/D365Xray.IntegrationTests --filter "Category=Integration"
```

Tests are automatically **skipped** when credentials are not configured, so `dotnet test` on the full solution always succeeds.

---

## 📊 Output Formats

Reports are written to the `--output` directory:

| File | Format | Description |
|------|--------|-------------|
| `risk-report.json` | JSON | Machine-readable, full data |
| `risk-report.md` | Markdown | Human-readable documentation |
| `risk-report.html` | HTML | Standalone local viewer with styling |

---

## 🧩 Technology Stack

| Component | Version | Purpose |
|-----------|---------|---------|
| .NET | 10.0 LTS | Runtime |
| C# | Latest | Language |
| System.CommandLine | 2.0.5 GA | CLI framework |
| Azure.Identity | 1.19.0 | Authentication |
| Microsoft.Extensions.Hosting | 10.0.5 | DI composition root |
| xUnit | 2.x / 3.x runner | Testing |

---

## 📁 Project Structure

```
d365-xray/
├── d365-xray.sln
├── Directory.Build.props       # Shared build settings (net10.0, nullable, warnings-as-errors)
├── global.json                 # SDK pin: 10.0.201
├── .editorconfig               # Code style (file-scoped namespaces, braces, naming)
├── .gitignore
├── README.md
├── src/
│   ├── D365Xray.Core/
│   │   ├── ServiceContracts.cs     # 5 interfaces
│   │   ├── NullAiAnalysisAdapter.cs
│   │   └── Model/                  # 9 domain model records
│   ├── D365Xray.Connectors/
│   │   ├── DataverseClient.cs      # Web API v9.2, 429 retry, OData paging
│   │   ├── CredentialFactory.cs    # AuthMethod → TokenCredential mapping
│   │   ├── DataverseConnector.cs   # Snapshot orchestrator
│   │   └── Collectors/             # Solution, Component, Layer, Dependency, Settings
│   ├── D365Xray.Diff/
│   │   └── 5 analyzers + SnapshotDiffEngine
│   ├── D365Xray.Risk/
│   │   └── 14 rules + RiskRuleEngine
│   ├── D365Xray.Reporting/
│   │   └── JSON, Markdown, HTML exporters
│   └── D365Xray.Cli/
│       ├── Program.cs              # CLI entry point + DI setup
│       ├── ScanCommand.cs          # 6-step pipeline orchestration
│       └── ExitCodes.cs
└── tests/
    └── 6 test projects (80 tests total)
```

---

## 📜 License

MIT

---

> Built with ❤️ for Dynamics 365 administrators and architects who need deep visibility into their environments.
