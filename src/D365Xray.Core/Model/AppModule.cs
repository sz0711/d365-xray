namespace D365Xray.Core.Model;

/// <summary>
/// A model-driven app (app module) registered in the environment.
/// OData entity: appmodules.
/// </summary>
public sealed record AppModule
{
    public Guid AppModuleId { get; init; }
    public required string Name { get; init; }
    public required string UniqueName { get; init; }
    public string? Description { get; init; }
    public string? AppVersion { get; init; }
    public bool IsManaged { get; init; }
    public bool IsDefault { get; init; }
    public bool IsPublished { get; init; }
    public string? ClientType { get; init; }
    public string? WebResourceId { get; init; }
    public DateTimeOffset? ModifiedOn { get; init; }
}
