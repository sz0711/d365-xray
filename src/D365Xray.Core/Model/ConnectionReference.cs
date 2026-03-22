namespace D365Xray.Core.Model;

/// <summary>
/// A connection reference in a Dataverse environment.
/// Maps to the connectionreference entity.
/// </summary>
public sealed record ConnectionReference
{
    public required Guid ConnectionReferenceId { get; init; }
    public required string ConnectionReferenceLogicalName { get; init; }
    public string? DisplayName { get; init; }
    public string? ConnectorId { get; init; }
    public string? ConnectionId { get; init; }
    public string? SolutionUniqueName { get; init; }
    public bool IsCustomConnector { get; init; }
    public string? OwnerName { get; init; }
    public int StatusCode { get; init; }
}
