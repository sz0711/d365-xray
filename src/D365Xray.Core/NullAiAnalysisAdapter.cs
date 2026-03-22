using D365Xray.Core.Model;

namespace D365Xray.Core;

/// <summary>
/// Default no-op implementation of <see cref="IAiAnalysisAdapter"/>.
/// Registered when no real AI adapter is configured,
/// so consumers never need null checks.
/// </summary>
public sealed class NullAiAnalysisAdapter : IAiAnalysisAdapter
{
    public Task<AiEnrichmentResult> EnrichAsync(
        RiskReport report,
        AiAnalysisOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = new AiEnrichmentResult
        {
            Summary = null,
            Provenance = new AiProvenance
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                ModelIdentifier = "none",
                AdapterName = nameof(NullAiAnalysisAdapter)
            }
        };

        return Task.FromResult(result);
    }
}
