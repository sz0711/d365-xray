namespace D365Xray.Core.Model;

/// <summary>
/// A business rule in a Dataverse environment.
/// Business rules are workflows with category = BusinessRule (2).
/// This record provides a focused view for business-rule-specific analysis.
/// </summary>
public sealed record BusinessRule
{
    public required Guid BusinessRuleId { get; init; }
    public required string Name { get; init; }
    public string? UniqueName { get; init; }
    public required string PrimaryEntity { get; init; }
    public required BusinessRuleScope Scope { get; init; }
    public required bool IsActivated { get; init; }
    public bool IsManaged { get; init; }
    public string? SolutionUniqueName { get; init; }
    public string? OwnerName { get; init; }
    public DateTimeOffset? ModifiedOn { get; init; }
}

/// <summary>
/// The scope at which a business rule applies.
/// </summary>
public enum BusinessRuleScope
{
    Entity = 0,
    AllForms = 1,
    SingleForm = 2,
    Unknown = -1
}
