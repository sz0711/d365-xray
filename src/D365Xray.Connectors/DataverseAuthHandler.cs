using Azure.Core;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors;

/// <summary>
/// HTTP delegating handler that injects a Bearer token for Dataverse Web API calls.
/// Automatically refreshes tokens before expiry.
/// Never logs the token value.
/// </summary>
internal sealed class DataverseAuthHandler : DelegatingHandler
{
    private readonly TokenCredential _credential;
    private readonly string _scope;
    private readonly ILogger<DataverseAuthHandler> _logger;

    private AccessToken _cachedToken;

    public DataverseAuthHandler(
        TokenCredential credential,
        string scope,
        ILogger<DataverseAuthHandler> logger)
    {
        _credential = credential;
        _scope = scope;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        // Refresh if token expires within the next 2 minutes
        if (_cachedToken.ExpiresOn <= DateTimeOffset.UtcNow.AddMinutes(2))
        {
            _logger.LogDebug("Acquiring new Dataverse access token for scope {Scope}", _scope);

            var context = new TokenRequestContext([_scope]);
            _cachedToken = await _credential.GetTokenAsync(context, cancellationToken);

            _logger.LogDebug("Token acquired, expires at {ExpiresOn}", _cachedToken.ExpiresOn);
        }

        return _cachedToken.Token;
    }
}
