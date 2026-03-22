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
        IReadOnlyList<EnvironmentSetting>? settings = null,
        IReadOnlyList<ConnectionReference>? connectionReferences = null,
        IReadOnlyList<ServiceEndpoint>? serviceEndpoints = null,
        IReadOnlyList<CustomConnector>? customConnectors = null,
        IReadOnlyList<EnvironmentVariable>? environmentVariables = null,
        IReadOnlyList<PluginAssembly>? pluginAssemblies = null,
        IReadOnlyList<SdkStep>? sdkSteps = null,
        IReadOnlyList<WebResource>? webResources = null,
        IReadOnlyList<WorkflowDefinition>? workflows = null,
        IReadOnlyList<BusinessRule>? businessRules = null,
        EnvironmentType envType = EnvironmentType.Unknown)
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
                EnvironmentUrl = new Uri($"https://{envName.ToLowerInvariant()}.crm4.dynamics.com"),
                EnvironmentType = envType
            },
            Solutions = solutions ?? [],
            Components = components ?? [],
            Layers = layers ?? [],
            Dependencies = dependencies ?? [],
            Settings = settings ?? [],
            ConnectionReferences = connectionReferences ?? [],
            ServiceEndpoints = serviceEndpoints ?? [],
            CustomConnectors = customConnectors ?? [],
            EnvironmentVariables = environmentVariables ?? [],
            PluginAssemblies = pluginAssemblies ?? [],
            SdkSteps = sdkSteps ?? [],
            WebResources = webResources ?? [],
            Workflows = workflows ?? [],
            BusinessRules = businessRules ?? []
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

    // ── Connection drift ────────────────────────────────────────

    [Fact]
    public void Detects_Missing_ConnectionReference()
    {
        var refs = new[]
        {
            new ConnectionReference
            {
                ConnectionReferenceId = Guid.NewGuid(),
                ConnectionReferenceLogicalName = "cr_sharepoint",
                ConnectorId = "/providers/Microsoft.PowerApps/apis/shared_sharepointonline",
                ConnectionId = "abc"
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", connectionReferences: refs),
            MakeSnapshot("Test")
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.ConnectionConfiguration);
        Assert.Contains("cr_sharepoint", finding.FindingId);
        Assert.Equal(Severity.High, finding.Severity);
    }

    [Fact]
    public void Detects_ConnectionReference_Connector_Drift()
    {
        var devRefs = new[]
        {
            new ConnectionReference
            {
                ConnectionReferenceId = Guid.NewGuid(),
                ConnectionReferenceLogicalName = "cr_sharepoint",
                ConnectorId = "/providers/Microsoft.PowerApps/apis/shared_sharepointonline",
                ConnectionId = "abc"
            }
        };
        var testRefs = new[]
        {
            new ConnectionReference
            {
                ConnectionReferenceId = Guid.NewGuid(),
                ConnectionReferenceLogicalName = "cr_sharepoint",
                ConnectorId = "/providers/Microsoft.PowerApps/apis/shared_onedrive",
                ConnectionId = "def"
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", connectionReferences: devRefs),
            MakeSnapshot("Test", connectionReferences: testRefs)
        ]);

        var finding = Assert.Single(result.Findings, f => f.FindingId.Contains("CONNECTORDRIFT"));
        Assert.Equal(Severity.Medium, finding.Severity);
    }

    [Fact]
    public void Detects_Missing_ServiceEndpoint()
    {
        var eps = new[]
        {
            new ServiceEndpoint
            {
                ServiceEndpointId = Guid.NewGuid(),
                Name = "MyWebhook",
                Contract = EndpointContract.Webhook,
                AuthType = AuthType.HttpHeader
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", serviceEndpoints: eps),
            MakeSnapshot("Test")
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.IntegrationEndpointDrift);
        Assert.Contains("MyWebhook", finding.FindingId);
    }

    [Fact]
    public void Detects_CustomConnector_Missing()
    {
        var connectors = new[]
        {
            new CustomConnector { ConnectorId = Guid.NewGuid(), Name = "MyCustomApi" }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", customConnectors: connectors),
            MakeSnapshot("Test")
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.ConnectorGovernance);
        Assert.Contains("MyCustomApi", finding.Title);
    }

    // ── Plugin drift ────────────────────────────────────────────

    [Fact]
    public void Detects_Missing_PluginAssembly()
    {
        var plugins = new[]
        {
            new PluginAssembly
            {
                PluginAssemblyId = Guid.NewGuid(),
                Name = "Contoso.Plugins",
                Version = "1.0.0.0",
                IsolationMode = PluginIsolationMode.Sandbox,
                SourceType = PluginSourceType.Database
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", pluginAssemblies: plugins),
            MakeSnapshot("Test")
        ]);

        var finding = Assert.Single(result.Findings, f => f.FindingId.Contains("PLG-MISSING"));
        Assert.Equal(Severity.High, finding.Severity);
    }

    [Fact]
    public void Detects_Plugin_Version_Drift()
    {
        var devPlugin = new[]
        {
            new PluginAssembly
            {
                PluginAssemblyId = Guid.NewGuid(),
                Name = "Contoso.Plugins",
                Version = "2.0.0.0",
                IsolationMode = PluginIsolationMode.Sandbox,
                SourceType = PluginSourceType.Database
            }
        };
        var testPlugin = new[]
        {
            new PluginAssembly
            {
                PluginAssemblyId = Guid.NewGuid(),
                Name = "Contoso.Plugins",
                Version = "1.0.0.0",
                IsolationMode = PluginIsolationMode.Sandbox,
                SourceType = PluginSourceType.Database
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", pluginAssemblies: devPlugin),
            MakeSnapshot("Test", pluginAssemblies: testPlugin)
        ]);

        var finding = Assert.Single(result.Findings, f => f.FindingId.Contains("PLG-VERSION"));
        Assert.Equal(Severity.Medium, finding.Severity);
    }

    [Fact]
    public void Detects_SdkStep_Missing()
    {
        var steps = new[]
        {
            new SdkStep
            {
                StepId = Guid.NewGuid(),
                Name = "Contoso.Plugins.OnCreate: Create of account",
                MessageName = "Create",
                PrimaryEntity = "account",
                Stage = SdkStepStage.PostOperation,
                Mode = SdkStepMode.Asynchronous,
                IsDisabled = false
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", sdkSteps: steps),
            MakeSnapshot("Test")
        ]);

        var finding = Assert.Single(result.Findings, f => f.FindingId.Contains("STEP-MISSING"));
        Assert.Equal(Severity.High, finding.Severity);
    }

    [Fact]
    public void Detects_SdkStep_State_Drift()
    {
        var devSteps = new[]
        {
            new SdkStep
            {
                StepId = Guid.NewGuid(),
                Name = "MyStep",
                MessageName = "Update",
                PrimaryEntity = "contact",
                Stage = SdkStepStage.PreOperation,
                Mode = SdkStepMode.Synchronous,
                IsDisabled = false
            }
        };
        var testSteps = new[]
        {
            new SdkStep
            {
                StepId = Guid.NewGuid(),
                Name = "MyStep",
                MessageName = "Update",
                PrimaryEntity = "contact",
                Stage = SdkStepStage.PreOperation,
                Mode = SdkStepMode.Synchronous,
                IsDisabled = true
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", sdkSteps: devSteps),
            MakeSnapshot("Test", sdkSteps: testSteps)
        ]);

        var finding = Assert.Single(result.Findings, f => f.FindingId.Contains("STEP-STATE"));
        Assert.Equal(Severity.High, finding.Severity); // disabled in target
    }

    // ── Web resource drift ──────────────────────────────────────

    [Fact]
    public void Detects_Missing_WebResource()
    {
        var resources = new[]
        {
            new WebResource
            {
                WebResourceId = Guid.NewGuid(),
                Name = "new_/scripts/main.js",
                WebResourceType = WebResourceType.JScript
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", webResources: resources),
            MakeSnapshot("Test")
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.WebResourceDrift);
        Assert.Equal(Severity.High, finding.Severity); // JScript → high
    }

    // ── Workflow drift ──────────────────────────────────────────

    [Fact]
    public void Detects_Missing_Workflow()
    {
        var flows = new[]
        {
            new WorkflowDefinition
            {
                WorkflowId = Guid.NewGuid(),
                Name = "SendWelcomeEmail",
                UniqueName = "SendWelcomeEmail",
                Category = WorkflowCategory.ModernFlow,
                Mode = WorkflowMode.Background,
                IsActivated = true
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", workflows: flows),
            MakeSnapshot("Test")
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.WorkflowConfiguration);
        Assert.Equal(Severity.High, finding.Severity); // active flow missing → high
    }

    [Fact]
    public void Detects_Workflow_Activation_State_Drift()
    {
        var devFlows = new[]
        {
            new WorkflowDefinition
            {
                WorkflowId = Guid.NewGuid(),
                Name = "ApprovalFlow",
                UniqueName = "ApprovalFlow",
                Category = WorkflowCategory.ModernFlow,
                Mode = WorkflowMode.Background,
                IsActivated = true
            }
        };
        var testFlows = new[]
        {
            new WorkflowDefinition
            {
                WorkflowId = Guid.NewGuid(),
                Name = "ApprovalFlow",
                UniqueName = "ApprovalFlow",
                Category = WorkflowCategory.ModernFlow,
                Mode = WorkflowMode.Background,
                IsActivated = false
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", workflows: devFlows),
            MakeSnapshot("Test", workflows: testFlows)
        ]);

        var finding = Assert.Single(result.Findings, f => f.FindingId.Contains("WFL-STATE"));
        Assert.Equal(Severity.High, finding.Severity); // active→inactive
    }

    // ── Environment variable drift ──────────────────────────────

    [Fact]
    public void Detects_Missing_EnvironmentVariable()
    {
        var vars = new[]
        {
            new EnvironmentVariable
            {
                DefinitionId = Guid.NewGuid(),
                SchemaName = "cr_ApiBaseUrl",
                Type = EnvironmentVariableType.String,
                DefaultValue = "https://api.contoso.com",
                IsRequired = true
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", environmentVariables: vars),
            MakeSnapshot("Test")
        ]);

        var finding = Assert.Single(result.Findings, f => f.FindingId.Contains("ENVVAR-MISSING"));
        Assert.Equal(Severity.High, finding.Severity); // IsRequired → high
    }

    [Fact]
    public void Detects_EnvironmentVariable_Value_Drift()
    {
        var devVars = new[]
        {
            new EnvironmentVariable
            {
                DefinitionId = Guid.NewGuid(),
                SchemaName = "cr_ApiBaseUrl",
                Type = EnvironmentVariableType.String,
                DefaultValue = "https://dev.contoso.com"
            }
        };
        var testVars = new[]
        {
            new EnvironmentVariable
            {
                DefinitionId = Guid.NewGuid(),
                SchemaName = "cr_ApiBaseUrl",
                Type = EnvironmentVariableType.String,
                DefaultValue = "https://test.contoso.com"
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", environmentVariables: devVars),
            MakeSnapshot("Test", environmentVariables: testVars)
        ]);

        var finding = Assert.Single(result.Findings, f => f.FindingId.Contains("ENVVAR-VALUEDRIFT"));
        Assert.Equal(Severity.Medium, finding.Severity);
    }

    [Fact]
    public void Detects_Required_EnvironmentVariable_Without_Value()
    {
        var devVars = new[]
        {
            new EnvironmentVariable
            {
                DefinitionId = Guid.NewGuid(),
                SchemaName = "cr_ApiKey",
                Type = EnvironmentVariableType.String,
                DefaultValue = "secretkey",
                IsRequired = true
            }
        };
        var testVars = new[]
        {
            new EnvironmentVariable
            {
                DefinitionId = Guid.NewGuid(),
                SchemaName = "cr_ApiKey",
                Type = EnvironmentVariableType.String,
                IsRequired = true
                // No default, no current → HasValue = false
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", environmentVariables: devVars),
            MakeSnapshot("Test", environmentVariables: testVars)
        ]);

        Assert.Contains(result.Findings, f => f.FindingId.Contains("ENVVAR-NOVAL"));
    }

    // ── Business rule drift ─────────────────────────────────────

    [Fact]
    public void Detects_Missing_BusinessRule()
    {
        var rules = new[]
        {
            new BusinessRule
            {
                BusinessRuleId = Guid.NewGuid(),
                Name = "SetDefaultStatus",
                UniqueName = "SetDefaultStatus",
                PrimaryEntity = "account",
                Scope = BusinessRuleScope.Entity,
                IsActivated = true
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", businessRules: rules),
            MakeSnapshot("Test")
        ]);

        var finding = Assert.Single(result.Findings, f => f.Category == FindingCategory.BusinessRuleDrift);
        Assert.Contains("SetDefaultStatus", finding.FindingId);
        Assert.Equal(Severity.High, finding.Severity); // active rule missing
    }

    [Fact]
    public void Detects_BusinessRule_Scope_Drift()
    {
        var devRules = new[]
        {
            new BusinessRule
            {
                BusinessRuleId = Guid.NewGuid(),
                Name = "ValidateName",
                UniqueName = "ValidateName",
                PrimaryEntity = "contact",
                Scope = BusinessRuleScope.Entity,
                IsActivated = true
            }
        };
        var testRules = new[]
        {
            new BusinessRule
            {
                BusinessRuleId = Guid.NewGuid(),
                Name = "ValidateName",
                UniqueName = "ValidateName",
                PrimaryEntity = "contact",
                Scope = BusinessRuleScope.AllForms,
                IsActivated = true
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", businessRules: devRules),
            MakeSnapshot("Test", businessRules: testRules)
        ]);

        var finding = Assert.Single(result.Findings, f => f.FindingId.Contains("BRL-SCOPE"));
        Assert.Equal(Severity.Medium, finding.Severity);
    }

    // ── Single-environment analysis ─────────────────────────────

    [Fact]
    public void SingleEnv_Detects_Disabled_SdkStep()
    {
        var steps = new[]
        {
            new SdkStep
            {
                StepId = Guid.NewGuid(),
                Name = "DisabledStep",
                MessageName = "Create",
                PrimaryEntity = "account",
                Stage = SdkStepStage.PostOperation,
                Mode = SdkStepMode.Asynchronous,
                IsDisabled = true
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Prod", sdkSteps: steps, envType: EnvironmentType.Prod)
        ]);

        Assert.Contains(result.Findings, f => f.FindingId.Contains("SINGLE-STEP-DISABLED"));
    }

    [Fact]
    public void SingleEnv_Detects_Deactivated_Workflow_In_Prod()
    {
        var flows = new[]
        {
            new WorkflowDefinition
            {
                WorkflowId = Guid.NewGuid(),
                Name = "InactiveFlow",
                Category = WorkflowCategory.ModernFlow,
                Mode = WorkflowMode.Background,
                IsActivated = false
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Prod", workflows: flows, envType: EnvironmentType.Prod)
        ]);

        Assert.Contains(result.Findings, f => f.FindingId.Contains("SINGLE-WFL-INACTIVE"));
    }

    [Fact]
    public void SingleEnv_Skips_Deactivated_Workflow_In_Dev()
    {
        var flows = new[]
        {
            new WorkflowDefinition
            {
                WorkflowId = Guid.NewGuid(),
                Name = "InactiveFlow",
                Category = WorkflowCategory.ModernFlow,
                Mode = WorkflowMode.Background,
                IsActivated = false
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Dev", workflows: flows, envType: EnvironmentType.Dev)
        ]);

        Assert.DoesNotContain(result.Findings, f => f.FindingId.Contains("SINGLE-WFL-INACTIVE"));
    }

    [Fact]
    public void SingleEnv_Detects_Deactivated_BusinessRule_In_Prod()
    {
        var rules = new[]
        {
            new BusinessRule
            {
                BusinessRuleId = Guid.NewGuid(),
                Name = "InactiveRule",
                PrimaryEntity = "account",
                Scope = BusinessRuleScope.Entity,
                IsActivated = false
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Prod", businessRules: rules, envType: EnvironmentType.Prod)
        ]);

        Assert.Contains(result.Findings, f => f.FindingId.Contains("SINGLE-BRL-INACTIVE"));
    }

    [Fact]
    public void SingleEnv_Detects_Missing_EnvironmentVariable_Value()
    {
        var vars = new[]
        {
            new EnvironmentVariable
            {
                DefinitionId = Guid.NewGuid(),
                SchemaName = "cr_EmptyRequired",
                Type = EnvironmentVariableType.String,
                IsRequired = true
                // No default, no current → HasValue = false
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Prod", environmentVariables: vars, envType: EnvironmentType.Prod)
        ]);

        Assert.Contains(result.Findings, f => f.FindingId.Contains("SINGLE-ENVVAR-NOVAL"));
    }

    [Fact]
    public void SingleEnv_Detects_Orphaned_ConnectionReference()
    {
        var refs = new[]
        {
            new ConnectionReference
            {
                ConnectionReferenceId = Guid.NewGuid(),
                ConnectionReferenceLogicalName = "cr_orphaned",
                ConnectorId = "/providers/Microsoft.PowerApps/apis/shared_commondataservice"
                // No ConnectionId → orphaned
            }
        };

        IDiffEngine engine = new SnapshotDiffEngine();
        var result = engine.Compare([
            MakeSnapshot("Prod", connectionReferences: refs)
        ]);

        Assert.Contains(result.Findings, f => f.FindingId.Contains("SINGLE-CONN-ORPHAN"));
    }
}
