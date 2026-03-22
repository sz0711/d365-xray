using System.Text.Json;

namespace D365Xray.Connectors;

/// <summary>
/// Thin abstraction over the Dataverse Web API for read-only queries.
/// Implementations must never modify data in the target environment.
/// </summary>
public interface IDataverseClient
{
    /// <summary>
    /// Executes a GET request against the Dataverse Web API and returns deserialized results.
    /// </summary>
    /// <param name="entitySetOrPath">
    /// Relative OData path (e.g. "solutions", "RetrieveComponentChangeSummary(SolutionId=@id)?@id=...").
    /// </param>
    /// <param name="queryOptions">OData query options ($select, $filter, $expand, $top, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw JSON document from the API response.</returns>
    Task<JsonDocument> GetAsync(
        string entitySetOrPath,
        string? queryOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a GET request and automatically follows @odata.nextLink for paged results,
    /// yielding all pages as individual JSON documents.
    /// </summary>
    IAsyncEnumerable<JsonDocument> GetPagedAsync(
        string entitySetOrPath,
        string? queryOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The base URL of the connected environment.
    /// </summary>
    Uri EnvironmentUrl { get; }
}
