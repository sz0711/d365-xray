using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects components with an Active (unmanaged) layer on top of managed layers.
/// This indicates unmanaged customizations overriding a managed solution —
/// a common source of upgrade breakage and deployment conflicts.
/// </summary>
internal static class LayerOverrideAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Layers.Count == 0)
            {
                continue;
            }

            // Group layers by component and check for Active layer overrides
            var layersByComponent = snapshot.Layers
                .GroupBy(l => (l.ComponentId, l.ComponentType))
                .OrderBy(g => g.Key.ComponentId);

            foreach (var group in layersByComponent)
            {
                var layers = group.OrderByDescending(l => l.Order).ToList();

                // Check if there's an Active layer AND at least one managed layer beneath it
                var hasActiveLayer = layers.Any(l => l.IsActiveLayer);
                var hasManagedLayer = layers.Any(l => l.IsManaged);

                if (hasActiveLayer && hasManagedLayer)
                {
                    var activeLayer = layers.First(l => l.IsActiveLayer);
                    var managedLayers = layers.Where(l => l.IsManaged).ToList();
                    var topManaged = managedLayers.First();

                    yield return new Finding
                    {
                        FindingId = $"LAYER-OVERRIDE-{group.Key.ComponentId:N}-{snapshot.Environment.DisplayName}",
                        Category = FindingCategory.LayerOverride,
                        Severity = Severity.High,
                        Title = $"Active layer overrides managed {group.Key.ComponentType} in {snapshot.Environment.DisplayName}",
                        Description = $"Component {group.Key.ComponentId} ({group.Key.ComponentType}) has an Active " +
                            $"(unmanaged) layer on top of {managedLayers.Count} managed layer(s). " +
                            $"Top managed solution: '{topManaged.SolutionUniqueName}' " +
                            $"(publisher: {topManaged.PublisherName ?? "unknown"}). " +
                            $"This may block solution upgrades.",
                        AffectedEnvironments = [snapshot.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["ComponentId"] = group.Key.ComponentId.ToString(),
                            ["ComponentType"] = group.Key.ComponentType.ToString(),
                            ["ManagedLayerCount"] = managedLayers.Count.ToString(),
                            ["TopManagedSolution"] = topManaged.SolutionUniqueName,
                            ["TotalLayerCount"] = layers.Count.ToString()
                        }
                    };
                }
            }
        }
    }
}
