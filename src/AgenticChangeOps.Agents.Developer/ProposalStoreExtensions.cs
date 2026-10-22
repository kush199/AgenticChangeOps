using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Agents.Developer;

/// <summary>The Developer agent's view of the store's agent-neutral output point.</summary>
public static class ProposalStoreExtensions
{
    /// <summary>Stores a proposal and its audit entry together, or neither.</summary>
    public static void SaveProposal(
        this IChangeRequestStore store, ChangeProposal proposal, ChangeRequestAuditEntry auditEntry) =>
        store.SaveAgentOutput(proposal, auditEntry);

    /// <summary>Returns the proposals of a change request in ascending version order.</summary>
    public static IReadOnlyList<ChangeProposal> GetProposals(this IChangeRequestStore store, Guid changeRequestId) =>
        store.GetAgentOutputs<ChangeProposal>(changeRequestId);
}
