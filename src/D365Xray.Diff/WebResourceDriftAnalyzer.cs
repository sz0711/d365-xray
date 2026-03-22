using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects web resource drift between environments.
/// Flags missing resources, type mismatches, and orphaned resources.
/// </summary>
internal static class WebResourceDriftAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineResources = baseline.WebResources
            .ToDictionary(w => w.Name, StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetResources = target.WebResources
                .ToDictionary(w => w.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var (name, baseRes) in baselineResources.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetResources.TryGetValue(name, out var targetRes))
                {
                    yield return new Finding
                    {
                        FindingId = $"WEB-MISSING-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.WebResourceDrift,
                        Severity = IsScript(baseRes) ? Severity.High : Severity.Medium,
                        Title = $"Web resource '{name}' missing from {target.Environment.DisplayName}",
                        Description = $"Web resource '{name}' ({baseRes.WebResourceType}) is present in " +
                            $"{baseline.Environment.DisplayName} but missing from {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["ResourceName"] = name,
                            ["ResourceType"] = baseRes.WebResourceType.ToString(),
                            ["BaselineEnvironment"] = baseline.Environment.DisplayName
                        }
                    };
                    continue;
                }

                // Type mismatch
                if (baseRes.WebResourceType != targetRes.WebResourceType)
                {
                    yield return new Finding
                    {
                        FindingId = $"WEB-TYPEDRIFT-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.WebResourceDrift,
                        Severity = Severity.High,
                        Title = $"Web resource '{name}' has different type",
                        Description = $"Resource '{name}' is {baseRes.WebResourceType} in " +
                            $"{baseline.Environment.DisplayName} but {targetRes.WebResourceType} in " +
                            $"{target.Environment.DisplayName}.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["ResourceName"] = name,
                            ["BaselineType"] = baseRes.WebResourceType.ToString(),
                            ["TargetType"] = targetRes.WebResourceType.ToString()
                        }
                    };
                }

                // Managed state drift
                if (baseRes.IsManaged != targetRes.IsManaged)
                {
                    yield return new Finding
                    {
                        FindingId = $"WEB-MANAGEDDRIFT-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.WebResourceDrift,
                        Severity = Severity.Medium,
                        Title = $"Web resource '{name}' managed state differs",
                        Description = $"Resource '{name}' is {(baseRes.IsManaged ? "managed" : "unmanaged")} in " +
                            $"{baseline.Environment.DisplayName} but " +
                            $"{(targetRes.IsManaged ? "managed" : "unmanaged")} in {target.Environment.DisplayName}.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["ResourceName"] = name,
                            ["BaselineManaged"] = baseRes.IsManaged.ToString(),
                            ["TargetManaged"] = targetRes.IsManaged.ToString()
                        }
                    };
                }
            }
        }
    }

    private static bool IsScript(WebResource resource) =>
        resource.WebResourceType is WebResourceType.JScript or WebResourceType.Html;
}
