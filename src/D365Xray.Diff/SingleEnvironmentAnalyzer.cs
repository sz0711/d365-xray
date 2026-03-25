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

        foreach (var finding in DetectDisabledSdkSteps(snapshot))
        {
            yield return finding;
        }

        foreach (var finding in DetectDeactivatedWorkflows(snapshot))
        {
            yield return finding;
        }

        foreach (var finding in DetectDeactivatedBusinessRules(snapshot))
        {
            yield return finding;
        }

        foreach (var finding in DetectMissingEnvironmentVariableValues(snapshot))
        {
            yield return finding;
        }

        foreach (var finding in DetectOrphanedConnectionReferences(snapshot))
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
                    ["SolutionId"] = sol.SolutionId.ToString(),
                    ["Version"] = sol.Version,
                    ["Publisher"] = sol.Publisher.UniqueName,
                    ["EnvironmentType"] = envLabel,
                    ["EnvironmentUrl"] = snapshot.Environment.EnvironmentUrl.ToString()
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
                    ["Publishers"] = string.Join(", ", publishers),
                    ["EnvironmentUrl"] = snapshot.Environment.EnvironmentUrl.ToString()
                }
            };
        }
    }

    /// <summary>
    /// Flags SDK steps that are disabled — may indicate incomplete deployment or intentional deactivation.
    /// </summary>
    private static IEnumerable<Finding> DetectDisabledSdkSteps(EnvironmentSnapshot snapshot)
    {
        var isProdLike = IsProductionLike(snapshot.Environment.EnvironmentType);

        foreach (var step in snapshot.SdkSteps.Where(s => s.IsDisabled).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            yield return new Finding
            {
                FindingId = $"SINGLE-STEP-DISABLED-{step.Name}-{snapshot.Environment.DisplayName}",
                Category = FindingCategory.PluginConfiguration,
                Severity = isProdLike ? Severity.Medium : Severity.Info,
                Title = $"SDK step '{step.Name}' is disabled in {snapshot.Environment.DisplayName}",
                Description = $"Plugin step '{step.Name}' ({step.MessageName}/{step.PrimaryEntity}) is disabled. " +
                    (isProdLike ? "Verify this is intentional in a production-like environment." : "Expected for development."),
                AffectedEnvironments = [snapshot.Environment.DisplayName],
                Details = new Dictionary<string, string>
                {
                    ["StepName"] = step.Name,
                    ["StepId"] = step.StepId.ToString(),
                    ["Message"] = step.MessageName ?? "(null)",
                    ["Entity"] = step.PrimaryEntity ?? "(null)",
                    ["EnvironmentUrl"] = snapshot.Environment.EnvironmentUrl.ToString()
                }
            };
        }
    }

    /// <summary>
    /// Flags workflows/flows that are deactivated in production-like environments.
    /// </summary>
    private static IEnumerable<Finding> DetectDeactivatedWorkflows(EnvironmentSnapshot snapshot)
    {
        var isProdLike = IsProductionLike(snapshot.Environment.EnvironmentType);
        if (!isProdLike)
        {
            yield break;
        }

        foreach (var wf in snapshot.Workflows.Where(w => !w.IsActivated).OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase))
        {
            yield return new Finding
            {
                FindingId = $"SINGLE-WFL-INACTIVE-{wf.Name}-{snapshot.Environment.DisplayName}",
                Category = FindingCategory.WorkflowConfiguration,
                Severity = Severity.Medium,
                Title = $"Workflow '{wf.Name}' is inactive in {snapshot.Environment.DisplayName}",
                Description = $"Workflow '{wf.Name}' ({wf.Category}) is deactivated in a production-like environment. " +
                    $"Verify this is intentional.",
                AffectedEnvironments = [snapshot.Environment.DisplayName],
                Details = new Dictionary<string, string>
                {
                    ["WorkflowName"] = wf.Name,
                    ["WorkflowId"] = wf.WorkflowId.ToString(),
                    ["Category"] = wf.Category.ToString(),
                    ["EnvironmentUrl"] = snapshot.Environment.EnvironmentUrl.ToString()
                }
            };
        }
    }

    /// <summary>
    /// Flags business rules that are deactivated in production-like environments.
    /// </summary>
    private static IEnumerable<Finding> DetectDeactivatedBusinessRules(EnvironmentSnapshot snapshot)
    {
        var isProdLike = IsProductionLike(snapshot.Environment.EnvironmentType);
        if (!isProdLike)
        {
            yield break;
        }

        foreach (var rule in snapshot.BusinessRules.Where(r => !r.IsActivated).OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            yield return new Finding
            {
                FindingId = $"SINGLE-BRL-INACTIVE-{rule.Name}-{snapshot.Environment.DisplayName}",
                Category = FindingCategory.BusinessRuleDrift,
                Severity = Severity.Medium,
                Title = $"Business rule '{rule.Name}' is inactive in {snapshot.Environment.DisplayName}",
                Description = $"Business rule '{rule.Name}' on entity '{rule.PrimaryEntity}' is deactivated " +
                    $"in a production-like environment. Verify this is intentional.",
                AffectedEnvironments = [snapshot.Environment.DisplayName],
                Details = new Dictionary<string, string>
                {
                    ["RuleName"] = rule.Name,
                    ["BusinessRuleId"] = rule.BusinessRuleId.ToString(),
                    ["Entity"] = rule.PrimaryEntity,
                    ["EnvironmentUrl"] = snapshot.Environment.EnvironmentUrl.ToString()
                }
            };
        }
    }

    /// <summary>
    /// Flags required environment variables that have no value.
    /// </summary>
    private static IEnumerable<Finding> DetectMissingEnvironmentVariableValues(EnvironmentSnapshot snapshot)
    {
        foreach (var v in snapshot.EnvironmentVariables
            .Where(v => v.IsRequired && !v.HasValue)
            .OrderBy(v => v.SchemaName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new Finding
            {
                FindingId = $"SINGLE-ENVVAR-NOVAL-{v.SchemaName}-{snapshot.Environment.DisplayName}",
                Category = FindingCategory.EnvironmentVariableDrift,
                Severity = Severity.High,
                Title = $"Required environment variable '{v.SchemaName}' has no value",
                Description = $"Environment variable '{v.DisplayName ?? v.SchemaName}' ({v.Type}) is marked as " +
                    $"required but has no default or current value in {snapshot.Environment.DisplayName}.",
                AffectedEnvironments = [snapshot.Environment.DisplayName],
                Details = new Dictionary<string, string>
                {
                    ["SchemaName"] = v.SchemaName,
                    ["DefinitionId"] = v.DefinitionId.ToString(),
                    ["Type"] = v.Type.ToString(),
                    ["EnvironmentUrl"] = snapshot.Environment.EnvironmentUrl.ToString()
                }
            };
        }
    }

    /// <summary>
    /// Flags connection references that have no connection ID bound (orphaned).
    /// Excludes Microsoft first-party connection references (msdyn_, mscrm_) which
    /// are system-managed and typically don't require user-bound connections.
    /// </summary>
    private static IEnumerable<Finding> DetectOrphanedConnectionReferences(EnvironmentSnapshot snapshot)
    {
        foreach (var cr in snapshot.ConnectionReferences
            .Where(c => string.IsNullOrEmpty(c.ConnectionId) && !IsMicrosoftConnectionReference(c))
            .OrderBy(c => c.ConnectionReferenceLogicalName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new Finding
            {
                FindingId = $"SINGLE-CONN-ORPHAN-{cr.ConnectionReferenceLogicalName}-{snapshot.Environment.DisplayName}",
                Category = FindingCategory.ConnectionConfiguration,
                Severity = Severity.High,
                Title = $"Connection reference '{cr.ConnectionReferenceLogicalName}' has no connection",
                Description = $"Connection reference '{cr.DisplayName ?? cr.ConnectionReferenceLogicalName}' " +
                    $"(connector: {cr.ConnectorId ?? "unknown"}) has no active connection bound. " +
                    $"Flows and plugins relying on this reference will fail.",
                AffectedEnvironments = [snapshot.Environment.DisplayName],
                Details = new Dictionary<string, string>
                {
                    ["ConnectionReferenceLogicalName"] = cr.ConnectionReferenceLogicalName,
                    ["ConnectionReferenceId"] = cr.ConnectionReferenceId.ToString(),
                    ["ConnectorId"] = cr.ConnectorId ?? "(null)",
                    ["EnvironmentUrl"] = snapshot.Environment.EnvironmentUrl.ToString()
                }
            };
        }
    }

    private static bool IsMicrosoftConnectionReference(ConnectionReference cr)
    {
        var name = cr.ConnectionReferenceLogicalName;
        return name.StartsWith("msdyn_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("mscrm_", StringComparison.OrdinalIgnoreCase);
    }
}
