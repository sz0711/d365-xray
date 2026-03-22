using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects environment variable definitions and their current values.
/// OData entity: environmentvariabledefinitions expanded with environmentvariablevalues.
/// </summary>
internal static class EnvironmentVariableCollector
{
    private const string _entitySet = "environmentvariabledefinitions";
    private const string _queryOptions =
        "$select=environmentvariabledefinitionid,schemaname,displayname,type,defaultvalue,isrequired" +
        "&$expand=environmentvariabledefinition_environmentvariablevalue($select=value)" +
        "&$orderby=schemaname asc";

    public static async Task<IReadOnlyList<EnvironmentVariable>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<EnvironmentVariable>();

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
                "Environment variable definitions entity is not available. " +
                "Environment variable analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static EnvironmentVariable Map(JsonElement item)
    {
        string? currentValue = null;

        if (item.TryGetProperty("environmentvariabledefinition_environmentvariablevalue", out var values)
            && values.ValueKind == JsonValueKind.Array)
        {
            foreach (var val in values.EnumerateArray())
            {
                currentValue = JsonHelper.GetString(val, "value");
                break; // Take the first value record
            }
        }

        var typeValue = JsonHelper.GetInt(item, "type", -1);

        return new EnvironmentVariable
        {
            DefinitionId = JsonHelper.GetGuid(item, "environmentvariabledefinitionid"),
            SchemaName = JsonHelper.GetString(item, "schemaname") ?? "unknown",
            DisplayName = JsonHelper.GetString(item, "displayname"),
            Type = Enum.IsDefined(typeof(EnvironmentVariableType), typeValue)
                ? (EnvironmentVariableType)typeValue
                : EnvironmentVariableType.Unknown,
            DefaultValue = JsonHelper.GetString(item, "defaultvalue"),
            CurrentValue = currentValue,
            IsRequired = JsonHelper.GetBool(item, "isrequired")
        };
    }
}
