using System.Diagnostics;
using D365Xray.Connectors;
using D365Xray.Core;
using D365Xray.Core.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace D365Xray.Cli;

/// <summary>
/// Orchestrates the full scan pipeline:
/// connect → snapshot → diff → risk → export.
/// </summary>
internal sealed class ScanCommand
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ScanCommand> _logger;
    private readonly IAiAnalysisAdapter _aiAdapter;

    public ScanCommand(
        IServiceProvider services,
        ILogger<ScanCommand> logger,
        IAiAnalysisAdapter aiAdapter)
    {
        _services = services;
        _logger = logger;
        _aiAdapter = aiAdapter;
    }

    public async Task<int> ExecuteAsync(
        IReadOnlyList<ScanEnvironmentArg> environments,
        string outputDirectory,
        string? aiInstructionsPath,
        ComparisonMode comparisonMode,
        CancellationToken cancellationToken)
    {
        if (environments.Count == 0)
        {
            _logger.LogError("At least one --env is required.");
            return ExitCodes.ConfigurationError;
        }

        var toolVersion = typeof(ScanCommand).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        var totalSw = Stopwatch.StartNew();

        // 1. Register environments & capture snapshots
        _logger.LogInformation("Capturing snapshots for {Count} environment(s)...", environments.Count);
        var snapshots = new List<EnvironmentSnapshot>(environments.Count);

        foreach (var envArg in environments)
        {
            _logger.LogInformation("  → {Name} ({Url})", envArg.Config.DisplayName, envArg.Config.EnvironmentUrl);

            // Register the per-environment keyed services dynamically
            // Note: For V1 we re-use the singleton connector which resolves keyed clients
            var connector = _services.GetRequiredService<IEnvironmentConnector>();

            var envInfo = new EnvironmentInfo
            {
                EnvironmentId = envArg.Config.DisplayName.ToLowerInvariant(),
                DisplayName = envArg.Config.DisplayName,
                EnvironmentUrl = envArg.Config.EnvironmentUrl,
                EnvironmentType = envArg.EnvironmentType
            };

            var snapshot = await connector.CaptureSnapshotAsync(envInfo, cancellationToken);
            snapshots.Add(snapshot);
        }

        _logger.LogInformation("All snapshots captured.");

        // 2. Diff
        _logger.LogInformation("Running diff engine...");
        var diffEngine = _services.GetRequiredService<IDiffEngine>();
        var comparison = diffEngine.Compare(snapshots, comparisonMode);
        _logger.LogInformation("Diff complete: {Count} finding(s).", comparison.Findings.Count);

        // 3. Risk scoring
        _logger.LogInformation("Evaluating risk...");
        var riskScorer = _services.GetRequiredService<IRiskScorer>();
        var riskReport = riskScorer.Evaluate(comparison);

        // Build environment summaries from captured snapshots
        var summaries = snapshots.Select(s => new EnvironmentSummary
        {
            EnvironmentDisplayName = s.Environment.DisplayName,
            EnvironmentUrl = s.Environment.EnvironmentUrl,
            EnvironmentType = s.Environment.EnvironmentType,
            Solutions = s.Solutions.Count,
            Components = s.Components.Count,
            Workflows = s.Workflows.Count,
            PluginAssemblies = s.PluginAssemblies.Count,
            SdkSteps = s.SdkSteps.Count,
            WebResources = s.WebResources.Count,
            ConnectionReferences = s.ConnectionReferences.Count,
            EnvironmentVariables = s.EnvironmentVariables.Count,
            BusinessRules = s.BusinessRules.Count,
            CustomConnectors = s.CustomConnectors.Count,
            ServiceEndpoints = s.ServiceEndpoints.Count,
            Forms = s.Forms.Count,
            Views = s.Views.Count,
            Charts = s.Charts.Count,
            AppModules = s.AppModules.Count,
            SecurityRoles = s.SecurityRoles.Count,
            FieldSecurityProfiles = s.FieldSecurityProfiles.Count,
            Entities = s.EntityMetadata.Count
        }).ToList();
        riskReport = riskReport with { EnvironmentSummaries = summaries };

        // Build solution inventories and custom artifact drill-downs
        var solutionInventories = snapshots.Select(BuildSolutionInventory).ToList();
        var customArtifacts = snapshots.Select(BuildCustomArtifactSummary).ToList();
        var settingsSnapshots = snapshots.Select(s => new EnvironmentSettingsSnapshot
        {
            EnvironmentDisplayName = s.Environment.DisplayName,
            DataverseVersion = s.Environment.DataverseVersion,
            OrganizationId = s.Environment.OrganizationId,
            ScanDuration = s.Metadata.CapturedDuration,
            Settings = s.Settings
        }).ToList();
        riskReport = riskReport with
        {
            SolutionInventories = solutionInventories,
            CustomArtifactSummaries = customArtifacts,
            SettingsSnapshots = settingsSnapshots
        };

        _logger.LogInformation(
            "Risk assessment: {Level} (score {Score}/100).",
            riskReport.OverallRiskLevel,
            riskReport.OverallRiskScore);

        // 4. Optional AI enrichment
        if (aiInstructionsPath is not null)
        {
            _logger.LogInformation("Loading AI instructions from {Path}...", aiInstructionsPath);
            var instructionsMd = await File.ReadAllTextAsync(aiInstructionsPath, cancellationToken);

            var aiOptions = new AiAnalysisOptions
            {
                CustomInstructionsMarkdown = instructionsMd
            };

            _logger.LogInformation("Running AI enrichment...");
            var aiResult = await _aiAdapter.EnrichAsync(riskReport, aiOptions, cancellationToken);
            riskReport = riskReport with { AiEnrichment = aiResult };

            _logger.LogInformation(
                "AI enrichment complete (adapter: {Adapter}, model: {Model}).",
                aiResult.Provenance.AdapterName,
                aiResult.Provenance.ModelIdentifier);
        }

        // 5. Export
        _logger.LogInformation("Exporting reports to {Dir}...", outputDirectory);
        var exporter = _services.GetRequiredService<IReportExporter>();
        await exporter.ExportAsync(riskReport, outputDirectory, cancellationToken);

        totalSw.Stop();
        _logger.LogInformation("Done in {Elapsed:F1}s. Output: {Dir}", totalSw.Elapsed.TotalSeconds, outputDirectory);

        // 6. Exit code based on risk level
        return riskReport.OverallRiskLevel == RiskLevel.Critical
            ? ExitCodes.CriticalRisk
            : ExitCodes.Success;
    }

    // ── Microsoft publisher detection ───────────────────────

    private static readonly HashSet<string> MicrosoftPublishers = new(StringComparer.OrdinalIgnoreCase)
    {
        "MicrosoftCorporation", "microsoftcorporation", "Microsoft",
        "Microsoftd365accelerator", "MicrosoftPowerCAT", "MicrosoftDataverse",
        "Cds", "cds", "CdsCore", "dynamics365customerengagement"
    };

    private static readonly string[] MicrosoftSolutionPrefixes =
        ["msdyn", "mscrm", "Mscrm", "Dynamics365", "PowerApps", "MicrosoftDynamics",
         "msa_", "GlobalDiscovery", "AdminSettingsProvider"];

    private static bool IsMicrosoftSolution(Solution solution)
    {
        if (MicrosoftPublishers.Contains(solution.Publisher.UniqueName))
        {
            return true;
        }

        foreach (var prefix in MicrosoftSolutionPrefixes)
        {
            if (solution.UniqueName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // System solutions
        return solution.UniqueName is "Active" or "Default" or "System"
            or "ActivityFeeds" or "ActivityFeedsCore";
    }

    private static bool IsMicrosoftPlugin(string name) =>
        name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("MicrosoftPower", StringComparison.OrdinalIgnoreCase);

    private static readonly string[] MicrosoftAppModulePrefixes =
        ["msdyn_", "mspp_", "PowerPlatform", "Dynamics365"];

    private static bool IsMicrosoftAppModule(string uniqueName)
    {
        foreach (var prefix in MicrosoftAppModulePrefixes)
        {
            if (uniqueName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] MicrosoftWebResourcePrefixes =
        ["cc_shared/", "msdyn_", "mscrm/", "mspp_"];

    private static bool IsMicrosoftWebResource(string name)
    {
        foreach (var prefix in MicrosoftWebResourcePrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // ── Solution inventory builder ──────────────────────────

    private static SolutionInventory BuildSolutionInventory(EnvironmentSnapshot snapshot)
    {
        // Build component-type counts per solution from SolutionComponents
        var componentsBySolution = snapshot.Components
            .GroupBy(c => c.SolutionUniqueName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var breakdowns = snapshot.Solutions
            .OrderByDescending(s => componentsBySolution.GetValueOrDefault(s.UniqueName)?.Count ?? 0)
            .Select(sol =>
            {
                var components = componentsBySolution.GetValueOrDefault(sol.UniqueName) ?? [];
                var byType = components.GroupBy(c => c.ComponentType)
                    .ToDictionary(g => g.Key, g => g.Count());

                return new SolutionBreakdown
                {
                    UniqueName = sol.UniqueName,
                    DisplayName = sol.DisplayName,
                    Version = sol.Version,
                    PublisherName = sol.Publisher.DisplayName,
                    IsManaged = sol.IsManaged,
                    IsMicrosoft = IsMicrosoftSolution(sol),
                    TotalComponents = components.Count,
                    Entities = byType.GetValueOrDefault(ComponentType.Entity),
                    Forms = byType.GetValueOrDefault(ComponentType.Form),
                    Views = byType.GetValueOrDefault(ComponentType.SavedQuery),
                    Workflows = byType.GetValueOrDefault(ComponentType.Workflow),
                    PluginAssemblies = byType.GetValueOrDefault(ComponentType.PluginAssembly),
                    WebResources = byType.GetValueOrDefault(ComponentType.WebResource),
                    Roles = byType.GetValueOrDefault(ComponentType.Role),
                    Charts = byType.GetValueOrDefault(ComponentType.SavedQueryVisualization),
                    SdkSteps = byType.GetValueOrDefault(ComponentType.SdkMessageProcessingStep),
                    OptionSets = byType.GetValueOrDefault(ComponentType.OptionSet),
                    OtherComponents = components.Count
                        - byType.GetValueOrDefault(ComponentType.Entity)
                        - byType.GetValueOrDefault(ComponentType.Form)
                        - byType.GetValueOrDefault(ComponentType.SavedQuery)
                        - byType.GetValueOrDefault(ComponentType.Workflow)
                        - byType.GetValueOrDefault(ComponentType.PluginAssembly)
                        - byType.GetValueOrDefault(ComponentType.WebResource)
                        - byType.GetValueOrDefault(ComponentType.Role)
                        - byType.GetValueOrDefault(ComponentType.SavedQueryVisualization)
                        - byType.GetValueOrDefault(ComponentType.SdkMessageProcessingStep)
                        - byType.GetValueOrDefault(ComponentType.OptionSet)
                };
            })
            .ToList();

        return new SolutionInventory
        {
            EnvironmentDisplayName = snapshot.Environment.DisplayName,
            Solutions = breakdowns
        };
    }

    // ── Custom artifact summary builder ─────────────────────

    private static CustomArtifactSummary BuildCustomArtifactSummary(EnvironmentSnapshot snapshot)
    {
        return new CustomArtifactSummary
        {
            EnvironmentDisplayName = snapshot.Environment.DisplayName,
            Workflows = snapshot.Workflows
                .Where(w => !w.IsManaged)
                .OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase)
                .Select(w => new WorkflowSummaryItem(w.Name, w.Category.ToString(), w.IsActivated, w.IsManaged))
                .ToList(),
            Plugins = snapshot.PluginAssemblies
                .Where(p => !IsMicrosoftPlugin(p.Name))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => new PluginSummaryItem(p.Name, p.Version, p.IsolationMode.ToString()))
                .ToList(),
            Forms = snapshot.Forms
                .Where(f => !f.IsManaged)
                .OrderBy(f => f.EntityLogicalName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => new FormSummaryItem(f.Name, f.EntityLogicalName, f.FormType.ToString(), f.IsManaged))
                .ToList(),
            Entities = snapshot.EntityMetadata
                .Where(e => !e.IsManaged && e.IsCustomEntity)
                .OrderBy(e => e.LogicalName, StringComparer.OrdinalIgnoreCase)
                .Select(e => new EntitySummaryItem(e.LogicalName, e.DisplayName, e.IsManaged, e.IsCustomEntity))
                .ToList(),
            AppModules = snapshot.AppModules
                .Where(a => !IsMicrosoftAppModule(a.UniqueName))
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(a => new AppModuleSummaryItem(a.Name, a.UniqueName, a.IsPublished, a.IsManaged))
                .ToList(),
            ConnectionReferences = snapshot.ConnectionReferences
                .Where(c => !c.ConnectionReferenceLogicalName.StartsWith("msdyn_", StringComparison.OrdinalIgnoreCase)
                          && !c.ConnectionReferenceLogicalName.StartsWith("mscrm_", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.ConnectionReferenceLogicalName, StringComparer.OrdinalIgnoreCase)
                .Select(c => new ConnectionReferenceSummaryItem(
                    c.ConnectionReferenceLogicalName,
                    c.DisplayName,
                    c.ConnectorId,
                    !string.IsNullOrEmpty(c.ConnectionId)))
                .ToList(),
            WebResources = snapshot.WebResources
                .Where(w => !w.IsManaged && !IsMicrosoftWebResource(w.Name))
                .OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase)
                .Take(100) // Cap to avoid gigantic reports
                .Select(w => new WebResourceSummaryItem(w.Name, w.WebResourceType.ToString(), w.IsManaged))
                .ToList()
        };
    }
}

/// <summary>
/// Parsed environment argument combining config + auth settings.
/// </summary>
internal sealed record ScanEnvironmentArg
{
    public required DataverseConnectionConfig Config { get; init; }
    public EnvironmentType EnvironmentType { get; init; } = EnvironmentType.Unknown;
}
