using System.Text.Json;
using D365Xray.Core.Model;

namespace D365Xray.Core.Tests;

public class EnvironmentSnapshotSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Snapshot_RoundTrips_Through_Json()
    {
        var snapshot = CreateTestSnapshot();

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<EnvironmentSnapshot>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(snapshot.Metadata.SchemaVersion, deserialized.Metadata.SchemaVersion);
        Assert.Equal(snapshot.Environment.EnvironmentId, deserialized.Environment.EnvironmentId);
        Assert.Equal(snapshot.Solutions.Count, deserialized.Solutions.Count);
        Assert.Equal(snapshot.Components.Count, deserialized.Components.Count);
        Assert.Equal(snapshot.Layers.Count, deserialized.Layers.Count);
        Assert.Equal(snapshot.Dependencies.Count, deserialized.Dependencies.Count);
        Assert.Equal(snapshot.Settings.Count, deserialized.Settings.Count);
    }

    [Fact]
    public void Snapshot_Json_Contains_SchemaVersion()
    {
        var snapshot = CreateTestSnapshot();

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);

        Assert.Contains("\"schemaVersion\": 1", json);
    }

    [Fact]
    public void Finding_Preserves_All_Properties_Through_Json()
    {
        var finding = new Finding
        {
            FindingId = "F-001",
            Category = FindingCategory.SolutionDrift,
            Severity = Severity.High,
            Title = "Solution missing in Prod",
            Description = "Solution 'CustomCRM' exists in Dev but not in Prod.",
            AffectedEnvironments = ["Dev", "Prod"],
            Details = new Dictionary<string, string>
            {
                ["SolutionUniqueName"] = "CustomCRM",
                ["SourceEnv"] = "Dev"
            },
            RuleId = "R-SOLUTION-MISSING",
            RiskScore = 75
        };

        var json = JsonSerializer.Serialize(finding, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Finding>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(finding.FindingId, deserialized.FindingId);
        Assert.Equal(finding.Category, deserialized.Category);
        Assert.Equal(finding.Severity, deserialized.Severity);
        Assert.Equal(finding.RuleId, deserialized.RuleId);
        Assert.Equal(finding.RiskScore, deserialized.RiskScore);
        Assert.Equal(2, deserialized.AffectedEnvironments.Count);
        Assert.Equal(2, deserialized.Details.Count);
    }

    [Fact]
    public void ComponentLayer_IsActiveLayer_Returns_True_For_Active()
    {
        var layer = new ComponentLayer
        {
            ComponentId = Guid.NewGuid(),
            ComponentType = ComponentType.Entity,
            SolutionUniqueName = "Active",
            SolutionDisplayName = "Active",
            Order = 0,
            IsManaged = false
        };

        Assert.True(layer.IsActiveLayer);
    }

    [Fact]
    public void ComponentLayer_IsActiveLayer_Returns_False_For_Managed()
    {
        var layer = new ComponentLayer
        {
            ComponentId = Guid.NewGuid(),
            ComponentType = ComponentType.Entity,
            SolutionUniqueName = "MyCrmSolution",
            SolutionDisplayName = "My CRM Solution",
            Order = 1,
            IsManaged = true
        };

        Assert.False(layer.IsActiveLayer);
    }

    private static EnvironmentSnapshot CreateTestSnapshot()
    {
        var publisherId = Guid.NewGuid();
        var solutionId = Guid.NewGuid();
        var componentId = Guid.NewGuid();

        return new EnvironmentSnapshot
        {
            Metadata = new SnapshotMetadata
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                ToolVersion = "1.0.0"
            },
            Environment = new EnvironmentInfo
            {
                EnvironmentId = Guid.NewGuid().ToString(),
                DisplayName = "Dev",
                EnvironmentUrl = new Uri("https://org-dev.crm4.dynamics.com")
            },
            Solutions =
            [
                new Solution
                {
                    SolutionId = solutionId,
                    UniqueName = "CustomCRM",
                    DisplayName = "Custom CRM",
                    Version = "1.2.0.0",
                    IsManaged = true,
                    Publisher = new Publisher
                    {
                        UniqueName = "contoso",
                        DisplayName = "Contoso",
                        CustomizationPrefix = "ctx"
                    }
                }
            ],
            Components =
            [
                new SolutionComponent
                {
                    ComponentId = componentId,
                    ComponentType = ComponentType.Entity,
                    SolutionUniqueName = "CustomCRM",
                    DisplayName = "Account",
                    SchemaName = "account",
                    Behavior = RootComponentBehavior.IncludeSubcomponents
                }
            ],
            Layers =
            [
                new ComponentLayer
                {
                    ComponentId = componentId,
                    ComponentType = ComponentType.Entity,
                    SolutionUniqueName = "CustomCRM",
                    SolutionDisplayName = "Custom CRM",
                    Order = 1,
                    IsManaged = true,
                    PublisherName = "contoso"
                },
                new ComponentLayer
                {
                    ComponentId = componentId,
                    ComponentType = ComponentType.Entity,
                    SolutionUniqueName = "Active",
                    SolutionDisplayName = "Active",
                    Order = 0,
                    IsManaged = false
                }
            ],
            Dependencies =
            [
                new SolutionDependency
                {
                    RequiredComponentId = componentId,
                    RequiredComponentType = ComponentType.Entity,
                    RequiredComponentSolution = "System",
                    DependentComponentId = Guid.NewGuid(),
                    DependentComponentType = ComponentType.Form,
                    DependentComponentSolution = "CustomCRM",
                    DependencyType = DependencyType.Required
                }
            ],
            Settings =
            [
                new EnvironmentSetting
                {
                    Category = "Auditing",
                    Key = "IsAuditEnabled",
                    Value = "true",
                    Description = "Organization-level audit flag"
                }
            ]
        };
    }
}
