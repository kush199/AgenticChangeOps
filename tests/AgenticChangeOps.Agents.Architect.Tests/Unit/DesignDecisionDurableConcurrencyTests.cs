using AgenticChangeOps.Agents.Architect.Tests.Fakes;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Intake.Tests.Fakes;
using AgenticChangeOps.Persistence;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

/// <summary>
/// Overlapping decisions on the durable store: exactly one is stored and the rest are refused, and a
/// version conflict reported by the store itself is the same "already decided" refusal.
/// </summary>
public sealed class DesignDecisionDurableConcurrencyTests : IDisposable
{
    private const int Callers = 12;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "changeops-decide-" + Guid.NewGuid().ToString("N"));
    private readonly FakeSystemClock _clock = new();
    private readonly FakeRequirementsBasisSource _basis = new();
    private readonly SqliteChangeRequestStore _store;

    public DesignDecisionDurableConcurrencyTests()
    {
        Directory.CreateDirectory(_folder);
        _store = new SqliteChangeRequestStore(new SqliteStorageOptions(Path.Combine(_folder, "changeops.db")));
    }

    public void Dispose()
    {
        _store.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private async Task<Guid> ApprovedWithDesignAsync()
    {
        var ids = new GuidIdGenerator();
        var intake = new ChangeRequestService(_store, _clock, ids, new ChangeRequestValidator());
        var id = intake.Submit(new SubmissionRequest(
            "Move nightly report schedule", "Move the nightly synthetic report job to a quieter window.",
            "Avery Quill", "Sample Billing Portal", Priority.Medium, ChangeType.ConfigChange)).ChangeRequest!.ChangeRequestId;
        _basis.Set(id, FakeRequirementsBasisSource.Basis(1));
        new ApprovalService(_store, _clock, ids, new ApprovalDecisionValidator())
            .RecordDecision(new ApprovalDecisionRequest(id, "Jordan Reed", ApprovalDecision.Approve, null));
        var designing = new DesignDraftingService(
            _store, _clock, ids, _basis, new FakeDesignGenerator(), new DesignContentValidator());
        Assert.True((await designing.DesignAsync(id)).IsSuccess);
        return id;
    }

    [Fact]
    public async Task OverlappingDecisions_OnTheDurableStore_StoreExactlyOne_AndRefuseTheRest()
    {
        var id = await ApprovedWithDesignAsync();
        var service = new DesignDecisionService(_store, _clock, new GuidIdGenerator(), _basis);
        using var gate = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, Callers).Select(i => Task.Run(() =>
        {
            gate.Wait();
            return service.Decide(
                id, 1, "Approver " + i, i % 2 == 0 ? DesignVerdict.Accept : DesignVerdict.Reject, "Because.");
        })).ToArray();
        gate.Set();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Equal(Callers - 1, results.Count(r => r.Refusal == DesignDecisionRefusal.AlreadyDecided));
        Assert.Single(_store.GetAgentOutputs<DesignDecision>(id));
        Assert.Equal(1, _store.GetAuditEntries(id).Count(e => e.EventType == AuditEventType.DesignDecided));
    }

    [Fact]
    public async Task ASeparateServiceInstance_OnTheSameStore_SeesTheDecisionAsFinal()
    {
        var id = await ApprovedWithDesignAsync();
        var first = new DesignDecisionService(_store, _clock, new GuidIdGenerator(), _basis);
        var second = new DesignDecisionService(_store, _clock, new GuidIdGenerator(), _basis);

        Assert.True(first.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null).IsSuccess);

        Assert.Equal(DesignDecisionRefusal.AlreadyDecided, second.Decide(id, 1, "Casey Lin", DesignVerdict.Reject, "No.").Refusal);
        Assert.Single(second.GetDecisions(id));
    }

    [Fact]
    public async Task AVersionConflictReportedByTheStore_IsTheSameAlreadyDecidedRefusal_AndStoresNothing()
    {
        var id = await ApprovedWithDesignAsync();
        var store = new DecisionTestStore(_store) { FailDecisionSave = new StorageVersionConflictException() };
        var service = new DesignDecisionService(store, _clock, new GuidIdGenerator(), _basis);

        var result = service.Decide(id, 1, "Jordan Reed", DesignVerdict.Accept, null);

        Assert.Equal(DesignDecisionRefusal.AlreadyDecided, result.Refusal);
        Assert.Empty(_store.GetAgentOutputs<DesignDecision>(id));
        Assert.Equal(0, _store.GetAuditEntries(id).Count(e => e.EventType == AuditEventType.DesignDecided));
    }
}
