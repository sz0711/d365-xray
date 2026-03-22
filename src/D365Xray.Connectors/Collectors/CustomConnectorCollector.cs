using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects custom connectors from a Dataverse environment.
/// OData entity: connectors.
/// </summary>
internal static class CustomConnectorCollector
{
    private const string _entitySet = "connectors";
    private const string _queryOptions =
        "$select=connectorid,name,displayname,connectortype,description" +
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<CustomConnector>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<CustomConnector>();

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
                                               or System.Net.HttpStatusCode.BadRequest
                                               or System.Net.HttpStatusCode.Forbidden)
        {
            logger.LogWarning(
                "Custom connectors entity is not available or insufficient permissions. " +
                "Connector governance analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static CustomConnector Map(JsonElement item)
    {
        return new CustomConnector
        {
            ConnectorId = JsonHelper.GetGuid(item, "connectorid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            DisplayName = JsonHelper.GetString(item, "displayname"),
            ConnectorType = JsonHelper.GetString(item, "connectortype"),
            Description = JsonHelper.GetString(item, "description")
        };
    }
}
