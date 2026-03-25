using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects security roles from a Dataverse environment.
/// OData entity: roles.
/// </summary>
internal static class SecurityRoleCollector
{
    private const string EntitySet = "roles";
    private const string QueryOptions =
        "$select=roleid,name,_businessunitid_value,ismanaged,iscustomizable," +
        "isinherited,modifiedon" +
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<SecurityRole>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<SecurityRole>();

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
                "Security roles entity is not available. " +
                "Security role analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static SecurityRole Map(JsonElement item)
    {
        return new SecurityRole
        {
            RoleId = JsonHelper.GetGuid(item, "roleid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            BusinessUnitId = JsonHelper.GetGuid(item, "_businessunitid_value") != Guid.Empty
                ? JsonHelper.GetGuid(item, "_businessunitid_value")
                : null,
            IsManaged = JsonHelper.GetBool(item, "ismanaged"),
            IsCustomizable = JsonHelper.GetBool(item, "iscustomizable"),
            IsInherited = JsonHelper.GetBool(item, "isinherited"),
            ModifiedOn = JsonHelper.GetDateTimeOffset(item, "modifiedon")
        };
    }
}
