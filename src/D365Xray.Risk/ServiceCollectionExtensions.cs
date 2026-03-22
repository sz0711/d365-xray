using D365Xray.Core;
using D365Xray.Core.Model;
using Microsoft.Extensions.DependencyInjection;

namespace D365Xray.Risk;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the rule-based risk scoring services.
    /// </summary>
    public static IServiceCollection AddRiskScoring(this IServiceCollection services)
    {
        services.AddSingleton<IReadOnlyList<RiskRule>>(DefaultRiskRules.All);
        services.AddSingleton<IRiskScorer, RiskRuleEngine>();
        return services;
    }
}
