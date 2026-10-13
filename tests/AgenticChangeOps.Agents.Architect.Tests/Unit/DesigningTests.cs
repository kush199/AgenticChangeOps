using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesigningTests
{
    private readonly DesigningTestFixture _f = new();

    [Fact]
    public async Task Design_ApprovedRequestWithBasis_StoresVersionOneWithAllSections()
    {
        var id = _f.ApproveWithBasis();
        var expected = FakeDesignGenerator.ValidContent();

        var result = await _f.Designing.DesignAsync(id);

        Assert.True(result.IsSuccess);
        var draft = result.Draft!;
        Assert.Equal(id, draft.ChangeRequestId);
        Assert.Equal(1, draft.Version);
        Assert.Equal(DesignDraftStatus.Draft, draft.Status);
        Assert.Equal("ArchitectAgent", draft.AgentName);
        Assert.Equal(_f.Clock.UtcNow, draft.GeneratedAt);
        Assert.Equal(expected.ApproachSummary, draft.ApproachSummary);
        Assert.Equal(expected.AffectedComponents, draft.AffectedComponents);
        Assert.Equal(expected.ImpactAndRisks, draft.ImpactAndRisks);
        Assert.Equal(expected.Dependencies, draft.Dependencies);
        Assert.Equal(expected.RollbackConsiderations, draft.RollbackConsiderations);
        Assert.Equal(expected.OpenQuestions, draft.OpenQuestions);
        Assert.Equal(draft, Assert.Single(_f.Store.GetDesigns(id)));
    }

    [Fact]
    public async Task Design_RecordsTheRequirementsVersionTheModelWasGiven()
    {
        var id = _f.ApproveWithBasis(basisVersion: 3);

        var result = await _f.Designing.DesignAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Draft!.RequirementsVersion);
        Assert.Equal(3, _f.Generator.LastBasis!.Version);
    }

    [Fact]
    public async Task Design_Success_LeavesChangeRequestUnchanged()
    {
        var id = _f.ApproveWithBasis();
        var before = _f.Store.FindById(id);

        await _f.Designing.DesignAsync(id);

        Assert.Equal(before, _f.Store.FindById(id));
        Assert.Equal(ChangeRequestStatus.Approved, _f.Store.FindById(id)!.Status);
        Assert.True(_f.Approval.IsClearedToProceed(id));
    }

    [Fact]
    public async Task Design_Success_AppendsExactlyOneDesignGeneratedAuditEntryAfterEarlierEntries()
    {
        var id = _f.ApproveWithBasis();
        var earlier = _f.Store.GetAuditEntries(id);

        var result = await _f.Designing.DesignAsync(id);

        var history = _f.Store.GetAuditEntries(id);
        Assert.Equal(earlier.Count + 1, history.Count);
        Assert.Equal(earlier, history.Take(earlier.Count));
        var entry = history[^1];
        Assert.Equal(AuditEventType.DesignGenerated, entry.EventType);
        Assert.Equal(1, entry.DraftVersion);
        Assert.Equal("ArchitectAgent", entry.AgentName);
        Assert.Equal(result.Draft!.GeneratedAt, entry.OccurredAt);
        Assert.Equal(ChangeRequestStatus.Approved, entry.StatusBefore);
        Assert.Equal(ChangeRequestStatus.Approved, entry.StatusAfter);
        Assert.Null(entry.ApproverName);
        Assert.Null(entry.Reason);
        Assert.Null(entry.Details);
    }

    [Fact]
    public async Task Design_PassesTheStoredRequestAndTheBasisToTheGeneratorOnce()
    {
        var id = _f.ApproveWithBasis(basisVersion: 2);

        await _f.Designing.DesignAsync(id);

        Assert.Equal(1, _f.Generator.CallCount);
        Assert.Equal(_f.Store.FindById(id), _f.Generator.LastRequest);
        ContentAssert.Equal(FakeRequirementsBasisSource.Basis(2), _f.Generator.LastBasis);
    }

    [Fact]
    public async Task Design_PaddedAndBlankItems_AreTrimmedAndDropped()
    {
        var id = _f.ApproveWithBasis();
        _f.Generator.Behavior = FakeDesignGenerator.Returns(new DesignContent(
            ApproachSummary: "  Change the schedule entry.  ",
            AffectedComponents: new[] { "  Scheduler  ", "   ", "" },
            ImpactAndRisks: new[] { "Later delivery", "\t" },
            Dependencies: new[] { " " },
            RollbackConsiderations: new[] { "  Restore the old entry " },
            OpenQuestions: Array.Empty<string>()));

        var result = await _f.Designing.DesignAsync(id);

        Assert.True(result.IsSuccess);
        var draft = result.Draft!;
        Assert.Equal("Change the schedule entry.", draft.ApproachSummary);
        Assert.Equal(new[] { "Scheduler" }, draft.AffectedComponents);
        Assert.Equal(new[] { "Later delivery" }, draft.ImpactAndRisks);
        Assert.Empty(draft.Dependencies);
        Assert.Equal(new[] { "Restore the old entry" }, draft.RollbackConsiderations);
        Assert.Empty(draft.OpenQuestions);
    }
}
