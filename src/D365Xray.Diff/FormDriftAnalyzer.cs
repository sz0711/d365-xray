using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects form drift between environments.
/// Flags missing forms and managed-state differences.
/// </summary>
internal static class FormDriftAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineForms = BuildLookup(baseline.Forms);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetForms = BuildLookup(target.Forms);

            foreach (var (key, baseForm) in baselineForms.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetForms.TryGetValue(key, out var targetForm))
                {
                    yield return new Finding
                    {
                        FindingId = $"FORM-MISSING-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.FormDrift,
                        Severity = baseForm.FormType == FormType.Main ? Severity.High : Severity.Medium,
                        Title = $"Form '{baseForm.Name}' ({baseForm.FormType}) missing from {target.Environment.DisplayName}",
                        Description = $"Form '{baseForm.Name}' for entity '{baseForm.EntityLogicalName}' " +
                            $"({baseForm.FormType}) exists in {baseline.Environment.DisplayName} " +
                            $"but is missing from {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["FormName"] = baseForm.Name,
                            ["FormId"] = baseForm.FormId.ToString(),
                            ["Entity"] = baseForm.EntityLogicalName,
                            ["FormType"] = baseForm.FormType.ToString(),
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                    continue;
                }

                // Form type drift (main form became quick create, etc.)
                if (baseForm.FormType != targetForm.FormType)
                {
                    yield return new Finding
                    {
                        FindingId = $"FORM-TYPE-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.FormDrift,
                        Severity = Severity.Medium,
                        Title = $"Form '{baseForm.Name}' type differs",
                        Description = $"Form '{baseForm.Name}' is {baseForm.FormType} in " +
                            $"{baseline.Environment.DisplayName} but {targetForm.FormType} in " +
                            $"{target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["FormName"] = baseForm.Name,
                            ["FormId"] = baseForm.FormId.ToString(),
                            ["BaselineType"] = baseForm.FormType.ToString(),
                            ["TargetType"] = targetForm.FormType.ToString(),
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }
            }
        }
    }

    private static Dictionary<string, FormDefinition> BuildLookup(IReadOnlyList<FormDefinition> forms)
    {
        var lookup = new Dictionary<string, FormDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var form in forms)
        {
            var key = $"{form.EntityLogicalName}|{form.UniqueName ?? form.Name}";
            lookup.TryAdd(key, form);
        }
        return lookup;
    }
}
