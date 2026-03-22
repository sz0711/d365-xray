using System.Text.Json;
using D365Xray.Core.Model;

namespace D365Xray.Reporting.Tests;

public class ReportExporterTests : IDisposable
{
    private readonly string _outputDir;

    public ReportExporterTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "d365xray-test-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    private static RiskReport MakeReport(params Finding[] findings)
    {
        var severityCounts = new Dictionary<Severity, int>();
        foreach (var f in findings)
        {
            severityCounts.TryGetValue(f.Severity, out var c);
            severityCounts[f.Severity] = c + 1;
        }

        return new RiskReport
        {
            Metadata = new SnapshotMetadata
            {
                CapturedAtUtc = new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero),
                ToolVersion = "1.0.0-test"
            },
            ComparedEnvironments =
            [
                new EnvironmentInfo { EnvironmentId = "dev-001", DisplayName = "Dev", EnvironmentUrl = new Uri("https://dev.crm.dynamics.com") },
                new EnvironmentInfo { EnvironmentId = "prod-001", DisplayName = "Prod", EnvironmentUrl = new Uri("https://prod.crm.dynamics.com") }
            ],
            OverallRiskScore = findings.Length > 0 ? findings.Max(f => f.RiskScore ?? 0) : 0,
            OverallRiskLevel = findings.Length > 0 ? RiskLevel.High : RiskLevel.Low,
            Findings = findings,
            SeverityCounts = severityCounts
        };
    }

    private static Finding MakeFinding(
        string id,
        FindingCategory category,
        Severity severity,
        int score,
        string? ruleId = null) => new()
    {
        FindingId = id,
        Category = category,
        Severity = severity,
        Title = $"Finding {id}",
        Description = $"Description for {id}",
        AffectedEnvironments = ["Dev", "Prod"],
        RuleId = ruleId ?? $"R-{id}",
        RiskScore = score
    };

    // ── JSON Exporter ───────────────────────────────────────

    [Fact]
    public async Task JsonExporter_CreatesValidJsonFile()
    {
        var report = MakeReport(
            MakeFinding("F1", FindingCategory.DependencyConflict, Severity.Critical, 95));

        var exporter = new JsonReportExporter();
        await exporter.ExportAsync(report, _outputDir);

        var path = Path.Combine(_outputDir, "report.json");
        Assert.True(File.Exists(path));

        var json = await File.ReadAllTextAsync(path);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(95, root.GetProperty("overallRiskScore").GetInt32());
        Assert.Equal("high", root.GetProperty("overallRiskLevel").GetString());
        Assert.Equal(1, root.GetProperty("findings").GetArrayLength());
    }

    [Fact]
    public async Task JsonExporter_RoundTripsReport()
    {
        var report = MakeReport(
            MakeFinding("F1", FindingCategory.LayerOverride, Severity.High, 80, "R-LAY-001"));

        var exporter = new JsonReportExporter();
        await exporter.ExportAsync(report, _outputDir);

        var json = await File.ReadAllTextAsync(Path.Combine(_outputDir, "report.json"));
        var deserialized = JsonSerializer.Deserialize<RiskReport>(json, JsonReportExporter.SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(report.OverallRiskScore, deserialized.OverallRiskScore);
        Assert.Equal(report.OverallRiskLevel, deserialized.OverallRiskLevel);
        Assert.Equal(report.Findings.Count, deserialized.Findings.Count);
        Assert.Equal("R-LAY-001", deserialized.Findings[0].RuleId);
    }

    [Fact]
    public async Task JsonExporter_EmptyReport_ProducesValidJson()
    {
        var report = MakeReport();
        var exporter = new JsonReportExporter();
        await exporter.ExportAsync(report, _outputDir);

        var json = await File.ReadAllTextAsync(Path.Combine(_outputDir, "report.json"));
        var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("overallRiskScore").GetInt32());
    }

    // ── Markdown Exporter ───────────────────────────────────

    [Fact]
    public void Markdown_ContainsTitle()
    {
        var md = MarkdownReportExporter.Build(MakeReport());
        Assert.Contains("# D365 X-Ray Risk Report", md);
    }

    [Fact]
    public void Markdown_ContainsEnvironments()
    {
        var md = MarkdownReportExporter.Build(MakeReport());
        Assert.Contains("**Dev**", md);
        Assert.Contains("**Prod**", md);
    }

    [Fact]
    public void Markdown_ContainsExecutiveSummary()
    {
        var report = MakeReport(
            MakeFinding("F1", FindingCategory.SettingsDrift, Severity.High, 75));
        var md = MarkdownReportExporter.Build(report);

        Assert.Contains("## Executive Summary", md);
        Assert.Contains("Overall Risk Score", md);
        Assert.Contains("**High**", md);
    }

    [Fact]
    public void Markdown_GroupsFindingsByCategory()
    {
        var report = MakeReport(
            MakeFinding("F1", FindingCategory.DependencyConflict, Severity.Critical, 95),
            MakeFinding("F2", FindingCategory.SettingsDrift, Severity.Medium, 30));
        var md = MarkdownReportExporter.Build(report);

        Assert.Contains("### DependencyConflict", md);
        Assert.Contains("### SettingsDrift", md);
    }

    [Fact]
    public void Markdown_AnalysisCoverage_Shows_Applicability_For_SingleDevRun()
    {
        var report = new RiskReport
        {
            Metadata = new SnapshotMetadata
            {
                CapturedAtUtc = new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero),
                ToolVersion = "1.0.0-test"
            },
            ComparedEnvironments =
            [
                new EnvironmentInfo
                {
                    EnvironmentId = "dev-001",
                    DisplayName = "Dev",
                    EnvironmentUrl = new Uri("https://dev.crm.dynamics.com"),
                    EnvironmentType = EnvironmentType.Dev
                }
            ],
            OverallRiskScore = 0,
            OverallRiskLevel = RiskLevel.Low,
            Findings = [],
            SeverityCounts = new Dictionary<Severity, int>()
        };

        var md = MarkdownReportExporter.Build(report);

        Assert.Contains("| Category | Scope | Applicable | Findings |", md);
        Assert.Contains("| WorkflowConfiguration | cross-env + prod-like(single-env) | no | 0 |", md);
        Assert.Contains("| ConfigurationAnomaly | single-env | yes | 0 |", md);
    }

    [Fact]
    public void Markdown_IncludesRuleId()
    {
        var report = MakeReport(
            MakeFinding("F1", FindingCategory.LayerOverride, Severity.High, 80, "R-LAY-001"));
        var md = MarkdownReportExporter.Build(report);

        Assert.Contains("`R-LAY-001`", md);
    }

    [Fact]
    public async Task MarkdownExporter_WritesFile()
    {
        var exporter = new MarkdownReportExporter();
        await exporter.ExportAsync(MakeReport(), _outputDir);

        Assert.True(File.Exists(Path.Combine(_outputDir, "report.md")));
    }

    // ── HTML Exporter ───────────────────────────────────────

    [Fact]
    public void Html_ContainsDoctype()
    {
        var html = HtmlReportExporter.Build(MakeReport());
        Assert.StartsWith("<!DOCTYPE html>", html);
    }

    [Fact]
    public void Html_ContainsRiskBadge()
    {
        var report = MakeReport(
            MakeFinding("F1", FindingCategory.DependencyConflict, Severity.Critical, 95));
        var html = HtmlReportExporter.Build(report);

        Assert.Contains("risk-badge", html);
        Assert.Contains("high", html);
    }

    [Fact]
    public void Html_ContainsSeverityTags()
    {
        var report = MakeReport(
            MakeFinding("F1", FindingCategory.DependencyConflict, Severity.Critical, 95));
        var html = HtmlReportExporter.Build(report);

        Assert.Contains("class=\"tag critical\"", html);
    }

    [Fact]
    public void Html_ContainsCollapsibleDetails()
    {
        var report = MakeReport(
            MakeFinding("F1", FindingCategory.LayerOverride, Severity.High, 80));
        var html = HtmlReportExporter.Build(report);

        Assert.Contains("<details open>", html);
        Assert.Contains("</details>", html);
    }

    [Fact]
    public void Html_AnalysisCoverage_Shows_Applicability_Columns()
    {
        var report = new RiskReport
        {
            Metadata = new SnapshotMetadata
            {
                CapturedAtUtc = new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero),
                ToolVersion = "1.0.0-test"
            },
            ComparedEnvironments =
            [
                new EnvironmentInfo
                {
                    EnvironmentId = "dev-001",
                    DisplayName = "Dev",
                    EnvironmentUrl = new Uri("https://dev.crm.dynamics.com"),
                    EnvironmentType = EnvironmentType.Dev
                }
            ],
            OverallRiskScore = 0,
            OverallRiskLevel = RiskLevel.Low,
            Findings = [],
            SeverityCounts = new Dictionary<Severity, int>()
        };

        var html = HtmlReportExporter.Build(report);

        Assert.Contains("<th>Scope</th><th>Applicable</th><th>Findings</th>", html);
        Assert.Contains("<td>WorkflowConfiguration</td><td>cross-env + prod-like(single-env)</td><td>no</td><td>0</td>", html);
        Assert.Contains("<td>ConfigurationAnomaly</td><td>single-env</td><td>yes</td><td>0</td>", html);
    }

    [Fact]
    public void Html_EncodesSpecialCharacters()
    {
        var finding = new Finding
        {
            FindingId = "F1",
            Category = FindingCategory.ConfigurationAnomaly,
            Severity = Severity.Info,
            Title = "Test <script>alert('xss')</script>",
            Description = "Desc with & and \"quotes\""
        };
        var report = MakeReport(finding);
        var html = HtmlReportExporter.Build(report);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&amp;", html);
    }

    [Fact]
    public async Task HtmlExporter_WritesFile()
    {
        var exporter = new HtmlReportExporter();
        await exporter.ExportAsync(MakeReport(), _outputDir);

        Assert.True(File.Exists(Path.Combine(_outputDir, "report.html")));
    }

    // ── Composite Exporter ──────────────────────────────────

    [Fact]
    public async Task CompositeExporter_ProducesAllThreeFiles()
    {
        var composite = new CompositeReportExporter(
            new JsonReportExporter(),
            new MarkdownReportExporter(),
            new HtmlReportExporter());

        var report = MakeReport(
            MakeFinding("F1", FindingCategory.SolutionDrift, Severity.Medium, 40));

        await composite.ExportAsync(report, _outputDir);

        Assert.True(File.Exists(Path.Combine(_outputDir, "report.json")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "report.md")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "report.html")));
    }
}
