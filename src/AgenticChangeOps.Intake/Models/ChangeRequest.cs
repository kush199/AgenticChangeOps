using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Intake.Models;

public sealed record ChangeRequest(
    Guid ChangeRequestId,
    string Title,
    string Description,
    string Requester,
    string AffectedSystem,
    Priority Priority,
    ChangeType ChangeType,
    ChangeRequestStatus Status,
    DateTime SubmittedAt);
