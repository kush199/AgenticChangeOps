using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

public sealed class DraftingTests
{
    private readonly DraftingTestFixture _f = new();

    // ---- US1: draft requirements for a change request ----

    [Fact(Skip = "synthetic")]
    public async Task Draft_SubmittedRequest_StoresVersionOneDraftWithAllSections()
    {
        var id = _f.SubmitSynthetic();
        var expected = FakeDraftGenerator.ValidContent();

        var result = await _f.Drafting.DraftAsync(id);

        Assert.True(result.IsSuccess);
        var draft = result.Draft!;
        Assert.Equal(id, draft.ChangeRequestId);
        Assert.Equal(1, draft.Version);
        Assert.Equal(DraftStatus.Draft, draft.Status);
        Assert.Equal("RequirementAgent", draft.AgentName);
        Assert.Equal(_f.Clock.UtcNow, draft.GeneratedAt);
        Assert.Equal(expected.Summary, draft.Summary);
        Assert.Equal(expected.FunctionalRequirements, draft.FunctionalRequirements);
        Assert.Equal(expected.AcceptanceCriteria, draft.AcceptanceCriteria);
        Assert.Equal(expected.Assumptions, draft.Assumptions);
        Assert.Equal(expected.OpenQuestions, draft.OpenQuestions);
        Assert.Equal(draft, Assert.Single(_f.Store.GetDrafts(id)));
    }

