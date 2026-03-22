using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects business rule drift between environments.
/// Flags missing rules, activation state drift, scope differences,
/// and potentially competing rules on the same entity.
/// </summary>
internal static class BusinessRuleDriftAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        foreach (var f in AnalyzeCrossEnvironment(snapshots))
        {
            yield return f;
        }
    }

    private static IEnumerable<Finding> AnalyzeCrossEnvironment(
        IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineRules = BuildLookup(baseline.BusinessRules);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetRules = BuildLookup(target.BusinessRules);

            foreach (var (key, baseRule) in baselineRules.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetRules.TryGetValue(key, out var targetRule))
                {
                    yield return new Finding
                    {
                        FindingId = $"BRL-MISSING-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.BusinessRuleDrift,
                        Severity = baseRule.IsActivated ? Severity.High : Severity.Medium,
                        Title = $"Business rule '{baseRule.Name}' missing from {target.Environment.DisplayName}",
                        Description = $"Business rule '{baseRule.Name}' on entity '{baseRule.PrimaryEntity}' is " +
                            $"{(baseRule.IsActivated ? "active" : "inactive")} in " +
                            $"{baseline.Environment.DisplayName} but missing from {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["RuleName"] = baseRule.Name,
                            ["Entity"] = baseRule.PrimaryEntity,
                            ["Scope"] = baseRule.Scope.ToString(),
                            ["BaselineActivated"] = baseRule.IsActivated.ToString()
                        }
                    };
                    continue;
                }

                // Activation state drift
                if (baseRule.IsActivated != targetRule.IsActivated)
                {
                    var moreRisky = baseRule.IsActivated && !targetRule.IsActivated;
                    yield return new Finding
                    {
                        FindingId = $"BRL-STATE-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.BusinessRuleDrift,
                        Severity = moreRisky ? Severity.High : Severity.Medium,
                        Title = $"Business rule '{baseRule.Name}' activation state differs",
                        Description = $"Business rule '{baseRule.Name}' on '{baseRule.PrimaryEntity}' is " +
                            $"{(baseRule.IsActivated ? "active" : "inactive")} in " +
                            $"{baseline.Environment.DisplayName} but " +
                            $"{(targetRule.IsActivated ? "active" : "inactive")} in {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["RuleName"] = baseRule.Name,
                            ["Entity"] = baseRule.PrimaryEntity,
                            ["BaselineActivated"] = baseRule.IsActivated.ToString(),
                            ["TargetActivated"] = targetRule.IsActivated.ToString()
                        }
                    };
                }

                // Scope drift
                if (baseRule.Scope != targetRule.Scope)
                {
                    yield return new Finding
                    {
                        FindingId = $"BRL-SCOPE-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.BusinessRuleDrift,
                        Severity = Severity.Medium,
                        Title = $"Business rule '{baseRule.Name}' scope differs",
                        Description = $"Business rule '{baseRule.Name}' has scope {baseRule.Scope} in " +
                            $"{baseline.Environment.DisplayName} but {targetRule.Scope} in " +
                            $"{target.Environment.DisplayName}.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["RuleName"] = baseRule.Name,
                            ["BaselineScope"] = baseRule.Scope.ToString(),
                            ["TargetScope"] = targetRule.Scope.ToString()
                        }
                    };
                }

                // Entity drift (rule reassigned to different entity)
                if (!string.Equals(baseRule.PrimaryEntity, targetRule.PrimaryEntity, StringComparison.OrdinalIgnoreCase))
                {
                    yield return new Finding
                    {
                        FindingId = $"BRL-ENTITY-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.BusinessRuleDrift,
                        Severity = Severity.High,
                        Title = $"Business rule '{baseRule.Name}' bound to different entity",
                        Description = $"Business rule '{baseRule.Name}' is bound to '{baseRule.PrimaryEntity}' in " +
                            $"{baseline.Environment.DisplayName} but '{targetRule.PrimaryEntity}' in " +
                            $"{target.Environment.DisplayName}.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["RuleName"] = baseRule.Name,
                            ["BaselineEntity"] = baseRule.PrimaryEntity,
                            ["TargetEntity"] = targetRule.PrimaryEntity
                        }
                    };
                }
            }
        }
    }

    private static Dictionary<string, BusinessRule> BuildLookup(IReadOnlyList<BusinessRule> rules)
    {
        return rules.ToDictionary(
            r => r.UniqueName ?? r.Name,
            StringComparer.OrdinalIgnoreCase);
    }
}
