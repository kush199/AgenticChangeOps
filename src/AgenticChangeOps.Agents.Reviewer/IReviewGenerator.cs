using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Reviewer;

/// <summary>
/// Produces review content by asking a language model. Generates content only: it never changes
/// status, stores anything, reads the sample or takes any decision. The system check is given to
/// it as read-only data and cannot be changed by what it returns.
/// </summary>
public interface IReviewGenerator
{
    Task<ReviewContent?> GenerateAsync(
        ChangeRequest changeRequest,
        ReviewRequirementsBasis requirements,
        ReviewDesignBasis design,
        ReviewProposalBasis proposal,
        SystemCheck systemCheck,
        CancellationToken cancellationToken);
}
