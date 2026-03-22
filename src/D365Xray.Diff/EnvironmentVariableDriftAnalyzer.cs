using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects environment variable drift between environments.
/// Flags missing definitions, type mismatches, and value differences.
/// </summary>
internal static class EnvironmentVariableDriftAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineVars = baseline.EnvironmentVariables
            .ToDictionary(v => v.SchemaName, StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetVars = target.EnvironmentVariables
                .ToDictionary(v => v.SchemaName, StringComparer.OrdinalIgnoreCase);

            foreach (var (schema, baseVar) in baselineVars.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetVars.TryGetValue(schema, out var targetVar))
                {
                    yield return new Finding
                    {
                        FindingId = $"ENVVAR-MISSING-{schema}-{target.Environment.DisplayName}",
                        Category = FindingCategory.EnvironmentVariableDrift,
                        Severity = baseVar.IsRequired ? Severity.High : Severity.Medium,
                        Title = $"Environment variable '{schema}' missing from {target.Environment.DisplayName}",
                        Description = $"Environment variable '{baseVar.DisplayName ?? schema}' ({baseVar.Type}) " +
                            $"is defined in {baseline.Environment.DisplayName} but missing from " +
                            $"{target.Environment.DisplayName}." +
                            (baseVar.IsRequired ? " This variable is marked as required." : ""),
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["SchemaName"] = schema,
                            ["Type"] = baseVar.Type.ToString(),
                            ["IsRequired"] = baseVar.IsRequired.ToString(),
                            ["BaselineEnvironment"] = baseline.Environment.DisplayName
                        }
                    };
                    continue;
                }

                // Type mismatch
                if (baseVar.Type != targetVar.Type)
                {
                    yield return new Finding
                    {
                        FindingId = $"ENVVAR-TYPEDRIFT-{schema}-{target.Environment.DisplayName}",
                        Category = FindingCategory.EnvironmentVariableDrift,
                        Severity = Severity.High,
                        Title = $"Environment variable '{schema}' type differs",
                        Description = $"Variable '{schema}' is {baseVar.Type} in " +
                            $"{baseline.Environment.DisplayName} but {targetVar.Type} in " +
                            $"{target.Environment.DisplayName}.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["SchemaName"] = schema,
                            ["BaselineType"] = baseVar.Type.ToString(),
                            ["TargetType"] = targetVar.Type.ToString()
                        }
                    };
                }

                // Value drift (compare effective values: current ?? default)
                var baseEffective = baseVar.CurrentValue ?? baseVar.DefaultValue;
                var targetEffective = targetVar.CurrentValue ?? targetVar.DefaultValue;

                if (!string.Equals(baseEffective, targetEffective, StringComparison.Ordinal))
                {
                    // Skip secrets — we can detect presence but not compare values
                    if (baseVar.Type == EnvironmentVariableType.Secret)
                    {
                        continue;
                    }

                    yield return new Finding
                    {
                        FindingId = $"ENVVAR-VALUEDRIFT-{schema}-{target.Environment.DisplayName}",
                        Category = FindingCategory.EnvironmentVariableDrift,
                        Severity = Severity.Medium,
                        Title = $"Environment variable '{schema}' value differs",
                        Description = $"Variable '{schema}' has different effective values: " +
                            $"'{baseEffective ?? "(empty)"}' in {baseline.Environment.DisplayName} vs " +
                            $"'{targetEffective ?? "(empty)"}' in {target.Environment.DisplayName}.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["SchemaName"] = schema,
                            ["BaselineValue"] = baseEffective ?? "(null)",
                            ["TargetValue"] = targetEffective ?? "(null)"
                        }
                    };
                }

                // Required variable without value
                if (targetVar.IsRequired && !targetVar.HasValue)
                {
                    yield return new Finding
                    {
                        FindingId = $"ENVVAR-NOVAL-{schema}-{target.Environment.DisplayName}",
                        Category = FindingCategory.EnvironmentVariableDrift,
                        Severity = Severity.High,
                        Title = $"Required environment variable '{schema}' has no value in {target.Environment.DisplayName}",
                        Description = $"Variable '{schema}' is marked as required but has no default or current value " +
                            $"in {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["SchemaName"] = schema,
                            ["TargetEnvironment"] = target.Environment.DisplayName
                        }
                    };
                }
            }
        }
    }
}
