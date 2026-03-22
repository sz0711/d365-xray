using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects web resources from a Dataverse environment.
/// OData entity: webresourceset.
/// </summary>
internal static class WebResourceCollector
{
    private const string _entitySet = "webresourceset";
    private const string _queryOptions =
        "$select=webresourceid,name,displayname,webresourcetype,ismanaged,iscustomizable,modifiedon" +
        "&$filter=ishidden eq false" +
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<WebResource>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<WebResource>();

        try
        {
            await foreach (var page in client.GetPagedAsync(_entitySet, _queryOptions, cancellationToken))
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
                "Web resources entity is not available. " +
                "Web resource analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static WebResource Map(JsonElement item)
    {
        var typeValue = JsonHelper.GetInt(item, "webresourcetype");

        return new WebResource
        {
            WebResourceId = JsonHelper.GetGuid(item, "webresourceid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            DisplayName = JsonHelper.GetString(item, "displayname"),
            WebResourceType = Enum.IsDefined(typeof(WebResourceType), typeValue)
                ? (WebResourceType)typeValue
                : WebResourceType.Unknown,
            IsManaged = JsonHelper.GetBool(item, "ismanaged"),
            IsCustomizable = JsonHelper.GetBool(item, "iscustomizable", true),
            ModifiedOn = JsonHelper.GetDateTimeOffset(item, "modifiedon")
        };
    }
}
