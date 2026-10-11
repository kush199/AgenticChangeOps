using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Intake.Models;

public sealed record SubmissionRequest(
    string Title,
    string Description,
    string Requester,
    string AffectedSystem,
    Priority Priority,
    ChangeType ChangeType);
