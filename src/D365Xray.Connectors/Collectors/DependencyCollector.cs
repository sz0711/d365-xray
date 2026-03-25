using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects solution dependency data from a Dataverse environment.
/// OData entity: dependencies.
/// Solution names are resolved via a pre-built lookup from collected solutions.
/// </summary>
internal static class DependencyCollector
{
    private const string EntitySet = "dependencies";
    private const string QueryOptions =
        "$select=requiredcomponentobjectid,requiredcomponenttype," +
        "dependentcomponentobjectid,dependentcomponenttype," +
        "dependencytype";

    public static async Task<IReadOnlyList<SolutionDependency>> CollectAsync(
        IDataverseClient client,
        IReadOnlyDictionary<Guid, string> solutionIdToName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var dependencies = new List<SolutionDependency>();

        try
        {
            await foreach (var page in client.GetPagedAsync(EntitySet, QueryOptions, cancellationToken))
            {
                using (page)
                {
                    foreach (var item in JsonHelper.GetValueArray(page))
                    {
                        dependencies.Add(MapDependency(item, solutionIdToName));
                    }
                }
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest)
        {
            logger.LogWarning(
                "Dependencies entity is not available in this environment. " +
                "Dependency analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return dependencies;
    }

    private static SolutionDependency MapDependency(
        JsonElement item,
        IReadOnlyDictionary<Guid, string> solutionIdToName)
    {
        var requiredSolutionId = JsonHelper.GetGuid(item, "_requiredcomponentbasesolutionid_value");
        var dependentSolutionId = JsonHelper.GetGuid(item, "_dependentcomponentbasesolutionid_value");

        return new SolutionDependency
        {
            RequiredComponentId = JsonHelper.GetGuid(item, "requiredcomponentobjectid"),
            RequiredComponentType = MapComponentType(JsonHelper.GetInt(item, "requiredcomponenttype")),
            RequiredComponentSolution = ResolveSolutionName(requiredSolutionId, solutionIdToName),

            DependentComponentId = JsonHelper.GetGuid(item, "dependentcomponentobjectid"),
            DependentComponentType = MapComponentType(JsonHelper.GetInt(item, "dependentcomponenttype")),
            DependentComponentSolution = ResolveSolutionName(dependentSolutionId, solutionIdToName),

            DependencyType = MapDependencyType(JsonHelper.GetInt(item, "dependencytype"))
        };
    }

    private static ComponentType MapComponentType(int value)
    {
        return Enum.IsDefined(typeof(ComponentType), value)
            ? (ComponentType)value
            : ComponentType.Unknown;
    }

    /// <summary>
    /// Maps Dataverse dependency type to our domain model.
    /// Dataverse: 0=None, 1=SolutionInternal, 2=Published, 4=Unpublished.
    /// Domain:    0=Required, 1=Optional, 2=SolutionInternal.
    /// </summary>
    private static DependencyType MapDependencyType(int dataverseValue)
    {
        return dataverseValue switch
        {
            2 => DependencyType.Required,        // Published → hard dependency
            1 => DependencyType.SolutionInternal, // Internal
            _ => DependencyType.Optional           // None (0) or Unpublished (4) → soft
        };
    }

    private static string? ResolveSolutionName(Guid solutionId, IReadOnlyDictionary<Guid, string> lookup)
    {
        if (solutionId == Guid.Empty)
        {
            return null;
        }
        return lookup.TryGetValue(solutionId, out var name) ? name : null;
    }
}
