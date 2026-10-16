using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Persistence.Tests.Support;

namespace AgenticChangeOps.Persistence.Tests.Contract;

/// <summary>
/// The behavioural tests every <see cref="IChangeRequestStore"/> must pass (contracts/store-contract.md,
/// C1 to C13). The in-memory and durable stores each run this same suite.
/// </summary>
public abstract class ChangeRequestStoreContract : IDisposable
{
    private readonly List<IDisposable> _owned = new();

    protected abstract IChangeRequestStore CreateStore();

    protected IChangeRequestStore NewStore()
    {
        var store = CreateStore();
        if (store is IDisposable disposable)
            _owned.Add(disposable);
        return store;
    }

    public virtual void Dispose()
    {
        foreach (var owned in _owned)
            owned.Dispose();
    }

    // C1
    [Fact]
    public void C1_SaveThenFind_ReturnsTheSameRecord_AndUnknownIdReturnsNull()
    {
        var store = NewStore();
        var request = SampleRecords.Request();

        store.Save(request);

        RecordAssert.Same(request, store.FindById(request.ChangeRequestId));
        Assert.Null(store.FindById(Guid.NewGuid()));
    }

    // C2
    [Fact]
    public void C2_SaveOfAnExistingId_IsRefused_AndTheStoredRecordIsUnchanged()
    {
        var store = NewStore();
        var request = SampleRecords.Request();
        store.Save(request);

        Assert.ThrowsAny<Exception>(() =>
            store.Save(request with { Title = "A different title" }));

        RecordAssert.Same(request, store.FindById(request.ChangeRequestId));
    }

    // C3
    [Fact]
    public void C3_Update_ReplacesTheRecord_AndAnUnknownIdIsRefused()
    {
        var store = NewStore();
        var request = SampleRecords.Request();
        store.Save(request);
        var approved = request with { Status = ChangeRequestStatus.Approved };

        store.Update(approved);

        RecordAssert.Same(approved, store.FindById(request.ChangeRequestId));
        var unknown = SampleRecords.Request();
        Assert.ThrowsAny<Exception>(() => store.Update(unknown));
        Assert.Null(store.FindById(unknown.ChangeRequestId));
    }

    // C4
    [Fact]
    public void C4_AuditEntries_ComeBackEqual_InRecordingOrder_EqualTimestampsKeepInsertionOrder()
    {
        var store = NewStore();
        var id = Guid.NewGuid();
        var other = Guid.NewGuid();
        var first = SampleRecords.Entry(id, minute: 0);
        var second = SampleRecords.Entry(id, AuditEventType.Approved, minute: 5);
        var third = SampleRecords.Entry(id, AuditEventType.DraftGenerated, occurredAt: second.OccurredAt);
        var fourth = SampleRecords.Entry(id, AuditEventType.DesignGenerated, minute: 9);

        store.SaveAuditEntry(first);
        store.SaveAuditEntry(SampleRecords.Entry(other, minute: 1));
        store.SaveAuditEntry(second);
        store.SaveAuditEntry(third);
        store.SaveAuditEntry(fourth);

        RecordAssert.Same(new[] { first, second, third, fourth }, store.GetAuditEntries(id));
        Assert.Empty(store.GetAuditEntries(Guid.NewGuid()));
    }

    // C14
    [Fact]
    public void C14_AnEntryRecordedLaterWithAnEarlierTimestamp_ComesBackInTimestampOrder_EqualTimestampsKeepInsertionOrder()
    {
        var store = NewStore();
        var id = Guid.NewGuid();
        var late = SampleRecords.Entry(id, minute: 10, details: "late timestamp, recorded first");
        var earlyFirst = SampleRecords.Entry(id, minute: 5, details: "early timestamp, recorded second");
        var earlySecond = SampleRecords.Entry(id, minute: 5, details: "same timestamp, recorded third");

        store.SaveAuditEntry(late);
        store.SaveAuditEntry(earlyFirst);
        store.SaveAuditEntry(earlySecond);

        RecordAssert.Same(new[] { earlyFirst, earlySecond, late }, store.GetAuditEntries(id));
    }

    // C5
    [Fact]
    public void C5_SaveAgentOutput_StoresTheOutputAndItsAuditEntryTogether()
    {
        var store = NewStore();
        var id = Guid.NewGuid();
        var output = SampleRecords.Output(id, 1);
        var entry = SampleRecords.Entry(id, AuditEventType.DraftGenerated);

        store.SaveAgentOutput(output, entry);

        RecordAssert.Same(new[] { output }, store.GetAgentOutputs<FakeAgentOutput>(id));
        RecordAssert.Same(new[] { entry }, store.GetAuditEntries(id));
    }

