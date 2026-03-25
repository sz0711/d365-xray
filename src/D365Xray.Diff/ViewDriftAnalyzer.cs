using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects view (saved query) drift between environments.
/// Flags missing views.
/// </summary>
internal static class ViewDriftAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineViews = BuildLookup(baseline.Views);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetViews = BuildLookup(target.Views);

            foreach (var (key, baseView) in baselineViews.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetViews.ContainsKey(key))
                {
                    yield return new Finding
                    {
                        FindingId = $"VIEW-MISSING-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.ViewDrift,
                        Severity = baseView.IsDefault ? Severity.High : Severity.Medium,
                        Title = $"View '{baseView.Name}' missing from {target.Environment.DisplayName}",
                        Description = $"View '{baseView.Name}' for entity '{baseView.EntityLogicalName}' " +
                            $"exists in {baseline.Environment.DisplayName} " +
                            $"but is missing from {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["ViewName"] = baseView.Name,
                            ["ViewId"] = baseView.ViewId.ToString(),
                            ["Entity"] = baseView.EntityLogicalName,
                            ["IsDefault"] = baseView.IsDefault.ToString(),
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }
            }
        }
    }

    private static Dictionary<string, ViewDefinition> BuildLookup(IReadOnlyList<ViewDefinition> views)
    {
        var lookup = new Dictionary<string, ViewDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var view in views)
        {
            var key = $"{view.EntityLogicalName}|{view.Name}";
            lookup.TryAdd(key, view);
        }
        return lookup;
    }
}
