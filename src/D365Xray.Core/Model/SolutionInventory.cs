namespace D365Xray.Core.Model;

/// <summary>
/// Per-solution breakdown of components with Microsoft/custom classification.
/// Used in reports to show what each solution contributes.
/// </summary>
public sealed record SolutionInventory
{
    public required string EnvironmentDisplayName { get; init; }
    public required IReadOnlyList<SolutionBreakdown> Solutions { get; init; }
}

/// <summary>
/// Component counts for a single solution.
/// </summary>
public sealed record SolutionBreakdown
{
    public required string UniqueName { get; init; }
    public required string DisplayName { get; init; }
    public required string Version { get; init; }
    public required string PublisherName { get; init; }
    public required bool IsManaged { get; init; }
    public required bool IsMicrosoft { get; init; }
    public int TotalComponents { get; init; }
    public int Entities { get; init; }
    public int Forms { get; init; }
    public int Views { get; init; }
    public int Workflows { get; init; }
    public int PluginAssemblies { get; init; }
    public int WebResources { get; init; }
    public int Roles { get; init; }
    public int Charts { get; init; }
    public int SdkSteps { get; init; }
    public int OptionSets { get; init; }
    public int OtherComponents { get; init; }
}

/// <summary>
/// Per-category artifact list for drill-down reports — only custom (non-Microsoft) items.
/// </summary>
public sealed record CustomArtifactSummary
{
    public required string EnvironmentDisplayName { get; init; }
    public IReadOnlyList<WorkflowSummaryItem> Workflows { get; init; } = [];
    public IReadOnlyList<PluginSummaryItem> Plugins { get; init; } = [];
    public IReadOnlyList<FormSummaryItem> Forms { get; init; } = [];
    public IReadOnlyList<EntitySummaryItem> Entities { get; init; } = [];
    public IReadOnlyList<AppModuleSummaryItem> AppModules { get; init; } = [];
    public IReadOnlyList<ConnectionReferenceSummaryItem> ConnectionReferences { get; init; } = [];
    public IReadOnlyList<WebResourceSummaryItem> WebResources { get; init; } = [];
}

public sealed record WorkflowSummaryItem(string Name, string Category, bool IsActivated, bool IsManaged);
public sealed record PluginSummaryItem(string Name, string? Version, string IsolationMode);
public sealed record FormSummaryItem(string Name, string Entity, string FormType, bool IsManaged);
public sealed record EntitySummaryItem(string LogicalName, string? DisplayName, bool IsManaged, bool IsCustom);
public sealed record AppModuleSummaryItem(string Name, string UniqueName, bool IsPublished, bool IsManaged);
public sealed record ConnectionReferenceSummaryItem(string LogicalName, string? DisplayName, string? ConnectorId, bool HasConnection);
public sealed record WebResourceSummaryItem(string Name, string Type, bool IsManaged);

/// <summary>
/// Per-environment snapshot of organization settings for governance auditing.
/// </summary>
public sealed record EnvironmentSettingsSnapshot
{
    public required string EnvironmentDisplayName { get; init; }
    public string? DataverseVersion { get; init; }
    public string? OrganizationId { get; init; }
    public TimeSpan? ScanDuration { get; init; }
    public IReadOnlyList<EnvironmentSetting> Settings { get; init; } = [];
}
