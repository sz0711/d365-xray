using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects entity metadata drift between environments.
/// Flags missing custom entities and audit/change tracking configuration differences.
/// </summary>
internal static class EntityMetadataDriftAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineEntities = BuildLookup(baseline.EntityMetadata);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetEntities = BuildLookup(target.EntityMetadata);

            foreach (var (key, baseEntity) in baselineEntities.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetEntities.TryGetValue(key, out var targetEntity))
                {
                    // Only flag missing custom entities (system entities should always exist)
                    if (baseEntity.IsCustomEntity)
                    {
                        yield return new Finding
                        {
                            FindingId = $"META-MISSING-{key}-{target.Environment.DisplayName}",
                            Category = FindingCategory.EntityMetadataDrift,
                            Severity = Severity.High,
                            Title = $"Custom entity '{key}' missing from {target.Environment.DisplayName}",
                            Description = $"Custom entity '{baseEntity.DisplayName ?? key}' exists in " +
                                $"{baseline.Environment.DisplayName} but is missing from " +
                                $"{target.Environment.DisplayName}.",
                            AffectedEnvironments = [target.Environment.DisplayName],
                            Details = new Dictionary<string, string>
                            {
                                ["EntityLogicalName"] = key,
                                ["IsCustomEntity"] = "true",
                                ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                            }
                        };
                    }
                    continue;
                }

                // Audit configuration drift
                if (baseEntity.IsAuditEnabled != targetEntity.IsAuditEnabled)
                {
                    yield return new Finding
                    {
                        FindingId = $"META-AUDIT-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.EntityMetadataDrift,
                        Severity = baseEntity.IsAuditEnabled && !targetEntity.IsAuditEnabled
                            ? Severity.High : Severity.Medium,
                        Title = $"Entity '{key}' audit configuration differs",
                        Description = $"Entity '{key}' has auditing " +
                            $"{(baseEntity.IsAuditEnabled ? "enabled" : "disabled")} in " +
                            $"{baseline.Environment.DisplayName} but " +
                            $"{(targetEntity.IsAuditEnabled ? "enabled" : "disabled")} in " +
                            $"{target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["EntityLogicalName"] = key,
                            ["BaselineAudit"] = baseEntity.IsAuditEnabled.ToString(),
                            ["TargetAudit"] = targetEntity.IsAuditEnabled.ToString(),
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }

                // Change tracking drift
                if (baseEntity.ChangeTrackingEnabled != targetEntity.ChangeTrackingEnabled)
                {
                    yield return new Finding
                    {
                        FindingId = $"META-CHANGETRACK-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.EntityMetadataDrift,
                        Severity = Severity.Medium,
                        Title = $"Entity '{key}' change tracking configuration differs",
                        Description = $"Entity '{key}' has change tracking " +
                            $"{(baseEntity.ChangeTrackingEnabled ? "enabled" : "disabled")} in " +
                            $"{baseline.Environment.DisplayName} but " +
                            $"{(targetEntity.ChangeTrackingEnabled ? "enabled" : "disabled")} in " +
                            $"{target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["EntityLogicalName"] = key,
                            ["BaselineChangeTracking"] = baseEntity.ChangeTrackingEnabled.ToString(),
                            ["TargetChangeTracking"] = targetEntity.ChangeTrackingEnabled.ToString(),
                            ["EnvironmentUrl"] = baseline.Environment.EnvironmentUrl.ToString()
                        }
                    };
                }
            }
        }
    }

    private static Dictionary<string, EntityMetadataInfo> BuildLookup(IReadOnlyList<EntityMetadataInfo> entities)
    {
        var lookup = new Dictionary<string, EntityMetadataInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities)
        {
            lookup.TryAdd(entity.LogicalName, entity);
        }
        return lookup;
    }
}
