namespace D365Xray.Core.Model;

/// <summary>
/// Aggregated risk assessment produced by the risk scorer.
/// </summary>
public sealed record RiskReport
{
    public required SnapshotMetadata Metadata { get; init; }
    public required IReadOnlyList<EnvironmentInfo> ComparedEnvironments { get; init; }

    /// <summary>
    /// Overall risk score (0–100). Derived from individual finding scores.
    /// </summary>
    public required int OverallRiskScore { get; init; }

    /// <summary>
    /// Human-readable risk level derived from the overall score.
    /// </summary>
    public required RiskLevel OverallRiskLevel { get; init; }

    /// <summary>
    /// All findings with risk scores and triggered rules.
    /// </summary>
    public IReadOnlyList<Finding> Findings { get; init; } = [];

    /// <summary>
    /// Summary counts by severity.
    /// </summary>
    public IReadOnlyDictionary<Severity, int> SeverityCounts { get; init; } =
        new Dictionary<Severity, int>();

    /// <summary>
    /// Comparison mode used by the diff engine.
    /// </summary>
    public ComparisonMode ComparisonMode { get; init; } = ComparisonMode.Baseline;

    /// <summary>
    /// Per-environment inventory counts for report dashboards.
    /// </summary>
    public IReadOnlyList<EnvironmentSummary> EnvironmentSummaries { get; init; } = [];

    /// <summary>
    /// Per-environment solution breakdowns with Microsoft/custom classification.
    /// </summary>
    public IReadOnlyList<SolutionInventory> SolutionInventories { get; init; } = [];

    /// <summary>
    /// Per-environment custom artifact drill-downs (non-Microsoft items only).
    /// </summary>
    public IReadOnlyList<CustomArtifactSummary> CustomArtifactSummaries { get; init; } = [];

    /// <summary>
    /// Per-environment organization settings for governance audit.
    /// </summary>
    public IReadOnlyList<EnvironmentSettingsSnapshot> SettingsSnapshots { get; init; } = [];

    /// <summary>
    /// Optional AI-generated enrichment. Null when AI was not invoked.
    /// When present, always carries explicit <see cref="AiProvenance"/>.
    /// </summary>
    public AiEnrichmentResult? AiEnrichment { get; init; }
}

/// <summary>
/// Discrete risk level buckets derived from the overall score.
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}
