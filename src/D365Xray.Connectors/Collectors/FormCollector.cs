using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects system forms from a Dataverse environment.
/// OData entity: systemforms.
/// </summary>
internal static class FormCollector
{
    private const string EntitySet = "systemforms";
    private const string QueryOptions =
        "$select=formid,name,description,objecttypecode,type,ismanaged," +
        "isdefault,formactivationstate,uniquename,modifiedon" +
        "&$filter=formactivationstate eq 1" + // only active forms
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<FormDefinition>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<FormDefinition>();

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
                "System forms entity is not available. " +
                "Form analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static FormDefinition Map(JsonElement item)
    {
        var typeValue = JsonHelper.GetInt(item, "type", -1);

        return new FormDefinition
        {
            FormId = JsonHelper.GetGuid(item, "formid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            Description = JsonHelper.GetString(item, "description"),
            EntityLogicalName = JsonHelper.GetString(item, "objecttypecode") ?? "unknown",
            FormType = Enum.IsDefined(typeof(FormType), typeValue)
                ? (FormType)typeValue
                : FormType.Unknown,
            IsManaged = JsonHelper.GetBool(item, "ismanaged"),
            IsDefault = JsonHelper.GetBool(item, "isdefault"),
            FormActivationState = JsonHelper.GetInt(item, "formactivationstate"),
            UniqueName = JsonHelper.GetString(item, "uniquename"),
            ModifiedOn = JsonHelper.GetDateTimeOffset(item, "modifiedon")
        };
    }
}
