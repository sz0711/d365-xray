using D365Xray.Core;
using Microsoft.Extensions.DependencyInjection;

namespace D365Xray.Diff;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the deterministic diff engine and related services.
    /// </summary>
    public static IServiceCollection AddDiffEngine(this IServiceCollection services)
    {
        services.AddSingleton<IDiffEngine, SnapshotDiffEngine>();
        return services;
    }
}
