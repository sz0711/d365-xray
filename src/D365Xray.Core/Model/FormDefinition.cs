namespace D365Xray.Core.Model;

/// <summary>
/// A system or custom form (main, quick create, quick view, card, etc.).
/// OData entity: systemforms.
/// </summary>
public sealed record FormDefinition
{
    public Guid FormId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string EntityLogicalName { get; init; }
    public FormType FormType { get; init; }
    public bool IsManaged { get; init; }
    public bool IsDefault { get; init; }
    public int? FormActivationState { get; init; }
    public string? UniqueName { get; init; }
    public DateTimeOffset? ModifiedOn { get; init; }
}

public enum FormType
{
    Unknown = -1,
    Dashboard = 0,
    AppointmentBook = 1,
    Main = 2,
    MiniCampaignBO = 3,
    Preview = 4,
    MobileExpress = 5,
    QuickView = 6,
    QuickCreate = 7,
    Dialog = 8,
    TaskFlow = 9,
    InteractionCentricDashboard = 10,
    Card = 11,
    MainInteractiveExperience = 12,
    ContextualDashboard = 13,
    Other = 100
}
