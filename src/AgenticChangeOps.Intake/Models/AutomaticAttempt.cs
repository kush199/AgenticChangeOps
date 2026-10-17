namespace AgenticChangeOps.Intake.Models;

/// <summary>
/// Bookkeeping for automatic advancement: the consecutive failures of one step for one change
/// request. Not a draft, a decision or an audit entry. Step and category are kept as their enum
/// names because Intake cannot reference the projects that define them. Holds no free text.
/// </summary>
public sealed record AutomaticAttempt(
    Guid ChangeRequestId,
    string Step,
    int CompletedStepCount,
    int ConsecutiveFailures,
    string LastFailureCategory,
    DateTime LastFailureAt);
