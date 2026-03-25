using System.Text.Json;
using D365Xray.Core;
using D365Xray.Core.Model;
using D365Xray.Connectors.Collectors;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors;

/// <summary>
/// Orchestrates capturing a full environment snapshot from a Dataverse environment.
/// Calls individual collectors for solutions, components, layers, dependencies, and settings.
/// </summary>
internal sealed class DataverseConnector : IEnvironmentConnector
{
    private readonly IDataverseClient _client;
    private readonly ILogger<DataverseConnector> _logger;

    public DataverseConnector(IDataverseClient client, ILogger<DataverseConnector> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<EnvironmentSnapshot> CaptureSnapshotAsync(
        EnvironmentInfo environment,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation("Starting snapshot capture for environment {EnvName} ({EnvUrl})",
            environment.DisplayName, environment.EnvironmentUrl);

        // 1. Enrich environment with Dataverse version
        var dataverseVersion = await GetDataverseVersionAsync(cancellationToken);
        var enrichedEnv = environment with { DataverseVersion = dataverseVersion };

        // 2. Collect solutions (needed for dependency cross-reference)
        _logger.LogInformation("Collecting solutions...");
        var solutions = await SolutionCollector.CollectAsync(_client, cancellationToken);
        _logger.LogInformation("Collected {Count} solutions", solutions.Count);

        // 3. Collect solution components
        _logger.LogInformation("Collecting solution components...");
        var components = await ComponentCollector.CollectAsync(_client, cancellationToken);
        _logger.LogInformation("Collected {Count} solution components", components.Count);

        // 4. Collect component layers (graceful degradation)
        _logger.LogInformation("Collecting component layers...");
        var layers = await LayerCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} component layers", layers.Count);

        // 5. Collect dependencies (cross-reference with solutions for names)
        _logger.LogInformation("Collecting dependencies...");
        var solutionLookup = solutions.ToDictionary(s => s.SolutionId, s => s.UniqueName);
        var dependencies = await DependencyCollector.CollectAsync(_client, solutionLookup, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} dependencies", dependencies.Count);

        // 6. Collect environment settings
        _logger.LogInformation("Collecting environment settings...");
        var settings = await SettingsCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} environment settings", settings.Count);

        // 7. Collect connection references
        _logger.LogInformation("Collecting connection references...");
        var connectionRefs = await ConnectionReferenceCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} connection references", connectionRefs.Count);

        // 8. Collect service endpoints (webhooks)
        _logger.LogInformation("Collecting service endpoints...");
        var endpoints = await ServiceEndpointCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} service endpoints", endpoints.Count);

        // 9. Collect custom connectors
        _logger.LogInformation("Collecting custom connectors...");
        var connectors = await CustomConnectorCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} custom connectors", connectors.Count);

        // 10. Collect environment variables
        _logger.LogInformation("Collecting environment variables...");
        var envVars = await EnvironmentVariableCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} environment variables", envVars.Count);

        // 11. Collect plugin assemblies
        _logger.LogInformation("Collecting plugin assemblies...");
        var plugins = await PluginAssemblyCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} plugin assemblies", plugins.Count);

        // 12. Collect SDK message processing steps
        _logger.LogInformation("Collecting SDK steps...");
        var sdkSteps = await SdkStepCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} SDK steps", sdkSteps.Count);

        // 13. Collect web resources
        _logger.LogInformation("Collecting web resources...");
        var webResources = await WebResourceCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} web resources", webResources.Count);

        // 14. Collect workflows (classic + modern flows, excluding business rules)
        _logger.LogInformation("Collecting workflows...");
        var workflows = await WorkflowCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} workflows", workflows.Count);

        // 15. Collect business rules
        _logger.LogInformation("Collecting business rules...");
        var businessRules = await BusinessRuleCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} business rules", businessRules.Count);

        // 16. Collect forms
        _logger.LogInformation("Collecting forms...");
        var forms = await FormCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} forms", forms.Count);

        // 17. Collect views (saved queries)
        _logger.LogInformation("Collecting views...");
        var views = await ViewCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} views", views.Count);

        // 18. Collect charts
        _logger.LogInformation("Collecting charts...");
        var charts = await ChartCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} charts", charts.Count);

        // 19. Collect app modules
        _logger.LogInformation("Collecting app modules...");
        var appModules = await AppModuleCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} app modules", appModules.Count);

        // 20. Collect security roles
        _logger.LogInformation("Collecting security roles...");
        var securityRoles = await SecurityRoleCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} security roles", securityRoles.Count);

        // 21. Collect field security profiles
        _logger.LogInformation("Collecting field security profiles...");
        var fieldSecurityProfiles = await FieldSecurityProfileCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} field security profiles", fieldSecurityProfiles.Count);

        // 22. Collect entity metadata
        _logger.LogInformation("Collecting entity metadata...");
        var entityMetadata = await EntityMetadataCollector.CollectAsync(_client, _logger, cancellationToken);
        _logger.LogInformation("Collected {Count} entity definitions", entityMetadata.Count);

        stopwatch.Stop();

        _logger.LogInformation("Snapshot capture completed for {EnvName} in {Duration}",
            enrichedEnv.DisplayName, stopwatch.Elapsed);

        return new EnvironmentSnapshot
        {
            Metadata = new SnapshotMetadata
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                ToolVersion = typeof(DataverseConnector).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                CapturedDuration = stopwatch.Elapsed
            },
            Environment = enrichedEnv,
            Solutions = solutions,
            Components = components,
            Layers = layers,
            Dependencies = dependencies,
            Settings = settings,
            ConnectionReferences = connectionRefs,
            ServiceEndpoints = endpoints,
            CustomConnectors = connectors,
            EnvironmentVariables = envVars,
            PluginAssemblies = plugins,
            SdkSteps = sdkSteps,
            WebResources = webResources,
            Workflows = workflows,
            BusinessRules = businessRules,
            Forms = forms,
            Views = views,
            Charts = charts,
            AppModules = appModules,
            SecurityRoles = securityRoles,
            FieldSecurityProfiles = fieldSecurityProfiles,
            EntityMetadata = entityMetadata
        };
    }

    /// <summary>
    /// Calls the Dataverse RetrieveVersion function to get the API version string.
    /// </summary>
    private async Task<string?> GetDataverseVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await _client.GetAsync("RetrieveVersion", cancellationToken: cancellationToken);
            return JsonHelper.GetString(doc.RootElement, "Version");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not retrieve Dataverse version");
            return null;
        }
    }
}
