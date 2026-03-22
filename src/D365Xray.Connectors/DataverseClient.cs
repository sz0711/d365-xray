using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors;

/// <summary>
/// Concrete Dataverse Web API client. Read-only: only GET requests are supported.
/// Handles OData paging via @odata.nextLink, retry on 429 (throttling), and structured logging.
/// </summary>
internal sealed class DataverseClient : IDataverseClient
{
    private const string ApiVersion = "v9.2";
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(5);
    private const int MaxRetries = 3;

    private readonly HttpClient _httpClient;
    private readonly ILogger<DataverseClient> _logger;

    public DataverseClient(HttpClient httpClient, Uri environmentUrl, ILogger<DataverseClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        EnvironmentUrl = environmentUrl;
        _httpClient.BaseAddress = new Uri(environmentUrl, $"/api/data/{ApiVersion}/");
    }

    public Uri EnvironmentUrl { get; }

    public async Task<JsonDocument> GetAsync(
        string entitySetOrPath,
        string? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(entitySetOrPath, queryOptions);
        return await ExecuteWithRetryAsync(url, cancellationToken);
    }

    public async IAsyncEnumerable<JsonDocument> GetPagedAsync(
        string entitySetOrPath,
        string? queryOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? url = BuildUrl(entitySetOrPath, queryOptions);
        var page = 0;

        while (url is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            page++;

            _logger.LogDebug("Fetching page {Page} from {Url}", page, SanitizeUrlForLog(url));

            var doc = await ExecuteWithRetryAsync(url, cancellationToken);

            // Extract next link BEFORE yielding – caller may dispose the document
            url = doc.RootElement.TryGetProperty("@odata.nextLink", out var nextLink)
                ? nextLink.GetString()
                : null;

            yield return doc;
        }

        _logger.LogDebug("Paging complete after {PageCount} page(s)", page);
    }

    private static string BuildUrl(string entitySetOrPath, string? queryOptions)
    {
        if (string.IsNullOrEmpty(queryOptions))
        {
            return entitySetOrPath;
        }

        var separator = entitySetOrPath.Contains('?') ? '&' : '?';
        return $"{entitySetOrPath}{separator}{queryOptions}";
    }

    private async Task<JsonDocument> ExecuteWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt == MaxRetries)
                {
                    _logger.LogWarning("Max retries ({MaxRetries}) exceeded for throttled request to {Url}", MaxRetries, SanitizeUrlForLog(url));
                    response.EnsureSuccessStatusCode();
                }

                var retryAfter = response.Headers.RetryAfter?.Delta ?? DefaultRetryDelay;
                _logger.LogWarning("Throttled (429) on attempt {Attempt}/{MaxRetries}. Retrying after {RetryAfter}", attempt + 1, MaxRetries, retryAfter);
                await Task.Delay(retryAfter, cancellationToken);
                continue;
            }

            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }

        // Unreachable, but compiler needs it
        throw new InvalidOperationException("Retry loop exited unexpectedly.");
    }

    /// <summary>
    /// Strips query parameters from URL for safe logging (avoid leaking tokens/filters with PII).
    /// </summary>
    private static string SanitizeUrlForLog(string url)
    {
        var idx = url.IndexOf('?');
        return idx >= 0 ? $"{url[..idx]}?..." : url;
    }
}
