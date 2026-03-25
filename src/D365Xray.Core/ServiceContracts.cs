using D365Xray.Core.Model;

namespace D365Xray.Core;

/// <summary>
/// Connects to a Dataverse environment and captures a point-in-time snapshot (read-only).
/// Implementations live in D365Xray.Connectors.
/// </summary>
public interface IEnvironmentConnector
{
    Task<EnvironmentSnapshot> CaptureSnapshotAsync(
        EnvironmentInfo environment,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compares two or more environment snapshots and produces deterministic findings.
/// Implementations live in D365Xray.Diff.
/// </summary>
public interface IDiffEngine
{
    ComparisonResult Compare(IReadOnlyList<EnvironmentSnapshot> snapshots,
        ComparisonMode mode = ComparisonMode.Baseline);
}

/// <summary>
/// Evaluates findings against a rule set and assigns risk scores.
/// Implementations live in D365Xray.Risk.
/// </summary>
public interface IRiskScorer
{
    RiskReport Evaluate(ComparisonResult comparisonResult);
}

/// <summary>
/// Exports a risk report to one or more output formats (JSON, Markdown, HTML, CSV).
/// Implementations live in D365Xray.Reporting.
/// </summary>
public interface IReportExporter
{
    Task ExportAsync(
        RiskReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional AI-powered analysis enrichment.
/// Implementations accept a completed risk report and return AI-generated insights
/// with explicit provenance markers. The default NullAiAnalysisAdapter returns an
/// empty result so callers never need null checks.
/// </summary>
public interface IAiAnalysisAdapter
{
    Task<AiEnrichmentResult> EnrichAsync(
        RiskReport report,
        AiAnalysisOptions options,
        CancellationToken cancellationToken = default);
}
