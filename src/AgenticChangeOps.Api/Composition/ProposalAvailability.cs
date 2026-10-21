using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Workflow;

namespace AgenticChangeOps.Api.Composition;

public enum ProposalAvailabilityState
{
    /// <summary>The sample tools are switched on and the model and proposal settings are valid.</summary>
    Available,

    /// <summary>The sample tools are switched off (the default).</summary>
    ToolsUnavailable,

    /// <summary>The tools are on but a model, tool or proposal setting is invalid.</summary>
    NotConfigured
}

/// <summary>Records once, at startup, whether proposals can run. Holds no setting value.</summary>
public sealed class ProposalAvailability
{
    public ProposalAvailability(ProposalAvailabilityState state)
    {
        State = state;
    }

    public ProposalAvailabilityState State { get; }

    public bool IsAvailable => State == ProposalAvailabilityState.Available;
}

/// <summary>Tells the workflow whether to offer the proposal. Only ever shown, never run by the workflow.</summary>
public sealed class ProposalOffer : IProposalOffer
{
    private readonly ProposalAvailability _availability;

    public ProposalOffer(ProposalAvailability availability)
    {
        _availability = availability;
    }

    public bool IsOffered => _availability.IsAvailable;
}

/// <summary>
/// Gives the Developer the latest requirements and design drafts without the Developer referencing
/// the other agents: reads them through their own services and copies the sections into the
/// Developer's own read model.
/// </summary>
public sealed class ProposalBasisAdapter : IProposalBasisSource
{
    private readonly IRequirementsDraftingService _requirements;
    private readonly IDesignDraftingService _designs;

    public ProposalBasisAdapter(IRequirementsDraftingService requirements, IDesignDraftingService designs)
    {
        _requirements = requirements;
        _designs = designs;
    }

    public ProposalBasis GetLatest(Guid changeRequestId)
    {
        ProposalRequirementsBasis? requirements = null;
        var drafts = _requirements.GetLatestDraft(changeRequestId);
        if (drafts.IsSuccess && drafts.Value is { } draft)
            requirements = new ProposalRequirementsBasis(
                draft.Version, draft.Summary, draft.FunctionalRequirements,
                draft.AcceptanceCriteria, draft.Assumptions, draft.OpenQuestions);

        ProposalDesignBasis? design = null;
        var designs = _designs.GetLatestDesign(changeRequestId);
        if (designs.IsSuccess && designs.Value is { } latest)
            design = new ProposalDesignBasis(
                latest.Version, latest.RequirementsVersion, latest.ApproachSummary,
                latest.AffectedComponents, latest.ImpactAndRisks, latest.Dependencies,
                latest.RollbackConsiderations, latest.OpenQuestions);

        return new ProposalBasis(requirements, design);
    }
}
