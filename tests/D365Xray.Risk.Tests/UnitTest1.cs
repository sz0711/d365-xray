using D365Xray.Core.Model;

namespace D365Xray.Risk.Tests;

public class RiskRuleEngineTests
{
    private readonly RiskRuleEngine _engine = new(DefaultRiskRules.All);

    private static ComparisonResult MakeResult(params Finding[] findings) => new()
    {
        Metadata = new SnapshotMetadata { CapturedAtUtc = DateTime.UtcNow, ToolVersion = "test" },
        ComparedEnvironments =
        [
            new EnvironmentInfo { EnvironmentId = "dev", DisplayName = "Dev", EnvironmentUrl = new Uri("https://dev.crm.dynamics.com") },
            new EnvironmentInfo { EnvironmentId = "prod", DisplayName = "Prod", EnvironmentUrl = new Uri("https://prod.crm.dynamics.com") }
        ],
        Findings = findings
    };

    private static Finding MakeFinding(
        string id, FindingCategory category, Severity severity) => new()
    {
        FindingId = id,
        Category = category,
        Severity = severity,
        Title = $"Test finding {id}",
        Description = "Test description"
    };

    // ── Rule matching ───────────────────────────────────────

    [Fact]
    public void MatchRule_CriticalDependencyConflict_ReturnsHighScoreRule()
    {
        var finding = MakeFinding("F1", FindingCategory.DependencyConflict, Severity.Critical);
        var rule = _engine.MatchRule(finding);

        Assert.NotNull(rule);
        Assert.Equal("R-DEP-001", rule.RuleId);
        Assert.Equal(95, rule.BaseScore);
    }

    [Fact]
    public void MatchRule_LowDependencyConflict_ReturnsFallbackRule()
    {
        var finding = MakeFinding("F1", FindingCategory.DependencyConflict, Severity.Low);
        var rule = _engine.MatchRule(finding);

        Assert.NotNull(rule);
        Assert.Equal("R-DEP-002", rule.RuleId);
        Assert.Equal(60, rule.BaseScore);
    }

    [Fact]
    public void MatchRule_HighLayerOverride_ReturnsSpecificRule()
    {
        var finding = MakeFinding("F1", FindingCategory.LayerOverride, Severity.High);
        var rule = _engine.MatchRule(finding);

        Assert.NotNull(rule);
        Assert.Equal("R-LAY-001", rule.RuleId);
        Assert.Equal(80, rule.BaseScore);
    }

    [Fact]
    public void MatchRule_InfoLayerOverride_ReturnsFallbackRule()
    {
        var finding = MakeFinding("F1", FindingCategory.LayerOverride, Severity.Info);
        var rule = _engine.MatchRule(finding);

        Assert.NotNull(rule);
        Assert.Equal("R-LAY-002", rule.RuleId);
    }

    [Fact]
    public void MatchRule_HighSettingsDrift_ReturnsSecurityRule()
    {
        var finding = MakeFinding("F1", FindingCategory.SettingsDrift, Severity.High);
        var rule = _engine.MatchRule(finding);

        Assert.NotNull(rule);
        Assert.Equal("R-SET-001", rule.RuleId);
        Assert.Equal(75, rule.BaseScore);
    }

    // ── Overall score computation ───────────────────────────

    [Fact]
    public void ComputeOverallScore_EmptyFindings_ReturnsZero()
    {
        Assert.Equal(0, RiskRuleEngine.ComputeOverallScore([]));
    }

    [Fact]
    public void ComputeOverallScore_UsesMaxScore()
    {
        var findings = new[]
        {
            MakeFinding("F1", FindingCategory.SettingsDrift, Severity.Low) with { RiskScore = 30 },
            MakeFinding("F2", FindingCategory.DependencyConflict, Severity.Critical) with { RiskScore = 95 }
        };

        Assert.Equal(95, RiskRuleEngine.ComputeOverallScore(findings));
    }

    // ── Risk level derivation ───────────────────────────────

    [Theory]
    [InlineData(0, RiskLevel.Low)]
    [InlineData(25, RiskLevel.Low)]
    [InlineData(26, RiskLevel.Medium)]
    [InlineData(50, RiskLevel.Medium)]
    [InlineData(51, RiskLevel.High)]
    [InlineData(75, RiskLevel.High)]
    [InlineData(76, RiskLevel.Critical)]
    [InlineData(100, RiskLevel.Critical)]
    public void DeriveRiskLevel_ReturnsCorrectBucket(int score, RiskLevel expected)
    {
        Assert.Equal(expected, RiskRuleEngine.DeriveRiskLevel(score));
    }

    // ── Full evaluation ─────────────────────────────────────

    [Fact]
    public void Evaluate_EmptyFindings_ReturnsLowRisk()
    {
        var result = MakeResult();
        var report = _engine.Evaluate(result);

        Assert.Equal(0, report.OverallRiskScore);
        Assert.Equal(RiskLevel.Low, report.OverallRiskLevel);
        Assert.Empty(report.Findings);
    }

    [Fact]
    public void Evaluate_EnrichesFindingsWithRuleIdAndScore()
    {
        var result = MakeResult(
            MakeFinding("F1", FindingCategory.DependencyConflict, Severity.Critical));

        var report = _engine.Evaluate(result);

        var finding = Assert.Single(report.Findings);
        Assert.Equal("R-DEP-001", finding.RuleId);
        Assert.Equal(95, finding.RiskScore);
    }

    [Fact]
    public void Evaluate_SeverityCounts_AreCorrect()
    {
        var result = MakeResult(
            MakeFinding("F1", FindingCategory.DependencyConflict, Severity.Critical),
            MakeFinding("F2", FindingCategory.SettingsDrift, Severity.High),
            MakeFinding("F3", FindingCategory.SolutionDrift, Severity.High));

        var report = _engine.Evaluate(result);

        Assert.Equal(1, report.SeverityCounts[Severity.Critical]);
        Assert.Equal(2, report.SeverityCounts[Severity.High]);
    }

    [Fact]
    public void Evaluate_Findings_AreSortedByFindingId()
    {
        var result = MakeResult(
            MakeFinding("F3", FindingCategory.LayerOverride, Severity.High),
            MakeFinding("F1", FindingCategory.SolutionDrift, Severity.Medium),
            MakeFinding("F2", FindingCategory.SettingsDrift, Severity.Low));

        var report = _engine.Evaluate(result);

        Assert.Equal(3, report.Findings.Count);
        Assert.Equal("F1", report.Findings[0].FindingId);
        Assert.Equal("F2", report.Findings[1].FindingId);
        Assert.Equal("F3", report.Findings[2].FindingId);
    }

    [Fact]
    public void Evaluate_UnmatchedCategory_GetsZeroScore()
    {
        // Create engine with empty rule set
        var engine = new RiskRuleEngine([]);
        var result = MakeResult(
            MakeFinding("F1", FindingCategory.ConfigurationAnomaly, Severity.Info));

        var report = engine.Evaluate(result);

        var finding = Assert.Single(report.Findings);
        Assert.Equal(0, finding.RiskScore);
        Assert.Null(finding.RuleId);
    }

    // ── DI integration ──────────────────────────────────────

    [Fact]
    public void DefaultRiskRules_ContainsRulesForAllCategories()
    {
        var coveredCategories = DefaultRiskRules.All
            .Select(r => r.Category)
            .Distinct()
            .ToHashSet();

        foreach (var category in Enum.GetValues<FindingCategory>())
        {
            Assert.Contains(category, coveredCategories);
        }
    }
}
