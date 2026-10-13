using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Intake.Services;

/// <summary>
/// Produces draft content for a change request. Implementations generate content only; they
/// must not change status, call other services, or take any decision.
/// </summary>
public interface IRequirementsDraftGenerator
{
    Task<DraftContent?> GenerateAsync(ChangeRequest changeRequest, CancellationToken cancellationToken);
}
