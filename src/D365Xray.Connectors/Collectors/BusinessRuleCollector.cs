using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects business rules from a Dataverse environment.
/// Business rules are workflows with category=2 and type=1 (definition).
/// </summary>
internal static class BusinessRuleCollector
{
    private const string EntitySet = "workflows";
    private const string QueryOptions =
        "$select=workflowid,name,uniquename,primaryentity,scope,statecode,ismanaged,modifiedon" +
        "&$filter=category eq 2 and type eq 1" + // business rules only, definition only
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<BusinessRule>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<BusinessRule>();

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
                "Business rules could not be retrieved. " +
                "Business rule analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static BusinessRule Map(JsonElement item)
    {
        var scopeValue = JsonHelper.GetInt(item, "scope", -1);
        var isActivated = JsonHelper.GetInt(item, "statecode") == 1;

        return new BusinessRule
        {
            BusinessRuleId = JsonHelper.GetGuid(item, "workflowid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            UniqueName = JsonHelper.GetString(item, "uniquename"),
            PrimaryEntity = JsonHelper.GetString(item, "primaryentity") ?? "unknown",
            Scope = Enum.IsDefined(typeof(BusinessRuleScope), scopeValue)
                ? (BusinessRuleScope)scopeValue
                : BusinessRuleScope.Unknown,
            IsActivated = isActivated,
            IsManaged = JsonHelper.GetBool(item, "ismanaged"),
            ModifiedOn = JsonHelper.GetDateTimeOffset(item, "modifiedon")
        };
    }
}
