namespace D365Xray.Connectors;

/// <summary>
/// Configuration for connecting to a single Dataverse environment.
/// </summary>
public sealed class DataverseConnectionConfig
{
    /// <summary>
    /// The Dataverse environment URL (e.g. https://org.crm4.dynamics.com).
    /// </summary>
    public required Uri EnvironmentUrl { get; init; }

    /// <summary>
    /// A human-readable label for this environment (e.g. "Dev", "Test", "Prod").
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The authentication method to use.
    /// </summary>
    public AuthMethod AuthMethod { get; init; } = AuthMethod.Default;

    /// <summary>
    /// Entra ID (Azure AD) tenant ID. Required for ClientSecret auth.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Application (client) ID of the Entra app registration.
    /// Required for ClientSecret and DeviceCode auth.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Client secret. Required for ClientSecret auth.
    /// NEVER log this value.
    /// </summary>
    public string? ClientSecret { get; init; }
}

/// <summary>
/// Supported authentication methods for Dataverse connections.
/// </summary>
public enum AuthMethod
{
    /// <summary>
    /// Uses DefaultAzureCredential (automatic chain: env vars → managed identity → VS/CLI → interactive).
    /// </summary>
    Default,

    /// <summary>
    /// Service principal with client secret. Requires TenantId, ClientId, ClientSecret.
    /// </summary>
    ClientSecret,

    /// <summary>
    /// Interactive browser login. Requires ClientId.
    /// </summary>
    Interactive,

    /// <summary>
    /// Device code flow for headless environments. Requires ClientId.
    /// </summary>
    DeviceCode
}
