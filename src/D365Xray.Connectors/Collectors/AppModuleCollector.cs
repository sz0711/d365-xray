using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects model-driven app modules from a Dataverse environment.
/// OData entity: appmodules.
/// </summary>
internal static class AppModuleCollector
{
    private const string EntitySet = "appmodules";
    private const string QueryOptions =
        "$select=appmoduleid,name,uniquename,description,appversion," +
        "ismanaged,isdefault,statecode,clienttype,webresourceid,modifiedon" +
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<AppModule>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<AppModule>();

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
                "App modules entity is not available. " +
                "App module analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static AppModule Map(JsonElement item)
    {
        // statecode: 0 = Active, 1 = Inactive
        var isPublished = JsonHelper.GetInt(item, "statecode") == 0;

        return new AppModule
        {
            AppModuleId = JsonHelper.GetGuid(item, "appmoduleid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            UniqueName = JsonHelper.GetString(item, "uniquename") ?? "unknown",
            Description = JsonHelper.GetString(item, "description"),
            AppVersion = JsonHelper.GetString(item, "appversion"),
            IsManaged = JsonHelper.GetBool(item, "ismanaged"),
            IsDefault = JsonHelper.GetBool(item, "isdefault"),
            IsPublished = isPublished,
            ClientType = JsonHelper.GetString(item, "clienttype"),
            WebResourceId = JsonHelper.GetString(item, "webresourceid"),
            ModifiedOn = JsonHelper.GetDateTimeOffset(item, "modifiedon")
        };
    }
}
