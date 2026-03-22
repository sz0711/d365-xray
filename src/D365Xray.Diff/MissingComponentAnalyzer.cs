using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects components that exist in some environments but not in others.
/// Uses the first snapshot as the baseline: components in baseline but absent
/// from target environments generate findings.
/// </summary>
internal static class MissingComponentAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineComponents = baseline.Components
            .ToDictionary(c => (c.ComponentId, c.ComponentType));

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetComponentIds = target.Components
                .Select(c => (c.ComponentId, c.ComponentType))
                .ToHashSet();

            // Components in baseline but missing from target
            foreach (var (key, component) in baselineComponents)
            {
                if (!targetComponentIds.Contains(key))
                {
                    yield return new Finding
                    {
                        FindingId = $"COMP-MISSING-{component.ComponentId:N}-{target.Environment.DisplayName}",
                        Category = FindingCategory.MissingComponent,
                        Severity = Severity.Medium,
                        Title = $"{component.ComponentType} component missing from {target.Environment.DisplayName}",
                        Description = $"Component {component.ComponentId} ({component.ComponentType}) in solution " +
                            $"'{component.SolutionUniqueName}' exists in {baseline.Environment.DisplayName} " +
                            $"but is missing from {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["ComponentId"] = component.ComponentId.ToString(),
                            ["ComponentType"] = component.ComponentType.ToString(),
                            ["SolutionUniqueName"] = component.SolutionUniqueName,
                            ["BaselineEnvironment"] = baseline.Environment.DisplayName,
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }
            }
        }
    }
}
