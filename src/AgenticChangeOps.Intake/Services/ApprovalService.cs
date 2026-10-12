using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Intake.Services;

public sealed class ApprovalService : IApprovalService
{
    private readonly IChangeRequestStore _store;
    private readonly ISystemClock _clock;
    private readonly IIdGenerator _idGenerator;
    private readonly ApprovalDecisionValidator _validator;

    public ApprovalService(
        IChangeRequestStore store,
        ISystemClock clock,
        IIdGenerator idGenerator,
        ApprovalDecisionValidator validator)
    {
        _store = store;
        _clock = clock;
        _idGenerator = idGenerator;
        _validator = validator;
    }

    public DecisionResult RecordDecision(ApprovalDecisionRequest request)
    {
        // Validate, update and audit form one uninterrupted step, so of several overlapping
        // decisions exactly one wins and the rest are refused by the final-state rule.
        lock (_store.SyncRoot)
            return RecordDecisionLocked(request);
    }

    private DecisionResult RecordDecisionLocked(ApprovalDecisionRequest request)
    {
        var existing = _store.FindById(request.ChangeRequestId);
        var errors = _validator.Validate(request, existing);
        if (errors.Count > 0)
            return DecisionResult.Failure(errors);

        var before = existing!.Status;
        var (after, eventType) = request.Decision switch
        {
            ApprovalDecision.Approve => (ChangeRequestStatus.Approved, AuditEventType.Approved),
            ApprovalDecision.Reject => (ChangeRequestStatus.Rejected, AuditEventType.Rejected),
            _ => (ChangeRequestStatus.Deferred, AuditEventType.Deferred)
        };

        // Build everything that can fail before touching the store so a failure leaves no
        // partial change (status updated without an audit entry).
        var updated = existing with { Status = after };
        var entry = new ChangeRequestAuditEntry(
            AuditEntryId: _idGenerator.NewId(),
            ChangeRequestId: existing.ChangeRequestId,
            EventType: eventType,
            OccurredAt: _clock.UtcNow,
            Details: null,
            ApproverName: request.ApproverName.Trim(),
            Reason: request.Reason?.Trim() ?? string.Empty,
            StatusBefore: before,
            StatusAfter: after);

        _store.Update(updated);
        _store.SaveAuditEntry(entry);

        return DecisionResult.Success(updated);
    }

    public bool IsClearedToProceed(Guid changeRequestId) =>
        _store.FindById(changeRequestId)?.Status == ChangeRequestStatus.Approved;
}
