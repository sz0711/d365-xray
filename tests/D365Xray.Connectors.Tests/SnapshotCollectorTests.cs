using D365Xray.Connectors.Collectors;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D365Xray.Connectors.Tests;

public class SnapshotCollectorTests
{
    // ── SolutionCollector ───────────────────────────────────────

    [Fact]
    public async Task SolutionCollector_Maps_Solutions_And_Publishers()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("solutions", """
        {
          "value": [
            {
              "solutionid": "11111111-1111-1111-1111-111111111111",
              "uniquename": "CoreSolution",
              "friendlyname": "Core Solution",
              "version": "1.2.3.4",
              "ismanaged": true,
              "installedon": "2024-06-15T10:00:00Z",
              "modifiedon": "2025-01-20T08:30:00Z",
              "publisherid": {
                "uniquename": "contoso",
                "friendlyname": "Contoso Ltd",
                "customizationprefix": "con"
              }
            },
            {
              "solutionid": "22222222-2222-2222-2222-222222222222",
              "uniquename": "Customizations",
              "friendlyname": "Custom Work",
              "version": "1.0.0.0",
              "ismanaged": false,
              "publisherid": {
                "uniquename": "default",
                "friendlyname": "Default Publisher",
                "customizationprefix": "new"
              }
            }
          ]
        }
        """);

        var result = await SolutionCollector.CollectAsync(fake, CancellationToken.None);

        Assert.Equal(2, result.Count);

        var managed = result[0];
        Assert.Equal("CoreSolution", managed.UniqueName);
        Assert.True(managed.IsManaged);
        Assert.Equal("1.2.3.4", managed.Version);
        Assert.Equal("contoso", managed.Publisher.UniqueName);
        Assert.Equal("con", managed.Publisher.CustomizationPrefix);
        Assert.NotNull(managed.InstalledOn);

