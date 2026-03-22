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

        var result = await DependencyCollector.CollectAsync(fake, solutionLookup, CancellationToken.None);

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

        var result = await DependencyCollector.CollectAsync(fake, new Dictionary<Guid, string>(), CancellationToken.None);

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

        var result = await SettingsCollector.CollectAsync(fake, CancellationToken.None);

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

        var result = await SettingsCollector.CollectAsync(fake, CancellationToken.None);

        Assert.Empty(result);
    }
}
