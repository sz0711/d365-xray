using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects service endpoints (webhooks) from a Dataverse environment.
/// OData entity: serviceendpoints.
/// </summary>
internal static class ServiceEndpointCollector
{
    private const string _entitySet = "serviceendpoints";
    private const string _queryOptions =
        "$select=serviceendpointid,name,description,contract,url,authtype" +
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<ServiceEndpoint>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<ServiceEndpoint>();

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
                "Service endpoints entity is not available. " +
                "Endpoint analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static ServiceEndpoint Map(JsonElement item)
    {
        return new ServiceEndpoint
        {
            ServiceEndpointId = JsonHelper.GetGuid(item, "serviceendpointid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            Description = JsonHelper.GetString(item, "description"),
            Contract = MapContract(JsonHelper.GetInt(item, "contract")),
            Url = JsonHelper.GetString(item, "url"),
            AuthType = MapAuth(JsonHelper.GetInt(item, "authtype"))
        };
    }

    private static EndpointContract MapContract(int value) =>
        Enum.IsDefined(typeof(EndpointContract), value) ? (EndpointContract)value : EndpointContract.Unknown;

    private static AuthType MapAuth(int value) =>
        Enum.IsDefined(typeof(AuthType), value) ? (AuthType)value : AuthType.Unknown;
}
