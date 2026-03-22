using D365Xray.Core;
using D365Xray.Core.Model;

namespace D365Xray.Diff.Tests;

public class SnapshotDiffEngineTests
{
    private static readonly Publisher DefaultPublisher = new()
    {
        UniqueName = "contoso",
        DisplayName = "Contoso",
        CustomizationPrefix = "con"
    };

    private static EnvironmentSnapshot MakeSnapshot(
        string envName,
        IReadOnlyList<Solution>? solutions = null,
        IReadOnlyList<SolutionComponent>? components = null,
        IReadOnlyList<ComponentLayer>? layers = null,
        IReadOnlyList<SolutionDependency>? dependencies = null,
        IReadOnlyList<EnvironmentSetting>? settings = null)
    {
        return new EnvironmentSnapshot
        {
            Metadata = new SnapshotMetadata
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                ToolVersion = "test"
            },
            Environment = new EnvironmentInfo
            {
                EnvironmentId = envName.ToLowerInvariant(),
                DisplayName = envName,
                EnvironmentUrl = new Uri($"https://{envName.ToLowerInvariant()}.crm4.dynamics.com")
            },
            Solutions = solutions ?? [],
            Components = components ?? [],
            Layers = layers ?? [],
            Dependencies = dependencies ?? [],
            Settings = settings ?? []
        };
    }

    // ── Basic contract ──────────────────────────────────────────

    [Fact]
    public void Compare_SingleSnapshot_RunsSingleEnvironmentAnalysis()
    {
        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([MakeSnapshot("Dev")]);

        Assert.Single(result.ComparedEnvironments);
        Assert.Equal("Dev", result.ComparedEnvironments[0].DisplayName);
    }

    [Fact]
    public void Compare_IdenticalSnapshots_ProducesNoFindings()
    {
        var solutions = new[]
        {
            new Solution { SolutionId = Guid.NewGuid(), UniqueName = "Sol1", DisplayName = "Sol1", Version = "1.0.0.0", IsManaged = true, Publisher = DefaultPublisher }
        };

        var settings = new[]
        {
            new EnvironmentSetting { Category = "Security", Key = "isauditenabled", Value = "true" }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", solutions: solutions, settings: settings),
            MakeSnapshot("Test", solutions: solutions, settings: settings)
        ]);

        Assert.Empty(result.Findings);
        Assert.Equal(2, result.ComparedEnvironments.Count);
    }

    // ── Solution drift ──────────────────────────────────────────

    [Fact]
    public void Detects_Solution_Missing_From_Target()
    {
        var sol = new Solution { SolutionId = Guid.NewGuid(), UniqueName = "CrmApp", DisplayName = "CRM App", Version = "2.0.0.0", IsManaged = true, Publisher = DefaultPublisher };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", solutions: [sol]),
            MakeSnapshot("Test", solutions: [])
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.SolutionDrift);
        Assert.Contains("CrmApp", finding.FindingId);
        Assert.Equal(Severity.High, finding.Severity); // managed → high
        Assert.Contains("Test", finding.AffectedEnvironments);
    }

    [Fact]
    public void Detects_Solution_Version_Mismatch()
    {
        var solDev = new Solution { SolutionId = Guid.NewGuid(), UniqueName = "CrmApp", DisplayName = "CRM", Version = "2.0.0.0", IsManaged = true, Publisher = DefaultPublisher };
        var solTest = new Solution { SolutionId = Guid.NewGuid(), UniqueName = "CrmApp", DisplayName = "CRM", Version = "1.5.0.0", IsManaged = true, Publisher = DefaultPublisher };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", solutions: [solDev]),
            MakeSnapshot("Test", solutions: [solTest])
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.VersionMismatch);
        Assert.Contains("version drift", finding.Title.ToLowerInvariant());
    }

    // ── Missing components ──────────────────────────────────────

    [Fact]
    public void Detects_Component_Missing_From_Target()
    {
        var compId = Guid.NewGuid();
        var comp = new SolutionComponent
        {
            ComponentId = compId,
            ComponentType = ComponentType.Entity,
            SolutionUniqueName = "Sol1",
            Behavior = RootComponentBehavior.IncludeSubcomponents
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", components: [comp]),
            MakeSnapshot("Test", components: [])
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.MissingComponent);
        Assert.Contains("Test", finding.AffectedEnvironments);
        Assert.Equal(Severity.Medium, finding.Severity);
    }

    // ── Layer overrides ─────────────────────────────────────────

    [Fact]
    public void Detects_Active_Layer_Override()
    {
        var compId = Guid.NewGuid();
        var layers = new ComponentLayer[]
        {
            new()
            {
                ComponentId = compId, ComponentType = ComponentType.Entity,
                SolutionUniqueName = "Active", SolutionDisplayName = "Active",
                Order = 2, IsManaged = false
            },
            new()
            {
                ComponentId = compId, ComponentType = ComponentType.Entity,
                SolutionUniqueName = "ManagedSol", SolutionDisplayName = "Managed Solution",
                Order = 1, IsManaged = true, PublisherName = "Contoso"
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", layers: layers),
            MakeSnapshot("Test")
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.LayerOverride);
        Assert.Contains("Dev", finding.AffectedEnvironments);
        Assert.Equal(Severity.High, finding.Severity);
    }

    [Fact]
    public void No_Override_Finding_When_Only_Managed_Layers()
    {
        var compId = Guid.NewGuid();
        var layers = new ComponentLayer[]
        {
            new()
            {
                ComponentId = compId, ComponentType = ComponentType.Entity,
                SolutionUniqueName = "ManagedSol1", SolutionDisplayName = "Managed 1",
                Order = 2, IsManaged = true
            },
            new()
            {
                ComponentId = compId, ComponentType = ComponentType.Entity,
                SolutionUniqueName = "ManagedSol2", SolutionDisplayName = "Managed 2",
                Order = 1, IsManaged = true
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", layers: layers),
            MakeSnapshot("Test")
        ]);

        Assert.DoesNotContain(result.Findings, f => f.Category == FindingCategory.LayerOverride);
    }

    // ── Dependency conflicts ────────────────────────────────────

    [Fact]
    public void Detects_Missing_Required_Dependency_Solution()
    {
        var dep = new SolutionDependency
        {
            RequiredComponentId = Guid.NewGuid(),
            RequiredComponentType = ComponentType.Entity,
            RequiredComponentSolution = "MissingSolution",
            DependentComponentId = Guid.NewGuid(),
            DependentComponentType = ComponentType.WebResource,
            DependentComponentSolution = "MySolution",
            DependencyType = DependencyType.Required
        };

        var installedSolutions = new[]
        {
            new Solution { SolutionId = Guid.NewGuid(), UniqueName = "MySolution", DisplayName = "My", Version = "1.0", IsManaged = true, Publisher = DefaultPublisher }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", solutions: installedSolutions, dependencies: [dep]),
            MakeSnapshot("Test")
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.DependencyConflict);
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Contains("MissingSolution", finding.Title);
    }

    [Fact]
    public void No_Conflict_When_Required_Solution_Is_Installed()
    {
        var reqSolId = Guid.NewGuid();
        var dep = new SolutionDependency
        {
            RequiredComponentId = Guid.NewGuid(),
            RequiredComponentType = ComponentType.Entity,
            RequiredComponentSolution = "RequiredSol",
            DependentComponentId = Guid.NewGuid(),
            DependentComponentType = ComponentType.WebResource,
            DependentComponentSolution = "MySolution",
            DependencyType = DependencyType.Required
        };

        var solutions = new[]
        {
            new Solution { SolutionId = Guid.NewGuid(), UniqueName = "MySolution", DisplayName = "My", Version = "1.0", IsManaged = true, Publisher = DefaultPublisher },
            new Solution { SolutionId = reqSolId, UniqueName = "RequiredSol", DisplayName = "Required", Version = "1.0", IsManaged = true, Publisher = DefaultPublisher }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", solutions: solutions, dependencies: [dep]),
            MakeSnapshot("Test")
        ]);

        Assert.DoesNotContain(result.Findings, f => f.Category == FindingCategory.DependencyConflict);
    }

    // ── Settings drift ──────────────────────────────────────────

    [Fact]
    public void Detects_Settings_Value_Drift()
    {
        var settingsDev = new[] { new EnvironmentSetting { Category = "Security", Key = "isauditenabled", Value = "true" } };
        var settingsTest = new[] { new EnvironmentSetting { Category = "Security", Key = "isauditenabled", Value = "false" } };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", settings: settingsDev),
            MakeSnapshot("Test", settings: settingsTest)
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.SettingsDrift);
        Assert.Contains("isauditenabled", finding.FindingId);
        Assert.Equal(Severity.High, finding.Severity); // Security category → high
    }

    [Fact]
    public void Detects_Settings_Missing_From_Target()
    {
        var settingsDev = new[] { new EnvironmentSetting { Category = "Limits", Key = "maxuploadfilesize", Value = "32768" } };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", settings: settingsDev),
            MakeSnapshot("Test", settings: [])
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.SettingsDrift);
        Assert.Contains("missing", finding.Title.ToLowerInvariant());
        Assert.Equal(Severity.Low, finding.Severity); // Non-security → low
    }

    // ── Determinism ─────────────────────────────────────────────

    [Fact]
    public void Findings_Are_Sorted_Deterministically_By_FindingId()
    {
        var sol1 = new Solution { SolutionId = Guid.NewGuid(), UniqueName = "ZZZ_Last", DisplayName = "Z", Version = "1.0", IsManaged = true, Publisher = DefaultPublisher };
        var sol2 = new Solution { SolutionId = Guid.NewGuid(), UniqueName = "AAA_First", DisplayName = "A", Version = "1.0", IsManaged = true, Publisher = DefaultPublisher };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", solutions: [sol1, sol2]),
            MakeSnapshot("Test", solutions: [])
        ]);

        var ids = result.Findings.Select(f => f.FindingId).ToList();
        var sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.Equal(sorted, ids);
    }
}
