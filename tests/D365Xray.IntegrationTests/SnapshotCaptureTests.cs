using D365Xray.Core;
using D365Xray.Core.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D365Xray.IntegrationTests;

/// <summary>
/// End-to-end test: captures a full snapshot from a real Dataverse environment.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SnapshotCaptureTests : IClassFixture<DataverseFixture>
{
    private readonly DataverseFixture _fixture;

    public SnapshotCaptureTests(DataverseFixture fixture)
    {
        _fixture = fixture;
    }

    [RequiresDataverseFact]
    public async Task CaptureSnapshot_ReturnsPopulatedSnapshot()
    {
        var connector = _fixture.Services.GetRequiredService<IEnvironmentConnector>();

        var envInfo = new EnvironmentInfo
        {
            EnvironmentId = "integration-test",
            DisplayName = "IntegrationTest",
            EnvironmentUrl = _fixture.Config.EnvironmentUrl
        };

        var snapshot = await connector.CaptureSnapshotAsync(envInfo);

        // Metadata
        Assert.NotNull(snapshot.Metadata);
        Assert.True(snapshot.Metadata.CapturedAtUtc > DateTimeOffset.MinValue);
        Assert.True(snapshot.Metadata.CapturedDuration > TimeSpan.Zero);

        // Environment
        Assert.Equal("IntegrationTest", snapshot.Environment.DisplayName);
        Assert.NotNull(snapshot.Environment.DataverseVersion);

        // Solutions
        Assert.NotEmpty(snapshot.Solutions);
        Assert.All(snapshot.Solutions, s => Assert.False(string.IsNullOrEmpty(s.UniqueName)));

        // Components
        Assert.NotEmpty(snapshot.Components);

        // Settings (may be empty if organization entity returns 400)
        Assert.NotNull(snapshot.Settings);
    }

    [RequiresDataverseFact]
    public async Task CaptureSnapshot_SolutionData_HasExpectedFields()
    {
        var connector = _fixture.Services.GetRequiredService<IEnvironmentConnector>();

        var envInfo = new EnvironmentInfo
        {
            EnvironmentId = "integration-test",
            DisplayName = "IntegrationTest",
            EnvironmentUrl = _fixture.Config.EnvironmentUrl
        };

        var snapshot = await connector.CaptureSnapshotAsync(envInfo);

        var anySolution = snapshot.Solutions.First();
        Assert.False(string.IsNullOrEmpty(anySolution.UniqueName));
        Assert.False(string.IsNullOrEmpty(anySolution.DisplayName));
        Assert.NotEqual(Guid.Empty, anySolution.SolutionId);
    }
}
