using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects solutions present in some environments but not others,
/// and version mismatches for solutions that exist across environments.
/// </summary>
internal static class SolutionDriftAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        // Collect all unique solution names across all environments
        var allSolutionNames = snapshots
            .SelectMany(s => s.Solutions)
            .Select(s => s.UniqueName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var solutionName in allSolutionNames)
        {
            // Which environments have this solution?
            var present = new List<(EnvironmentSnapshot Snapshot, Solution Solution)>();
            var missing = new List<EnvironmentSnapshot>();

            foreach (var snapshot in snapshots)
            {
                var sol = snapshot.Solutions.FirstOrDefault(
                    s => string.Equals(s.UniqueName, solutionName, StringComparison.OrdinalIgnoreCase));
                if (sol is not null)
                {
                    present.Add((snapshot, sol));
                }
                else
                {
                    missing.Add(snapshot);
                }
            }

            // Missing from some environments
            if (missing.Count > 0 && present.Count > 0)
            {
                var referenceSol = present[0].Solution;
                yield return new Finding
                {
                    FindingId = $"SOL-MISSING-{solutionName}",
                    Category = FindingCategory.SolutionDrift,
                    Severity = referenceSol.IsManaged ? Severity.High : Severity.Medium,
                    Title = $"Solution '{solutionName}' missing from {missing.Count} environment(s)",
                    Description = $"Solution '{referenceSol.DisplayName}' (v{referenceSol.Version}, " +
                        $"{(referenceSol.IsManaged ? "managed" : "unmanaged")}) is present in " +
                        $"[{string.Join(", ", present.Select(p => p.Snapshot.Environment.DisplayName))}] " +
                        $"but missing from [{string.Join(", ", missing.Select(m => m.Environment.DisplayName))}].",
                    AffectedEnvironments = missing.Select(m => m.Environment.DisplayName).ToList(),
                    Details = new Dictionary<string, string>
                    {
                        ["SolutionUniqueName"] = solutionName,
                        ["SolutionId"] = referenceSol.SolutionId.ToString(),
                        ["IsManaged"] = referenceSol.IsManaged.ToString(),
                        ["ReferenceVersion"] = referenceSol.Version,
                        ["EnvironmentUrl"] = present[0].Snapshot.Environment.EnvironmentUrl.ToString()
                    }
                };
            }

            // Version mismatches across environments that do have the solution
            if (present.Count >= 2)
            {
                var distinctVersions = present
                    .Select(p => p.Solution.Version)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (distinctVersions.Count > 1)
                {
                    var versionMap = present
                        .Select(p => $"{p.Snapshot.Environment.DisplayName}=v{p.Solution.Version}");

                    yield return new Finding
                    {
                        FindingId = $"SOL-VERSION-{solutionName}",
                        Category = FindingCategory.VersionMismatch,
                        Severity = Severity.Medium,
                        Title = $"Solution '{solutionName}' has version drift across environments",
                        Description = $"Solution '{solutionName}' has {distinctVersions.Count} different versions: " +
                            $"{string.Join(", ", versionMap)}.",
                        AffectedEnvironments = present.Select(p => p.Snapshot.Environment.DisplayName).ToList(),
                        Details = new Dictionary<string, string>
                        {
                            ["SolutionUniqueName"] = solutionName,
                            ["Versions"] = string.Join(" | ", distinctVersions),
                            ["EnvironmentUrl"] = present[0].Snapshot.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }
            }
        }
    }
}
