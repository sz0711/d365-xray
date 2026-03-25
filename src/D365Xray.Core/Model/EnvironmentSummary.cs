namespace D365Xray.Core.Model;

/// <summary>
/// High-level inventory counts for a single environment snapshot.
/// Used in reports to show the scope of each scanned environment.
/// </summary>
public sealed record EnvironmentSummary
{
    public required string EnvironmentDisplayName { get; init; }
    public required Uri EnvironmentUrl { get; init; }
    public EnvironmentType EnvironmentType { get; init; }
    public int Solutions { get; init; }
    public int Components { get; init; }
    public int Workflows { get; init; }
    public int PluginAssemblies { get; init; }
    public int SdkSteps { get; init; }
    public int WebResources { get; init; }
    public int ConnectionReferences { get; init; }
    public int EnvironmentVariables { get; init; }
    public int BusinessRules { get; init; }
    public int CustomConnectors { get; init; }
    public int ServiceEndpoints { get; init; }
    public int Forms { get; init; }
    public int Views { get; init; }
    public int Charts { get; init; }
    public int AppModules { get; init; }
    public int SecurityRoles { get; init; }
    public int FieldSecurityProfiles { get; init; }
    public int Entities { get; init; }
}
