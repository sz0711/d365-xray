namespace D365Xray.Core.Model;

/// <summary>
/// A single component within a Dynamics 365 solution.
/// Maps to the SolutionComponent entity in Dataverse.
/// </summary>
public sealed record SolutionComponent
{
    public required Guid ComponentId { get; init; }
    public required ComponentType ComponentType { get; init; }
    public required string SolutionUniqueName { get; init; }
    public string? DisplayName { get; init; }
    public string? SchemaName { get; init; }
    public required RootComponentBehavior Behavior { get; init; }
}

/// <summary>
/// Dataverse solution component types.
/// Values align with the ComponentType global option set in Dataverse.
/// </summary>
public enum ComponentType
{
    Entity = 1,
    Attribute = 2,
    Relationship = 3,
    AttributePicklistValue = 4,
    AttributeLookupValue = 5,
    ViewAttribute = 6,
    LocalizedLabel = 7,
    RelationshipExtraCondition = 8,
    OptionSet = 9,
    EntityRelationship = 10,
    EntityRelationshipRole = 11,
    EntityRelationshipRelationships = 12,
    ManagedProperty = 13,
    EntityKey = 14,
    Privilege = 16,
    PrivilegeObjectTypeCode = 17,
    Role = 20,
    RolePrivilege = 21,
    DisplayString = 22,
    DisplayStringMap = 23,
    Form = 24,
    Organization = 25,
    SavedQuery = 26,
    Workflow = 29,
    Report = 31,
    ReportEntity = 32,
    ReportCategory = 33,
    ReportVisibility = 34,
    Attachment = 35,
    EmailTemplate = 36,
    ContractTemplate = 37,
    KBArticleTemplate = 38,
    MailMergeTemplate = 39,
    DuplicateRule = 44,
    DuplicateRuleCondition = 45,
    EntityMap = 46,
    AttributeMap = 47,
    RibbonCommand = 48,
    RibbonContextGroup = 49,
    RibbonCustomization = 50,
    RibbonRule = 52,
    RibbonTabToCommandMap = 53,
    RibbonDiff = 55,
    SavedQueryVisualization = 59,
    SystemForm = 60,
    WebResource = 61,
    SiteMap = 62,
    ConnectionRole = 63,
    FieldSecurityProfile = 70,
    FieldPermission = 71,
    PluginType = 90,
    PluginAssembly = 91,
    SdkMessageProcessingStep = 92,
    SdkMessageProcessingStepImage = 93,
    ServiceEndpoint = 95,
    RoutingRule = 150,
    RoutingRuleItem = 151,
    SLA = 152,
    SLAItem = 153,
    ConvertRule = 154,
    ConvertRuleItem = 155,
    HierarchyRule = 65,
    MobileOfflineProfile = 161,
    MobileOfflineProfileItem = 162,
    SimilarityRule = 165,
    CustomControl = 66,
    CustomControlDefaultConfig = 68,
    ModelDrivenApp = 80,
    DataSourceMapping = 166,
    EnvironmentVariableDefinition = 380,
    EnvironmentVariableValue = 381,
    AIProjectType = 400,
    AIProject = 401,
    AIConfiguration = 402,
    CanvasApp = 300,
    Connector = 371,
    ConnectorMapping = 372,
    FlowMachineLearningModel = 406,
    ProcessSession = 4710,
    Catalog = 10090,
    CatalogAssignment = 10091,
    Unknown = -1
}

/// <summary>
/// Behavior of a root component in a solution.
/// Maps to the rootcomponentbehavior attribute in Dataverse.
/// </summary>
public enum RootComponentBehavior
{
    IncludeSubcomponents = 0,
    DoNotIncludeSubcomponents = 1,
    IncludeAsShellOnly = 2
}
