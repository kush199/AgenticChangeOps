using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Intake.Models;

public sealed record ChangeRequestAuditEntry(
    Guid AuditEntryId,
    Guid ChangeRequestId,
    AuditEventType EventType,
    DateTime OccurredAt,
    string? Details,
    string? ApproverName = null,
    string? Reason = null,
    ChangeRequestStatus? StatusBefore = null,
    ChangeRequestStatus? StatusAfter = null,
    string? AgentName = null,
    int? DraftVersion = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    bool? Automatic = null);
