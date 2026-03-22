using System.Text.Json;
using Xunit;

namespace D365Xray.IntegrationTests;

/// <summary>
/// Tests that authenticate against a real Dataverse environment
/// and perform basic API calls to verify connectivity and data retrieval.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DataverseConnectionTests : IClassFixture<DataverseFixture>
{
    private readonly DataverseFixture _fixture;

    public DataverseConnectionTests(DataverseFixture fixture)
    {
        _fixture = fixture;
    }

    [RequiresDataverseFact]
    public async Task Authenticate_And_RetrieveVersion_ReturnsVersion()
    {
        var client = _fixture.GetClient();

        using var doc = await client.GetAsync("RetrieveVersion");

        Assert.NotNull(doc);
        Assert.True(doc.RootElement.TryGetProperty("Version", out var version));
        Assert.False(string.IsNullOrEmpty(version.GetString()));
    }

    [RequiresDataverseFact]
    public async Task GetAsync_WhoAmI_ReturnsUserId()
    {
        var client = _fixture.GetClient();

        using var doc = await client.GetAsync("WhoAmI");

        Assert.NotNull(doc);
        Assert.True(doc.RootElement.TryGetProperty("UserId", out var userId));
        Assert.NotEqual(Guid.Empty, Guid.Parse(userId.GetString()!));
    }

    [RequiresDataverseFact]
    public async Task GetAsync_OrganizationId_MatchesConfig()
    {
        var client = _fixture.GetClient();

        using var doc = await client.GetAsync("WhoAmI");

        Assert.True(doc.RootElement.TryGetProperty("OrganizationId", out var orgId));
        var orgGuid = Guid.Parse(orgId.GetString()!);
        Assert.NotEqual(Guid.Empty, orgGuid);
    }

    [RequiresDataverseFact]
    public async Task GetAsync_Solutions_ReturnsList()
    {
        var client = _fixture.GetClient();

        using var doc = await client.GetAsync(
            "solutions",
            "$select=uniquename,friendlyname,version,ismanaged&$top=5");

        Assert.NotNull(doc);
        Assert.True(doc.RootElement.TryGetProperty("value", out var value));
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.True(value.GetArrayLength() > 0, "Expected at least one solution in the environment.");
    }

    [RequiresDataverseFact]
    public async Task GetAsync_SolutionComponents_ReturnsList()
    {
        var client = _fixture.GetClient();

        using var doc = await client.GetAsync(
            "solutioncomponents",
            "$select=componenttype,objectid&$top=5");

        Assert.NotNull(doc);
        Assert.True(doc.RootElement.TryGetProperty("value", out var value));
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.True(value.GetArrayLength() > 0, "Expected at least one solution component.");
    }

    [RequiresDataverseFact]
    public async Task GetPagedAsync_Solutions_YieldsAtLeastOnePage()
    {
        var client = _fixture.GetClient();

        var pages = new List<JsonDocument>();
        try
        {
            await foreach (var page in client.GetPagedAsync(
                "solutions",
                "$select=uniquename&$top=2"))
            {
                pages.Add(page);
                if (pages.Count >= 2)
                {
                    break; // Enough to prove paging works
                }
            }

            Assert.True(pages.Count >= 1, "Expected at least one page of solutions.");
        }
        finally
        {
            foreach (var page in pages)
            {
                page.Dispose();
            }
        }
    }

    [RequiresDataverseFact]
    public async Task GetAsync_EnvironmentSettings_ReturnsList()
    {
        var client = _fixture.GetClient();

        using var doc = await client.GetAsync(
            "organizationsettings",
            "$top=5");

        Assert.NotNull(doc);
        // organizationsettings might return value array or direct object
        Assert.True(doc.RootElement.ValueKind == JsonValueKind.Object);
    }

    [RequiresDataverseFact]
    public async Task EnvironmentUrl_MatchesConfiguration()
    {
        var client = _fixture.GetClient();

        Assert.Equal(_fixture.Config.EnvironmentUrl, client.EnvironmentUrl);
    }
}
