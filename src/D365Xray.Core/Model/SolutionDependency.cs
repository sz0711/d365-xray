namespace D365Xray.Core.Model;

/// <summary>
/// A dependency relationship between two solution components.
/// Maps to the Dependency entity in Dataverse.
/// </summary>
public sealed record SolutionDependency
{
    public required Guid RequiredComponentId { get; init; }
    public required ComponentType RequiredComponentType { get; init; }
    public string? RequiredComponentSolution { get; init; }

    public required Guid DependentComponentId { get; init; }
    public required ComponentType DependentComponentType { get; init; }
    public string? DependentComponentSolution { get; init; }

    public required DependencyType DependencyType { get; init; }
}

/// <summary>
/// The type of dependency between two components.
/// </summary>
public enum DependencyType
{
    /// <summary>Removal of the required component will fail.</summary>
    Required = 0,

    /// <summary>Dependency exists but removal is still possible.</summary>
    Optional = 1,

    /// <summary>Solution-internal dependency.</summary>
    SolutionInternal = 2
}
