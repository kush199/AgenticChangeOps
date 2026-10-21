namespace AgenticChangeOps.Workflow;

/// <summary>
/// Whether the proposal is available to offer once a current design exists. Holds no setting value.
/// The offer is only ever shown; it never makes the proposal a step the workflow or the automatic
/// runner can run.
/// </summary>
public interface IProposalOffer
{
    bool IsOffered { get; }
}
