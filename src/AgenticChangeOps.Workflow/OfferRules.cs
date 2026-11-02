namespace AgenticChangeOps.Workflow;

/// <summary>
/// Which step, if any, the workflow status offers a person, as a pure function of recorded facts.
/// Nothing here reads a store, calls a service or consults a model.
/// </summary>
public static class OfferRules
{
    /// <summary>
    /// Only at the design-complete stage. A current proposal is the highest-version proposal based on
    /// the latest design: where one exists the review is offered until a review of it exists, and the
    /// proposal is never offered; where none exists the proposal is offered when it is available.
    /// </summary>
    public static WorkflowStep? Derive(
        WorkflowStage stage,
        int? latestDesignVersion,
        bool proposalsOffered,
        Func<bool> reviewingOffered,
        IReadOnlyList<ProposalFact> proposals,
        IReadOnlyList<ReviewFact> reviews)
    {
        if (stage != WorkflowStage.DesignComplete)
            return null;

        var current = latestDesignVersion is { } design
            ? proposals.Where(p => p.DesignVersion == design).OrderByDescending(p => p.Version).FirstOrDefault()
            : null;

        if (current is null)
            return proposalsOffered ? WorkflowStep.Proposal : null;

        if (reviews.Any(r => r.ProposalVersion == current.Version))
            return null;

        return reviewingOffered() ? WorkflowStep.Review : null;
    }
}