        var unmanaged = result[1];
        Assert.False(unmanaged.IsManaged);
        Assert.Null(unmanaged.InstalledOn);
    }

    [Fact]
    public async Task SolutionCollector_Handles_Missing_Publisher()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("solutions", """
        {
          "value": [
            {
              "solutionid": "33333333-3333-3333-3333-333333333333",
              "uniquename": "NoPub",
              "friendlyname": "No Publisher",
              "version": "1.0.0.0",
              "ismanaged": false
            }
          ]
        }
        """);

        var result = await SolutionCollector.CollectAsync(fake, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("unknown", result[0].Publisher.UniqueName);
    }

    // ── ComponentCollector ──────────────────────────────────────

    [Fact]
    public async Task ComponentCollector_Maps_Types_And_Solution()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("solutioncomponents", """
        {
          "value": [
            {
              "solutioncomponentid": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "objectid": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
              "componenttype": 1,
              "rootcomponentbehavior": 0,
              "solutionid": { "uniquename": "MySolution" }
            },
            {
              "solutioncomponentid": "cccccccc-cccc-cccc-cccc-cccccccccccc",
              "objectid": "dddddddd-dddd-dddd-dddd-dddddddddddd",
              "componenttype": 9999,
              "rootcomponentbehavior": 0,
              "solutionid": { "uniquename": "MySolution" }
            }
          ]
        }
        """);

        var result = await ComponentCollector.CollectAsync(fake, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(ComponentType.Entity, result[0].ComponentType);
        Assert.Equal("MySolution", result[0].SolutionUniqueName);

        // Unknown component type falls back to Unknown
        Assert.Equal(ComponentType.Unknown, result[1].ComponentType);
    }

    // ── LayerCollector ──────────────────────────────────────────

    [Fact]
    public async Task LayerCollector_Maps_Layers_And_Detects_Active()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("msdyn_componentlayers", """
        {
          "value": [
            {
              "msdyn_componentid": "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
              "msdyn_solutioncomponentname": "Entity",
              "msdyn_name": "Active",
              "msdyn_solutionname": "Active",
              "msdyn_order": 2,
              "msdyn_publishername": "Default",
              "createdon": "2025-03-01T12:00:00Z"
            },
            {
              "msdyn_componentid": "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
              "msdyn_solutioncomponentname": "Entity",
              "msdyn_name": "Core Solution",
              "msdyn_solutionname": "CoreSolution",
              "msdyn_order": 1,
              "msdyn_publishername": "Contoso",
              "createdon": "2024-06-15T10:00:00Z"
            }
          ]
        }
        """);

        var result = await LayerCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(2, result.Count);

        var activeLayer = result[0];
        Assert.False(activeLayer.IsManaged);
        Assert.True(activeLayer.IsActiveLayer);
        Assert.Equal(2, activeLayer.Order);
        Assert.Equal(ComponentType.Entity, activeLayer.ComponentType);

        var managedLayer = result[1];
        Assert.True(managedLayer.IsManaged);
        Assert.False(managedLayer.IsActiveLayer);
        Assert.Equal("CoreSolution", managedLayer.SolutionUniqueName);
    }

    // ── DependencyCollector ─────────────────────────────────────

    [Fact]
    public async Task DependencyCollector_Maps_Types_And_Resolves_Solutions()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("dependencies", """
        {
          "value": [
            {
              "requiredcomponentobjectid": "11111111-1111-1111-1111-111111111111",
              "requiredcomponenttype": 1,
              "_requiredcomponentbasesolutionid_value": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "dependentcomponentobjectid": "22222222-2222-2222-2222-222222222222",
              "dependentcomponenttype": 61,
              "_dependentcomponentbasesolutionid_value": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
              "dependencytype": 2
            }
          ]
        }
        """);

        var solutionLookup = new Dictionary<Guid, string>
        {
            [Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")] = "SystemSolution",
            [Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")] = "CustomSolution"
        };

        var result = await DependencyCollector.CollectAsync(fake, solutionLookup, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var dep = result[0];
        Assert.Equal(ComponentType.Entity, dep.RequiredComponentType);
        Assert.Equal(ComponentType.WebResource, dep.DependentComponentType);
        Assert.Equal(DependencyType.Required, dep.DependencyType); // Dataverse Published=2 → Required
        Assert.Equal("SystemSolution", dep.RequiredComponentSolution);
        Assert.Equal("CustomSolution", dep.DependentComponentSolution);
    }

    [Fact]
    public async Task DependencyCollector_Returns_Optional_For_Unknown_Type()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("dependencies", """
        {
          "value": [
            {
              "requiredcomponentobjectid": "11111111-1111-1111-1111-111111111111",
              "requiredcomponenttype": 1,
              "dependentcomponentobjectid": "22222222-2222-2222-2222-222222222222",
              "dependentcomponenttype": 1,
              "dependencytype": 0
            }
          ]
        }
        """);

        var result = await DependencyCollector.CollectAsync(fake, new Dictionary<Guid, string>(), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, CancellationToken.None);

        Assert.Equal(DependencyType.Optional, result[0].DependencyType);
    }

    // ── SettingsCollector ───────────────────────────────────────

    [Fact]
    public async Task SettingsCollector_Extracts_Settings_From_Organization()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("organizations", """
        {
          "value": [
            {
              "organizationid": "org-001",
              "isauditenabled": true,
              "plugintracelogsetting": 2,
              "maxuploadfilesize": 32768,
              "blockedattachments": "ade;adp;app",
              "languagecode": 1033,
              "sessiontimeoutinmins": 1440
            }
          ]
        }
        """);

        var result = await SettingsCollector.CollectAsync(fake, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, CancellationToken.None);

        Assert.True(result.Count > 0);

        var audit = result.First(s => s.Key == "isauditenabled");
        Assert.Equal("Security", audit.Category);
        Assert.Equal("true", audit.Value);

        var traceLog = result.First(s => s.Key == "plugintracelogsetting");
        Assert.Equal("Diagnostics", traceLog.Category);
        Assert.Equal("2", traceLog.Value);

        var maxUpload = result.First(s => s.Key == "maxuploadfilesize");
        Assert.Equal("32768", maxUpload.Value);

        var blocked = result.First(s => s.Key == "blockedattachments");
        Assert.Equal("ade;adp;app", blocked.Value);

        var lang = result.First(s => s.Key == "languagecode");
        Assert.Equal("1033", lang.Value);
    }

    [Fact]
    public async Task SettingsCollector_Handles_Empty_Response()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("organizations", """{ "value": [] }""");

        var result = await SettingsCollector.CollectAsync(fake, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, CancellationToken.None);

        Assert.Empty(result);
    }

    // ── ConnectionReferenceCollector ────────────────────────────

    [Fact]
    public async Task ConnectionReferenceCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("connectionreferences", """
        {
          "value": [
            {
              "connectionreferenceid": "11111111-1111-1111-1111-111111111111",
              "connectionreferencelogicalname": "cr_sharepoint",
              "connectionreferencedisplayname": "SharePoint Ref",
              "connectorid": "/providers/Microsoft.PowerApps/apis/shared_sharepointonline",
              "connectionid": "conn-abc",
              "iscustomconnector": false,
              "statuscode": 1
            }
          ]
        }
        """);

        var result = await ConnectionReferenceCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var cr = result[0];
        Assert.Equal("cr_sharepoint", cr.ConnectionReferenceLogicalName);
        Assert.Equal("SharePoint Ref", cr.DisplayName);
        Assert.Equal("conn-abc", cr.ConnectionId);
        Assert.False(cr.IsCustomConnector);
    }

    // ── ServiceEndpointCollector ────────────────────────────────

    [Fact]
    public async Task ServiceEndpointCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("serviceendpoints", """
        {
          "value": [
            {
              "serviceendpointid": "22222222-2222-2222-2222-222222222222",
              "name": "MyWebhook",
              "description": "Test webhook",
              "contract": 8,
              "url": "https://example.com/hook",
              "authtype": 5
            }
          ]
        }
        """);

        var result = await ServiceEndpointCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var ep = result[0];
        Assert.Equal("MyWebhook", ep.Name);
        Assert.Equal(EndpointContract.Webhook, ep.Contract);
        Assert.Equal(AuthType.HttpHeader, ep.AuthType);
    }

    // ── CustomConnectorCollector ────────────────────────────────

    [Fact]
    public async Task CustomConnectorCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("connectors", """
        {
          "value": [
            {
              "connectorid": "33333333-3333-3333-3333-333333333333",
              "name": "MyApi",
              "displayname": "My API Connector",
              "connectortype": "Custom",
              "description": "A custom API connector"
            }
          ]
        }
        """);

        var result = await CustomConnectorCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var cc = result[0];
        Assert.Equal("MyApi", cc.Name);
        Assert.Equal("My API Connector", cc.DisplayName);
        Assert.Equal("Custom", cc.ConnectorType);
    }

    // ── EnvironmentVariableCollector ────────────────────────────

    [Fact]
    public async Task EnvironmentVariableCollector_Maps_Fields_With_CurrentValue()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("environmentvariabledefinitions", """
        {
          "value": [
            {
              "environmentvariabledefinitionid": "44444444-4444-4444-4444-444444444444",
              "schemaname": "cr_ApiBaseUrl",
              "displayname": "API Base URL",
              "type": 100000000,
              "defaultvalue": "https://default.contoso.com",
              "isrequired": true,
              "environmentvariabledefinition_environmentvariablevalue": [
                { "value": "https://prod.contoso.com" }
              ]
            }
          ]
        }
        """);

        var result = await EnvironmentVariableCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var ev = result[0];
        Assert.Equal("cr_ApiBaseUrl", ev.SchemaName);
        Assert.Equal(EnvironmentVariableType.String, ev.Type);
        Assert.Equal("https://default.contoso.com", ev.DefaultValue);
        Assert.Equal("https://prod.contoso.com", ev.CurrentValue);
        Assert.True(ev.IsRequired);
        Assert.True(ev.HasValue);
    }

    // ── PluginAssemblyCollector ─────────────────────────────────

    [Fact]
    public async Task PluginAssemblyCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("pluginassemblies", """
        {
          "value": [
            {
              "pluginassemblyid": "55555555-5555-5555-5555-555555555555",
              "name": "Contoso.Plugins",
              "version": "1.2.3.0",
              "publickeytoken": "abc123",
              "isolationmode": 2,
              "sourcetype": 0,
              "modifiedon": "2025-01-15T10:00:00Z"
            }
          ]
        }
        """);

        var result = await PluginAssemblyCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var plg = result[0];
        Assert.Equal("Contoso.Plugins", plg.Name);
        Assert.Equal("1.2.3.0", plg.Version);
        Assert.Equal(PluginIsolationMode.Sandbox, plg.IsolationMode);
        Assert.Equal(PluginSourceType.Database, plg.SourceType);
    }

    // ── SdkStepCollector ────────────────────────────────────────

    [Fact]
    public async Task SdkStepCollector_Maps_Fields_With_Expanded_Navigation()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("sdkmessageprocessingsteps", """
        {
          "value": [
            {
              "sdkmessageprocessingstepid": "66666666-6666-6666-6666-666666666666",
              "name": "Contoso.OnCreate",
              "stage": 40,
              "mode": 1,
              "rank": 1,
              "statecode": 0,
              "filteringattributes": "name,email",
              "configuration": null,
              "sdkmessageid": { "name": "Create" },
              "sdkmessagefilterid": { "primaryobjecttypecode": "account" },
              "plugintypeid": { "name": "OnCreateHandler" }
            }
          ]
        }
        """);

        var result = await SdkStepCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var step = result[0];
        Assert.Equal("Contoso.OnCreate", step.Name);
        Assert.Equal("Create", step.MessageName);
        Assert.Equal("account", step.PrimaryEntity);
        Assert.Equal(SdkStepStage.PostOperation, step.Stage);
        Assert.Equal(SdkStepMode.Asynchronous, step.Mode);
        Assert.False(step.IsDisabled); // statecode=0 → enabled
        Assert.Equal("OnCreateHandler", step.PluginTypeName);
    }

    [Fact]
    public async Task SdkStepCollector_DisabledStep_Has_IsDisabled_True()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("sdkmessageprocessingsteps", """
        {
          "value": [
            {
              "sdkmessageprocessingstepid": "66666666-6666-6666-6666-666666666666",
              "name": "Contoso.Disabled",
              "stage": 20,
              "mode": 0,
              "rank": 1,
              "statecode": 1
            }
          ]
        }
        """);

        var result = await SdkStepCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        Assert.True(result[0].IsDisabled); // statecode=1 → disabled
    }

    // ── WebResourceCollector ────────────────────────────────────

    [Fact]
    public async Task WebResourceCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("webresourceset", """
        {
          "value": [
            {
              "webresourceid": "77777777-7777-7777-7777-777777777777",
              "name": "new_/scripts/main.js",
              "displayname": "Main Script",
              "webresourcetype": 3,
              "ismanaged": true,
              "iscustomizable": true,
              "modifiedon": "2025-01-20T12:00:00Z"
            }
          ]
        }
        """);

        var result = await WebResourceCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var wr = result[0];
        Assert.Equal("new_/scripts/main.js", wr.Name);
        Assert.Equal(WebResourceType.JScript, wr.WebResourceType);
        Assert.True(wr.IsManaged);
    }

    // ── WorkflowCollector ───────────────────────────────────────

    [Fact]
    public async Task WorkflowCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("workflows", """
        {
          "value": [
            {
              "workflowid": "88888888-8888-8888-8888-888888888888",
              "name": "ApprovalFlow",
              "uniquename": "ApprovalFlow",
              "category": 5,
              "mode": 0,
              "statecode": 1,
              "primaryentity": "account",
              "triggeroncreate": true,
              "triggeronupdateattributelist": "name,revenue",
              "triggerondelete": false,
              "ismanaged": true,
              "modifiedon": "2025-02-01T08:00:00Z"
            }
          ]
        }
        """);

        var result = await WorkflowCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var wf = result[0];
        Assert.Equal("ApprovalFlow", wf.Name);
        Assert.Equal(WorkflowCategory.ModernFlow, wf.Category);
        Assert.True(wf.IsActivated); // statecode=1 → activated
        Assert.Equal("account", wf.PrimaryEntity);
    }

    // ── BusinessRuleCollector ───────────────────────────────────

    [Fact]
    public async Task BusinessRuleCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("workflows", """
        {
          "value": [
            {
              "workflowid": "99999999-9999-9999-9999-999999999999",
              "name": "SetDefaultStatus",
              "uniquename": "SetDefaultStatus",
              "primaryentity": "account",
              "scope": 0,
              "statecode": 1,
              "ismanaged": false,
              "modifiedon": "2025-03-01T09:00:00Z"
            }
          ]
        }
        """);

        var result = await BusinessRuleCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var br = result[0];
        Assert.Equal("SetDefaultStatus", br.Name);
        Assert.Equal("account", br.PrimaryEntity);
        Assert.Equal(BusinessRuleScope.Entity, br.Scope);
        Assert.True(br.IsActivated); // statecode=1 → activated
        Assert.False(br.IsManaged);
    }

    // ── FormCollector ───────────────────────────────────────────

    [Fact]
    public async Task FormCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("systemforms", """
        {
          "value": [
            {
              "formid": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "name": "Account Main",
              "description": "Main account form",
              "objecttypecode": "account",
              "type": 2,
              "ismanaged": true,
              "isdefault": true,
              "formactivationstate": 1,
              "uniquename": "account_main_1",
              "modifiedon": "2025-03-15T10:00:00Z"
            }
          ]
        }
        """);

        var result = await FormCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var form = result[0];
        Assert.Equal("Account Main", form.Name);
        Assert.Equal("account", form.EntityLogicalName);
        Assert.Equal(FormType.Main, form.FormType);
        Assert.True(form.IsManaged);
        Assert.True(form.IsDefault);
        Assert.Equal("account_main_1", form.UniqueName);
    }

    // ── ViewCollector ───────────────────────────────────────────

    [Fact]
    public async Task ViewCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("savedqueries", """
        {
          "value": [
            {
              "savedqueryid": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
              "name": "Active Accounts",
              "returnedtypecode": "account",
              "isdefault": true,
              "ismanaged": false,
              "iscustomizable": true,
              "querytype": 0,
              "modifiedon": "2025-02-20T08:00:00Z"
            }
          ]
        }
        """);

        var result = await ViewCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var view = result[0];
        Assert.Equal("Active Accounts", view.Name);
        Assert.Equal("account", view.EntityLogicalName);
        Assert.True(view.IsDefault);
    }

    // ── ChartCollector ──────────────────────────────────────────

    [Fact]
    public async Task ChartCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("savedqueryvisualizations", """
        {
          "value": [
            {
              "savedqueryvisualizationid": "cccccccc-cccc-cccc-cccc-cccccccccccc",
              "name": "Pipeline Chart",
              "primaryentitytypecode": "opportunity",
              "ismanaged": true,
              "isdefault": false,
              "modifiedon": "2025-01-10T11:00:00Z"
            }
          ]
        }
        """);

        var result = await ChartCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var chart = result[0];
        Assert.Equal("Pipeline Chart", chart.Name);
        Assert.Equal("opportunity", chart.EntityLogicalName);
        Assert.True(chart.IsManaged);
    }

    // ── AppModuleCollector ──────────────────────────────────────

    [Fact]
    public async Task AppModuleCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("appmodules", """
        {
          "value": [
            {
              "appmoduleid": "dddddddd-dddd-dddd-dddd-dddddddddddd",
              "name": "Sales Hub",
              "uniquename": "saleshub",
              "appversion": "1.0.0.0",
              "ismanaged": true,
              "isdefault": false,
              "statecode": 0,
              "clienttype": 4,
              "modifiedon": "2025-01-25T14:00:00Z"
            }
          ]
        }
        """);

        var result = await AppModuleCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var app = result[0];
        Assert.Equal("Sales Hub", app.Name);
        Assert.Equal("saleshub", app.UniqueName);
        Assert.Equal("1.0.0.0", app.AppVersion);
        Assert.True(app.IsPublished); // statecode=0 → active/published
        Assert.True(app.IsManaged);
    }

    // ── SecurityRoleCollector ───────────────────────────────────

    [Fact]
    public async Task SecurityRoleCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("roles", """
        {
          "value": [
            {
              "roleid": "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
              "name": "Sales Manager",
              "_businessunitid_value": "ffffffff-ffff-ffff-ffff-ffffffffffff",
              "ismanaged": true,
              "iscustomizable": true,
              "isinherited": false,
              "modifiedon": "2025-02-10T09:00:00Z"
            }
          ]
        }
        """);

        var result = await SecurityRoleCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var role = result[0];
        Assert.Equal("Sales Manager", role.Name);
        Assert.True(role.IsManaged);
        Assert.False(role.IsInherited);
        Assert.Equal(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), role.BusinessUnitId);
    }

    // ── FieldSecurityProfileCollector ───────────────────────────

    [Fact]
    public async Task FieldSecurityProfileCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("fieldsecurityprofiles", """
        {
          "value": [
            {
              "fieldsecurityprofileid": "11111111-2222-3333-4444-555555555555",
              "name": "Restrict Email",
              "description": "Restrict access to email fields",
              "ismanaged": true,
              "modifiedon": "2025-01-05T07:00:00Z"
            }
          ]
        }
        """);

        var result = await FieldSecurityProfileCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var profile = result[0];
        Assert.Equal("Restrict Email", profile.Name);
        Assert.Equal("Restrict access to email fields", profile.Description);
        Assert.True(profile.IsManaged);
    }

    // ── EntityMetadataCollector ─────────────────────────────────

    [Fact]
    public async Task EntityMetadataCollector_Maps_Fields()
    {
        var fake = new FakeDataverseClient();
        fake.Enqueue("EntityDefinitions", """
        {
          "value": [
            {
              "MetadataId": "aabbccdd-1122-3344-5566-778899aabbcc",
              "LogicalName": "account",
              "DisplayName": { "UserLocalizedLabel": { "Label": "Account", "LanguageCode": 1033 } },
              "SchemaName": "Account",
              "IsManaged": true,
              "IsCustomEntity": false,
              "IsCustomizable": { "Value": true },
              "IsAuditEnabled": { "Value": true },
              "ChangeTrackingEnabled": true,
              "OwnershipType": "UserOwned"
            }
          ]
        }
        """);

        var result = await EntityMetadataCollector.CollectAsync(fake, NullLogger.Instance, CancellationToken.None);

        Assert.Single(result);
        var entity = result[0];
        Assert.Equal("account", entity.LogicalName);
        Assert.Equal("Account", entity.DisplayName);
        Assert.Equal("Account", entity.SchemaName);
        Assert.True(entity.IsAuditEnabled);
        Assert.True(entity.ChangeTrackingEnabled);
        Assert.False(entity.IsCustomEntity);
        Assert.Equal(OwnershipType.UserOwned, entity.OwnershipType);
    }
}
