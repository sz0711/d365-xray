using System.Globalization;
using System.Text;
using D365Xray.Core.Model;

namespace D365Xray.Reporting;

/// <summary>
/// Exports a <see cref="RiskReport"/> as a structured Markdown document.
/// Produces <c>report.md</c> in the output directory.
/// </summary>
internal sealed class MarkdownReportExporter
{
    public async Task ExportAsync(
        RiskReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "report.md");
        var md = Build(report);
        await File.WriteAllTextAsync(path, md, Encoding.UTF8, cancellationToken);
    }

    internal static string Build(RiskReport report)
    {
        var sb = new StringBuilder();

        // Title
        sb.AppendLine("# D365 X-Ray Risk Report");
        sb.AppendLine();

        // Metadata
        sb.AppendLine("## Metadata");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"- **Captured**: {report.Metadata.CapturedAtUtc:u}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"- **Tool Version**: {report.Metadata.ToolVersion}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"- **Schema Version**: {report.Metadata.SchemaVersion}");
        sb.AppendLine();

        // Environments
        sb.AppendLine("## Compared Environments");
        sb.AppendLine();
        foreach (var env in report.ComparedEnvironments)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- **{env.DisplayName}** — `{env.EnvironmentUrl}`");
        }
        sb.AppendLine();

        // Executive Summary
        sb.AppendLine("## Executive Summary");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"| Metric | Value |");
        sb.AppendLine("| --- | --- |");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"| Overall Risk Score | **{report.OverallRiskScore}** / 100 |");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"| Risk Level | **{report.OverallRiskLevel}** |");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"| Total Findings | {report.Findings.Count} |");
        sb.AppendLine();

        // Severity breakdown
        if (report.SeverityCounts.Count > 0)
        {
            sb.AppendLine("### Severity Breakdown");
            sb.AppendLine();
            sb.AppendLine("| Severity | Count |");
            sb.AppendLine("| --- | --- |");
            foreach (var severity in Enum.GetValues<Severity>().OrderByDescending(s => s))
            {
                if (report.SeverityCounts.TryGetValue(severity, out var count) && count > 0)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"| {severity} | {count} |");
                }
            }
            sb.AppendLine();
        }

        // Analysis coverage
        var categoryCounts = report.Findings
            .GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.Count());
        var envCount = report.ComparedEnvironments.Count;
        var hasProdLike = report.ComparedEnvironments.Any(e =>
            e.EnvironmentType is EnvironmentType.Prod or EnvironmentType.Staging);

        sb.AppendLine("### Analysis Coverage");
        sb.AppendLine();
        sb.AppendLine("| Category | Scope | Applicable | Findings |");
        sb.AppendLine("| --- | --- | --- | --- |");
        foreach (var category in Enum.GetValues<FindingCategory>().OrderBy(c => c.ToString(), StringComparer.Ordinal))
        {
            categoryCounts.TryGetValue(category, out var count);
            var scope = GetCategoryScope(category);
            var applicable = IsCategoryApplicable(category, envCount, hasProdLike) ? "yes" : "no";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {category} | {scope} | {applicable} | {count} |");
        }
        sb.AppendLine();
        sb.AppendLine("Applicable=no means the check is out of scope for the current run shape (for example, cross-environment checks in a single-environment run).");
        sb.AppendLine();
        sb.AppendLine("Categories with 0 findings were analyzed but produced no issues for this run.");
        sb.AppendLine();

        // Findings by category
        if (report.Findings.Count > 0)
        {
            sb.AppendLine("## Findings");
            sb.AppendLine();

            var grouped = report.Findings
                .GroupBy(f => f.Category)
                .OrderByDescending(g => g.Max(f => f.RiskScore ?? 0));

            foreach (var group in grouped)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"### {group.Key}");
                sb.AppendLine();

                foreach (var finding in group.OrderByDescending(f => f.Severity))
                {
                    var score = finding.RiskScore.HasValue
                        ? $" (Score: {finding.RiskScore})"
                        : string.Empty;
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"#### [{finding.Severity}] {finding.Title}{score}");
                    sb.AppendLine();
                    sb.AppendLine(finding.Description);
                    sb.AppendLine();

                    if (finding.AffectedEnvironments.Count > 0)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"**Affected**: {string.Join(", ", finding.AffectedEnvironments)}");
                        sb.AppendLine();
                    }

                    if (finding.RuleId is not null)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"**Rule**: `{finding.RuleId}`");
                        sb.AppendLine();
                    }
                }
            }
        }

        sb.AppendLine("---");
        sb.AppendLine("*Generated by d365-xray*");

        return sb.ToString();
    }

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
}
