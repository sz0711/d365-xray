using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Self-analysis mode for a single environment. Produces findings about
/// unmanaged customizations, active layer overrides, dependency conflicts,
/// and configuration anomalies without needing a second environment to compare against.
/// </summary>
internal static class SingleEnvironmentAnalyzer
{
    private static bool IsProductionLike(EnvironmentType type) =>
        type is EnvironmentType.Prod or EnvironmentType.Staging;

    public static IEnumerable<Finding> Analyze(EnvironmentSnapshot snapshot)
    {
        foreach (var finding in DetectUnmanagedSolutions(snapshot))
        {
            yield return finding;
        }

        foreach (var finding in DetectActiveLayerOverrides(snapshot))
        {
            yield return finding;
        }

        foreach (var finding in DetectDependencyConflicts(snapshot))
        {
            yield return finding;
        }

        foreach (var finding in DetectDuplicatePublisherPrefixes(snapshot))
        {
            yield return finding;
        }
    }

    /// <summary>
    /// Flags unmanaged (non-system) solutions.
    /// Severity depends on environment type: Info for Dev/Test, Medium for Prod/Staging/Unknown.
    /// </summary>
    private static IEnumerable<Finding> DetectUnmanagedSolutions(EnvironmentSnapshot snapshot)
    {
        var systemSolutions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Active", "Default", "System", "ActivityFeeds", "msdynce_Activities",
            "msdynce_ActivityPatch", "msdynce_LeadManagement", "msdynce_SalesCore"
        };

        var unmanagedSolutions = snapshot.Solutions
            .Where(s => !s.IsManaged && !systemSolutions.Contains(s.UniqueName))
            .OrderBy(s => s.UniqueName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var isProdLike = IsProductionLike(snapshot.Environment.EnvironmentType);
        var severity = isProdLike ? Severity.Medium : Severity.Info;
        var envLabel = snapshot.Environment.EnvironmentType.ToString();

        foreach (var sol in unmanagedSolutions)
        {
            var description = isProdLike
                ? $"Solution '{sol.DisplayName}' (v{sol.Version}, publisher: " +
                  $"{sol.Publisher.DisplayName}) is unmanaged in a {envLabel} environment. " +
                  $"Unmanaged solutions in production-like environments can cause upgrade conflicts " +
                  $"and make ALM harder to manage."
                : $"Solution '{sol.DisplayName}' (v{sol.Version}, publisher: " +
                  $"{sol.Publisher.DisplayName}) is unmanaged. This is expected in a {envLabel} " +
                  $"environment for active development.";

            yield return new Finding
            {
                FindingId = $"SINGLE-UNMANAGED-{sol.UniqueName}-{snapshot.Environment.DisplayName}",
                Category = FindingCategory.ConfigurationAnomaly,
                Severity = severity,
                Title = $"Unmanaged solution '{sol.UniqueName}' in {snapshot.Environment.DisplayName}",
                Description = description,
                AffectedEnvironments = [snapshot.Environment.DisplayName],
                Details = new Dictionary<string, string>
                {
                    ["SolutionUniqueName"] = sol.UniqueName,
                    ["Version"] = sol.Version,
                    ["Publisher"] = sol.Publisher.UniqueName,
                    ["EnvironmentType"] = envLabel
                }
            };
        }
    }

    /// <summary>
    /// Detects components with an Active layer on top of managed layers (reuses LayerOverrideAnalyzer logic).
    /// </summary>
    private static IEnumerable<Finding> DetectActiveLayerOverrides(EnvironmentSnapshot snapshot)
    {
        // Delegate to LayerOverrideAnalyzer which already works per-snapshot
        return LayerOverrideAnalyzer.Analyze([snapshot]);
    }

    /// <summary>
    /// Detects required dependencies whose solution is not installed.
    /// </summary>
    private static IEnumerable<Finding> DetectDependencyConflicts(EnvironmentSnapshot snapshot)
    {
        // Delegate to DependencyConflictAnalyzer which already works per-snapshot
        return DependencyConflictAnalyzer.Analyze([snapshot]);
    }

    /// <summary>
    /// Detects multiple publishers using the same customization prefix — a sign of
    /// copy-paste mistakes or publisher confusion.
    /// </summary>
    private static IEnumerable<Finding> DetectDuplicatePublisherPrefixes(EnvironmentSnapshot snapshot)
    {
        var publishersByPrefix = snapshot.Solutions
            .Select(s => s.Publisher)
            .DistinctBy(p => p.UniqueName, StringComparer.OrdinalIgnoreCase)
            .GroupBy(p => p.CustomizationPrefix, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in publishersByPrefix)
        {
            var publishers = group.Select(p => p.UniqueName).ToList();
            yield return new Finding
            {
                FindingId = $"SINGLE-DUPPREFIX-{group.Key}-{snapshot.Environment.DisplayName}",
                Category = FindingCategory.ConfigurationAnomaly,
                Severity = Severity.Low,
                Title = $"Duplicate publisher prefix '{group.Key}' in {snapshot.Environment.DisplayName}",
                Description = $"Multiple publishers share the customization prefix '{group.Key}': " +
                    $"[{string.Join(", ", publishers)}]. This may indicate publisher misconfiguration.",
                AffectedEnvironments = [snapshot.Environment.DisplayName],
                Details = new Dictionary<string, string>
                {
                    ["Prefix"] = group.Key,
                    ["Publishers"] = string.Join(", ", publishers)
                }
            };
        }
    }
}
