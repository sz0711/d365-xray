using Azure.Core;
using Azure.Identity;

namespace D365Xray.Connectors;

/// <summary>
/// Creates a <see cref="TokenCredential"/> based on the configured <see cref="AuthMethod"/>.
/// Encapsulates all MSAL/Azure.Identity logic in one place.
/// </summary>
internal static class CredentialFactory
{
    /// <summary>
    /// The default scope required for Dataverse Web API access.
    /// </summary>
    public static string GetScope(Uri environmentUrl)
        => $"{environmentUrl.GetLeftPart(UriPartial.Authority)}/.default";

    /// <summary>
    /// Creates the appropriate <see cref="TokenCredential"/> for the given configuration.
    /// </summary>
    public static TokenCredential Create(DataverseConnectionConfig config)
    {
        return config.AuthMethod switch
        {
            AuthMethod.Default => new DefaultAzureCredential(),

            AuthMethod.ClientSecret => new ClientSecretCredential(
                config.TenantId ?? throw new InvalidOperationException("TenantId is required for ClientSecret auth."),
                config.ClientId ?? throw new InvalidOperationException("ClientId is required for ClientSecret auth."),
                config.ClientSecret ?? throw new InvalidOperationException("ClientSecret is required for ClientSecret auth.")),

            AuthMethod.Interactive => new InteractiveBrowserCredential(
                new InteractiveBrowserCredentialOptions
                {
                    TenantId = config.TenantId,
                    ClientId = config.ClientId ?? throw new InvalidOperationException("ClientId is required for Interactive auth.")
                }),

            AuthMethod.DeviceCode => new DeviceCodeCredential(
                new DeviceCodeCredentialOptions
                {
                    TenantId = config.TenantId,
                    ClientId = config.ClientId ?? throw new InvalidOperationException("ClientId is required for DeviceCode auth.")
                }),

            _ => throw new ArgumentOutOfRangeException(nameof(config), $"Unknown auth method: {config.AuthMethod}")
        };
    }
}
