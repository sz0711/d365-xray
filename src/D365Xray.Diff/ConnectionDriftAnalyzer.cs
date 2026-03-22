using D365Xray.Core.Model;

namespace D365Xray.Diff;

/// <summary>
/// Detects drift in connection references, service endpoints, and custom connectors
/// across environments. Also flags missing connections and governance issues.
/// </summary>
internal static class ConnectionDriftAnalyzer
{
    public static IEnumerable<Finding> Analyze(IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        foreach (var f in AnalyzeConnectionReferences(snapshots))
        {
            yield return f;
        }

        foreach (var f in AnalyzeServiceEndpoints(snapshots))
        {
            yield return f;
        }

        foreach (var f in AnalyzeCustomConnectors(snapshots))
        {
            yield return f;
        }
    }

    private static IEnumerable<Finding> AnalyzeConnectionReferences(
        IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineRefs = baseline.ConnectionReferences
            .ToDictionary(c => c.ConnectionReferenceLogicalName, StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetRefs = target.ConnectionReferences
                .ToDictionary(c => c.ConnectionReferenceLogicalName, StringComparer.OrdinalIgnoreCase);

            foreach (var (key, baseRef) in baselineRefs.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetRefs.TryGetValue(key, out var targetRef))
                {
                    yield return new Finding
                    {
                        FindingId = $"CONN-MISSING-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.ConnectionConfiguration,
                        Severity = Severity.High,
                        Title = $"Connection reference '{key}' missing from {target.Environment.DisplayName}",
                        Description = $"Connection reference '{baseRef.DisplayName ?? key}' " +
                            $"(connector: {baseRef.ConnectorId ?? "unknown"}) is present in " +
                            $"{baseline.Environment.DisplayName} but missing from {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["ConnectionReferenceLogicalName"] = key,
                            ["ConnectorId"] = baseRef.ConnectorId ?? "(null)",
                            ["BaselineEnvironment"] = baseline.Environment.DisplayName
                        }
                    };
                    continue;
                }

                // Connector ID mismatch
                if (!string.Equals(baseRef.ConnectorId, targetRef.ConnectorId, StringComparison.OrdinalIgnoreCase))
                {
                    yield return new Finding
                    {
                        FindingId = $"CONN-CONNECTORDRIFT-{key}-{target.Environment.DisplayName}",
                        Category = FindingCategory.ConnectionConfiguration,
                        Severity = Severity.Medium,
                        Title = $"Connection reference '{key}' points to different connectors",
                        Description = $"Connection reference '{key}' uses connector " +
                            $"'{baseRef.ConnectorId}' in {baseline.Environment.DisplayName} but " +
                            $"'{targetRef.ConnectorId}' in {target.Environment.DisplayName}.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["ConnectionReferenceLogicalName"] = key,
                            ["BaselineConnectorId"] = baseRef.ConnectorId ?? "(null)",
                            ["TargetConnectorId"] = targetRef.ConnectorId ?? "(null)"
                        }
                    };
                }
            }
        }
    }

    private static IEnumerable<Finding> AnalyzeServiceEndpoints(
        IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineEndpoints = baseline.ServiceEndpoints
            .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetEndpoints = target.ServiceEndpoints
                .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var (name, baseEp) in baselineEndpoints.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetEndpoints.TryGetValue(name, out var targetEp))
                {
                    yield return new Finding
                    {
                        FindingId = $"EP-MISSING-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.IntegrationEndpointDrift,
                        Severity = Severity.High,
                        Title = $"Service endpoint '{name}' missing from {target.Environment.DisplayName}",
                        Description = $"Service endpoint '{name}' ({baseEp.Contract}) is registered in " +
                            $"{baseline.Environment.DisplayName} but missing from {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["EndpointName"] = name,
                            ["Contract"] = baseEp.Contract.ToString(),
                            ["BaselineEnvironment"] = baseline.Environment.DisplayName
                        }
                    };
                    continue;
                }

                // Contract type mismatch
                if (baseEp.Contract != targetEp.Contract)
                {
                    yield return new Finding
                    {
                        FindingId = $"EP-CONTRACTDRIFT-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.IntegrationEndpointDrift,
                        Severity = Severity.Medium,
                        Title = $"Service endpoint '{name}' has different contract type",
                        Description = $"Endpoint '{name}' uses {baseEp.Contract} in " +
                            $"{baseline.Environment.DisplayName} but {targetEp.Contract} in {target.Environment.DisplayName}.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["EndpointName"] = name,
                            ["BaselineContract"] = baseEp.Contract.ToString(),
                            ["TargetContract"] = targetEp.Contract.ToString()
                        }
                    };
                }

                // Auth type mismatch
                if (baseEp.AuthType != targetEp.AuthType)
                {
                    yield return new Finding
                    {
                        FindingId = $"EP-AUTHDRIFT-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.IntegrationEndpointDrift,
                        Severity = Severity.High,
                        Title = $"Service endpoint '{name}' has different auth type",
                        Description = $"Endpoint '{name}' uses auth type {baseEp.AuthType} in " +
                            $"{baseline.Environment.DisplayName} but {targetEp.AuthType} in {target.Environment.DisplayName}.",
                        AffectedEnvironments = [baseline.Environment.DisplayName, target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["EndpointName"] = name,
                            ["BaselineAuthType"] = baseEp.AuthType.ToString(),
                            ["TargetAuthType"] = targetEp.AuthType.ToString()
                        }
                    };
                }
            }
        }
    }

    private static IEnumerable<Finding> AnalyzeCustomConnectors(
        IReadOnlyList<EnvironmentSnapshot> snapshots)
    {
        var baseline = snapshots[0];
        var baselineConnectors = baseline.CustomConnectors
            .ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var target = snapshots[i];
            var targetConnectors = target.CustomConnectors
                .ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var (name, _) in baselineConnectors.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!targetConnectors.ContainsKey(name))
                {
                    yield return new Finding
                    {
                        FindingId = $"CCON-MISSING-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.ConnectorGovernance,
                        Severity = Severity.Medium,
                        Title = $"Custom connector '{name}' missing from {target.Environment.DisplayName}",
                        Description = $"Custom connector '{name}' is registered in " +
                            $"{baseline.Environment.DisplayName} but missing from {target.Environment.DisplayName}.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["ConnectorName"] = name,
                            ["BaselineEnvironment"] = baseline.Environment.DisplayName
                        }
                    };
                }
            }

            // Connectors in target but not in baseline (governance: extra connectors)
            foreach (var (name, _) in targetConnectors.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!baselineConnectors.ContainsKey(name))
                {
                    yield return new Finding
                    {
                        FindingId = $"CCON-EXTRA-{name}-{target.Environment.DisplayName}",
                        Category = FindingCategory.ConnectorGovernance,
                        Severity = Severity.Low,
                        Title = $"Extra custom connector '{name}' in {target.Environment.DisplayName}",
                        Description = $"Custom connector '{name}' exists in {target.Environment.DisplayName} " +
                            $"but not in baseline {baseline.Environment.DisplayName}. " +
                            $"This may indicate ungoverned connector proliferation.",
                        AffectedEnvironments = [target.Environment.DisplayName],
                        Details = new Dictionary<string, string>
                        {
                            ["ConnectorName"] = name,
                            ["TargetEnvironment"] = target.Environment.DisplayName
                        }
                    };
                }
            }
        }
    }
}
