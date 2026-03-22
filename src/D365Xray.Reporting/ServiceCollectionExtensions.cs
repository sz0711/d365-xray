using D365Xray.Core;
using Microsoft.Extensions.DependencyInjection;

namespace D365Xray.Reporting;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers report exporters (JSON, Markdown, HTML) and the composite facade.
    /// </summary>
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        services.AddSingleton<JsonReportExporter>();
        services.AddSingleton<MarkdownReportExporter>();
        services.AddSingleton<HtmlReportExporter>();
        services.AddSingleton<IReportExporter, CompositeReportExporter>();
        return services;
    }
}
