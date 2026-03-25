namespace D365Xray.Core.Model;

/// <summary>
/// A saved query (system view) from the Dataverse environment.
/// OData entity: savedqueries.
/// </summary>
public sealed record ViewDefinition
{
    public Guid ViewId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string EntityLogicalName { get; init; }
    public bool IsDefault { get; init; }
    public bool IsManaged { get; init; }
    public bool IsCustomizable { get; init; }
    public int QueryType { get; init; }
    public DateTimeOffset? ModifiedOn { get; init; }
}
