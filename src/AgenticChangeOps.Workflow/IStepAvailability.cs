namespace AgenticChangeOps.Workflow;

/// <summary>Whether the model settings a step needs are configured. Holds no setting value.</summary>
public interface IStepAvailability
{
    bool IsConfigured(WorkflowStep step);
}
