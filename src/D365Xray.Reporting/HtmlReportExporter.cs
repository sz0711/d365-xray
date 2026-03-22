using System.Globalization;
using System.Text;
using System.Web;
using D365Xray.Core.Model;

namespace D365Xray.Reporting;

/// <summary>
/// Exports a <see cref="RiskReport"/> as a self-contained HTML document
/// with inline CSS, color-coded severities, and collapsible finding groups.
/// Produces <c>report.html</c> in the output directory.
/// </summary>
internal sealed class HtmlReportExporter
{
    public async Task ExportAsync(
        RiskReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "report.html");
        var html = Build(report);
        await File.WriteAllTextAsync(path, html, Encoding.UTF8, cancellationToken);
    }

    internal static string Build(RiskReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("<title>D365 X-Ray Risk Report</title>");
        sb.AppendLine("<style>");
        AppendCss(sb);
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine("<header>");
        sb.AppendLine("<h1>D365 X-Ray Risk Report</h1>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<p class=\"meta\">Captured {Encode(report.Metadata.CapturedAtUtc.ToString("u"))} &middot; Tool {Encode(report.Metadata.ToolVersion)}</p>");
        sb.AppendLine("</header>");
        sb.AppendLine("<main>");

        // Risk badge
        var levelClass = report.OverallRiskLevel.ToString().ToLowerInvariant();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<div class=\"risk-badge {levelClass}\">");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  <span class=\"score\">{report.OverallRiskScore}</span>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  <span class=\"label\">{report.OverallRiskLevel}</span>");
        sb.AppendLine("</div>");

        // Environments
        sb.AppendLine("<section class=\"environments\">");
        sb.AppendLine("<h2>Compared Environments</h2>");
        sb.AppendLine("<ul>");
        foreach (var env in report.ComparedEnvironments)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<li><strong>{Encode(env.DisplayName)}</strong> &mdash; <code>{Encode(env.EnvironmentUrl.ToString())}</code></li>");
        }
        sb.AppendLine("</ul>");
        sb.AppendLine("</section>");

        // Severity summary
        if (report.SeverityCounts.Count > 0)
        {
            sb.AppendLine("<section class=\"severity-summary\">");
            sb.AppendLine("<h2>Severity Breakdown</h2>");
            sb.AppendLine("<table><thead><tr><th>Severity</th><th>Count</th></tr></thead><tbody>");
            foreach (var severity in Enum.GetValues<Severity>().OrderByDescending(s => s))
            {
                if (report.SeverityCounts.TryGetValue(severity, out var count) && count > 0)
                {
                    var sevClass = severity.ToString().ToLowerInvariant();
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"<tr><td><span class=\"tag {sevClass}\">{severity}</span></td><td>{count}</td></tr>");
                }
            }
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("</section>");
        }

        // Analysis coverage (always show, even if a category has 0 findings)
        var categoryCounts = report.Findings
            .GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.Count());
        var envCount = report.ComparedEnvironments.Count;
        var hasProdLike = report.ComparedEnvironments.Any(e =>
            e.EnvironmentType is EnvironmentType.Prod or EnvironmentType.Staging);

        sb.AppendLine("<section class=\"analysis-coverage\">");
        sb.AppendLine("<h2>Analysis Coverage</h2>");
        sb.AppendLine("<table><thead><tr><th>Category</th><th>Scope</th><th>Applicable</th><th>Findings</th></tr></thead><tbody>");
        foreach (var category in Enum.GetValues<FindingCategory>().OrderBy(c => c.ToString(), StringComparer.Ordinal))
        {
            categoryCounts.TryGetValue(category, out var count);
            var scope = GetCategoryScope(category);
            var applicable = IsCategoryApplicable(category, envCount, hasProdLike) ? "yes" : "no";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<tr><td>{Encode(category.ToString())}</td><td>{Encode(scope)}</td><td>{applicable}</td><td>{count}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        sb.AppendLine("<p class=\"meta\">Applicable=no means the check is out of scope for the current run shape (for example, cross-environment checks in a single-environment run).</p>");
        sb.AppendLine("<p class=\"meta\">Categories with 0 findings were analyzed but produced no issues for this run.</p>");
        sb.AppendLine("</section>");

        // Findings
        if (report.Findings.Count > 0)
        {
            sb.AppendLine("<section class=\"findings\">");
            sb.AppendLine("<h2>Findings</h2>");

            var grouped = report.Findings
                .GroupBy(f => f.Category)
                .OrderByDescending(g => g.Max(f => f.RiskScore ?? 0));

            foreach (var group in grouped)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"<details open>");
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"<summary><h3>{Encode(group.Key.ToString())} ({group.Count()})</h3></summary>");

                foreach (var finding in group.OrderByDescending(f => f.Severity))
                {
                    var sevClass = finding.Severity.ToString().ToLowerInvariant();
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"<div class=\"finding\">");
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"  <h4><span class=\"tag {sevClass}\">{finding.Severity}</span> {Encode(finding.Title)}</h4>");
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"  <p>{Encode(finding.Description)}</p>");

                    if (finding.RiskScore.HasValue)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"  <p class=\"score-line\">Risk Score: <strong>{finding.RiskScore}</strong></p>");
                    }

                    if (finding.AffectedEnvironments.Count > 0)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"  <p class=\"affected\">Affected: {Encode(string.Join(", ", finding.AffectedEnvironments))}</p>");
                    }

                    if (finding.RuleId is not null)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"  <p class=\"rule\">Rule: <code>{Encode(finding.RuleId)}</code></p>");
                    }

                    sb.AppendLine("</div>");
                }

                sb.AppendLine("</details>");
            }

            sb.AppendLine("</section>");
        }

        sb.AppendLine("</main>");
        sb.AppendLine("<footer><p>Generated by d365-xray</p></footer>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string Encode(string value) => HttpUtility.HtmlEncode(value);

    private static string GetCategoryScope(FindingCategory category) => category switch
    {
        FindingCategory.LayerOverride => "cross-env + single-env",
        FindingCategory.DependencyConflict => "cross-env + single-env",
        FindingCategory.ConnectionConfiguration => "cross-env + single-env",
        FindingCategory.PluginConfiguration => "cross-env + single-env",
        FindingCategory.EnvironmentVariableDrift => "cross-env + single-env",
        FindingCategory.WorkflowConfiguration => "cross-env + prod-like(single-env)",
        FindingCategory.BusinessRuleDrift => "cross-env + prod-like(single-env)",
        FindingCategory.ConfigurationAnomaly => "single-env",
        _ => "cross-env"
    };

    private static bool IsCategoryApplicable(FindingCategory category, int envCount, bool hasProdLike)
    {
        var isSingleEnvRun = envCount == 1;
        var isCrossEnvRun = envCount > 1;

        return category switch
        {
            FindingCategory.ConfigurationAnomaly => isSingleEnvRun,
            FindingCategory.LayerOverride => isCrossEnvRun || isSingleEnvRun,
            FindingCategory.DependencyConflict => isCrossEnvRun || isSingleEnvRun,
            FindingCategory.ConnectionConfiguration => isCrossEnvRun || isSingleEnvRun,
            FindingCategory.PluginConfiguration => isCrossEnvRun || isSingleEnvRun,
            FindingCategory.EnvironmentVariableDrift => isCrossEnvRun || isSingleEnvRun,
            FindingCategory.WorkflowConfiguration => isCrossEnvRun || (isSingleEnvRun && hasProdLike),
            FindingCategory.BusinessRuleDrift => isCrossEnvRun || (isSingleEnvRun && hasProdLike),
            _ => isCrossEnvRun
        };
    }

    private static void AppendCss(StringBuilder sb)
    {
        sb.AppendLine("""
            :root { --bg: #f8f9fa; --card: #fff; --border: #dee2e6; --text: #212529; }
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                   background: var(--bg); color: var(--text); padding: 2rem; max-width: 960px; margin: auto; }
            header { margin-bottom: 2rem; }
            h1 { font-size: 1.8rem; margin-bottom: .25rem; }
            h2 { font-size: 1.3rem; margin: 1.5rem 0 .75rem; }
            h3 { display: inline; font-size: 1.1rem; }
            .meta { color: #6c757d; font-size: .9rem; }
            .risk-badge { display: inline-flex; flex-direction: column; align-items: center;
                          padding: 1rem 2rem; border-radius: .5rem; color: #fff; margin: 1rem 0; }
            .risk-badge .score { font-size: 2.5rem; font-weight: 700; }
            .risk-badge .label { font-size: .9rem; text-transform: uppercase; letter-spacing: .1em; }
            .risk-badge.low { background: #28a745; }
            .risk-badge.medium { background: #ffc107; color: #212529; }
            .risk-badge.high { background: #fd7e14; }
            .risk-badge.critical { background: #dc3545; }
            table { border-collapse: collapse; width: 100%; margin: .5rem 0; }
            th, td { padding: .5rem .75rem; text-align: left; border-bottom: 1px solid var(--border); }
            th { background: var(--card); font-weight: 600; }
            .tag { display: inline-block; padding: .15rem .5rem; border-radius: .25rem;
                   font-size: .8rem; font-weight: 600; color: #fff; }
            .tag.critical { background: #dc3545; }
            .tag.high { background: #fd7e14; }
            .tag.medium { background: #ffc107; color: #212529; }
            .tag.low { background: #28a745; }
            .tag.info { background: #6c757d; }
            details { background: var(--card); border: 1px solid var(--border);
                      border-radius: .5rem; margin: .75rem 0; }
            summary { cursor: pointer; padding: .75rem 1rem; }
            summary::-webkit-details-marker { margin-right: .5rem; }
            .finding { padding: .75rem 1rem; border-top: 1px solid var(--border); }
            .finding h4 { font-size: 1rem; margin-bottom: .4rem; }
            .finding p { font-size: .9rem; color: #495057; margin: .2rem 0; }
            .score-line, .affected, .rule { font-size: .85rem; }
            footer { margin-top: 3rem; text-align: center; color: #6c757d; font-size: .8rem; }
            """);
    }
}
