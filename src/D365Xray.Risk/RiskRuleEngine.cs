using D365Xray.Core;
using D365Xray.Core.Model;

namespace D365Xray.Risk;

/// <summary>
/// Rule-based risk scorer. Matches each finding against the built-in rule
/// catalog, enriches findings with RuleId + RiskScore, and derives an
/// overall risk assessment.
/// </summary>
internal sealed class RiskRuleEngine : IRiskScorer
{
    private readonly IReadOnlyList<RiskRule> _rules;

    public RiskRuleEngine(IReadOnlyList<RiskRule> rules)
    {
        _rules = rules;
    }

    public RiskReport Evaluate(ComparisonResult comparisonResult)
    {
        var scoredFindings = new List<Finding>(comparisonResult.Findings.Count);

        foreach (var finding in comparisonResult.Findings)
        {
            var rule = MatchRule(finding);
            if (rule is not null)
            {
                scoredFindings.Add(finding with
                {
                    RuleId = rule.RuleId,
                    RiskScore = rule.BaseScore
                });
            }
            else
            {
                // No matching rule — keep finding unchanged, default score 0.
                scoredFindings.Add(finding with { RiskScore = 0 });
            }
        }

        var overallScore = ComputeOverallScore(scoredFindings);
        var severityCounts = ComputeSeverityCounts(scoredFindings);

        return new RiskReport
        {
            Metadata = comparisonResult.Metadata,
            ComparedEnvironments = comparisonResult.ComparedEnvironments,
            OverallRiskScore = overallScore,
            OverallRiskLevel = DeriveRiskLevel(overallScore),
            Findings = scoredFindings.OrderBy(f => f.FindingId, StringComparer.Ordinal).ToList(),
            SeverityCounts = severityCounts
        };
    }

    /// <summary>
    /// Finds the most specific rule for a finding.
    /// Rules with a MinimumSeverity constraint are preferred over wildcard rules
    /// when both match.
    /// </summary>
    internal RiskRule? MatchRule(Finding finding)
    {
        RiskRule? bestMatch = null;

        foreach (var rule in _rules)
        {
            if (rule.Category != finding.Category)
            {
                continue;
            }

            if (rule.MinimumSeverity.HasValue && finding.Severity < rule.MinimumSeverity.Value)
            {
                continue;
            }

            // A rule with a MinimumSeverity constraint is more specific
            // than a wildcard (null) rule → prefer it.
            if (bestMatch is null)
            {
                bestMatch = rule;
            }
            else if (rule.MinimumSeverity.HasValue && !bestMatch.MinimumSeverity.HasValue)
            {
                bestMatch = rule;
            }
            else if (rule.MinimumSeverity.HasValue && bestMatch.MinimumSeverity.HasValue
                      && rule.MinimumSeverity.Value > bestMatch.MinimumSeverity.Value)
            {
                // Both have severity constraints — pick the stricter one that still matches.
                bestMatch = rule;
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Overall score = max score among all findings (capped at 100).
    /// If no findings exist, the score is 0.
    /// </summary>
    internal static int ComputeOverallScore(IReadOnlyList<Finding> findings)
    {
        if (findings.Count == 0)
        {
            return 0;
        }

        return Math.Min(100, findings.Max(f => f.RiskScore ?? 0));
    }

    internal static RiskLevel DeriveRiskLevel(int score) => score switch
    {
        <= 25 => RiskLevel.Low,
        <= 50 => RiskLevel.Medium,
        <= 75 => RiskLevel.High,
        _ => RiskLevel.Critical
    };

    private static Dictionary<Severity, int> ComputeSeverityCounts(IReadOnlyList<Finding> findings)
    {
        var counts = new Dictionary<Severity, int>();
        foreach (var finding in findings)
        {
            counts.TryGetValue(finding.Severity, out var count);
            counts[finding.Severity] = count + 1;
        }
        return counts;
    }
}
