using System.Runtime.CompilerServices;
using System.Text.Json;

namespace D365Xray.Connectors.Tests;

/// <summary>
/// In-memory fake for IDataverseClient.
/// Queue JSON responses per entity-set prefix; GetPagedAsync yields one page per queued response.
/// </summary>
internal sealed class FakeDataverseClient : IDataverseClient
{
    private readonly Dictionary<string, Queue<string>> _responses = new(StringComparer.OrdinalIgnoreCase);

    public Uri EnvironmentUrl { get; } = new("https://fake.crm4.dynamics.com");

    /// <summary>
    /// Enqueues a raw JSON response for requests whose path starts with <paramref name="entitySetPrefix"/>.
    /// </summary>
    public void Enqueue(string entitySetPrefix, string json)
    {
        if (!_responses.TryGetValue(entitySetPrefix, out var queue))
        {
            queue = new Queue<string>();
            _responses[entitySetPrefix] = queue;
        }
        queue.Enqueue(json);
    }

    public Task<JsonDocument> GetAsync(
        string entitySetOrPath,
        string? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        var json = Dequeue(entitySetOrPath);
        return Task.FromResult(JsonDocument.Parse(json));
    }

    public async IAsyncEnumerable<JsonDocument> GetPagedAsync(
        string entitySetOrPath,
        string? queryOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Yield all queued responses as separate pages, or a single page if only one is queued.
        while (HasResponse(entitySetOrPath))
        {
            var json = Dequeue(entitySetOrPath);
            yield return JsonDocument.Parse(json);
        }

        await Task.CompletedTask; // Keep async signature happy
    }

    private string Dequeue(string path)
    {
        foreach (var (prefix, queue) in _responses)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && queue.Count > 0)
            {
                return queue.Dequeue();
            }
        }
        throw new InvalidOperationException($"No response configured for '{path}'");
    }

    private bool HasResponse(string path)
    {
        foreach (var (prefix, queue) in _responses)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && queue.Count > 0)
            {
                return true;
            }
        }
        return false;
    }
}
