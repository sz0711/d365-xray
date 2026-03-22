using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects environment settings that differ between snapshots.
/// Compares by setting key; the first snapshot is the baseline.
/// </summary>
internal static class SettingsDriftAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineSettings = baseline.Settings
            .ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetSettings = target.Settings
                .ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);

            // Check every baseline setting against target
            foreach (var (key, baselineSetting) in baselineSettings.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetSettings.TryGetValue(key, out var targetSetting))
                {
                    yield return new Finding
                    {
                        FindingId = $"SET-MISSING-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.SettingsDrift,
                        Severity = IsSecurity(baselineSetting) ? Severity.High : Severity.Low,
                        Title = $"Setting '{key}' missing from {target.Environment.DisplayName}",
                        Description = $"Setting '{key}' ({baselineSetting.Category}) is present in " +
                            $"{baseline.Environment.DisplayName} with value '{baselineSetting.Value}' " +
                            $"but missing from {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["SettingKey"] = key,
                            ["Category"] = baselineSetting.Category,
                            ["BaselineValue"] = baselineSetting.Value ?? "(null)",
                            ["BaselineEnvironment"] = baseline.Environment.DisplayName
                        }
                    };
                    continue;
                }

                // Both exist — compare values
                if (!string.Equals(baselineSetting.Value, targetSetting.Value, StringComparison.Ordinal))
                {
                    yield return new Finding
                    {
                        FindingId = $"SET-DRIFT-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.SettingsDrift,
                        Severity = IsSecurity(baselineSetting) ? Severity.High : Severity.Medium,
                        Title = $"Setting '{key}' differs between {baseline.Environment.DisplayName} and {target.Environment.DisplayName}",
                        Description = $"Setting '{key}' ({baselineSetting.Category}): " +
                            $"{baseline.Environment.DisplayName}='{baselineSetting.Value}' vs " +
                            $"{target.Environment.DisplayName}='{targetSetting.Value}'.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["SettingKey"] = key,
                            ["Category"] = baselineSetting.Category,
                            ["BaselineValue"] = baselineSetting.Value ?? "(null)",
                            ["TargetValue"] = targetSetting.Value ?? "(null)",
                            ["BaselineEnvironment"] = baseline.Environment.DisplayName,
                            ["TargetEnvironment"] = target.Environment.DisplayName
                        }
                    };
                }
            }
        }
    }

    private static bool IsSecurity(EnvironmentSetting setting) =>
        string.Equals(setting.Category, "Security", StringComparison.OrdinalIgnoreCase);
}
