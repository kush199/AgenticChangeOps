using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Intake.Services;

public sealed class ChangeRequestService : IChangeRequestService
{
    private readonly IChangeRequestStore _store;
    private readonly ISystemClock _clock;
    private readonly IIdGenerator _idGenerator;
    private readonly ChangeRequestValidator _validator;

    public ChangeRequestService(
        IChangeRequestStore store,
        ISystemClock clock,
        IIdGenerator idGenerator,
        ChangeRequestValidator validator)
    {
        _store = store;
        _clock = clock;
        _idGenerator = idGenerator;
        _validator = validator;
    }

    public SubmissionResult Submit(SubmissionRequest request)
    {
        var errors = _validator.Validate(request);
        if (errors.Count > 0)
            return SubmissionResult.Failure(errors);

        var changeRequestId = _idGenerator.NewId();
        var submittedAt = _clock.UtcNow;

        var changeRequest = new ChangeRequest(
            ChangeRequestId: changeRequestId,
            Title: request.Title.Trim(),
            Description: request.Description.Trim(),
            Requester: request.Requester.Trim(),
            AffectedSystem: request.AffectedSystem.Trim(),
            Priority: request.Priority,
            ChangeType: request.ChangeType,
            Status: ChangeRequestStatus.Submitted,
            SubmittedAt: submittedAt);

        _store.Save(changeRequest);

        var auditEntry = new ChangeRequestAuditEntry(
            AuditEntryId: _idGenerator.NewId(),
            ChangeRequestId: changeRequestId,
            EventType: AuditEventType.Submitted,
            OccurredAt: submittedAt,
            Details: null);

        _store.SaveAuditEntry(auditEntry);

        return SubmissionResult.Success(changeRequest);
    }

    public ChangeRequest? GetById(Guid changeRequestId) =>
        _store.FindById(changeRequestId);

    public IReadOnlyList<ChangeRequestAuditEntry>? GetAuditHistory(Guid changeRequestId)
    {
        if (_store.FindById(changeRequestId) is null)
            return null;
        return _store.GetAuditEntries(changeRequestId);
    }
}
