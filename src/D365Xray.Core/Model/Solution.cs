namespace D365Xray.Core.Model;

/// <summary>
/// A Dynamics 365 solution installed in an environment.
/// </summary>
public sealed record Solution
{
    public required Guid SolutionId { get; init; }
    public required string UniqueName { get; init; }
    public required string DisplayName { get; init; }
    public required string Version { get; init; }
    public required bool IsManaged { get; init; }
    public required Publisher Publisher { get; init; }
    public DateTimeOffset? InstalledOn { get; init; }
    public DateTimeOffset? ModifiedOn { get; init; }
}

/// <summary>
/// The publisher of a Dynamics 365 solution.
/// </summary>
public sealed record Publisher
{
    public required string UniqueName { get; init; }
    public required string DisplayName { get; init; }
    public required string CustomizationPrefix { get; init; }
}
