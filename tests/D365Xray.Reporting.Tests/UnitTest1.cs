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

    private static RiskReport MakeReport(
        IReadOnlyList<Finding>? findings = null,
        IReadOnlyList<EnvironmentSummary>? summaries = null,
        AiEnrichmentResult? ai = null)
    {
        findings ??= [];

        var severityCounts = new Dictionary<Severity, int>();
        foreach (var f in findings)
        {
            severityCounts.TryGetValue(f.Severity, out var c);
            severityCounts[f.Severity] = c + 1;
        }

        summaries ??=
        [
            new EnvironmentSummary
            {
                EnvironmentDisplayName = "Dev",
                EnvironmentUrl = new Uri("https://dev.crm.dynamics.com"),
                EnvironmentType = EnvironmentType.Dev,
                Solutions = 10,
                Components = 100,
                Workflows = 12,
                PluginAssemblies = 4,
                SdkSteps = 20,
                WebResources = 40,
                ConnectionReferences = 9,
                EnvironmentVariables = 8,
                BusinessRules = 5,
                CustomConnectors = 2,
                ServiceEndpoints = 1
            },
            new EnvironmentSummary
            {
                EnvironmentDisplayName = "Prod",
                EnvironmentUrl = new Uri("https://prod.crm.dynamics.com"),
                EnvironmentType = EnvironmentType.Prod,
                Solutions = 9,
                Components = 96,
                Workflows = 10,
                PluginAssemblies = 4,
                SdkSteps = 18,
                WebResources = 38,
                ConnectionReferences = 8,
                EnvironmentVariables = 8,
                BusinessRules = 5,
                CustomConnectors = 2,
                ServiceEndpoints = 1
            }
        ];

        return new RiskReport
        {
            Metadata = new SnapshotMetadata
            {
                CapturedAtUtc = new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero),
                ToolVersion = "1.0.0-test"
            },
            ComparedEnvironments =
            [
                new EnvironmentInfo { EnvironmentId = "dev-001", DisplayName = "Dev", EnvironmentUrl = new Uri("https://dev.crm.dynamics.com"), EnvironmentType = EnvironmentType.Dev },
                new EnvironmentInfo { EnvironmentId = "prod-001", DisplayName = "Prod", EnvironmentUrl = new Uri("https://prod.crm.dynamics.com"), EnvironmentType = EnvironmentType.Prod }
            ],
            OverallRiskScore = findings.Count > 0 ? findings.Max(f => f.RiskScore ?? 0) : 0,
            OverallRiskLevel = findings.Count > 0 ? RiskLevel.High : RiskLevel.Low,
            Findings = findings,
            SeverityCounts = severityCounts,
            EnvironmentSummaries = summaries,
            AiEnrichment = ai
        };
    }

    private static Finding MakeFinding(
        string id,
        FindingCategory category,
        Severity severity,
        int score,
        IReadOnlyDictionary<string, string>? details = null,
        string? ruleId = null) => new()
    {
        FindingId = id,
        Category = category,
        Severity = severity,
        Title = $"Finding {id}",
        Description = $"Description for {id}",
        AffectedEnvironments = ["Dev", "Prod"],
        RuleId = ruleId ?? $"R-{id}",
        RiskScore = score,
        Details = details ?? new Dictionary<string, string>()
    };

    [Fact]
    public async Task JsonExporter_CreatesValidJsonFile()
    {
        var report = MakeReport(
            findings: [MakeFinding("F1", FindingCategory.DependencyConflict, Severity.Critical, 95)]);

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
        Assert.Equal(2, root.GetProperty("environmentSummaries").GetArrayLength());
    }

    [Fact]
    public void Markdown_ContainsEnvironmentInventory_AndDetails()
    {
        var details = new Dictionary<string, string>
        {
            ["EnvironmentUrl"] = "https://dev.crm.dynamics.com",
            ["WorkflowId"] = Guid.NewGuid().ToString()
        };
        var report = MakeReport(findings: [MakeFinding("F1", FindingCategory.WorkflowConfiguration, Severity.High, 77, details)]);

        var md = MarkdownReportExporter.Build(report);

        Assert.Contains("### Environment Inventory", md);
        Assert.Contains("| Environment | Type | Solutions", md);
        Assert.Contains("**Details**:", md);
        Assert.Contains("- EnvironmentUrl: https://dev.crm.dynamics.com", md);
        Assert.Contains("- WorkflowId:", md);
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
            SeverityCounts = new Dictionary<Severity, int>(),
            EnvironmentSummaries =
            [
                new EnvironmentSummary
                {
                    EnvironmentDisplayName = "Dev",
                    EnvironmentUrl = new Uri("https://dev.crm.dynamics.com"),
                    EnvironmentType = EnvironmentType.Dev
                }
            ]
        };

        var md = MarkdownReportExporter.Build(report);

        Assert.Contains("| Category | Scope | Applicable | Findings |", md);
        Assert.Contains("| WorkflowConfiguration | cross-env + prod-like(single-env) | no | 0 |", md);
        Assert.Contains("| ConfigurationAnomaly | single-env | yes | 0 |", md);
    }

    [Fact]
    public void Html_ContainsDashboardAndCharts()
    {
        var report = MakeReport(findings: [
            MakeFinding("F1", FindingCategory.DependencyConflict, Severity.Critical, 95),
            MakeFinding("F2", FindingCategory.SettingsDrift, Severity.High, 70)
        ]);

        var html = HtmlReportExporter.Build(report);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("id=\"theme-toggle\"", html);
        Assert.Contains("<canvas id=\"severityChart\"></canvas>", html);
        Assert.Contains("<canvas id=\"categoryChart\"></canvas>", html);
        Assert.Contains("cdn.jsdelivr.net/npm/chart.js@4", html);
        Assert.Contains("class=\"gauge-card\"", html);
    }

    [Fact]
    public void Html_ContainsEnvironmentInventory_WhenSummariesPresent()
    {
        var html = HtmlReportExporter.Build(MakeReport());

        Assert.Contains("<h2>Environment Inventory</h2>", html);
        Assert.Contains("<th>Environment</th><th>Type</th>", html);
        Assert.Contains(">Dev</a></td><td>Dev</td><td>10</td><td>100</td>", html);
    }

    [Fact]
    public void Html_RendersFindingDetails_AndDeepLink()
    {
        var workflowId = Guid.NewGuid();
        var details = new Dictionary<string, string>
        {
            ["EnvironmentUrl"] = "https://dev.crm.dynamics.com",
            ["WorkflowId"] = workflowId.ToString(),
            ["SolutionName"] = "Core"
        };

        var report = MakeReport(findings: [
            MakeFinding("F1", FindingCategory.WorkflowConfiguration, Severity.High, 80, details)
        ]);

        var html = HtmlReportExporter.Build(report);

        Assert.Contains("<summary>Details</summary>", html);
        Assert.Contains("class=\"details-table\"", html);
        Assert.Contains("Open in Dynamics 365", html);
        Assert.Contains($"etn=workflow&amp;id={workflowId}", html);
    }

    [Fact]
    public void Html_ContainsAiSection_AndFindingAnnotation()
    {
        var report = MakeReport(
            findings: [MakeFinding("F1", FindingCategory.PluginConfiguration, Severity.High, 85)],
            ai: new AiEnrichmentResult
            {
                Summary = "AI summary text",
                Provenance = new AiProvenance
                {
                    GeneratedAtUtc = DateTimeOffset.UtcNow,
                    AdapterName = "test-adapter",
                    ModelIdentifier = "test-model"
                },
                FindingAnnotations = new Dictionary<string, FindingAnnotation>
                {
                    ["F1"] = new FindingAnnotation
                    {
                        Commentary = "Investigate registration order.",
                        SuggestedActions = ["Validate step stage", "Re-deploy plugin assembly"]
                    }
                }
            });

        var html = HtmlReportExporter.Build(report);

        Assert.Contains("AI Analysis", html);
        Assert.Contains("AI summary text", html);
        Assert.Contains("Investigate registration order.", html);
        Assert.Contains("Validate step stage", html);
        Assert.Contains("Adapter: test-adapter", html);
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
        var report = MakeReport(findings: [finding]);
        var html = HtmlReportExporter.Build(report);

        Assert.DoesNotContain("Test <script>alert('xss')</script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&amp;", html);
    }

    [Theory]
    [InlineData("SolutionId", "solution")]
    [InlineData("WorkflowId", "workflow")]
    [InlineData("BusinessRuleId", "workflow")]
    [InlineData("PluginAssemblyId", "pluginassembly")]
    [InlineData("StepId", "sdkmessageprocessingstep")]
    [InlineData("ConnectionReferenceId", "connectionreference")]
    [InlineData("DefinitionId", "environmentvariabledefinition")]
    [InlineData("ServiceEndpointId", "serviceendpoint")]
    [InlineData("ComponentId", "solutioncomponent")]
    public void DeepLinkBuilder_ConstructsEntityRecordLinks(string detailsKey, string entityName)
    {
        var id = Guid.NewGuid();
        var details = new Dictionary<string, string>
        {
            ["EnvironmentUrl"] = "https://dev.crm.dynamics.com",
            [detailsKey] = id.ToString()
        };

        var link = DeepLinkBuilder.TryBuildLink(details);

        Assert.NotNull(link);
        Assert.Contains($"etn={entityName}", link);
        Assert.Contains(id.ToString(), link);
    }

    [Fact]
    public void DeepLinkBuilder_ConstructsWebResourceLink()
    {
        var id = Guid.NewGuid();
        var details = new Dictionary<string, string>
        {
            ["EnvironmentUrl"] = "https://dev.crm.dynamics.com",
            ["WebResourceId"] = id.ToString()
        };

        var link = DeepLinkBuilder.TryBuildLink(details);

        Assert.NotNull(link);
        Assert.Contains("pagetype=webresourceedit", link);
        Assert.Contains(id.ToString(), link);
    }

    [Fact]
    public async Task MarkdownExporter_WritesFile()
    {
        var exporter = new MarkdownReportExporter();
        await exporter.ExportAsync(MakeReport(), _outputDir);

        Assert.True(File.Exists(Path.Combine(_outputDir, "report.md")));
    }

    [Fact]
    public async Task HtmlExporter_WritesFile()
    {
        var exporter = new HtmlReportExporter();
        await exporter.ExportAsync(MakeReport(), _outputDir);

        Assert.True(File.Exists(Path.Combine(_outputDir, "report.html")));
    }

    [Fact]
    public async Task CompositeExporter_ProducesAllThreeFiles()
    {
        var composite = new CompositeReportExporter(
            new JsonReportExporter(),
            new MarkdownReportExporter(),
            new HtmlReportExporter());

        var report = MakeReport(
            findings: [MakeFinding("F1", FindingCategory.SolutionDrift, Severity.Medium, 40)]);

        await composite.ExportAsync(report, _outputDir);

        Assert.True(File.Exists(Path.Combine(_outputDir, "report.json")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "report.md")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "report.html")));
    }
}
