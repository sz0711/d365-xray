using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects security role drift between environments.
/// Flags missing security roles.
/// </summary>
internal static class SecurityRoleDriftAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineRoles = BuildLookup(baseline.SecurityRoles);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetRoles = BuildLookup(target.SecurityRoles);

            foreach (var (key, baseRole) in baselineRoles.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetRoles.ContainsKey(key))
                {
                    yield return new Finding
                    {
                        FindingId = $"ROLE-MISSING-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.SecurityRoleDrift,
                        Severity = Severity.High,
                        Title = $"Security role '{baseRole.Name}' missing from {target.Environment.DisplayName}",
                        Description = $"Security role '{baseRole.Name}' exists in " +
                            $"{baseline.Environment.DisplayName} but is missing from " +
                            $"{target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["RoleName"] = baseRole.Name,
                            ["RoleId"] = baseRole.RoleId.ToString(),
                            ["IsManaged"] = baseRole.IsManaged.ToString(),
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }
            }
        }
    }

    private static Dictionary<string, SecurityRole> BuildLookup(IReadOnlyList<SecurityRole> roles)
    {
        var lookup = new Dictionary<string, SecurityRole>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            lookup.TryAdd(role.Name, role);
        }
        return lookup;
    }
}
