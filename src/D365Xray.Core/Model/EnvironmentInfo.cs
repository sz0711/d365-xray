namespace D365Xray.Core.Model;

/// <summary>
/// Identity and version information about a Dataverse environment.
/// </summary>
public sealed record EnvironmentInfo
{
    public required string EnvironmentId { get; init; }
    public required string DisplayName { get; init; }
    public required Uri EnvironmentUrl { get; init; }
    public EnvironmentType EnvironmentType { get; init; } = EnvironmentType.Unknown;
    public string? OrganizationId { get; init; }
    public string? TenantId { get; init; }
    public string? DataverseVersion { get; init; }
}

/// <summary>
/// The lifecycle stage of a Dataverse environment.
/// Affects severity of findings (e.g. unmanaged solutions are normal in Dev but risky in Prod).
/// </summary>
public enum EnvironmentType
{
    Unknown,
    Dev,
    Test,
    Staging,
    Prod
}
