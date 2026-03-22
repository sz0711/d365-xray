namespace D365Xray.Core.Model;

/// <summary>
/// A workflow or Power Automate flow definition in a Dataverse environment.
/// Maps to the workflow entity (covers both classic workflows and modern flows).
/// </summary>
public sealed record WorkflowDefinition
{
    public required Guid WorkflowId { get; init; }
    public required string Name { get; init; }
    public string? UniqueName { get; init; }
    public required WorkflowCategory Category { get; init; }
    public required WorkflowMode Mode { get; init; }
    public required bool IsActivated { get; init; }
    public string? PrimaryEntity { get; init; }
    public string? TriggerOnCreate { get; init; }
    public string? TriggerOnUpdate { get; init; }
    public string? TriggerOnDelete { get; init; }
    public string? SolutionUniqueName { get; init; }
    public string? OwnerName { get; init; }
    public bool IsManaged { get; init; }
    public DateTimeOffset? ModifiedOn { get; init; }
}

/// <summary>
/// The workflow category (classic workflow, action, business process flow, modern flow, etc.).
/// </summary>
public enum WorkflowCategory
{
    Workflow = 0,
    Dialog = 1,
    BusinessRule = 2,
    Action = 3,
    BusinessProcessFlow = 4,
    ModernFlow = 5,
    Desktop = 6,
    Unknown = -1
}

/// <summary>
/// Whether the workflow is a background or real-time workflow.
/// </summary>
public enum WorkflowMode
{
    Background = 0,
    Realtime = 1,
    Unknown = -1
}
