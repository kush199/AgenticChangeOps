using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

public sealed class RetrievalTests
{
    private static readonly Guid CrId    = new("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid AuditId = new("bbbbbbbb-0000-0000-0000-000000000003");
    private static readonly DateTime FixedUtc = new(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);

    private static (ChangeRequestService svc, InMemoryChangeRequestStore store) BuildSut()
    {
        var store = new InMemoryChangeRequestStore();
        var clock = new FakeSystemClock { Now = FixedUtc };
        var idGen = new FakeIdGenerator(CrId, AuditId);
        var svc = new ChangeRequestService(store, clock, idGen, new ChangeRequestValidator());
        return (svc, store);
    }

    private static SubmissionRequest ValidRequest() => new(
        "Migrate legacy config files",
        "Move XML config to environment variables.",
        "Casey Rivera",
        "ConfigurationService",
        Priority.Low,
        ChangeType.ConfigChange);

    [Fact]
    public void GetById_KnownId_ReturnsChangeRequest()
    {
        var (svc, _) = BuildSut();
        var submitted = svc.Submit(ValidRequest()).ChangeRequest!;
        var retrieved = svc.GetById(submitted.ChangeRequestId);
        Assert.NotNull(retrieved);
    }

    [Fact]
    public void GetById_KnownId_AllOriginalFieldsIntact()
    {
        var (svc, _) = BuildSut();
        var req = ValidRequest();
        var submitted = svc.Submit(req).ChangeRequest!;
        var retrieved = svc.GetById(submitted.ChangeRequestId)!;

        Assert.Equal(req.Title, retrieved.Title);
        Assert.Equal(req.Description, retrieved.Description);
        Assert.Equal(req.Requester, retrieved.Requester);
        Assert.Equal(req.AffectedSystem, retrieved.AffectedSystem);
        Assert.Equal(req.Priority, retrieved.Priority);
        Assert.Equal(req.ChangeType, retrieved.ChangeType);
        Assert.Equal(ChangeRequestStatus.Submitted, retrieved.Status);
        Assert.Equal(FixedUtc, retrieved.SubmittedAt);
    }

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        var (svc, _) = BuildSut();
        var result = svc.GetById(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public void GetById_GuidEmpty_ReturnsNull()
    {
        var (svc, _) = BuildSut();
        var result = svc.GetById(Guid.Empty);
        Assert.Null(result);
    }

    [Fact]
    public void GetById_CalledTwiceWithSameId_ReturnsSameRecord()
    {
        var (svc, _) = BuildSut();
        var submitted = svc.Submit(ValidRequest()).ChangeRequest!;
        var first  = svc.GetById(submitted.ChangeRequestId);
        var second = svc.GetById(submitted.ChangeRequestId);
        Assert.Equal(first, second);
    }

    [Fact]
    public void GetById_AfterFailedSubmission_ReturnsNull()
    {
        var (svc, _) = BuildSut();
        svc.Submit(ValidRequest() with { Title = "" }); // invalid — nothing stored
        var result = svc.GetById(CrId);
        Assert.Null(result);
    }
}
