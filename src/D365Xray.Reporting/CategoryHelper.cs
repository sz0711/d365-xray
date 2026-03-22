using D365Xray.Core.Model;

namespace D365Xray.Reporting;

/// <summary>
/// Shared helpers for category scope and applicability logic,
/// used by both HTML and Markdown exporters.
/// </summary>
internal static class CategoryHelper
{
    public static string GetCategoryScope(FindingCategory category) => category switch
    {
        FindingCategory.LayerOverride => "cross-env + single-env",
        FindingCategory.DependencyConflict => "cross-env + single-env",
        FindingCategory.ConnectionConfiguration => "cross-env + single-env",
        FindingCategory.PluginConfiguration => "cross-env + single-env",
        FindingCategory.EnvironmentVariableDrift => "cross-env + single-env",
        FindingCategory.WorkflowConfiguration => "cross-env + prod-like(single-env)",
        FindingCategory.BusinessRuleDrift => "cross-env + prod-like(single-env)",
        FindingCategory.ConfigurationAnomaly => "single-env",
        _ => "cross-env"
    };

    public static bool IsCategoryApplicable(FindingCategory category, int envCount, bool hasProdLike)
    {
        var isSingleEnvRun = envCount == 1;
        var isCrossEnvRun = envCount > 1;

        return category switch
        {
            FindingCategory.ConfigurationAnomaly => isSingleEnvRun,
            FindingCategory.LayerOverride => isCrossEnvRun || isSingleEnvRun,
            FindingCategory.DependencyConflict => isCrossEnvRun || isSingleEnvRun,
            FindingCategory.ConnectionConfiguration => isCrossEnvRun || isSingleEnvRun,
            FindingCategory.PluginConfiguration => isCrossEnvRun || isSingleEnvRun,
            FindingCategory.EnvironmentVariableDrift => isCrossEnvRun || isSingleEnvRun,
            FindingCategory.WorkflowConfiguration => isCrossEnvRun || (isSingleEnvRun && hasProdLike),
            FindingCategory.BusinessRuleDrift => isCrossEnvRun || (isSingleEnvRun && hasProdLike),
            _ => isCrossEnvRun
        };
    }
}
