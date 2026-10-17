using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Workflow;

/// <summary>
/// When an attempt row still describes the present situation. It does only while the same step is
/// due and no draft has been added since the failure, so a person's success, a decision that makes
/// another step due, or a new requirements draft all end its effect without anyone clearing it.
/// </summary>
internal static class AutomaticAttemptRules
{
    public static bool Applies(AutomaticAttempt? row, WorkflowStep step, int completedStepCount) =>
        row is not null
        && row.Step == step.ToString()
        && row.CompletedStepCount == completedStepCount;
}
