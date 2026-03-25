using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects entity (table) metadata from the Dataverse metadata API.
/// Uses the EntityDefinitions endpoint which returns metadata rather than data.
/// </summary>
internal static class EntityMetadataCollector
{
    private const string EntitySet = "EntityDefinitions";
    private const string QueryOptions =
        "$select=MetadataId,LogicalName,DisplayName,SchemaName,IsManaged," +
        "IsCustomEntity,IsCustomizable,IsAuditEnabled,ChangeTrackingEnabled," +
        "OwnershipType";

    public static async Task<IReadOnlyList<EntityMetadataInfo>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<EntityMetadataInfo>();

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
                "Entity metadata endpoint is not available. " +
                "Entity metadata analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static EntityMetadataInfo Map(JsonElement item)
    {
        var ownershipValue = GetOwnershipType(item);

        return new EntityMetadataInfo
        {
            MetadataId = JsonHelper.GetGuid(item, "MetadataId"),
            LogicalName = JsonHelper.GetString(item, "LogicalName") ?? "unknown",
            DisplayName = GetLocalizedLabel(item, "DisplayName"),
            SchemaName = JsonHelper.GetString(item, "SchemaName"),
            IsManaged = JsonHelper.GetBool(item, "IsManaged"),
            IsCustomEntity = JsonHelper.GetBool(item, "IsCustomEntity"),
            IsCustomizable = GetManagedPropertyBool(item, "IsCustomizable"),
            IsAuditEnabled = GetManagedPropertyBool(item, "IsAuditEnabled"),
            ChangeTrackingEnabled = JsonHelper.GetBool(item, "ChangeTrackingEnabled"),
            OwnershipType = ownershipValue
        };
    }

    /// <summary>
    /// Extracts the UserLocalizedLabel from a DisplayName metadata property.
    /// </summary>
    private static string? GetLocalizedLabel(JsonElement item, string property)
    {
        if (item.TryGetProperty(property, out var dn) &&
            dn.TryGetProperty("UserLocalizedLabel", out var label) &&
            label.ValueKind == JsonValueKind.Object)
        {
            return JsonHelper.GetString(label, "Label");
        }
        return null;
    }

    /// <summary>
    /// Reads a ManagedProperty boolean (e.g. IsCustomizable, IsAuditEnabled).
    /// The Dataverse metadata API wraps these in { Value: true/false }.
    /// </summary>
    private static bool GetManagedPropertyBool(JsonElement item, string property)
    {
        if (item.TryGetProperty(property, out var mp) && mp.ValueKind == JsonValueKind.Object)
        {
            return JsonHelper.GetBool(mp, "Value");
        }
        return JsonHelper.GetBool(item, property);
    }

    private static OwnershipType GetOwnershipType(JsonElement item)
    {
        var raw = JsonHelper.GetString(item, "OwnershipType");
        if (raw is not null && Enum.TryParse<OwnershipType>(raw, ignoreCase: true, out var ot))
        {
            return ot;
        }
        // Also try numeric
        var numVal = JsonHelper.GetInt(item, "OwnershipType", 0);
        return Enum.IsDefined(typeof(OwnershipType), numVal)
            ? (OwnershipType)numVal
            : OwnershipType.Unknown;
    }
}
