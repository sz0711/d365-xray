using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects plugin assemblies from a Dataverse environment.
/// OData entity: pluginassemblies.
/// Filters out system assemblies to focus on custom code.
/// </summary>
internal static class PluginAssemblyCollector
{
    private const string _entitySet = "pluginassemblies";
    private const string _queryOptions =
        "$select=pluginassemblyid,name,version,publickeytoken,isolationmode,sourcetype,modifiedon" +
        "&$filter=ishidden/Value eq false" +
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<PluginAssembly>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<PluginAssembly>();

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
                "Plugin assemblies entity is not available or insufficient permissions. " +
                "Plugin analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static PluginAssembly Map(JsonElement item)
    {
        var isolationValue = JsonHelper.GetInt(item, "isolationmode");
        var sourceValue = JsonHelper.GetInt(item, "sourcetype", -1);

        return new PluginAssembly
        {
            PluginAssemblyId = JsonHelper.GetGuid(item, "pluginassemblyid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            Version = JsonHelper.GetString(item, "version"),
            PublicKeyToken = JsonHelper.GetString(item, "publickeytoken"),
            IsolationMode = Enum.IsDefined(typeof(PluginIsolationMode), isolationValue)
                ? (PluginIsolationMode)isolationValue
                : PluginIsolationMode.Unknown,
            SourceType = Enum.IsDefined(typeof(PluginSourceType), sourceValue)
                ? (PluginSourceType)sourceValue
                : PluginSourceType.Unknown,
            ModifiedOn = JsonHelper.GetDateTimeOffset(item, "modifiedon")
        };
    }
}
