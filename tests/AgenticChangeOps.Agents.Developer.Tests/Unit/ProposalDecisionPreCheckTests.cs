using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>
/// The service checks eligibility (decision included) before the engine, and only a decision error
/// raised after that first check is turned into the outdated-design error.
/// </summary>
public sealed class ProposalDecisionPreCheckTests
{
    private readonly ProposalTestFixture _f = new();

    [Fact]
    public async Task ADecisionRefusalFromTheFirstCheck_IsReturnedUnchanged_AndMakesNoGeneratorCall()
    {
        var id = _f.ApproveWithDrafts();
        _f.Basis.SetDecision(id, ProposalDesignVerdict.Rejected);

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.Equal("DesignAcceptance", Assert.Single(result.Errors).FieldName);
        Assert.Null(result.FailureCategory);
        Assert.Null(result.Rejection);
        Assert.Equal(0, _f.Generator.CallCount);
    }

    [Fact]
    public async Task AGeneratorFailure_AfterAPassingCheck_IsStillAGenerationFailure()
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = FakeProposalGenerator.ReturnsNull();

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain(result.Errors, e => e.FieldName == "DesignAcceptance");
        Assert.DoesNotContain(result.Errors, e => e.FieldName == "DesignDraftVersion");
        Assert.Empty(_f.Store.GetAgentOutputs<ChangeProposal>(id));
    }

    [Fact]
    public async Task ADecisionError_AfterAPassingCheck_IsReportedAsOutdated_NeverAsADecisionRefusal()
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = _ =>
        {
            _f.Basis.SetDecision(id, ProposalDesignVerdict.None);
            return Task.FromResult<ProposalContent?>(FakeProposalGenerator.ValidContent());
        };

        var result = await _f.Proposing.ProposeAsync(id);

        var error = Assert.Single(result.Errors);
        Assert.Equal("DesignDraftVersion", error.FieldName);
        Assert.Equal(1, _f.Generator.CallCount);
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ProposalGenerated));
    }

    [Fact]
    public async Task ContentTheSystemRejects_AfterAPassingCheck_IsTheSameRejectionAsBefore()
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = FakeProposalGenerator.Returns(FakeProposalGenerator.ValidContent() with
        {
            Changes = new[]
            {
                new ProposedChangeContent(FakeProposalGenerator.SettingsPath, "change", "text that is not in the file", "x")
            }
        });

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProposalRejection.QuoteNotFound, result.Rejection);
        Assert.DoesNotContain(result.Errors, e => e.FieldName is "DesignAcceptance" or "DesignDraftVersion");
        Assert.Empty(_f.Store.GetAgentOutputs<ChangeProposal>(id));
    }

    [Fact]
    public async Task AStorageFailure_AfterAPassingCheck_PropagatesAsBefore_NeverAsAnOutdatedDesign()
    {
        var failing = new ProposalSaveFailingStore(
            _f.Store, new StorageUnavailableException(StorageFailureCategory.Unavailable));
        var service = new ProposalService(
            failing, _f.Clock, _f.Ids, _f.Basis, _f.Generator,
            new ProposalContentValidator(), new ProposalVerifier(_f.Sample, ProposalLimits.Defaults));
        var id = _f.ApproveWithDrafts();

        await Assert.ThrowsAsync<StorageUnavailableException>(() => service.ProposeAsync(id));

        Assert.Empty(_f.Store.GetAgentOutputs<ChangeProposal>(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ProposalGenerated));
    }
}
