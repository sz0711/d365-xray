namespace D365Xray.Core.Model;

/// <summary>
/// A field security profile controlling column-level access.
/// OData entity: fieldsecurityprofiles.
/// </summary>
public sealed record FieldSecurityProfile
{
    public Guid FieldSecurityProfileId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool IsManaged { get; init; }
    public DateTimeOffset? ModifiedOn { get; init; }
}
