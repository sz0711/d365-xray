using D365Xray.Core.Model;

namespace D365Xray.Risk;

/// <summary>
/// A single scoring rule that maps a finding pattern to a risk score.
/// Rules are matched by category and optional minimum severity.
/// </summary>
public sealed record RiskRule
{
    /// <summary>
    /// Unique identifier for this rule (e.g. "R-DEP-001").
    /// </summary>
    public required string RuleId { get; init; }

    /// <summary>
    /// Which finding category this rule applies to.
    /// </summary>
    public required FindingCategory Category { get; init; }

    /// <summary>
    /// Minimum severity for this rule to match. Null = matches any severity.
    /// </summary>
    public Severity? MinimumSeverity { get; init; }

    /// <summary>
    /// Base risk score (0–100) assigned when this rule matches.
    /// </summary>
    public required int BaseScore { get; init; }

    /// <summary>
    /// Human-readable explanation of what this rule checks and why it matters.
    /// </summary>
    public required string Description { get; init; }
}
