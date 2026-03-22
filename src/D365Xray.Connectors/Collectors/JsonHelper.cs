using System.Text.Json;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Safe extraction helpers for OData JSON responses from the Dataverse Web API.
/// All property access is defensive — missing or mistyped values return defaults.
/// </summary>
internal static class JsonHelper
{
    public static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString()
            : null;
    }

    public static Guid GetGuid(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String)
        {
            return Guid.TryParse(val.GetString(), out var guid) ? guid : Guid.Empty;
        }
        return Guid.Empty;
    }

    public static int GetInt(JsonElement element, string property, int defaultValue = 0)
    {
        if (element.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.Number)
        {
            return val.GetInt32();
        }
        return defaultValue;
    }

    public static bool GetBool(JsonElement element, string property, bool defaultValue = false)
    {
        if (element.TryGetProperty(property, out var val))
        {
            return val.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => defaultValue
            };
        }
        return defaultValue;
    }

    public static DateTimeOffset? GetDateTimeOffset(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String)
        {
            return DateTimeOffset.TryParse(val.GetString(), out var dto) ? dto : null;
        }
        return null;
    }

    /// <summary>
    /// Extracts the "value" array from an OData collection response.
    /// Returns a materialized array so the caller can safely dispose the parent JsonDocument afterward.
    /// </summary>
    public static JsonElement[] GetValueArray(JsonDocument doc)
    {
        if (doc.RootElement.TryGetProperty("value", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            return arr.EnumerateArray().ToArray();
        }
        return [];
    }

    /// <summary>
    /// Reads a property value as its raw string representation (works for strings, numbers, booleans).
    /// </summary>
    public static string? GetRawValue(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var val))
        {
            return null;
        }
        return val.ValueKind switch
        {
            JsonValueKind.String => val.GetString(),
            JsonValueKind.Number => val.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => val.GetRawText()
        };
    }
}
