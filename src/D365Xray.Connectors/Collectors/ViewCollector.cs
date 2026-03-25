using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects saved queries (system views) from a Dataverse environment.
/// OData entity: savedqueries.
/// </summary>
internal static class ViewCollector
{
    private const string EntitySet = "savedqueries";
    private const string QueryOptions =
        "$select=savedqueryid,name,description,returnedtypecode,isdefault," +
        "ismanaged,iscustomizable,querytype,modifiedon" +
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<ViewDefinition>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<ViewDefinition>();

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
                "Saved queries entity is not available. " +
                "View analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static ViewDefinition Map(JsonElement item)
    {
        return new ViewDefinition
        {
            ViewId = JsonHelper.GetGuid(item, "savedqueryid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            Description = JsonHelper.GetString(item, "description"),
            EntityLogicalName = JsonHelper.GetString(item, "returnedtypecode") ?? "unknown",
            IsDefault = JsonHelper.GetBool(item, "isdefault"),
            IsManaged = JsonHelper.GetBool(item, "ismanaged"),
            IsCustomizable = JsonHelper.GetBool(item, "iscustomizable"),
            QueryType = JsonHelper.GetInt(item, "querytype"),
            ModifiedOn = JsonHelper.GetDateTimeOffset(item, "modifiedon")
        };
    }
}