    // C6
    [Fact]
    public void C6_MismatchedIdsOrNullArguments_AreRefused_AndNothingIsStored()
    {
        var store = NewStore();
        var id = Guid.NewGuid();
        var output = SampleRecords.Output(id, 1);
        var foreignEntry = SampleRecords.Entry(Guid.NewGuid());

        Assert.ThrowsAny<Exception>(() => store.SaveAgentOutput(output, foreignEntry));
        Assert.ThrowsAny<Exception>(() => store.SaveAgentOutput<FakeAgentOutput>(null!, SampleRecords.Entry(id)));
        Assert.ThrowsAny<Exception>(() => store.SaveAgentOutput(output, null!));

        Assert.Empty(store.GetAgentOutputs<FakeAgentOutput>(id));
        Assert.Empty(store.GetAuditEntries(id));
        Assert.Empty(store.GetAuditEntries(foreignEntry.ChangeRequestId));
    }

    // C7
    [Fact]
    public void C7_ADuplicateVersion_IsRefused_AndNeitherTheOutputNorItsAuditEntryIsStored()
    {
        var store = NewStore();
        var id = Guid.NewGuid();
        var first = SampleRecords.Output(id, 1);
        var firstEntry = SampleRecords.Entry(id, AuditEventType.DraftGenerated, minute: 1);
        store.SaveAgentOutput(first, firstEntry);

        var duplicate = SampleRecords.Output(id, 1) with { Summary = "Different" };
        var duplicateEntry = SampleRecords.Entry(id, AuditEventType.DraftGenerated, minute: 2);
        Assert.ThrowsAny<Exception>(() => store.SaveAgentOutput(duplicate, duplicateEntry));

        RecordAssert.Same(new[] { first }, store.GetAgentOutputs<FakeAgentOutput>(id));
        RecordAssert.Same(new[] { firstEntry }, store.GetAuditEntries(id));
    }

    // C8
    [Fact]
    public void C8_TheSameVersion_ForAnotherChangeRequestOrAnotherOutputType_IsAccepted()
    {
        var store = NewStore();
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        store.SaveAgentOutput(SampleRecords.Output(id, 1), SampleRecords.Entry(id, minute: 1));
        store.SaveAgentOutput(SampleRecords.Output(otherId, 1), SampleRecords.Entry(otherId, minute: 2));
        store.SaveAgentOutput(new OtherFakeOutput(id, 1, "note"), SampleRecords.Entry(id, minute: 3));

        Assert.Single(store.GetAgentOutputs<FakeAgentOutput>(id));
        Assert.Single(store.GetAgentOutputs<FakeAgentOutput>(otherId));
        Assert.Equal("note", Assert.Single(store.GetAgentOutputs<OtherFakeOutput>(id)).Note);
    }

    // C9
    [Fact]
    public void C9_GetAgentOutputs_ReturnsAscendingVersions_AndEmptyWhenNone()
    {
        var store = NewStore();
        var id = Guid.NewGuid();
        var v1 = SampleRecords.Output(id, 1);
        var v2 = SampleRecords.Output(id, 2);
        var v3 = SampleRecords.Output(id, 3);

        store.SaveAgentOutput(v1, SampleRecords.Entry(id, minute: 1));
        store.SaveAgentOutput(v3, SampleRecords.Entry(id, minute: 3));
        store.SaveAgentOutput(v2, SampleRecords.Entry(id, minute: 2));

        RecordAssert.Same(new[] { v1, v2, v3 }, store.GetAgentOutputs<FakeAgentOutput>(id));
        Assert.Empty(store.GetAgentOutputs<FakeAgentOutput>(Guid.NewGuid()));
    }

    // C10
    [Fact]
    public void C10_AnOutputTypeTheStoreHasNeverSeen_IsStoredAndReturned()
    {
        var store = NewStore();
        var id = Guid.NewGuid();
        var output = new OtherFakeOutput(id, 1, "an agent added later");

        store.SaveAgentOutput(output, SampleRecords.Entry(id));

        Assert.Equal(output, Assert.Single(store.GetAgentOutputs<OtherFakeOutput>(id)));
    }

