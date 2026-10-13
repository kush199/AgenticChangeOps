using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Agents.Requirement;

/// <summary>
/// The Requirement agent's view of the store's agent-neutral output point: drafts are saved with
/// their audit entry and read back in ascending version order.
/// </summary>
public static class RequirementDraftStoreExtensions
{
    /// <summary>Stores a draft and its audit entry together, or neither.</summary>
    public static void SaveDraft(
        this IChangeRequestStore store, RequirementsDraft draft, ChangeRequestAuditEntry auditEntry) =>
        store.SaveAgentOutput(draft, auditEntry);

    /// <summary>Returns the drafts of a change request in ascending version order.</summary>
    public static IReadOnlyList<RequirementsDraft> GetDrafts(this IChangeRequestStore store, Guid changeRequestId) =>
        store.GetAgentOutputs<RequirementsDraft>(changeRequestId);
}
