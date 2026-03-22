using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects dependency conflicts: required dependencies where the
/// required component's solution is not installed in the environment.
/// </summary>
internal static class DependencyConflictAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            var installedSolutions = snapshot.Solutions
                .Select(s => s.UniqueName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var requiredDeps = snapshot.Dependencies
                .Where(d => d.DependencyType == DependencyType.Required
                            && !string.IsNullOrEmpty(d.RequiredComponentSolution))
                .OrderBy(d => d.RequiredComponentSolution, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.RequiredComponentId);

            foreach (var dep in requiredDeps)
            {
                if (!installedSolutions.Contains(dep.RequiredComponentSolution!))
                {
                    yield return new Finding
                    {
                        FindingId = $"DEP-CONFLICT-{dep.RequiredComponentId:N}-{dep.DependentComponentId:N}-{snapshot.Environment.DisplayName}",
                        Category = FindingCategory.DependencyConflict,
                        Severity = Severity.Critical,
                        Title = $"Missing required dependency solution '{dep.RequiredComponentSolution}' in {snapshot.Environment.DisplayName}",
                        Description = $"Component {dep.DependentComponentId} ({dep.DependentComponentType}) " +
                            $"in solution '{dep.DependentComponentSolution ?? "unknown"}' requires component " +
                            $"{dep.RequiredComponentId} ({dep.RequiredComponentType}) from solution " +
                            $"'{dep.RequiredComponentSolution}', which is not installed.",
                        AffectedEnvironments = [snapshot.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["RequiredComponentId"] = dep.RequiredComponentId.ToString(),
                            ["RequiredComponentType"] = dep.RequiredComponentType.ToString(),
                            ["RequiredSolution"] = dep.RequiredComponentSolution!,
                            ["DependentComponentId"] = dep.DependentComponentId.ToString(),
                            ["DependentComponentType"] = dep.DependentComponentType.ToString(),
                            ["DependentSolution"] = dep.DependentComponentSolution ?? "unknown"
                        }
                    };
                }
            }
        }
    }
}
