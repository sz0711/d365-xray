using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects component layer data from a Dataverse environment.
/// Uses the msdyn_componentlayer virtual entity which provides the Solution Layers view.
/// Degrades gracefully if the entity is not available (older Dataverse versions).
/// </summary>
internal static class LayerCollector
{
    private const string EntitySet = "msdyn_componentlayers";
    private const string QueryOptions =
        "$select=msdyn_componentid,msdyn_solutioncomponentname,msdyn_name," +
        "msdyn_order,msdyn_publishername,msdyn_solutionname";

    public static async Task<IReadOnlyList<ComponentLayer>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var layers = new List<ComponentLayer>();

        try
        {
            await foreach (var page in client.GetPagedAsync(EntitySet, QueryOptions, cancellationToken))
            {
                using (page)
                {
                    foreach (var item in JsonHelper.GetValueArray(page))
                    {
                        var layer = MapLayer(item);
                        if (layer is not null)
                        {
                            layers.Add(layer);
                        }
                    }
                }
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound
                                               or System.Net.HttpStatusCode.BadRequest)
        {
            logger.LogWarning(
                "Component layer entity (msdyn_componentlayer) is not available in this environment. " +
                "Layer analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return layers;
    }

    private static ComponentLayer? MapLayer(JsonElement item)
    {
        var componentIdStr = JsonHelper.GetString(item, "msdyn_componentid");
        if (componentIdStr is null || !Guid.TryParse(componentIdStr, out var componentId))
        {
            return null;
        }

        var solutionName = JsonHelper.GetString(item, "msdyn_solutionname") ?? "unknown";
        var displayName = JsonHelper.GetString(item, "msdyn_name") ?? solutionName;

        // In Dataverse, the "Active" solution layer represents unmanaged customizations.
        // All other layers are from managed solutions.
        var isManaged = !string.Equals(solutionName, "Active", StringComparison.OrdinalIgnoreCase);

        var componentTypeName = JsonHelper.GetString(item, "msdyn_solutioncomponentname");
        var componentType = MapComponentTypeName(componentTypeName);

        return new ComponentLayer
        {
            ComponentId = componentId,
            ComponentType = componentType,
            SolutionUniqueName = solutionName,
            SolutionDisplayName = displayName,
            Order = JsonHelper.GetInt(item, "msdyn_order"),
            IsManaged = isManaged,
            PublisherName = JsonHelper.GetString(item, "msdyn_publishername")
        };
    }

    /// <summary>
    /// Maps the display string from msdyn_solutioncomponentname to ComponentType enum.
    /// Falls back to Unknown for unrecognized types.
    /// </summary>
    private static ComponentType MapComponentTypeName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return ComponentType.Unknown;
        }

        return name.ToLowerInvariant() switch
        {
            "entity" => ComponentType.Entity,
            "attribute" or "field" => ComponentType.Attribute,
            "relationship" => ComponentType.Relationship,
            "option set" or "optionset" => ComponentType.OptionSet,
            "form" or "system form" or "systemform" => ComponentType.SystemForm,
            "view" or "saved query" or "savedquery" => ComponentType.SavedQuery,
            "chart" or "saved query visualization" => ComponentType.SavedQueryVisualization,
            "web resource" or "webresource" => ComponentType.WebResource,
            "workflow" or "process" => ComponentType.Workflow,
            "plugin assembly" or "pluginassembly" => ComponentType.PluginAssembly,
            "plugin type" or "plugintype" or "plugin" => ComponentType.PluginType,
            "sdk message processing step" or "sdkmessageprocessingstep" => ComponentType.SdkMessageProcessingStep,
            "role" or "security role" => ComponentType.Role,
            "site map" or "sitemap" => ComponentType.SiteMap,
            "model-driven app" or "model driven app" or "app module" => ComponentType.ModelDrivenApp,
            "canvas app" or "canvasapp" => ComponentType.CanvasApp,
            "custom control" or "customcontrol" => ComponentType.CustomControl,
            "environment variable definition" => ComponentType.EnvironmentVariableDefinition,
            "environment variable value" => ComponentType.EnvironmentVariableValue,
            "connector" => ComponentType.Connector,
            "email template" => ComponentType.EmailTemplate,
            "report" => ComponentType.Report,
            "connection role" => ComponentType.ConnectionRole,
            "duplicate rule" or "duplicate detection rule" => ComponentType.DuplicateRule,
            "sla" => ComponentType.SLA,
            _ => ComponentType.Unknown
        };
    }
}
