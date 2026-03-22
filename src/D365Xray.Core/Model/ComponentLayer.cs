namespace D365Xray.Core.Model;

/// <summary>
/// A single layer in the managed layer stack for a component.
/// Dataverse stores components as a stack: bottom = first managed import, top = Active (unmanaged).
/// </summary>
public sealed record ComponentLayer
{
    public required Guid ComponentId { get; init; }
    public required ComponentType ComponentType { get; init; }
    public required string SolutionUniqueName { get; init; }
    public required string SolutionDisplayName { get; init; }
    public required int Order { get; init; }
    public required bool IsManaged { get; init; }
    public string? PublisherName { get; init; }
    public DateTimeOffset? CreatedOn { get; init; }

    /// <summary>
    /// True when this is the topmost "Active" layer (unmanaged customizations).
    /// </summary>
    public bool IsActiveLayer => !IsManaged && SolutionUniqueName == "Active";
}
