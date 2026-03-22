namespace D365Xray.Core.Model;

/// <summary>
/// A single environment-level configuration setting.
/// Captures values from the Dataverse Organization entity and other system settings.
/// </summary>
public sealed record EnvironmentSetting
{
    public required string Category { get; init; }
    public required string Key { get; init; }
    public string? Value { get; init; }
    public string? Description { get; init; }
}
