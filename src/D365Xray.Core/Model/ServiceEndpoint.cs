namespace D365Xray.Core.Model;

/// <summary>
/// A service endpoint (webhook) registered in a Dataverse environment.
/// Maps to the serviceendpoint entity.
/// </summary>
public sealed record ServiceEndpoint
{
    public required Guid ServiceEndpointId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required EndpointContract Contract { get; init; }
    public string? Url { get; init; }
    public required AuthType AuthType { get; init; }
    public string? SolutionUniqueName { get; init; }
}

/// <summary>
/// The contract type of a service endpoint.
/// </summary>
public enum EndpointContract
{
    OneWay = 1,
    TwoWay = 2,
    Queue = 3,
    Topic = 4,
    QueuePersistent = 5,
    EventHub = 6,
    Webhook = 8,
    EventGrid = 9,
    Unknown = 0
}

/// <summary>
/// Authentication type for a service endpoint.
/// </summary>
public enum AuthType
{
    AcsSecret = 1,
    SasKey = 2,
    SasToken = 3,
    WebhookKey = 4,
    HttpHeader = 5,
    HttpQueryString = 6,
    ConnectionString = 7,
    AccessKey = 8,
    Unknown = 0
}
