namespace D365Xray.Core.Model;

/// <summary>
/// A security role in the Dataverse environment.
/// OData entity: roles.
/// </summary>
public sealed record SecurityRole
{
    public Guid RoleId { get; init; }
    public required string Name { get; init; }
    public Guid? BusinessUnitId { get; init; }
    public string? BusinessUnitName { get; init; }
    public bool IsManaged { get; init; }
    public bool IsCustomizable { get; init; }
    public bool IsInherited { get; init; }
    public DateTimeOffset? ModifiedOn { get; init; }
}
