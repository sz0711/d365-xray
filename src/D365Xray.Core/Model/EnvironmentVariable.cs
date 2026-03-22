namespace D365Xray.Core.Model;

/// <summary>
/// An environment variable definition and its current value in an environment.
/// Maps to the environmentvariabledefinition and environmentvariablevalue entities.
/// </summary>
public sealed record EnvironmentVariable
{
    public required Guid DefinitionId { get; init; }
    public required string SchemaName { get; init; }
    public string? DisplayName { get; init; }
    public required EnvironmentVariableType Type { get; init; }
    public string? DefaultValue { get; init; }
    public string? CurrentValue { get; init; }
    public string? SolutionUniqueName { get; init; }
    public bool IsRequired { get; init; }

    /// <summary>
    /// True when a value entity exists (either default or current).
    /// </summary>
    public bool HasValue => CurrentValue is not null || DefaultValue is not null;
}

/// <summary>
/// Data type of an environment variable.
/// </summary>
public enum EnvironmentVariableType
{
    String = 100000000,
    Number = 100000001,
    Boolean = 100000002,
    JSON = 100000003,
    DataSource = 100000004,
    Secret = 100000005,
    Unknown = -1
}
