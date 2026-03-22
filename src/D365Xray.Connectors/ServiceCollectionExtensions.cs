using D365Xray.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all connector services (auth, environment snapshot ingestion).
    /// Call <see cref="AddDataverseEnvironment"/> for each environment to connect to.
    /// </summary>
    public static IServiceCollection AddConnectors(this IServiceCollection services)
    {
        services.AddSingleton<IEnvironmentConnector, DataverseConnector>();
        return services;
    }

    /// <summary>
    /// Registers a named <see cref="IDataverseClient"/> for a specific Dataverse environment.
    /// Sets up auth handler, HttpClient, and all per-environment wiring.
    /// </summary>
    public static IServiceCollection AddDataverseEnvironment(
        this IServiceCollection services,
        DataverseConnectionConfig config)
    {
        var credential = CredentialFactory.Create(config);
        var scope = CredentialFactory.GetScope(config.EnvironmentUrl);

        services.AddHttpClient(config.DisplayName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
            .AddHttpMessageHandler(sp =>
                new DataverseAuthHandler(
                    credential,
                    scope,
                    sp.GetRequiredService<ILogger<DataverseAuthHandler>>()));

        // Register a factory that creates a DataverseClient for this environment
        services.AddKeyedSingleton<IDataverseClient>(config.DisplayName, (sp, _) =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(config.DisplayName);
            var logger = sp.GetRequiredService<ILogger<DataverseClient>>();
            return new DataverseClient(httpClient, config.EnvironmentUrl, logger);
        });

        return services;
    }
}
