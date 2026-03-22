namespace D365Xray.Core.Model;

/// <summary>
/// A plugin assembly registered in a Dataverse environment.
/// Maps to the pluginassembly entity.
/// </summary>
public sealed record PluginAssembly
{
    public required Guid PluginAssemblyId { get; init; }
    public required string Name { get; init; }
    public string? Version { get; init; }
    public string? PublicKeyToken { get; init; }
    public required PluginIsolationMode IsolationMode { get; init; }
    public required PluginSourceType SourceType { get; init; }
    public string? SolutionUniqueName { get; init; }
    public DateTimeOffset? ModifiedOn { get; init; }
}

/// <summary>
/// Plugin assembly isolation mode.
/// </summary>
public enum PluginIsolationMode
{
    None = 1,
    Sandbox = 2,
    Unknown = 0
}

/// <summary>
/// Plugin assembly source type (how it is stored).
/// </summary>
public enum PluginSourceType
{
    Database = 0,
    Disk = 1,
    Normal = 2,
    AzureWebApp = 3,
    PackageStore = 4,
    Unknown = -1
}
