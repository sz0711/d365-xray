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
        var dependencies = await DependencyCollector.CollectAsync(_client, solutionLookup, cancellationToken);
        _logger.LogInformation("Collected {Count} dependencies", dependencies.Count);

        // 6. Collect environment settings
        _logger.LogInformation("Collecting environment settings...");
        var settings = await SettingsCollector.CollectAsync(_client, cancellationToken);
        _logger.LogInformation("Collected {Count} environment settings", settings.Count);

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
            Settings = settings
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
