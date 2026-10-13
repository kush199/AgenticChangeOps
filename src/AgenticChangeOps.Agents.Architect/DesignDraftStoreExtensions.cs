using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Agents.Architect;

/// <summary>
/// The Architect agent's view of the store's agent-neutral output point: designs are saved with
/// their audit entry and read back in ascending version order.
/// </summary>
public static class DesignDraftStoreExtensions
{
    /// <summary>Stores a design draft and its audit entry together, or neither.</summary>
    public static void SaveDesign(
        this IChangeRequestStore store, DesignDraft design, ChangeRequestAuditEntry auditEntry) =>
        store.SaveAgentOutput(design, auditEntry);

    /// <summary>Returns the design drafts of a change request in ascending version order.</summary>
    public static IReadOnlyList<DesignDraft> GetDesigns(this IChangeRequestStore store, Guid changeRequestId) =>
        store.GetAgentOutputs<DesignDraft>(changeRequestId);
}
