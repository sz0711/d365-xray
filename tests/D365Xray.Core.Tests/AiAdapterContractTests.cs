using System.Text.Json;
using D365Xray.Core;
using D365Xray.Core.Model;

namespace D365Xray.Core.Tests;

public class AiAdapterContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task NullAdapter_Returns_Empty_Result_With_Provenance()
    {
        var adapter = new NullAiAnalysisAdapter();
        var report = CreateMinimalReport();
        var options = new AiAnalysisOptions
        {
            CustomInstructionsMarkdown = "# Analyse this"
        };

        var result = await adapter.EnrichAsync(report, options);

        Assert.Null(result.Summary);
        Assert.Empty(result.FindingAnnotations);
        Assert.NotNull(result.Provenance);
        Assert.Equal("none", result.Provenance.ModelIdentifier);
        Assert.Equal(nameof(NullAiAnalysisAdapter), result.Provenance.AdapterName);
    }

    [Fact]
    public async Task NullAdapter_Provenance_Has_Valid_Timestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var adapter = new NullAiAnalysisAdapter();
        var result = await adapter.EnrichAsync(
            CreateMinimalReport(),
            new AiAnalysisOptions { CustomInstructionsMarkdown = "test" });
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(result.Provenance.GeneratedAtUtc, before, after);
    }

    [Fact]
    public void AiProvenance_Has_Default_Disclaimer()
    {
        var provenance = new AiProvenance
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            ModelIdentifier = "gpt-4o",
            AdapterName = "TestAdapter"
        };

        Assert.Contains("AI model", provenance.Disclaimer);
        Assert.Contains("inaccuracies", provenance.Disclaimer);
    }

    [Fact]
    public void AiEnrichmentResult_RoundTrips_Through_Json()
    {
        var result = new AiEnrichmentResult
        {
            Summary = "Two critical solution drifts detected.",
            FindingAnnotations = new Dictionary<string, FindingAnnotation>
            {
                ["F-001"] = new FindingAnnotation
                {
                    Commentary = "This drift indicates a deployment mismatch.",
                    SuggestedActions = ["Re-import the solution", "Verify publisher"]
                }
            },
            Provenance = new AiProvenance
            {
                GeneratedAtUtc = DateTimeOffset.Parse("2026-03-22T10:00:00Z"),
                ModelIdentifier = "gpt-4o",
                AdapterName = "AzureOpenAiAdapter"
            }
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AiEnrichmentResult>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(result.Summary, deserialized.Summary);
        Assert.Single(deserialized.FindingAnnotations);
        Assert.Equal("F-001", deserialized.FindingAnnotations.Keys.First());
        Assert.Equal(2, deserialized.FindingAnnotations["F-001"].SuggestedActions.Count);
        Assert.Equal(result.Provenance.ModelIdentifier, deserialized.Provenance.ModelIdentifier);
        Assert.Equal(result.Provenance.AdapterName, deserialized.Provenance.AdapterName);
    }

    [Fact]
    public void RiskReport_Can_Carry_AiEnrichment()
    {
        var report = CreateMinimalReport();

        var enriched = report with
        {
            AiEnrichment = new AiEnrichmentResult
            {
                Summary = "All clear.",
                Provenance = new AiProvenance
                {
                    GeneratedAtUtc = DateTimeOffset.UtcNow,
                    ModelIdentifier = "test-model",
                    AdapterName = "TestAdapter"
                }
            }
        };

        Assert.Null(report.AiEnrichment);
        Assert.NotNull(enriched.AiEnrichment);
        Assert.Equal("All clear.", enriched.AiEnrichment.Summary);
    }

    [Fact]
    public void RiskReport_AiEnrichment_RoundTrips_Through_Json()
    {
        var report = CreateMinimalReport() with
        {
            AiEnrichment = new AiEnrichmentResult
            {
                Summary = "Minor drift, low risk.",
                Provenance = new AiProvenance
                {
                    GeneratedAtUtc = DateTimeOffset.Parse("2026-03-22T12:00:00Z"),
                    ModelIdentifier = "claude-3.5-sonnet",
                    AdapterName = "AnthropicAdapter"
                }
            }
        };

        var json = JsonSerializer.Serialize(report, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<RiskReport>(json, JsonOptions);

        Assert.NotNull(deserialized?.AiEnrichment);
        Assert.Equal("Minor drift, low risk.", deserialized.AiEnrichment.Summary);
        Assert.Equal("AnthropicAdapter", deserialized.AiEnrichment.Provenance.AdapterName);
    }

    [Fact]
    public void AiAnalysisOptions_Stores_Instructions_And_Model()
    {
        var options = new AiAnalysisOptions
        {
            CustomInstructionsMarkdown = "# Focus on security findings",
            ModelIdentifier = "gpt-4o",
            MaxTokenBudget = 4096
        };

        Assert.Equal("# Focus on security findings", options.CustomInstructionsMarkdown);
        Assert.Equal("gpt-4o", options.ModelIdentifier);
        Assert.Equal(4096, options.MaxTokenBudget);
    }

    [Fact]
    public void FindingAnnotation_Defaults_To_Empty_Actions()
    {
        var annotation = new FindingAnnotation
        {
            Commentary = "Needs review."
        };

        Assert.Empty(annotation.SuggestedActions);
    }

    private static RiskReport CreateMinimalReport()
    {
        return new RiskReport
        {
            Metadata = new SnapshotMetadata
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                ToolVersion = "1.0.0"
            },
            ComparedEnvironments =
            [
                new EnvironmentInfo
                {
                    EnvironmentId = "dev",
                    DisplayName = "Dev",
                    EnvironmentUrl = new Uri("https://dev.crm4.dynamics.com")
                }
            ],
            OverallRiskScore = 25,
            OverallRiskLevel = RiskLevel.Low
        };
    }
}
