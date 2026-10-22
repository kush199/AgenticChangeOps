using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Developer;

/// <summary>
/// Produces proposal content by asking a language model. Generates content only: it never changes
/// status, stores anything or takes any decision.
/// </summary>
public interface IProposalGenerator
{
    Task<ProposalContent?> GenerateAsync(
        ChangeRequest changeRequest,
        ProposalRequirementsBasis requirements,
        ProposalDesignBasis design,
        CancellationToken cancellationToken);
}

public sealed class ProposalResult
{
    public bool IsSuccess { get; }
    public ChangeProposal? Proposal { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>Set only when generation failed or its content was unusable.</summary>
    public GenerationFailureCategory? FailureCategory { get; }

    /// <summary>Set only when the system refused what the model produced.</summary>
    public ProposalRejection? Rejection { get; }

    private ProposalResult(
        bool isSuccess,
        ChangeProposal? proposal,
        IReadOnlyList<ValidationError> errors,
        GenerationFailureCategory? failureCategory,
        ProposalRejection? rejection)
    {
        IsSuccess = isSuccess;
        Proposal = proposal;
        Errors = errors;
        FailureCategory = failureCategory;
        Rejection = rejection;
    }

    public static ProposalResult Success(ChangeProposal proposal) =>
        new(true, proposal, Array.Empty<ValidationError>(), null, null);

    public static ProposalResult Failure(IReadOnlyList<ValidationError> errors) =>
        new(false, null, errors, null, null);

    public static ProposalResult Failure(IReadOnlyList<ValidationError> errors, GenerationFailureCategory category) =>
        new(false, null, errors, category, null);

    public static ProposalResult Rejected(ProposalRejection rejection) =>
        new(false, null, new[] { new ValidationError("Proposal", $"The proposal was rejected ({rejection}).") }, null, rejection);
}

public interface IProposalService
{
    Task<ProposalResult> ProposeAsync(Guid changeRequestId, CancellationToken cancellationToken = default);

    QueryResult<ChangeProposal?> GetLatestProposal(Guid changeRequestId);

    QueryResult<IReadOnlyList<ChangeProposal>> GetProposalHistory(Guid changeRequestId);
}
