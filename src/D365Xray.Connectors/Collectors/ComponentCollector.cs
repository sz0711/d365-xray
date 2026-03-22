using System.Text.Json;
using D365Xray.Core.Model;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects solution components from a Dataverse environment.
/// OData entity: solutioncomponents, expanded with solutionid for the solution unique name.
/// </summary>
internal static class ComponentCollector
{
    private const string EntitySet = "solutioncomponents";
    private const string QueryOptions =
        "$select=solutioncomponentid,componenttype,objectid,rootcomponentbehavior" +
        "&$expand=solutionid($select=uniquename)";

    public static async Task<IReadOnlyList<SolutionComponent>> CollectAsync(
        IDataverseClient client,
        CancellationToken cancellationToken)
    {
        var components = new List<SolutionComponent>();

        await foreach (var page in client.GetPagedAsync(EntitySet, QueryOptions, cancellationToken))
        {
            using (page)
            {
                foreach (var item in JsonHelper.GetValueArray(page))
                {
                    components.Add(MapComponent(item));
                }
            }
        }

        return components;
    }

    private static SolutionComponent MapComponent(JsonElement item)
    {
        var typeValue = JsonHelper.GetInt(item, "componenttype");
        var componentType = Enum.IsDefined(typeof(ComponentType), typeValue)
            ? (ComponentType)typeValue
            : ComponentType.Unknown;

        var behaviorValue = JsonHelper.GetInt(item, "rootcomponentbehavior");
        var behavior = Enum.IsDefined(typeof(RootComponentBehavior), behaviorValue)
            ? (RootComponentBehavior)behaviorValue
            : RootComponentBehavior.IncludeSubcomponents;

        string solutionUniqueName = "unknown";
        if (item.TryGetProperty("solutionid", out var solObj) && solObj.ValueKind == JsonValueKind.Object)
        {
            solutionUniqueName = JsonHelper.GetString(solObj, "uniquename") ?? "unknown";
        }

        return new SolutionComponent
        {
            ComponentId = JsonHelper.GetGuid(item, "objectid"),
            ComponentType = componentType,
            SolutionUniqueName = solutionUniqueName,
            Behavior = behavior
        };
    }
}
