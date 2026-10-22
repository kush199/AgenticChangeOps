using AgenticChangeOps.Drafting;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Intake.Tests.Fakes;

namespace AgenticChangeOps.Drafting.Tests.Fakes;

/// <summary>Counts ids from a fixed start and is safe to call from overlapping threads.</summary>
public sealed class ThreadSafeIds : IIdGenerator
{
    private int _next;

    public Guid NewId()
    {
        var n = Interlocked.Increment(ref _next);
        return new Guid(n, 0, 0, new byte[8]);
    }
}

public sealed class EngineTestFixture
{
    public InMemoryChangeRequestStore Store { get; } = new();
    public FakeSystemClock Clock { get; } = new();
    public ThreadSafeIds Ids { get; } = new();
    public StandInAgent Agent { get; }
    public DraftingEngine<StandInRecord, StandInBasis, StandInGenerated, StandInContent, StandInRejection> Engine { get; }

    public EngineTestFixture()
    {
        Agent = new StandInAgent(Store);
        Engine = new DraftingEngine<StandInRecord, StandInBasis, StandInGenerated, StandInContent, StandInRejection>(
            Store, Clock, Agent);
    }

    /// <summary>The save delegate a stand-in service would pass for each call.</summary>
    public SaveStep<StandInRecord, StandInBasis, StandInContent> Save =>
        (current, basis, content, version, at) => Agent.Save(Ids, current, basis, content, version, at);

    public Guid SeedRequest(ChangeRequestStatus status = ChangeRequestStatus.Approved)
    {
        var id = Ids.NewId();
        Store.Save(new ChangeRequest(
            id, "Synthetic title", "Synthetic description", "Avery Quill", "Sample Billing Portal",
            Priority.Medium, ChangeType.ConfigChange, status, Clock.Now));
        return id;
    }

    public Task<DraftingOutcome<StandInRecord, StandInRejection>> RunAsync(Guid id, CancellationToken ct = default) =>
        Engine.RunAsync(id, ct, Save);

    public int AuditCount(Guid id) => Store.GetAuditEntries(id).Count;
}
