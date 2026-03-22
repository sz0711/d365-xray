using D365Xray.Core;
using D365Xray.Core.Model;

namespace D365Xray.Reporting;

/// <summary>
/// Delegates to all registered individual exporters (JSON, Markdown, HTML)
/// to produce a complete report output in a single call.
/// Registered as the public <see cref="IReportExporter"/> implementation.
/// </summary>
internal sealed class CompositeReportExporter : IReportExporter
{
    private readonly JsonReportExporter _json;
    private readonly MarkdownReportExporter _markdown;
    private readonly HtmlReportExporter _html;

    public CompositeReportExporter(
        JsonReportExporter json,
        MarkdownReportExporter markdown,
        HtmlReportExporter html)
    {
        _json = json;
        _markdown = markdown;
        _html = html;
    }

    public async Task ExportAsync(
        RiskReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            _json.ExportAsync(report, outputDirectory, cancellationToken),
            _markdown.ExportAsync(report, outputDirectory, cancellationToken),
            _html.ExportAsync(report, outputDirectory, cancellationToken));
    }
}
