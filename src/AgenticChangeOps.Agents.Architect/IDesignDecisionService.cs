namespace AgenticChangeOps.Agents.Architect;

public interface IDesignDecisionService
{
    DesignDecisionResult Decide(
        Guid changeRequestId, int version, string approverName, DesignVerdict verdict, string? reason);

    /// <summary>Every stored decision of a change request, in ascending design-version order.</summary>
    IReadOnlyList<DesignDecision> GetDecisions(Guid changeRequestId);
}
