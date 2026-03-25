using System.Web;

namespace D365Xray.Reporting;

/// <summary>
/// Builds deep links into Dynamics 365 / Power Platform admin pages
/// from environment URL + entity type + GUID.
/// </summary>
internal static class DeepLinkBuilder
{
    /// <summary>
    /// Tries to build a deep link from the finding's Details dictionary.
    /// Returns null when no suitable GUID + EnvironmentUrl pair is found.
    /// </summary>
    public static string? TryBuildLink(IReadOnlyDictionary<string, string> details)
    {
        if (!details.TryGetValue("EnvironmentUrl", out var envUrl) ||
            string.IsNullOrWhiteSpace(envUrl))
        {
            return null;
        }

        var baseUrl = envUrl.TrimEnd('/');

        // Solution
        if (details.TryGetValue("SolutionId", out var solId) && IsGuid(solId))
        {
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=solution&id={Uri.EscapeDataString(solId)}";
        }

        // Workflow / Cloud Flow — route modern flows to Power Automate portal
        if (details.TryGetValue("WorkflowId", out var wfId) && IsGuid(wfId))
        {
            if (details.TryGetValue("Category", out var cat) &&
                string.Equals(cat, "ModernFlow", StringComparison.OrdinalIgnoreCase))
            {
                // Power Automate cloud flow detail page
                return $"https://make.powerautomate.com/environments/{ExtractEnvironmentIdFromUrl(baseUrl)}/flows/{Uri.EscapeDataString(wfId)}/details";
            }
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=workflow&id={Uri.EscapeDataString(wfId)}";
        }

        // Business Rule
        if (details.TryGetValue("BusinessRuleId", out var brId) && IsGuid(brId))
        {
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=workflow&id={Uri.EscapeDataString(brId)}";
        }

        // Form
        if (details.TryGetValue("FormId", out var formId) && IsGuid(formId))
        {
            // Open form editor in the maker portal
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=systemform&id={Uri.EscapeDataString(formId)}";
        }

        // View (saved query)
        if (details.TryGetValue("ViewId", out var viewId) && IsGuid(viewId))
        {
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=savedquery&id={Uri.EscapeDataString(viewId)}";
        }

        // App Module
        if (details.TryGetValue("AppModuleId", out var appId) && IsGuid(appId))
        {
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=appmodule&id={Uri.EscapeDataString(appId)}";
        }

        // Security Role
        if (details.TryGetValue("RoleId", out var roleId) && IsGuid(roleId))
        {
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=role&id={Uri.EscapeDataString(roleId)}";
        }

        // Plugin Assembly
        if (details.TryGetValue("PluginAssemblyId", out var plgId) && IsGuid(plgId))
        {
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=pluginassembly&id={Uri.EscapeDataString(plgId)}";
        }

        // SDK Step
        if (details.TryGetValue("StepId", out var stepId) && IsGuid(stepId))
        {
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=sdkmessageprocessingstep&id={Uri.EscapeDataString(stepId)}";
        }

        // Web Resource
        if (details.TryGetValue("WebResourceId", out var wrId) && IsGuid(wrId))
        {
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=webresourceedit&id={Uri.EscapeDataString(wrId)}";
        }

        // Connection Reference
        if (details.TryGetValue("ConnectionReferenceId", out var crId) && IsGuid(crId))
        {
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=connectionreference&id={Uri.EscapeDataString(crId)}";
        }

        // Environment Variable
        if (details.TryGetValue("DefinitionId", out var evId) && IsGuid(evId))
        {
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=environmentvariabledefinition&id={Uri.EscapeDataString(evId)}";
        }

        // Service Endpoint
        if (details.TryGetValue("ServiceEndpointId", out var seId) && IsGuid(seId))
        {
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=serviceendpoint&id={Uri.EscapeDataString(seId)}";
        }

        // Component (generic)
        if (details.TryGetValue("ComponentId", out var compId) && IsGuid(compId))
        {
            return $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=solutioncomponent&id={Uri.EscapeDataString(compId)}";
        }

        return null;
    }

    private static bool IsGuid(string value) =>
        Guid.TryParse(value, out var g) && g != Guid.Empty;

    /// <summary>
    /// Extracts a pseudo environment-id from the Dataverse URL for use in Power Platform URLs.
    /// Falls back to using the hostname as the identifier.
    /// </summary>
    private static string ExtractEnvironmentIdFromUrl(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return Uri.EscapeDataString(uri.Host);
        }
        return "default";
    }
}
