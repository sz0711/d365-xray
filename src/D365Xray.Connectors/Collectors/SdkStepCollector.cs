using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects SDK message processing steps from a Dataverse environment.
/// OData entity: sdkmessageprocessingsteps expanded with plugintypeid.
/// </summary>
internal static class SdkStepCollector
{
    private const string EntitySet = "sdkmessageprocessingsteps";
    private const string QueryOptions =
        "$select=sdkmessageprocessingstepid,name,stage,mode,rank," +
        "statecode,filteringattributes,configuration" +
        "&$expand=sdkmessagefilterid($select=primaryobjecttypecode)," +
        "sdkmessageid($select=name)," +
        "plugintypeid($select=name,assemblyname)" +
        "&$filter=ishidden eq false" +
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<SdkStep>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<SdkStep>();

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
                                               or System.Net.HttpStatusCode.BadRequest
                                               or System.Net.HttpStatusCode.Forbidden)
        {
            logger.LogWarning(
                "SDK message processing steps entity is not available or insufficient permissions. " +
                "Plugin step analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static SdkStep Map(JsonElement item)
    {
        var stageValue = JsonHelper.GetInt(item, "stage");
        var modeValue = JsonHelper.GetInt(item, "mode", -1);

        string? messageName = null;
        if (item.TryGetProperty("sdkmessageid", out var msg) && msg.ValueKind == JsonValueKind.Object)
        {
            messageName = JsonHelper.GetString(msg, "name");
        }

        string? primaryEntity = null;
        if (item.TryGetProperty("sdkmessagefilterid", out var filter) && filter.ValueKind == JsonValueKind.Object)
        {
            primaryEntity = JsonHelper.GetString(filter, "primaryobjecttypecode");
        }

        string? pluginTypeName = null;
        if (item.TryGetProperty("plugintypeid", out var plugin) && plugin.ValueKind == JsonValueKind.Object)
        {
            pluginTypeName = JsonHelper.GetString(plugin, "name");
        }

        // statecode: 0 = Enabled, 1 = Disabled
        var isDisabled = JsonHelper.GetInt(item, "statecode") == 1;

        return new SdkStep
        {
            StepId = JsonHelper.GetGuid(item, "sdkmessageprocessingstepid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            MessageName = messageName,
            PrimaryEntity = primaryEntity,
            Stage = Enum.IsDefined(typeof(SdkStepStage), stageValue)
                ? (SdkStepStage)stageValue
                : SdkStepStage.Unknown,
            Mode = Enum.IsDefined(typeof(SdkStepMode), modeValue)
                ? (SdkStepMode)modeValue
                : SdkStepMode.Unknown,
            Rank = JsonHelper.GetInt(item, "rank"),
            IsDisabled = isDisabled,
            FilteringAttributes = JsonHelper.GetString(item, "filteringattributes"),
            PluginTypeName = pluginTypeName,
            Configuration = JsonHelper.GetString(item, "configuration")
        };
    }
}
