using System.Text.Json;
using D365Xray.Core.Model;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects installed solutions and their publishers from a Dataverse environment.
/// OData entity: solutions, expanded with publisherid.
/// </summary>
internal static class SolutionCollector
{
    private const string EntitySet = "solutions";
    private const string QueryOptions =
        "$select=solutionid,uniquename,friendlyname,version,ismanaged,installedon,modifiedon" +
        "&$expand=publisherid($select=uniquename,friendlyname,customizationprefix)" +
        "&$filter=isvisible eq true" +
        "&$orderby=uniquename asc";

    public static async Task<IReadOnlyList<Solution>> CollectAsync(
        IDataverseClient client,
        CancellationToken cancellationToken)
    {
        var solutions = new List<Solution>();

        await foreach (var page in client.GetPagedAsync(EntitySet, QueryOptions, cancellationToken))
        {
            using (page)
            {
                foreach (var item in JsonHelper.GetValueArray(page))
                {
                    solutions.Add(MapSolution(item));
                }
            }
        }

        return solutions;
    }

    private static Solution MapSolution(JsonElement item)
    {
        return new Solution
        {
            SolutionId = JsonHelper.GetGuid(item, "solutionid"),
            UniqueName = JsonHelper.GetString(item, "uniquename") ?? "unknown",
            DisplayName = JsonHelper.GetString(item, "friendlyname") ?? "unknown",
            Version = JsonHelper.GetString(item, "version") ?? "0.0.0.0",
            IsManaged = JsonHelper.GetBool(item, "ismanaged"),
            Publisher = MapPublisher(item),
            InstalledOn = JsonHelper.GetDateTimeOffset(item, "installedon"),
            ModifiedOn = JsonHelper.GetDateTimeOffset(item, "modifiedon")
        };
    }

    private static Publisher MapPublisher(JsonElement item)
    {
        if (item.TryGetProperty("publisherid", out var pub) && pub.ValueKind == JsonValueKind.Object)
        {
            return new Publisher
            {
                UniqueName = JsonHelper.GetString(pub, "uniquename") ?? "unknown",
                DisplayName = JsonHelper.GetString(pub, "friendlyname") ?? "unknown",
                CustomizationPrefix = JsonHelper.GetString(pub, "customizationprefix") ?? "new"
            };
        }

        return new Publisher
        {
            UniqueName = "unknown",
            DisplayName = "Unknown Publisher",
            CustomizationPrefix = "new"
        };
    }
}
