using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Requirement;

namespace AgenticChangeOps.Api.Composition;

/// <summary>
/// Gives the Architect the latest requirements draft without the Architect referencing the
/// Requirement agent: reads it through the requirements drafting service and copies the
/// sections into the Architect's own read model.
/// </summary>
public sealed class RequirementsBasisAdapter : IRequirementsBasisSource
{
    private readonly IRequirementsDraftingService _requirements;

    public RequirementsBasisAdapter(IRequirementsDraftingService requirements)
    {
        _requirements = requirements;
    }

    public RequirementsBasis? GetLatest(Guid changeRequestId)
    {
        var result = _requirements.GetLatestDraft(changeRequestId);
        if (!result.IsSuccess || result.Value is not { } draft)
            return null;

        return new RequirementsBasis(
            Version: draft.Version,
            Summary: draft.Summary,
            FunctionalRequirements: draft.FunctionalRequirements,
            AcceptanceCriteria: draft.AcceptanceCriteria,
            Assumptions: draft.Assumptions,
            OpenQuestions: draft.OpenQuestions);
    }
}
