using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects workflow/flow drift between environments.
/// Flags missing workflows, activation state drift, trigger changes, and mode mismatches.
/// </summary>
internal static class WorkflowDriftAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineFlows = BuildLookup(baseline.Workflows);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetFlows = BuildLookup(target.Workflows);

            foreach (var (key, baseFlow) in baselineFlows.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetFlows.TryGetValue(key, out var targetFlow))
                {
                    yield return new Finding
                    {
                        FindingId = $"WFL-MISSING-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.WorkflowConfiguration,
                        Severity = baseFlow.IsActivated ? Severity.High : Severity.Medium,
                        Title = $"Workflow '{baseFlow.Name}' missing from {target.Environment.DisplayName}",
                        Description = $"Workflow '{baseFlow.Name}' ({baseFlow.Category}) is " +
                            $"{(baseFlow.IsActivated ? "active" : "inactive")} in " +
                            $"{baseline.Environment.DisplayName} but missing from {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["WorkflowName"] = baseFlow.Name,
                            ["WorkflowId"] = baseFlow.WorkflowId.ToString(),
                            ["Category"] = baseFlow.Category.ToString(),
                            ["BaselineActivated"] = baseFlow.IsActivated.ToString(),
                            ["BaselineEnvironment"] = baseline.Environment.DisplayName,
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                    continue;
                }

                // Activation state drift
                if (baseFlow.IsActivated != targetFlow.IsActivated)
                {
                    var moreRisky = baseFlow.IsActivated && !targetFlow.IsActivated;
                    yield return new Finding
                    {
                        FindingId = $"WFL-STATE-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.WorkflowConfiguration,
                        Severity = moreRisky ? Severity.High : Severity.Medium,
                        Title = $"Workflow '{baseFlow.Name}' activation state differs",
                        Description = $"Workflow '{baseFlow.Name}' is " +
                            $"{(baseFlow.IsActivated ? "active" : "inactive")} in " +
                            $"{baseline.Environment.DisplayName} but " +
                            $"{(targetFlow.IsActivated ? "active" : "inactive")} in {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["WorkflowName"] = baseFlow.Name,
                            ["WorkflowId"] = baseFlow.WorkflowId.ToString(),
                            ["BaselineActivated"] = baseFlow.IsActivated.ToString(),
                            ["TargetActivated"] = targetFlow.IsActivated.ToString(),
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }

                // Mode drift (background vs realtime)
                if (baseFlow.Mode != targetFlow.Mode)
                {
                    yield return new Finding
                    {
                        FindingId = $"WFL-MODE-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.WorkflowConfiguration,
                        Severity = Severity.High,
                        Title = $"Workflow '{baseFlow.Name}' mode differs",
                        Description = $"Workflow '{baseFlow.Name}' runs as {baseFlow.Mode} in " +
                            $"{baseline.Environment.DisplayName} but as {targetFlow.Mode} in " +
                            $"{target.Environment.DisplayName}.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["WorkflowName"] = baseFlow.Name,
                            ["WorkflowId"] = baseFlow.WorkflowId.ToString(),
                            ["BaselineMode"] = baseFlow.Mode.ToString(),
                            ["TargetMode"] = targetFlow.Mode.ToString(),
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }

                // Trigger drift (create / update / delete)
                if (!string.Equals(baseFlow.TriggerOnCreate, targetFlow.TriggerOnCreate, StringComparison.Ordinal) ||
                    !string.Equals(baseFlow.TriggerOnUpdate, targetFlow.TriggerOnUpdate, StringComparison.Ordinal) ||
                    !string.Equals(baseFlow.TriggerOnDelete, targetFlow.TriggerOnDelete, StringComparison.Ordinal))
                {
                    yield return new Finding
                    {
                        FindingId = $"WFL-TRIGGER-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.WorkflowConfiguration,
                        Severity = Severity.Medium,
                        Title = $"Workflow '{baseFlow.Name}' trigger configuration differs",
                        Description = $"Workflow '{baseFlow.Name}' has different trigger settings in " +
                            $"{target.Environment.DisplayName} compared to {baseline.Environment.DisplayName}.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["WorkflowName"] = baseFlow.Name,
                            ["WorkflowId"] = baseFlow.WorkflowId.ToString(),
                            ["BaselineTriggerCreate"] = baseFlow.TriggerOnCreate ?? "(null)",
                            ["TargetTriggerCreate"] = targetFlow.TriggerOnCreate ?? "(null)",
                            ["BaselineTriggerUpdate"] = baseFlow.TriggerOnUpdate ?? "(null)",
                            ["TargetTriggerUpdate"] = targetFlow.TriggerOnUpdate ?? "(null)",
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }
            }
        }
    }

    /// <summary>
    /// Build lookup using UniqueName if available, otherwise Name.
    /// </summary>
    private static Dictionary<string, WorkflowDefinition> BuildLookup(
        IReadOnlyList<WorkflowDefinition> workflows)
    {
        return workflows.ToDictionary(
            w => w.UniqueName ?? w.Name,
            StringComparer.OrdinalIgnoreCase);
    }
}