    // C11
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void C11_EveryFieldIsReturnedExactlyAsStored(DateTimeKind kind)
    {
        var store = NewStore();
        var stamp = DateTime.SpecifyKind(new DateTime(2026, 3, 4, 5, 6, 7).AddTicks(1234567), kind);
        var request = SampleRecords.Request(status: ChangeRequestStatus.Deferred) with
        {
            SubmittedAt = stamp,
            Title = "Quotes \" and unicode é中 and a newline\nend",
            Priority = Priority.Critical,
            ChangeType = ChangeType.ConfigChange
        };
        var entry = new ChangeRequestAuditEntry(
            Guid.NewGuid(), request.ChangeRequestId, AuditEventType.Deferred, stamp, null,
            "Sample Approver", "", ChangeRequestStatus.Submitted, ChangeRequestStatus.Deferred, "SampleAgent", 7);
        var output = SampleRecords.Output(request.ChangeRequestId, 1) with
        {
            GeneratedAt = stamp,
            Items = new[] { "", "a \"quoted\" item", "é中" }
        };

        store.Save(request);
        store.SaveAgentOutput(output, entry);

        RecordAssert.Same(request, store.FindById(request.ChangeRequestId));
        RecordAssert.Same(new[] { entry }, store.GetAuditEntries(request.ChangeRequestId));
        RecordAssert.Same(new[] { output }, store.GetAgentOutputs<FakeAgentOutput>(request.ChangeRequestId));
    }

    // C12
    [Fact]
    public async Task C12_OverlappingCallersRacingForOneVersion_ExactlyOneSucceeds()
    {
        var store = NewStore();
        var id = Guid.NewGuid();
        const int callers = 8;
        var start = new ManualResetEventSlim(false);
        var successes = 0;

        var tasks = Enumerable.Range(0, callers).Select(i => Task.Run(() =>
        {
            start.Wait();
            try
            {
                store.SaveAgentOutput(
                    SampleRecords.Output(id, 1) with { Summary = $"caller {i}" },
                    SampleRecords.Entry(id, AuditEventType.DraftGenerated, minute: i));
                Interlocked.Increment(ref successes);
            }
            catch (Exception)
            {
                // Refused: the version was taken.
            }
        })).ToArray();

        start.Set();
        await Task.WhenAll(tasks);

        Assert.Equal(1, successes);
        Assert.Single(store.GetAgentOutputs<FakeAgentOutput>(id));
        Assert.Single(store.GetAuditEntries(id));
    }

    // C13
    [Fact]
    public void C13_SaveWithAudit_StoresBothTogether_AndRefusalsStoreNeither()
    {
        var store = NewStore();
        var request = SampleRecords.Request();
        var entry = SampleRecords.Entry(request.ChangeRequestId);

        store.SaveWithAudit(request, entry);

        RecordAssert.Same(request, store.FindById(request.ChangeRequestId));
        RecordAssert.Same(new[] { entry }, store.GetAuditEntries(request.ChangeRequestId));

        // Existing id: refused, nothing new stored.
        var again = SampleRecords.Entry(request.ChangeRequestId, minute: 2);
        Assert.ThrowsAny<Exception>(() => store.SaveWithAudit(request with { Title = "Other" }, again));
        RecordAssert.Same(request, store.FindById(request.ChangeRequestId));
        Assert.Single(store.GetAuditEntries(request.ChangeRequestId));

        // Mismatched ids: refused, nothing stored.
        var fresh = SampleRecords.Request();
        Assert.ThrowsAny<Exception>(() =>
            store.SaveWithAudit(fresh, SampleRecords.Entry(Guid.NewGuid())));
        Assert.Null(store.FindById(fresh.ChangeRequestId));
    }

    [Fact]
    public void C13_UpdateWithAudit_StoresBothTogether_AndRefusalsStoreNeither()
    {
        var store = NewStore();
        var request = SampleRecords.Request();
        store.SaveWithAudit(request, SampleRecords.Entry(request.ChangeRequestId));
        var approved = request with { Status = ChangeRequestStatus.Approved };
        var entry = SampleRecords.Entry(request.ChangeRequestId, AuditEventType.Approved, minute: 3);

        store.UpdateWithAudit(approved, entry);

        RecordAssert.Same(approved, store.FindById(request.ChangeRequestId));
        Assert.Equal(2, store.GetAuditEntries(request.ChangeRequestId).Count);
        RecordAssert.Same(entry, store.GetAuditEntries(request.ChangeRequestId)[1]);

        // Unknown id: refused, nothing stored.
        var unknown = SampleRecords.Request();
        Assert.ThrowsAny<Exception>(() =>
            store.UpdateWithAudit(unknown, SampleRecords.Entry(unknown.ChangeRequestId)));
        Assert.Null(store.FindById(unknown.ChangeRequestId));
        Assert.Empty(store.GetAuditEntries(unknown.ChangeRequestId));

        // Mismatched ids: refused, record and entries unchanged.
        Assert.ThrowsAny<Exception>(() =>
            store.UpdateWithAudit(
                approved with { Status = ChangeRequestStatus.Rejected },
                SampleRecords.Entry(Guid.NewGuid())));
        RecordAssert.Same(approved, store.FindById(request.ChangeRequestId));
        Assert.Equal(2, store.GetAuditEntries(request.ChangeRequestId).Count);
    }
}
