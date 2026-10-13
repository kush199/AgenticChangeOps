using System.Collections.Concurrent;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

/// <summary>
/// The services share one in-memory store across request threads. Overlapping submit, read,
/// decide and draft calls must neither fail nor lose or mix up records.
/// </summary>
public sealed class StoreConcurrencyTests
{
    private const int Workers = 8;
    private const int PerWorker = 40;
    private const int Requests = Workers * PerWorker;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    private readonly InMemoryChangeRequestStore _store = new();
    private readonly ChangeRequestService _intake;
    private readonly ApprovalService _approval;
    private readonly RequirementsDraftingService _drafting;

    public StoreConcurrencyTests()
    {
        var clock = new SystemClock();
        var ids = new GuidIdGenerator();
        _intake = new ChangeRequestService(_store, clock, ids, new ChangeRequestValidator());
        _approval = new ApprovalService(_store, clock, ids, new ApprovalDecisionValidator());
        var generator = new FakeDraftGenerator();
        _drafting = new RequirementsDraftingService(_store, clock, ids, generator, new DraftContentValidator());
    }

    private static SubmissionRequest Sample(int n) => new(
        Title: $"Sample change {n}",
        Description: "Synthetic change used to exercise concurrent access.",
        Requester: "Avery Quill",
        AffectedSystem: "Sample Billing Portal",
        Priority: Priority.Low,
        ChangeType: ChangeType.ConfigChange);

    [Fact]
    public async Task ParallelSubmitsAndReads_LoseNothingAndThrowNothing()
    {
        var submitted = new ConcurrentBag<Guid>();
        var reads = new ConcurrentBag<Guid>();
        using var gate = new ManualResetEventSlim(false);

        var writers = Enumerable.Range(0, Workers).Select(worker => Task.Run(() =>
        {
            gate.Wait();
            for (var i = 0; i < PerWorker; i++)
            {
                var result = _intake.Submit(Sample(worker * 1000 + i));
                Assert.True(result.IsSuccess);
                submitted.Add(result.ChangeRequest!.ChangeRequestId);
            }
        }));

        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            gate.Wait();
            for (var i = 0; i < 2000; i++)
            {
                var known = submitted.ToArray();
                if (known.Length == 0)
                    continue;
                var id = known[i % known.Length];
                Assert.NotNull(_intake.GetById(id));
                Assert.NotNull(_intake.GetAuditHistory(id));
                reads.Add(id);
            }
        }));

        gate.Set();
        await Task.WhenAll(writers.Concat(readers)).WaitAsync(Limit);

        Assert.Equal(Requests, submitted.Count);
        Assert.Equal(Requests, submitted.Distinct().Count());
        foreach (var id in submitted)
        {
            Assert.NotNull(_intake.GetById(id));
            var history = _intake.GetAuditHistory(id)!;
            Assert.Single(history);
            Assert.Equal(AuditEventType.Submitted, history[0].EventType);
        }
    }

    [Fact]
    public async Task ParallelDecisionsDraftsAndReads_KeepEveryRequestConsistent()
    {
        var ids = Enumerable.Range(0, 100)
            .Select(n => _intake.Submit(Sample(n)).ChangeRequest!.ChangeRequestId)
            .ToArray();
        using var gate = new ManualResetEventSlim(false);

        var deciders = ids.Select(id => Task.Run(() =>
        {
            gate.Wait();
            return _approval.RecordDecision(new ApprovalDecisionRequest(id, "Sam Sample", ApprovalDecision.Approve, null));
        }));

        var drafters = ids.Select(id => Task.Run(async () =>
        {
            gate.Wait();
            return await _drafting.DraftAsync(id);
        }));

        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            gate.Wait();
            for (var i = 0; i < 1000; i++)
            {
                var id = ids[i % ids.Length];
                Assert.NotNull(_intake.GetById(id));
                _approval.IsClearedToProceed(id);
                Assert.NotNull(_drafting.GetDraftHistory(id).Value);
            }
        }));

        gate.Set();
        await Task.WhenAll(deciders).WaitAsync(Limit);
        await Task.WhenAll(drafters).WaitAsync(Limit);
        await Task.WhenAll(readers).WaitAsync(Limit);

        foreach (var id in ids)
        {
            var request = _intake.GetById(id)!;
            Assert.Equal(ChangeRequestStatus.Approved, request.Status);

            var history = _intake.GetAuditHistory(id)!;
            Assert.Equal(1, history.Count(e => e.EventType == AuditEventType.Approved));
            var drafts = _drafting.GetDraftHistory(id).Value!;
            Assert.Equal(drafts.Count, history.Count(e => e.EventType == AuditEventType.DraftGenerated));
            Assert.Equal(Enumerable.Range(1, drafts.Count), drafts.Select(d => d.Version));
        }
    }
}
