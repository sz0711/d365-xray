using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects field security profiles from a Dataverse environment.
/// OData entity: fieldsecurityprofiles.
/// </summary>
internal static class FieldSecurityProfileCollector
{
    private const string EntitySet = "fieldsecurityprofiles";
    private const string QueryOptions =
        "$select=fieldsecurityprofileid,name,description,ismanaged,modifiedon" +
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<FieldSecurityProfile>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<FieldSecurityProfile>();

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
                "Field security profiles entity is not available. " +
                "Field security analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static FieldSecurityProfile Map(JsonElement item)
    {
        return new FieldSecurityProfile
        {
            FieldSecurityProfileId = JsonHelper.GetGuid(item, "fieldsecurityprofileid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            Description = JsonHelper.GetString(item, "description"),
            IsManaged = JsonHelper.GetBool(item, "ismanaged"),
            ModifiedOn = JsonHelper.GetDateTimeOffset(item, "modifiedon")
        };
    }
}
