namespace D365Xray.Core.Model;

/// <summary>
/// A custom connector registered in a Dataverse environment.
/// Maps to the connector entity.
/// </summary>
public sealed record CustomConnector
{
    public required Guid ConnectorId { get; init; }
    public required string Name { get; init; }
    public string? DisplayName { get; init; }
    public string? ConnectorType { get; init; }
    public string? Description { get; init; }
    public string? OwnerName { get; init; }
    public string? SolutionUniqueName { get; init; }
}
