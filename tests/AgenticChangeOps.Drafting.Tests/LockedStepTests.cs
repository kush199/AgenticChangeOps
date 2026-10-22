// Blocking is deliberate: the save step is synchronous (no await is allowed inside the locked step),
// so the test must wait for the other thread from inside it.
#pragma warning disable xUnit1031
using AgenticChangeOps.Drafting.Tests.Fakes;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Drafting.Tests;

/// <summary>The uninterrupted step that re-checks, versions and saves.</summary>
public sealed class LockedStepTests
{
    private sealed class CountingClock : ISystemClock
    {
        public int Reads { get; private set; }

        public DateTime UtcNow
        {
            get
            {
                Reads++;
                return new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            }
        }
    }

    private readonly EngineTestFixture _f = new();

    [Fact]
    public async Task ARequestThatBecameIneligibleAfterGeneration_ReturnsTheAgentsError_AndStoresNothing()
    {
        var id = _f.SeedRequest();
        _f.Agent.DuringGeneration = () => _f.Store.Update(_f.Store.FindById(id)! with { Status = ChangeRequestStatus.Submitted });

        var outcome = await _f.RunAsync(id);

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.FailureCategory);
        Assert.Equal(StandInAgent.NotEligibleField, Assert.Single(outcome.Errors).FieldName);
        Assert.Equal(new[] { "eligibility", "generate", "review", "eligibility" }, _f.Agent.Calls);
        Assert.Empty(_f.Store.GetAgentOutputs<StandInRecord>(id));
        Assert.Equal(0, _f.AuditCount(id));
    }

    [Fact]
    public async Task ABasisThatChangedAfterGeneration_ReturnsTheAgentsCurrencyError_AndStoresNothing()
    {
        var id = _f.SeedRequest();
        _f.Agent.DuringGeneration = () => _f.Agent.CurrentBasis = new StandInBasis(2);

        var outcome = await _f.RunAsync(id);

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.FailureCategory);
        Assert.Equal(StandInAgent.BasisField, Assert.Single(outcome.Errors).FieldName);
        Assert.DoesNotContain("save", _f.Agent.Calls);
        Assert.Empty(_f.Store.GetAgentOutputs<StandInRecord>(id));
    }

    [Fact]
    public async Task TheVersionIsTheCountOfStoredOutputsPlusOne_AndTheSavedRecordCarriesTheBasisUsedForGeneration()
    {
        var id = _f.SeedRequest();

        var first = await _f.RunAsync(id);
        var second = await _f.RunAsync(id);

        Assert.Equal(1, first.Record!.Version);
        Assert.Equal(2, second.Record!.Version);
        Assert.Equal(1, first.Record.BasisVersion);
        Assert.Equal(new[] { 1, 2 }, _f.Store.GetAgentOutputs<StandInRecord>(id).Select(r => r.Version));
    }

    [Fact]
    public async Task TheClockIsReadOnce_AndItsValueIsPassedToTheSaveStep()
    {
        var clock = new CountingClock();
        var agent = new StandInAgent(_f.Store);
        var engine = new DraftingEngine<StandInRecord, StandInBasis, StandInGenerated, StandInContent, StandInRejection>(
            _f.Store, clock, agent);
        var id = _f.SeedRequest();
        DateTime? seen = null;

        var outcome = await engine.RunAsync(id, CancellationToken.None, (current, basis, content, version, at) =>
        {
            seen = at;
            return agent.Save(_f.Ids, current, basis, content, version, at);
        });

        Assert.True(outcome.IsSuccess);
        Assert.Equal(1, clock.Reads);
        Assert.Equal(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc), seen);
        Assert.Equal(seen, _f.Store.GetAuditEntries(id).Single().OccurredAt);
    }

    [Fact]
    public async Task TheSaveStepRunsWhileTheStoreLockIsHeld_SoNoOtherThreadCanEnter()
    {
        var id = _f.SeedRequest();
        var heldByCaller = false;
        var otherThreadGotIn = true;

        var outcome = await _f.Engine.RunAsync(id, CancellationToken.None, (current, basis, content, version, at) =>
        {
            heldByCaller = Monitor.IsEntered(_f.Store.SyncRoot);
            otherThreadGotIn = Task.Run(() =>
            {
                var entered = Monitor.TryEnter(_f.Store.SyncRoot, 0);
                if (entered)
                    Monitor.Exit(_f.Store.SyncRoot);
                return entered;
            }).GetAwaiter().GetResult();
            return _f.Agent.Save(_f.Ids, current, basis, content, version, at);
        });

        Assert.True(outcome.IsSuccess);
        Assert.True(heldByCaller);
        Assert.False(otherThreadGotIn);
    }
}
