namespace D365Xray.Core.Model;

/// <summary>
/// The result of comparing two or more environment snapshots.
/// Contains all findings grouped by category.
/// </summary>
public sealed record ComparisonResult
{
    public required SnapshotMetadata Metadata { get; init; }
    public required IReadOnlyList<EnvironmentInfo> ComparedEnvironments { get; init; }
    public IReadOnlyList<Finding> Findings { get; init; } = [];
}

/// <summary>
/// A single finding produced by the diff engine or risk scorer.
/// </summary>
public sealed record Finding
{
    public required string FindingId { get; init; }
    public required FindingCategory Category { get; init; }
    public required Severity Severity { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }

    /// <summary>
    /// Which environments are affected (by display name or URL).
    /// </summary>
    public IReadOnlyList<string> AffectedEnvironments { get; init; } = [];

    /// <summary>
    /// Optional structured detail (e.g. component ID, solution name, setting key).
    /// </summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// The rule that triggered this finding (populated by the risk scorer).
    /// </summary>
    public string? RuleId { get; init; }

    /// <summary>
    /// Numeric risk score (0–100). Populated by the risk scorer.
    /// </summary>
    public int? RiskScore { get; init; }
}

/// <summary>
/// Broad category of a finding for grouping in reports.
/// </summary>
public enum FindingCategory
{
    SolutionDrift,
    LayerOverride,
    DependencyConflict,
    SettingsDrift,
    MissingComponent,
    VersionMismatch,
    SecurityRisk,
    ConfigurationAnomaly,
    ConnectionConfiguration,
    IntegrationEndpointDrift,
    ConnectorGovernance,
    PluginConfiguration,
    WorkflowConfiguration,
    WebResourceDrift,
    EnvironmentVariableDrift,
    BusinessRuleDrift
}

/// <summary>
/// Severity level of a finding.
/// </summary>
public enum Severity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
