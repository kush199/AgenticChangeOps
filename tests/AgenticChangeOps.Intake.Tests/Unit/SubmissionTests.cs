using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

public sealed class SubmissionTests
{
    private static readonly Guid ChangeRequestGuid = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid AuditEntryGuid    = new("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly DateTime FixedUtc      = new(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);

    private static (ChangeRequestService svc, InMemoryChangeRequestStore store) BuildSut(
        Guid? crId = null, Guid? auditId = null, DateTime? now = null)
    {
        var store = new InMemoryChangeRequestStore();
        var clock = new FakeSystemClock { Now = now ?? FixedUtc };
        var idGen = new FakeIdGenerator(crId ?? ChangeRequestGuid, auditId ?? AuditEntryGuid);
        var validator = new ChangeRequestValidator();
        var svc = new ChangeRequestService(store, clock, idGen, validator);
        return (svc, store);
    }

    private static SubmissionRequest ValidRequest(
        string title = "Implement CSV export for audit logs",
        string description = "Users need to export audit logs as CSV for offline review.",
        string requester = "Alex Taylor",
        string affectedSystem = "AuditReportingService",
        Priority priority = Priority.High,
        ChangeType changeType = ChangeType.Feature) =>
        new(title, description, requester, affectedSystem, priority, changeType);

    [Fact(Skip = "synthetic")]
    public void Submit_ValidRequest_ReturnsSuccess()
    {
        var (svc, _) = BuildSut();
        var result = svc.Submit(ValidRequest());
        Assert.True(result.IsSuccess);
    }

    [Fact(Skip = "synthetic")]
    public void Submit_ValidRequest_ReturnsFullChangeRequestRecord()
    {
        var (svc, _) = BuildSut();
        var result = svc.Submit(ValidRequest());
        Assert.NotNull(result.ChangeRequest);
    }

    [Fact(Skip = "synthetic")]
    public void Submit_ValidRequest_AssignsChangeRequestIdFromIdGenerator()
    {
        var (svc, _) = BuildSut();
        var result = svc.Submit(ValidRequest());
        Assert.Equal(ChangeRequestGuid, result.ChangeRequest!.ChangeRequestId);
    }

    [Fact(Skip = "synthetic")]
    public void Submit_ValidRequest_AssignsSubmittedAtFromClock()
    {
        var (svc, _) = BuildSut();
        var result = svc.Submit(ValidRequest());
        Assert.Equal(FixedUtc, result.ChangeRequest!.SubmittedAt);
    }

    [Fact(Skip = "synthetic")]
    public void Submit_ValidRequest_SetsStatusToSubmitted()
    {
        var (svc, _) = BuildSut();
        var result = svc.Submit(ValidRequest());
        Assert.Equal(ChangeRequestStatus.Submitted, result.ChangeRequest!.Status);
    }

    [Fact(Skip = "synthetic")]
    public void Submit_ValidRequest_HasEmptyErrors()
    {
        var (svc, _) = BuildSut();
        var result = svc.Submit(ValidRequest());
        Assert.Empty(result.Errors);
    }

    [Fact(Skip = "synthetic")]
    public void Submit_ValidRequest_PersistsRecordInStore()
    {
        var (svc, store) = BuildSut();
        var result = svc.Submit(ValidRequest());
        var persisted = store.FindById(result.ChangeRequest!.ChangeRequestId);
        Assert.NotNull(persisted);
    }

    [Fact(Skip = "synthetic")]
    public void Submit_ValidRequest_PersistedRecordMatchesReturnedRecord()
    {
        var (svc, store) = BuildSut();
        var result = svc.Submit(ValidRequest());
        var persisted = store.FindById(result.ChangeRequest!.ChangeRequestId);
        Assert.Equal(result.ChangeRequest, persisted);
    }

    [Fact(Skip = "synthetic")]
    public void Submit_ValidRequest_CreatesOneAuditEntry()
    {
        var (svc, store) = BuildSut();
        var result = svc.Submit(ValidRequest());
        var entries = store.GetAuditEntries(result.ChangeRequest!.ChangeRequestId);
        Assert.Single(entries);
    }

    [Fact(Skip = "synthetic")]
    public void Submit_ValidRequest_AuditEntryHasSubmittedEventType()
    {
        var (svc, store) = BuildSut();
        var result = svc.Submit(ValidRequest());
        var entry = store.GetAuditEntries(result.ChangeRequest!.ChangeRequestId)[0];
        Assert.Equal(AuditEventType.Submitted, entry.EventType);
    }

    [Fact(Skip = "synthetic")]
    public void Submit_ValidRequest_AuditEntryTimestampMatchesSubmittedAt()
    {
        var (svc, store) = BuildSut();
        var result = svc.Submit(ValidRequest());
        var entry = store.GetAuditEntries(result.ChangeRequest!.ChangeRequestId)[0];
        Assert.Equal(result.ChangeRequest!.SubmittedAt, entry.OccurredAt);
    }

    [Fact(Skip = "synthetic")]
    public void Submit_TitleWithLeadingTrailingWhitespace_StoredTrimmed()
    {
        var (svc, _) = BuildSut();
        var result = svc.Submit(ValidRequest(title: "  Export Feature  "));
        Assert.Equal("Export Feature", result.ChangeRequest!.Title);
    }

    [Fact(Skip = "synthetic")]
    public void Submit_AllFieldsWithWhitespace_StoredTrimmed()
    {
        var (svc, _) = BuildSut();
        var req = ValidRequest(
            title: "  Sync Config  ",
            description: "  Sync nightly.  ",
            requester: "  Sam Doe  ",
            affectedSystem: "  SchedulerService  ");
        var result = svc.Submit(req);
        Assert.Equal("Sync Config", result.ChangeRequest!.Title);
        Assert.Equal("Sync nightly.", result.ChangeRequest.Description);
        Assert.Equal("Sam Doe", result.ChangeRequest.Requester);
        Assert.Equal("SchedulerService", result.ChangeRequest.AffectedSystem);
    }
}
