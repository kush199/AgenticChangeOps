using AgenticChangeOps.Workflow;

namespace AgenticChangeOps.Api.Composition;

/// <summary>
/// Answers the workflow's "is this step's model configured?" from the availability facts each
/// agent route already uses, so both routes and the workflow always agree.
/// </summary>
public sealed class StepAvailabilityAdapter : IStepAvailability
{
    private readonly DraftingAvailability _drafting;
    private readonly DesignAvailability _design;

    public StepAvailabilityAdapter(DraftingAvailability drafting, DesignAvailability design)
    {
        _drafting = drafting;
        _design = design;
    }

    public bool IsConfigured(WorkflowStep step) => step switch
    {
        WorkflowStep.RequirementsDraft => _drafting.IsConfigured,
        WorkflowStep.DesignDraft => _design.IsConfigured,
        _ => false
    };
}
