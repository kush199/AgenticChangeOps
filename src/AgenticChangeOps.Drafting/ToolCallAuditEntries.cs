using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Drafting;

/// <summary>The audit entry for one tool call made while an agent was generating content.</summary>
public static class ToolCallAuditEntries
{
    private const int MaxAuditTargetLength = 120;

    public static ChangeRequestAuditEntry Create(
        string agentName,
        IIdGenerator ids,
        Guid changeRequestId,
        ChangeRequestStatus status,
        ToolCallRecord call,
        DateTime generatedAt)
    {
        var target = new string(call.Target.Where(c => !char.IsControl(c)).ToArray());
        if (target.Length > MaxAuditTargetLength)
            target = target[..MaxAuditTargetLength];

        return new ChangeRequestAuditEntry(
            AuditEntryId: ids.NewId(),
            ChangeRequestId: changeRequestId,
            EventType: AuditEventType.ToolCalled,
            // A call cannot have happened after the output it belongs to was recorded.
            OccurredAt: call.StartedAt <= generatedAt ? call.StartedAt : generatedAt,
            Details: null,
            StatusBefore: status,
            StatusAfter: status,
            AgentName: agentName,
            ToolName: call.ToolName,
            ToolTarget: target,
            ToolOutcome: call.Outcome.ToString(),
            ResultBytes: call.ResultBytes,
            ElapsedMilliseconds: (int)Math.Min(int.MaxValue, Math.Max(0, call.Elapsed.TotalMilliseconds)));
    }
}
