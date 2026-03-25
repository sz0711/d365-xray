using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects system charts (saved query visualizations) from a Dataverse environment.
/// OData entity: savedqueryvisualizations.
/// </summary>
internal static class ChartCollector
{
    private const string EntitySet = "savedqueryvisualizations";
    private const string QueryOptions =
        "$select=savedqueryvisualizationid,name,description,primaryentitytypecode," +
        "ismanaged,isdefault,modifiedon" +
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<ChartDefinition>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<ChartDefinition>();

        try
        {
            await foreach (var page in client.GetPagedAsync(EntitySet, QueryOptions, cancellationToken))
            {
                using (page)
                {
                    foreach (var item in JsonHelper.GetValueArray(page))
                    {
                        items.Add(Map(item));
                    }
                }
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound
                                               or System.Net.HttpStatusCode.BadRequest)
        {
            logger.LogWarning(
                "Chart visualizations entity is not available. " +
                "Chart analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static ChartDefinition Map(JsonElement item)
    {
        return new ChartDefinition
        {
            ChartId = JsonHelper.GetGuid(item, "savedqueryvisualizationid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            Description = JsonHelper.GetString(item, "description"),
            EntityLogicalName = JsonHelper.GetString(item, "primaryentitytypecode") ?? "unknown",
            IsManaged = JsonHelper.GetBool(item, "ismanaged"),
            IsDefault = JsonHelper.GetBool(item, "isdefault"),
            ModifiedOn = JsonHelper.GetDateTimeOffset(item, "modifiedon")
        };
    }
}
