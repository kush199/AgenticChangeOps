using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Persistence.Tests.Support;

/// <summary>Synthetic records only.</summary>
public static class SampleRecords
{
    public static readonly DateTime Start = new(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);

    public static ChangeRequest Request(
        Guid? id = null, ChangeRequestStatus status = ChangeRequestStatus.Submitted, int minute = 0) =>
        new(
            ChangeRequestId: id ?? Guid.NewGuid(),
            Title: "Add synthetic report export",
            Description: "Add a nightly export of the synthetic widget report.",
            Requester: "Sample Requester",
            AffectedSystem: "Sample Reporting Service",
            Priority: Priority.High,
            ChangeType: ChangeType.Feature,
            Status: status,
            SubmittedAt: Start.AddMinutes(minute).AddTicks(1234));

    public static ChangeRequestAuditEntry Entry(
        Guid changeRequestId,
        AuditEventType eventType = AuditEventType.Submitted,
        int minute = 0,
        DateTime? occurredAt = null,
        string? details = null) =>
        new(
            AuditEntryId: Guid.NewGuid(),
            ChangeRequestId: changeRequestId,
            EventType: eventType,
            OccurredAt: occurredAt ?? Start.AddMinutes(minute),
            Details: details ?? $"entry at minute {minute}",
            ApproverName: eventType == AuditEventType.Approved ? "Sample Approver" : null,
            Reason: eventType == AuditEventType.Approved ? "Looks fine." : null,
            StatusBefore: ChangeRequestStatus.Submitted,
            StatusAfter: ChangeRequestStatus.Approved,
            AgentName: null,
            DraftVersion: null);

    /// <summary>Writes one new change request with its matching audit entry.</summary>
    public static void WriteOne(AgenticChangeOps.Intake.Stores.IChangeRequestStore store)
    {
        var request = Request();
        store.SaveWithAudit(request, Entry(request.ChangeRequestId));
    }

    public static FakeAgentOutput Output(Guid changeRequestId, int version) =>
        new(
            OutputId: Guid.NewGuid(),
            ChangeRequestId: changeRequestId,
            Version: version,
            GeneratedAt: Start.AddMinutes(version).AddTicks(77),
            Priority: Priority.Critical,
            Summary: $"Summary {version}",
            Items: new[] { "first item", "second item", $"version {version}" });
}
