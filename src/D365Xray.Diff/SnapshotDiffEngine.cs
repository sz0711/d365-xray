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
    public ComparisonResult Compare(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        if (snapshots.Count == 0)
        {
            throw new ArgumentException("At least one snapshot is required.", nameof(snapshots));
        }

        var findings = new List<Finding>();

        if (snapshots.Count >= 2)
        {
            // Cross-environment comparison mode
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
            Findings = findings
        };
    }
}
