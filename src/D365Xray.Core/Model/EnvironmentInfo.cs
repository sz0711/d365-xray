namespace D365Xray.Core.Model;

/// <summary>
/// Identity and version information about a Dataverse environment.
/// </summary>
public sealed record EnvironmentInfo
{
    public required string EnvironmentId { get; init; }
    public required string DisplayName { get; init; }
    public required Uri EnvironmentUrl { get; init; }
    public string? OrganizationId { get; init; }
    public string? TenantId { get; init; }
    public string? DataverseVersion { get; init; }
}
