using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects environment-level settings from the Dataverse Organization entity.
/// The organizations entity always contains exactly one row per environment.
/// Each column value is mapped to a categorized EnvironmentSetting record.
/// </summary>
internal static class SettingsCollector
{
    private static readonly SettingDefinition[] Definitions =
    [
        // Security
        new("isauditenabled", "Security", "Organization-level auditing enabled"),
        new("isuseraccessauditenabled", "Security", "User access auditing enabled"),
        new("blockedattachments", "Security", "Blocked file attachment extensions"),

        // Limits
        new("maxuploadfilesize", "Limits", "Maximum upload file size in KB"),
        new("maxrecordsforexporttoexcel", "Limits", "Maximum records for Excel export"),

        // Diagnostics
        new("plugintracelogsetting", "Diagnostics", "Plugin trace log level (0=Off, 1=Exception, 2=All)"),

        // Session
        new("sessiontimeoutenabled", "Session", "Session timeout enforcement enabled"),
        new("sessiontimeoutinmins", "Session", "Session timeout in minutes"),
        new("inabortivetimeoutinmins", "Session", "Inactivity timeout in minutes"),

        // Localization
        new("languagecode", "Localization", "Base language code"),
        new("localeid", "Localization", "Locale ID"),

        // Email & Integration
        new("isemailmonitoringallowed", "Email", "Server-side sync / email monitoring allowed"),
        new("enablebingmapsintegration", "Integration", "Bing Maps integration enabled"),

        // Storage & Features
        new("isexternalfilesstorageenabled", "Storage", "External file storage enabled (e.g. Azure Blob)"),
        new("isactioncardenabled", "Features", "Action cards enabled"),

        // Advanced
        new("issabortivetimeoutenabled", "Advanced", "Session abort timeout enforcement enabled"),
        new("sharepointdeploymenttype", "Integration", "SharePoint deployment type"),
    ];

    /// <summary>
    /// Fetches the organization record and extracts configured settings.
    /// </summary>
    public static async Task<IReadOnlyList<EnvironmentSetting>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var selectColumns = string.Join(",", Definitions.Select(d => d.Column));
            var queryOptions = $"$select=organizationid,{selectColumns}";

            using var doc = await client.GetAsync("organizations", queryOptions, cancellationToken);

            var items = JsonHelper.GetValueArray(doc);
            if (items.Length == 0)
            {
                return [];
            }

            var orgRow = items[0];
            var settings = new List<EnvironmentSetting>(Definitions.Length);

            foreach (var def in Definitions)
            {
                var value = JsonHelper.GetRawValue(orgRow, def.Column);
                settings.Add(new EnvironmentSetting
                {
                    Category = def.Category,
                    Key = def.Column,
                    Value = value,
                    Description = def.Description
                });
            }

            return settings;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest)
        {
            logger.LogWarning(
                "Organization settings could not be retrieved. " +
                "Settings analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
            return [];
        }
    }

    private readonly record struct SettingDefinition(string Column, string Category, string Description);
}
