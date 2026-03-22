using System.Text.Json;
using D365Xray.Core.Model;
using Microsoft.Extensions.Logging;

namespace D365Xray.Connectors.Collectors;

/// <summary>
/// Collects workflow definitions from a Dataverse environment.
/// OData entity: workflows. Covers classic workflows, actions, and modern flows.
/// Business rules (category=2) are excluded — see BusinessRuleCollector.
/// </summary>
internal static class WorkflowCollector
{
    private const string EntitySet = "workflows";
    private const string QueryOptions =
        "$select=workflowid,name,uniquename,category,mode,statecode," +
        "primaryentity,triggeroncreate,triggeronupdateattributelist," +
        "triggerondelete,ismanaged,modifiedon" +
        "&$filter=category ne 2 and type eq 1" + // exclude business rules, only definitions (type=1)
        "&$orderby=name asc";

    public static async Task<IReadOnlyList<WorkflowDefinition>> CollectAsync(
        IDataverseClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var items = new List<WorkflowDefinition>();

        try
        {
            await foreach (var page in client.GetPagedAsync(EntitySet, QueryOptions, cancellationToken))
            {
                using (page)
                {
                    foreach (var item in JsonHelper.GetValueArray(page))
                    {
                        items.Add(Map(item));
                    }
                }
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound
                                               or System.Net.HttpStatusCode.BadRequest)
        {
            logger.LogWarning(
                "Workflows entity is not available. " +
                "Workflow analysis will be skipped. Status: {StatusCode}", ex.StatusCode);
        }

        return items;
    }

    private static WorkflowDefinition Map(JsonElement item)
    {
        var categoryValue = JsonHelper.GetInt(item, "category", -1);
        var modeValue = JsonHelper.GetInt(item, "mode", -1);
        // statecode: 0 = Draft, 1 = Activated
        var isActivated = JsonHelper.GetInt(item, "statecode") == 1;

        return new WorkflowDefinition
        {
            WorkflowId = JsonHelper.GetGuid(item, "workflowid"),
            Name = JsonHelper.GetString(item, "name") ?? "unknown",
            UniqueName = JsonHelper.GetString(item, "uniquename"),
            Category = Enum.IsDefined(typeof(WorkflowCategory), categoryValue)
                ? (WorkflowCategory)categoryValue
                : WorkflowCategory.Unknown,
            Mode = Enum.IsDefined(typeof(WorkflowMode), modeValue)
                ? (WorkflowMode)modeValue
                : WorkflowMode.Unknown,
            IsActivated = isActivated,
            PrimaryEntity = JsonHelper.GetString(item, "primaryentity"),
            TriggerOnCreate = JsonHelper.GetRawValue(item, "triggeroncreate"),
            TriggerOnUpdate = JsonHelper.GetString(item, "triggeronupdateattributelist"),
            TriggerOnDelete = JsonHelper.GetRawValue(item, "triggerondelete"),
            IsManaged = JsonHelper.GetBool(item, "ismanaged"),
            ModifiedOn = JsonHelper.GetDateTimeOffset(item, "modifiedon")
        };
    }
}
