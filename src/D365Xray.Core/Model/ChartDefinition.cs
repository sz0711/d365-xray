namespace D365Xray.Core.Model;

/// <summary>
/// A system chart (saved query visualization).
/// OData entity: savedqueryvisualizations.
/// </summary>
public sealed record ChartDefinition
{
    public Guid ChartId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string EntityLogicalName { get; init; }
    public bool IsManaged { get; init; }
    public bool IsDefault { get; init; }
    public DateTimeOffset? ModifiedOn { get; init; }
}
