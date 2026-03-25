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

/// <summary>
/// Per-environment plugin registration map showing SDK steps grouped by plugin.
/// </summary>
public sealed record PluginRegistrationMap
{
    public required string EnvironmentDisplayName { get; init; }
    public IReadOnlyList<PluginStepGroup> PluginGroups { get; init; } = [];
    public int TotalSteps { get; init; }
    public int SyncSteps { get; init; }
    public int AsyncSteps { get; init; }
    public int DisabledSteps { get; init; }
}

public sealed record PluginStepGroup(
    string PluginName,
    IReadOnlyList<StepSummaryItem> Steps);

public sealed record StepSummaryItem(
    string Name,
    string? Message,
    string? Entity,
    string Stage,
    string Mode,
    bool IsDisabled);

/// <summary>
/// Per-environment security posture overview.
/// </summary>
public sealed record SecurityPosture
{
    public required string EnvironmentDisplayName { get; init; }
    public IReadOnlyList<SecurityRoleSummaryItem> CustomRoles { get; init; } = [];
    public IReadOnlyList<SecurityRoleSummaryItem> SystemRoles { get; init; } = [];
    public IReadOnlyList<FieldSecurityProfileSummaryItem> FieldSecurityProfiles { get; init; } = [];
    public int TotalRoles { get; init; }
    public int UnmanagedRoleCount { get; init; }
}

public sealed record SecurityRoleSummaryItem(
    string Name,
    bool IsManaged,
    bool IsCustomizable,
    string? BusinessUnit,
    DateTimeOffset? ModifiedOn);

public sealed record FieldSecurityProfileSummaryItem(
    string Name,
    bool IsManaged,
    string? Description);

/// <summary>
/// Per-environment variable inventory with value status.
/// </summary>
public sealed record EnvironmentVariableInventory
{
    public required string EnvironmentDisplayName { get; init; }
    public IReadOnlyList<EnvironmentVariableSummaryItem> Variables { get; init; } = [];
    public int MissingValueCount { get; init; }
}

public sealed record EnvironmentVariableSummaryItem(
    string SchemaName,
    string? DisplayName,
    string Type,
    bool HasValue,
    bool IsRequired,
    string? SolutionName);

/// <summary>
/// Per-environment entity governance coverage (audit + change tracking).
/// </summary>
public sealed record EntityGovernance
{
    public required string EnvironmentDisplayName { get; init; }
    public IReadOnlyList<EntityGovernanceItem> Entities { get; init; } = [];
    public int TotalCustomEntities { get; init; }
    public int AuditEnabledCount { get; init; }
    public int ChangeTrackingEnabledCount { get; init; }
}

public sealed record EntityGovernanceItem(
    string LogicalName,
    string? DisplayName,
    bool IsAuditEnabled,
    bool ChangeTrackingEnabled,
    string OwnershipType,
    int? AttributeCount);
