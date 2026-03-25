using D365Xray.Core;
using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Deterministic diff engine. Compares environment snapshots and produces
/// findings sorted by FindingId for reproducible output. All comparisons
/// use the first snapshot as the baseline (reference environment).
/// </summary>
internal sealed class SnapshotDiffEngine : IDiffEngine
{
    public ComparisonResult Compare(IReadOnlyList<EnvironmentSnapshot> snapshots,
        ComparisonMode mode = ComparisonMode.Baseline)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        if (snapshots.Count == 0)
        {
            throw new ArgumentException("At least one snapshot is required.", nameof(snapshots));
        }

        var findings = new List<Finding>();

        if (snapshots.Count >= 2)
        {
            if (mode == ComparisonMode.AllToAll)
            {
                // Compare every pair of snapshots
                for (var i = 0; i < snapshots.Count; i++)
                {
                    for (var j = i + 1; j < snapshots.Count; j++)
                    {
                        var pair = new List<EnvironmentSnapshot> { snapshots[i], snapshots[j] };
                        var pairLabel = $"{snapshots[i].Environment.DisplayName}↔{snapshots[j].Environment.DisplayName}";
                        findings.AddRange(RunCrossEnvAnalyzers(pair, pairLabel));
                    }
                }
            }
            else
            {
                // Baseline mode: first snapshot vs rest
                findings.AddRange(RunCrossEnvAnalyzers(snapshots, null));
            }
        }
        else
        {
            // Single-environment self-analysis mode
            findings.AddRange(SingleEnvironmentAnalyzer.Analyze(snapshots[0]));
        }

        // Deterministic ordering: sort by FindingId so output is stable across runs
        findings.Sort((a, b) => string.Compare(a.FindingId, b.FindingId, StringComparison.Ordinal));

        return new ComparisonResult
        {
            Metadata = new SnapshotMetadata
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                ToolVersion = typeof(SnapshotDiffEngine).Assembly.GetName().Version?.ToString() ?? "0.0.0"
            },
            ComparedEnvironments = snapshots.Select(s => s.Environment).ToList(),
            ComparisonMode = mode,
            Findings = findings
        };
    }

    private static IEnumerable<Finding> RunCrossEnvAnalyzers(
        IReadOnlyList<EnvironmentSnapshot> snapshots, string? pairLabel)
    {
        var findings = new List<Finding>();
        findings.AddRange(SolutionDriftAnalyzer.Analyze(snapshots));
        findings.AddRange(MissingComponentAnalyzer.Analyze(snapshots));
        findings.AddRange(LayerOverrideAnalyzer.Analyze(snapshots));
        findings.AddRange(DependencyConflictAnalyzer.Analyze(snapshots));
        findings.AddRange(SettingsDriftAnalyzer.Analyze(snapshots));
        findings.AddRange(ConnectionDriftAnalyzer.Analyze(snapshots));
        findings.AddRange(PluginAnalyzer.Analyze(snapshots));
        findings.AddRange(WebResourceDriftAnalyzer.Analyze(snapshots));
        findings.AddRange(WorkflowDriftAnalyzer.Analyze(snapshots));
        findings.AddRange(EnvironmentVariableDriftAnalyzer.Analyze(snapshots));
        findings.AddRange(BusinessRuleDriftAnalyzer.Analyze(snapshots));
        findings.AddRange(FormDriftAnalyzer.Analyze(snapshots));
        findings.AddRange(ViewDriftAnalyzer.Analyze(snapshots));
        findings.AddRange(AppModuleDriftAnalyzer.Analyze(snapshots));
        findings.AddRange(SecurityRoleDriftAnalyzer.Analyze(snapshots));
        findings.AddRange(EntityMetadataDriftAnalyzer.Analyze(snapshots));

        // Tag findings with pair label for AllToAll differentiation
        if (pairLabel is not null)
        {
            findings = findings.Select(f => f with
            {
                FindingId = $"{pairLabel}|{f.FindingId}",
                Details = new Dictionary<string, string>(f.Details) { ["PairLabel"] = pairLabel }
            }).ToList();
        }

        return findings;
    }
}
