using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>US1: an eligible request stores a typed, versioned, audited proposal and changes nothing else.</summary>
public sealed class ProposalServiceStoreTests
{
    private readonly ProposalTestFixture _f = new();

    private static ToolCallRecord Call(string tool, string target, int bytes = 100) =>
        new(tool, target, ToolCallOutcome.Served, bytes, TimeSpan.FromMilliseconds(4),
            new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task Success_StoresATypedProposal_WithEveryRequiredPart()
    {
        var id = _f.ApproveWithDrafts(requirementsVersion: 2, designVersion: 3);

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.True(result.IsSuccess);
        var p = result.Proposal!;
        Assert.Equal(id, p.ChangeRequestId);
        Assert.Equal(1, p.Version);
        Assert.Equal(2, p.RequirementsVersion);
        Assert.Equal(3, p.DesignVersion);
        Assert.Equal(_f.Clock.UtcNow, p.GeneratedAt);
        Assert.Equal(ProposalStatus.Draft, p.Status);
        Assert.Equal("DeveloperAgent", p.AgentName);
        Assert.Equal("Not compiled, run or tested.", p.VerificationNote);
        Assert.False(string.IsNullOrWhiteSpace(p.Summary));
        var change = Assert.Single(p.Changes);
        Assert.Equal(ProposalChangeKind.Change, change.Kind);
        Assert.Equal("report_hour = 2", change.ExistingText);
        Assert.NotEmpty(p.Risks);
        Assert.NotEmpty(p.TestIdeas);
        Assert.NotEmpty(p.OpenQuestions);
        Assert.Equal(new[] { FakeProposalGenerator.SettingsPath }, p.SampleFilesRead);
        Assert.Equal(p, Assert.Single(_f.Store.GetAgentOutputs<ChangeProposal>(id)));
    }

    [Fact]
    public async Task TheVerificationNote_IsSetBySystem_NotByTheModel()
    {
        var id = _f.ApproveWithDrafts();

        var p = (await _f.Proposing.ProposeAsync(id)).Proposal!;

        Assert.Equal(ChangeProposal.VerificationNoteText, p.VerificationNote);
        Assert.Equal(ChangeProposal.DeveloperAgentName, p.AgentName);
    }

    [Fact]
    public async Task FilesRead_AndToolCalls_ComeFromTheSession_NotFromTheModelText()
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = FakeProposalGenerator.Returns(FakeProposalGenerator.ValidContent() with
        {
            SampleFilesRead = new[] { "orders-service/src/OrderIntake" },
            ToolCalls = new[] { Call("read_file", "orders-service/src/OrderIntake") }
        });

        var p = (await _f.Proposing.ProposeAsync(id)).Proposal!;

        Assert.Equal(new[] { "orders-service/src/OrderIntake" }, p.SampleFilesRead);
    }

    [Fact]
    public async Task OneToolCalledEntryPerCall_InOrder_ThenOneProposalGeneratedEntry()
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = FakeProposalGenerator.Returns(FakeProposalGenerator.ValidContent() with
        {
            ToolCalls = new[]
            {
                Call("list_files", "billing-portal"),
                Call("read_file", FakeProposalGenerator.SettingsPath, 321)
            }
        });

        await _f.Proposing.ProposeAsync(id);

        var added = _f.Store.GetAuditEntries(id).Skip(2).ToArray();   // after Submitted and Approved
        Assert.Equal(
            new[] { AuditEventType.ToolCalled, AuditEventType.ToolCalled, AuditEventType.ProposalGenerated },
            added.Select(e => e.EventType));
        Assert.All(added, e => Assert.Equal("DeveloperAgent", e.AgentName));
        Assert.Equal("list_files", added[0].ToolName);
        Assert.Equal("billing-portal", added[0].ToolTarget);
        Assert.Equal("read_file", added[1].ToolName);
        Assert.Equal(321, added[1].ResultBytes);
        Assert.Equal("Served", added[1].ToolOutcome);
        Assert.Equal(1, added[2].DraftVersion);
        Assert.Equal(added[2].StatusBefore, added[2].StatusAfter);
        Assert.Null(added[2].Automatic);
    }

    [Fact]
    public async Task AToolTarget_HasControlCharactersRemoved_AndIsTruncated()
    {
        var id = _f.ApproveWithDrafts();
        var target = "a\nb\t" + new string('x', 300);
        _f.Generator.Behavior = FakeProposalGenerator.Returns(FakeProposalGenerator.ValidContent() with
        {
            ToolCalls = new[] { Call("search_text", target) }
        });

        await _f.Proposing.ProposeAsync(id);

        var entry = _f.Store.GetAuditEntries(id).Single(e => e.EventType == AuditEventType.ToolCalled);
        Assert.Equal(120, entry.ToolTarget!.Length);
        Assert.DoesNotContain('\n', entry.ToolTarget);
        Assert.StartsWith("abxx", entry.ToolTarget);
    }

    [Fact]
    public async Task Proposing_ChangesNoStatus_AndAddsNoOtherRecord()
    {
        var id = _f.ApproveWithDrafts();
        var before = _f.Store.FindById(id);

        await _f.Proposing.ProposeAsync(id);

        Assert.Equal(before, _f.Store.FindById(id));
        Assert.Equal(ChangeRequestStatus.Approved, _f.Store.FindById(id)!.Status);
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.DesignGenerated));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.DraftGenerated));
    }

    [Fact]
    public async Task TheGenerator_IsCalledOnce_AndGivenTheLatestDrafts()
    {
        var id = _f.ApproveWithDrafts(requirementsVersion: 4, designVersion: 2);

        await _f.Proposing.ProposeAsync(id);

        Assert.Equal(1, _f.Generator.CallCount);
        Assert.Equal(4, _f.Generator.LastRequirements!.Version);
        Assert.Equal(2, _f.Generator.LastDesign!.Version);
        Assert.Equal(id, _f.Generator.LastRequest!.ChangeRequestId);
    }
}
