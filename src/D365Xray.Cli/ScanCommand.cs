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
        var comparison = diffEngine.Compare(snapshots);
        _logger.LogInformation("Diff complete: {Count} finding(s).", comparison.Findings.Count);

        // 3. Risk scoring
        _logger.LogInformation("Evaluating risk...");
        var riskScorer = _services.GetRequiredService<IRiskScorer>();
        var riskReport = riskScorer.Evaluate(comparison);
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
}

/// <summary>
/// Parsed environment argument combining config + auth settings.
/// </summary>
internal sealed record ScanEnvironmentArg
{
    public required DataverseConnectionConfig Config { get; init; }
    public EnvironmentType EnvironmentType { get; init; } = EnvironmentType.Unknown;
}
