namespace AgenticChangeOps.Workflow;

/// <summary>
/// Whether reviewing is available to offer once a current proposal exists. Holds no setting value.
/// The offer is only ever shown; it never makes the review a step the workflow or the automatic
/// runner can run.
/// </summary>
public interface IReviewOffer
{
    bool IsOffered { get; }
}
