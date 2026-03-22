using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects plugin assembly and SDK step drift between environments.
/// Flags version mismatches, missing assemblies, disabled steps, and configuration drift.
/// </summary>
internal static class PluginAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        foreach (var f in AnalyzeAssemblies(snapshots))
        {
            yield return f;
        }

        foreach (var f in AnalyzeSdkSteps(snapshots))
        {
            yield return f;
        }
    }

    private static IEnumerable<Finding> AnalyzeAssemblies(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselinePlugins = baseline.PluginAssemblies
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetPlugins = target.PluginAssemblies
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var (name, basePlg) in baselinePlugins.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetPlugins.TryGetValue(name, out var targetPlg))
                {
                    yield return new Finding
                    {
                        FindingId = $"PLG-MISSING-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.PluginConfiguration,
                        Severity = Severity.High,
                        Title = $"Plugin assembly '{name}' missing from {target.Environment.DisplayName}",
                        Description = $"Plugin assembly '{name}' (v{basePlg.Version}) is registered in " +
                            $"{baseline.Environment.DisplayName} but missing from {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["AssemblyName"] = name,
                            ["PluginAssemblyId"] = basePlg.PluginAssemblyId.ToString(),
                            ["BaselineVersion"] = basePlg.Version ?? "(null)",
                            ["BaselineEnvironment"] = baseline.Environment.DisplayName,
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                    continue;
                }

                // Version drift
                if (!string.Equals(basePlg.Version, targetPlg.Version, StringComparison.OrdinalIgnoreCase))
                {
                    yield return new Finding
                    {
                        FindingId = $"PLG-VERSION-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.PluginConfiguration,
                        Severity = Severity.Medium,
                        Title = $"Plugin assembly '{name}' version drift",
                        Description = $"Plugin '{name}' is v{basePlg.Version} in " +
                            $"{baseline.Environment.DisplayName} but v{targetPlg.Version} in {target.Environment.DisplayName}.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["AssemblyName"] = name,
                            ["PluginAssemblyId"] = basePlg.PluginAssemblyId.ToString(),
                            ["BaselineVersion"] = basePlg.Version ?? "(null)",
                            ["TargetVersion"] = targetPlg.Version ?? "(null)",
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }

                // Isolation mode drift
                if (basePlg.IsolationMode != targetPlg.IsolationMode)
                {
                    yield return new Finding
                    {
                        FindingId = $"PLG-ISOLATION-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.PluginConfiguration,
                        Severity = Severity.High,
                        Title = $"Plugin assembly '{name}' isolation mode differs",
                        Description = $"Plugin '{name}' runs in {basePlg.IsolationMode} mode in " +
                            $"{baseline.Environment.DisplayName} but {targetPlg.IsolationMode} in " +
                            $"{target.Environment.DisplayName}. This affects security boundaries.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["AssemblyName"] = name,
                            ["PluginAssemblyId"] = basePlg.PluginAssemblyId.ToString(),
                            ["BaselineIsolation"] = basePlg.IsolationMode.ToString(),
                            ["TargetIsolation"] = targetPlg.IsolationMode.ToString(),
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }
            }
        }
    }

    private static IEnumerable<Finding> AnalyzeSdkSteps(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineSteps = baseline.SdkSteps
            .ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetSteps = target.SdkSteps
                .ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var (name, baseStep) in baselineSteps.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetSteps.TryGetValue(name, out var targetStep))
                {
                    yield return new Finding
                    {
                        FindingId = $"STEP-MISSING-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.PluginConfiguration,
                        Severity = Severity.High,
                        Title = $"SDK step '{name}' missing from {target.Environment.DisplayName}",
                        Description = $"SDK step '{name}' ({baseStep.MessageName}/{baseStep.PrimaryEntity}) " +
                            $"is registered in {baseline.Environment.DisplayName} but missing from " +
                            $"{target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["StepName"] = name,
                            ["StepId"] = baseStep.StepId.ToString(),
                            ["Message"] = baseStep.MessageName ?? "(null)",
                            ["Entity"] = baseStep.PrimaryEntity ?? "(null)",
                            ["BaselineEnvironment"] = baseline.Environment.DisplayName,
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                    continue;
                }

                // Disabled state drift
                if (baseStep.IsDisabled != targetStep.IsDisabled)
                {
                    var moreRisky = targetStep.IsDisabled; // disabled in target = might break things
                    yield return new Finding
                    {
                        FindingId = $"STEP-STATE-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.PluginConfiguration,
                        Severity = moreRisky ? Severity.High : Severity.Medium,
                        Title = $"SDK step '{name}' enabled/disabled state differs",
                        Description = $"Step '{name}' is {(baseStep.IsDisabled ? "disabled" : "enabled")} in " +
                            $"{baseline.Environment.DisplayName} but " +
                            $"{(targetStep.IsDisabled ? "disabled" : "enabled")} in {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["StepName"] = name,
                            ["StepId"] = baseStep.StepId.ToString(),
                            ["BaselineDisabled"] = baseStep.IsDisabled.ToString(),
                            ["TargetDisabled"] = targetStep.IsDisabled.ToString(),
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }

                // Stage drift
                if (baseStep.Stage != targetStep.Stage)
                {
                    yield return new Finding
                    {
                        FindingId = $"STEP-STAGE-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.PluginConfiguration,
                        Severity = Severity.High,
                        Title = $"SDK step '{name}' runs at different pipeline stage",
                        Description = $"Step '{name}' runs at {baseStep.Stage} in " +
                            $"{baseline.Environment.DisplayName} but {targetStep.Stage} in " +
                            $"{target.Environment.DisplayName}. This changes execution order.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["StepName"] = name,
                            ["StepId"] = baseStep.StepId.ToString(),
                            ["BaselineStage"] = baseStep.Stage.ToString(),
                            ["TargetStage"] = targetStep.Stage.ToString(),
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }
            }
        }
    }
}
