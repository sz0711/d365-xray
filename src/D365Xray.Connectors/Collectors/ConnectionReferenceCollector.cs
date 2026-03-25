using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects connection references from a Dataverse environment.
/// OData entity: connectionreferences.
/// </summary>
internal static class ConnectionReferenceCollector
{
    private const string _entitySet = "connectionreferences";
    private const string _queryOptions =
        "$select=connectionreferenceid,connectionreferencelogicalname,connectionreferencedisplayname," +
        "connectorid,connectionid,statuscode" +
        "&$orderby=connectionreferencelogicalname asc";

    public static async Task<IReadOnlyList<ConnectionReference>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<ConnectionReference>();

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
                "Connection references entity is not available. " +
                "Connection reference analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static ConnectionReference Map(JsonElement item)
    {
        return new ConnectionReference
        {
            ConnectionReferenceId = JsonHelper.GetGuid(item, "connectionreferenceid"),
            ConnectionReferenceLogicalName = JsonHelper.GetString(item, "connectionreferencelogicalname") ?? "unknown",
            DisplayName = JsonHelper.GetString(item, "connectionreferencedisplayname"),
            ConnectorId = JsonHelper.GetString(item, "connectorid"),
            ConnectionId = JsonHelper.GetString(item, "connectionid"),
            IsCustomConnector = false,
            StatusCode = JsonHelper.GetInt(item, "statuscode", 1)
        };
    }
}