    [Fact(Skip = "synthetic")]
    public async Task Draft_DeferredRequest_StoresDraft()
    {
        var id = _f.SubmitSynthetic();
        _f.SeedStatus(id, ChangeRequestStatus.Deferred);

        var result = await _f.Drafting.DraftAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Draft!.Version);
    }

    [Fact(Skip = "synthetic")]
    public async Task Draft_Success_LeavesChangeRequestUnchanged()
    {
        var id = _f.SubmitSynthetic();
        var before = _f.Store.FindById(id);

        await _f.Drafting.DraftAsync(id);

        Assert.Equal(before, _f.Store.FindById(id));
        Assert.Equal(ChangeRequestStatus.Submitted, _f.Store.FindById(id)!.Status);
    }

    [Fact(Skip = "synthetic")]
    public async Task Draft_Success_AppendsExactlyOneDraftGeneratedAuditEntry()
    {
        var id = _f.SubmitSynthetic();

        var result = await _f.Drafting.DraftAsync(id);

        var history = _f.Store.GetAuditEntries(id);
        Assert.Equal(2, history.Count);
        Assert.Equal(AuditEventType.Submitted, history[0].EventType);
        var entry = history[1];
        Assert.Equal(AuditEventType.DraftGenerated, entry.EventType);
        Assert.Equal(1, entry.DraftVersion);
        Assert.Equal("RequirementAgent", entry.AgentName);
        Assert.Equal(result.Draft!.GeneratedAt, entry.OccurredAt);
        Assert.Equal(ChangeRequestStatus.Submitted, entry.StatusBefore);
        Assert.Equal(ChangeRequestStatus.Submitted, entry.StatusAfter);
        Assert.Null(entry.ApproverName);
        Assert.Null(entry.Reason);
        Assert.Null(entry.Details);
    }

    [Fact(Skip = "synthetic")]
    public async Task Draft_PaddedAndBlankItems_AreTrimmedAndDropped()
    {
        var id = _f.SubmitSynthetic();
        _f.Generator.Behavior = FakeDraftGenerator.Returns(new DraftContent(
            Summary: "  Move the report job.  ",
            FunctionalRequirements: new[] { "  Run at the new time  ", "   ", "" },
            AcceptanceCriteria: new[] { "Report arrives on time", "\t" },
            Assumptions: new[] { " " },
            OpenQuestions: new[] { "  When does the change start? " }));

        var result = await _f.Drafting.DraftAsync(id);

        Assert.True(result.IsSuccess);
        var draft = result.Draft!;
        Assert.Equal("Move the report job.", draft.Summary);
        Assert.Equal(new[] { "Run at the new time" }, draft.FunctionalRequirements);
        Assert.Equal(new[] { "Report arrives on time" }, draft.AcceptanceCriteria);
        Assert.Empty(draft.Assumptions);
        Assert.Equal(new[] { "When does the change start?" }, draft.OpenQuestions);
    }

    [Fact(Skip = "synthetic")]
    public async Task Draft_PassesTheStoredChangeRequestToTheGenerator()
    {
        var id = _f.SubmitSynthetic();

        await _f.Drafting.DraftAsync(id);

        Assert.Equal(1, _f.Generator.CallCount);
        Assert.Equal(_f.Store.FindById(id), _f.Generator.LastRequest);
    }

    // ---- US2: redraft creates a new version ----

    [Fact(Skip = "synthetic")]
    public async Task Redraft_CreatesVersionTwoAndKeepsVersionOneUnchanged()
    {
        var id = _f.SubmitSynthetic();
        var first = (await _f.Drafting.DraftAsync(id)).Draft!;

        var second = (await _f.Drafting.DraftAsync(id)).Draft!;

        Assert.Equal(2, second.Version);
        Assert.Equal(first, _f.Store.GetDrafts(id)[0]);
    }

    [Fact(Skip = "synthetic")]
    public async Task Redraft_NTimes_YieldsVersionsOneToNWithoutGaps()
    {
        var id = _f.SubmitSynthetic();

        for (var i = 0; i < 5; i++)
            await _f.Drafting.DraftAsync(id);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, _f.Store.GetDrafts(id).Select(d => d.Version));
    }

    [Fact(Skip = "synthetic")]
    public async Task Redraft_TwoRequests_NumberVersionsIndependently()
    {
        var a = _f.SubmitSynthetic();
        var b = _f.SubmitSynthetic();
        await _f.Drafting.DraftAsync(a);
        await _f.Drafting.DraftAsync(a);

        var firstForB = (await _f.Drafting.DraftAsync(b)).Draft!;

        Assert.Equal(1, firstForB.Version);
        Assert.Equal(2, _f.Store.GetDrafts(a).Count);
    }

    [Fact(Skip = "synthetic")]
    public async Task Redraft_WithDifferentContent_KeepsEachVersionsOwnContent()
    {
        var id = _f.SubmitSynthetic();
        await _f.Drafting.DraftAsync(id);
        _f.Generator.Behavior = FakeDraftGenerator.Returns(
            FakeDraftGenerator.ValidContent() with { Summary = "A revised summary." });

        await _f.Drafting.DraftAsync(id);

        var drafts = _f.Store.GetDrafts(id);
        Assert.Equal(FakeDraftGenerator.ValidContent().Summary, drafts[0].Summary);
        Assert.Equal("A revised summary.", drafts[1].Summary);
    }

    [Fact(Skip = "synthetic")]
    public async Task Redraft_OverlappingCalls_TakeDistinctConsecutiveVersions()
    {
        var id = _f.SubmitSynthetic();
        var gate = new TaskCompletionSource();
        _f.Generator.Behavior = async (_, _) =>
        {
            await gate.Task;
            return FakeDraftGenerator.ValidContent();
        };

        var first = _f.Drafting.DraftAsync(id);
        var second = _f.Drafting.DraftAsync(id);
        Assert.True(SpinWait.SpinUntil(() => _f.Generator.CallCount == 2, TimeSpan.FromSeconds(5)));
        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Equal(new[] { 1, 2 }, results.Select(r => r.Draft!.Version).OrderBy(v => v));
        Assert.Equal(new[] { 1, 2 }, _f.Store.GetDrafts(id).Select(d => d.Version));
    }

    [Fact(Skip = "synthetic")]
    public async Task Redraft_EachStoredDraftHasItsOwnAuditEntry()
    {
        var id = _f.SubmitSynthetic();
        await _f.Drafting.DraftAsync(id);
        await _f.Drafting.DraftAsync(id);
        await _f.Drafting.DraftAsync(id);

        var versions = _f.Store.GetAuditEntries(id)
            .Where(e => e.EventType == AuditEventType.DraftGenerated)
            .Select(e => e.DraftVersion);

        Assert.Equal(new int?[] { 1, 2, 3 }, versions);
    }
}
