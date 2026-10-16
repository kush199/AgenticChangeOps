using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Persistence.Tests.Support;

namespace AgenticChangeOps.Persistence.Tests.Durable;

/// <summary>D1: everything recorded before a restart is there afterwards, exactly as it was.</summary>
public sealed class RestartTests : IDisposable
{
    private readonly TempDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public void EveryRecordedFact_SurvivesClosingAndReopeningTheStorage()
    {
        var first = SampleRecords.Request(minute: 0);
        var second = SampleRecords.Request(minute: 1);
        var firstEntry = SampleRecords.Entry(first.ChangeRequestId, minute: 0);
        var secondEntry = SampleRecords.Entry(second.ChangeRequestId, minute: 1);
        var approved = first with { Status = ChangeRequestStatus.Approved };
        var approvedEntry = SampleRecords.Entry(first.ChangeRequestId, AuditEventType.Approved, minute: 2);
        var sameInstant = SampleRecords.Entry(
            first.ChangeRequestId, AuditEventType.DraftGenerated, occurredAt: approvedEntry.OccurredAt);
        var v1 = SampleRecords.Output(first.ChangeRequestId, 1);
        var v2 = SampleRecords.Output(first.ChangeRequestId, 2);
        var v1Entry = SampleRecords.Entry(first.ChangeRequestId, AuditEventType.DesignGenerated, minute: 5);
        var v2Entry = SampleRecords.Entry(first.ChangeRequestId, AuditEventType.DesignGenerated, minute: 6);

        using (var store = new SqliteChangeRequestStore(_database.Options))
        {
            store.SaveWithAudit(first, firstEntry);
            store.SaveWithAudit(second, secondEntry);
            store.UpdateWithAudit(approved, approvedEntry);
            store.SaveAuditEntry(sameInstant);
            store.SaveAgentOutput(v1, v1Entry);
            store.SaveAgentOutput(v2, v2Entry);
        }

        using var reopened = new SqliteChangeRequestStore(_database.Options);

        RecordAssert.Same(approved, reopened.FindById(first.ChangeRequestId));
        RecordAssert.Same(second, reopened.FindById(second.ChangeRequestId));
        RecordAssert.Same(
            new[] { firstEntry, approvedEntry, sameInstant, v1Entry, v2Entry },
            reopened.GetAuditEntries(first.ChangeRequestId));
        RecordAssert.Same(new[] { secondEntry }, reopened.GetAuditEntries(second.ChangeRequestId));
        RecordAssert.Same(new[] { v1, v2 }, reopened.GetAgentOutputs<FakeAgentOutput>(first.ChangeRequestId));
    }

    [Fact]
    public void AfterARestart_TheNextVersionContinuesTheSequence_AndTakenVersionsStayTaken()
    {
        var id = SampleRecords.Request().ChangeRequestId;
        using (var store = new SqliteChangeRequestStore(_database.Options))
        {
            store.SaveAgentOutput(SampleRecords.Output(id, 1), SampleRecords.Entry(id, minute: 1));
            store.SaveAgentOutput(SampleRecords.Output(id, 2), SampleRecords.Entry(id, minute: 2));
        }

        using var reopened = new SqliteChangeRequestStore(_database.Options);

        Assert.ThrowsAny<Exception>(() =>
            reopened.SaveAgentOutput(SampleRecords.Output(id, 2), SampleRecords.Entry(id, minute: 3)));
        var third = SampleRecords.Output(id, reopened.GetAgentOutputs<FakeAgentOutput>(id).Count + 1);
        reopened.SaveAgentOutput(third, SampleRecords.Entry(id, minute: 4));

        Assert.Equal(new[] { 1, 2, 3 }, reopened.GetAgentOutputs<FakeAgentOutput>(id).Select(o => o.Version));
        Assert.Equal(3, reopened.GetAuditEntries(id).Count);
    }

    [Fact]
    public void ASecondStoreOpenedWhileTheFirstIsStillOpen_SeesTheCommittedData()
    {
        var request = SampleRecords.Request();
        using var writer = new SqliteChangeRequestStore(_database.Options);
        writer.SaveWithAudit(request, SampleRecords.Entry(request.ChangeRequestId));

        using var reader = new SqliteChangeRequestStore(_database.Options);

        RecordAssert.Same(request, reader.FindById(request.ChangeRequestId));
    }
}
