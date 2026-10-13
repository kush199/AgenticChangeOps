using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Architect;

/// <summary>
/// Produces design content for a change request. Implementations generate content only; they
/// must not change status, call other services, or take any decision.
/// </summary>
public interface IDesignDraftGenerator
{
    Task<DesignContent?> GenerateAsync(
        ChangeRequest changeRequest, RequirementsBasis basis, CancellationToken cancellationToken);
}
