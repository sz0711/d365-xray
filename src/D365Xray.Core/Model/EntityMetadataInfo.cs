namespace D365Xray.Core.Model;

/// <summary>
/// Lightweight metadata about a Dataverse table (entity).
/// Retrieved from the EntityDefinitions metadata endpoint.
/// </summary>
public sealed record EntityMetadataInfo
{
    public Guid MetadataId { get; init; }
    public required string LogicalName { get; init; }
    public string? DisplayName { get; init; }
    public string? SchemaName { get; init; }
    public bool IsManaged { get; init; }
    public bool IsCustomEntity { get; init; }
    public bool IsCustomizable { get; init; }
    public bool IsAuditEnabled { get; init; }
    public bool ChangeTrackingEnabled { get; init; }
    public int? AttributeCount { get; init; }
    public OwnershipType OwnershipType { get; init; }
}

public enum OwnershipType
{
    Unknown = 0,
    None = 1,
    UserOwned = 2,
    OrganizationOwned = 3,
    BusinessOwned = 4,
    BusinessParenting = 5,
    TeamOwned = 6
}
