using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using D365Xray.Connectors;
using D365Xray.Core;

namespace D365Xray.IntegrationTests;

/// <summary>
/// Shared test fixture that builds DI container with real Dataverse connection.
/// Reads credentials from user-secrets (UserSecretsId: d365-xray-integration-tests).
/// </summary>
public sealed class DataverseFixture : IDisposable
{
    public IServiceProvider Services { get; }
    public DataverseConnectionConfig Config { get; }
    public bool IsConfigured { get; }

    public DataverseFixture()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(DataverseFixture).Assembly, optional: true)
            .AddEnvironmentVariables("D365XRAY_")
            .Build();

        var envUrl = configuration["Dataverse:EnvironmentUrl"];
        var tenantId = configuration["Dataverse:TenantId"];
        var clientId = configuration["Dataverse:ClientId"];
        var clientSecret = configuration["Dataverse:ClientSecret"];

        IsConfigured = !string.IsNullOrEmpty(envUrl)
                    && !string.IsNullOrEmpty(tenantId)
                    && !string.IsNullOrEmpty(clientId)
                    && !string.IsNullOrEmpty(clientSecret);

        Config = new DataverseConnectionConfig
        {
            EnvironmentUrl = new Uri(envUrl ?? "https://placeholder.crm.dynamics.com"),
            DisplayName = "IntegrationTest",
            AuthMethod = AuthMethod.ClientSecret,
            TenantId = tenantId,
            ClientId = clientId,
            ClientSecret = clientSecret
        };

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddConnectors();

        if (IsConfigured)
        {
            services.AddDataverseEnvironment(Config);

            // Also register the non-keyed IDataverseClient so DataverseConnector can resolve it
            services.AddSingleton<IDataverseClient>(sp =>
                sp.GetRequiredKeyedService<IDataverseClient>(Config.DisplayName));
        }

        Services = services.BuildServiceProvider();
    }

    public IDataverseClient GetClient()
    {
        return Services.GetRequiredKeyedService<IDataverseClient>(Config.DisplayName);
    }

    public void Dispose()
    {
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
