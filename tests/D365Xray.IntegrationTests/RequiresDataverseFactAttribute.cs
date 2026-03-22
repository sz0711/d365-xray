using Xunit;

namespace D365Xray.IntegrationTests;

/// <summary>
/// Skips tests when Dataverse credentials are not configured via user-secrets.
/// </summary>
internal sealed class RequiresDataverseFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> HasConfig = new(() =>
    {
        using var fixture = new DataverseFixture();
        return fixture.IsConfigured;
    });

    public RequiresDataverseFactAttribute()
    {
        if (!HasConfig.Value)
        {
            Skip = "Dataverse credentials not configured. Run: dotnet user-secrets set \"Dataverse:EnvironmentUrl\" \"<url>\" --project tests/D365Xray.IntegrationTests";
        }
    }
}
