namespace AgenticChangeOps.Workflow;

/// <summary>Describes the automatic-advancement mode for one request's workflow status.</summary>
public interface IAutomaticStatusSource
{
    AutomaticAdvancementStatus Describe(Guid changeRequestId, WorkflowStep? nextStep, int completedStepCount);
}
