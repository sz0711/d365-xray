using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects app module drift between environments.
/// Flags missing app modules and publication state differences.
/// </summary>
internal static class AppModuleDriftAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineApps = BuildLookup(baseline.AppModules);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetApps = BuildLookup(target.AppModules);

            foreach (var (key, baseApp) in baselineApps.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetApps.TryGetValue(key, out var targetApp))
                {
                    yield return new Finding
                    {
                        FindingId = $"APP-MISSING-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.AppModuleDrift,
                        Severity = baseApp.IsPublished ? Severity.High : Severity.Medium,
                        Title = $"App module '{baseApp.Name}' missing from {target.Environment.DisplayName}",
                        Description = $"App module '{baseApp.Name}' (v{baseApp.AppVersion ?? "?"}) " +
                            $"exists in {baseline.Environment.DisplayName} " +
                            $"but is missing from {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["AppModuleName"] = baseApp.Name,
                            ["AppModuleId"] = baseApp.AppModuleId.ToString(),
                            ["Version"] = baseApp.AppVersion ?? "",
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                    continue;
                }

                // Version drift
                if (!string.Equals(baseApp.AppVersion, targetApp.AppVersion, StringComparison.OrdinalIgnoreCase)
                    && baseApp.AppVersion is not null && targetApp.AppVersion is not null)
                {
                    yield return new Finding
                    {
                        FindingId = $"APP-VERSION-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.AppModuleDrift,
                        Severity = Severity.Medium,
                        Title = $"App module '{baseApp.Name}' version differs",
                        Description = $"App module '{baseApp.Name}' is v{baseApp.AppVersion} in " +
                            $"{baseline.Environment.DisplayName} but v{targetApp.AppVersion} in " +
                            $"{target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["AppModuleName"] = baseApp.Name,
                            ["AppModuleId"] = baseApp.AppModuleId.ToString(),
                            ["BaselineVersion"] = baseApp.AppVersion,
                            ["TargetVersion"] = targetApp.AppVersion,
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }

                // Publication state drift
                if (baseApp.IsPublished != targetApp.IsPublished)
                {
                    yield return new Finding
                    {
                        FindingId = $"APP-STATE-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.AppModuleDrift,
                        Severity = baseApp.IsPublished && !targetApp.IsPublished ? Severity.High : Severity.Medium,
                        Title = $"App module '{baseApp.Name}' publication state differs",
                        Description = $"App module '{baseApp.Name}' is " +
                            $"{(baseApp.IsPublished ? "published" : "unpublished")} in " +
                            $"{baseline.Environment.DisplayName} but " +
                            $"{(targetApp.IsPublished ? "published" : "unpublished")} in " +
                            $"{target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["AppModuleName"] = baseApp.Name,
                            ["AppModuleId"] = baseApp.AppModuleId.ToString(),
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }
            }
        }
    }

    private static Dictionary<string, AppModule> BuildLookup(IReadOnlyList<AppModule> apps)
    {
        return apps.ToDictionary(
            a => a.UniqueName,
            StringComparer.OrdinalIgnoreCase);
    }
}
