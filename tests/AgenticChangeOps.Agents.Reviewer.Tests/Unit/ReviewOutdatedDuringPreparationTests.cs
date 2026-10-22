using AgenticChangeOps.Agents.Reviewer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>
/// US5 / FR-015: the requirements, design and proposal are checked again just before storing. If any
/// stopped being the latest while the review was being prepared, the review is refused as outdated
/// and nothing is stored, whatever the sample check or the model said.
/// </summary>
public sealed class ReviewOutdatedDuringPreparationTests
{
    private readonly ReviewTestFixture _f = new();

    private void ChangeBasisWhileGenerating(Guid id, ReviewBases newer)
    {
        _f.Generator.Behavior = _ =>
        {
            _f.Basis.Set(id, newer);
            return Task.FromResult<ReviewContent?>(FakeReviewGenerator.ValidContent());
        };
    }

    private void AssertNothingStored(Guid id)
    {
        Assert.Empty(_f.Store.GetAgentOutputs<Review>(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ReviewGenerated));
        Assert.Equal(ChangeRequestStatus.Approved, _f.Store.FindById(id)!.Status);
    }

    [Fact]
    public async Task ANewerRequirementsDraft_SavedWhilePreparing_RefusesAndStoresNothing()
    {
        var id = _f.ApproveWithBases(1, 1, 1);
        ChangeBasisWhileGenerating(id, new ReviewBases(
            FakeReviewBasisSource.Requirements(2),
            FakeReviewBasisSource.Design(1, 1),
            FakeReviewBasisSource.Proposal(1, 1, 1)));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("DesignDraftVersion", Assert.Single(result.Errors).FieldName);
        AssertNothingStored(id);
    }

    [Fact]
    public async Task ANewerDesignDraft_SavedWhilePreparing_RefusesAndStoresNothing()
    {
        var id = _f.ApproveWithBases(1, 1, 1);
        ChangeBasisWhileGenerating(id, new ReviewBases(
            FakeReviewBasisSource.Requirements(1),
            FakeReviewBasisSource.Design(2, 1),
            FakeReviewBasisSource.Proposal(1, 1, 1)));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("ProposalVersion", Assert.Single(result.Errors).FieldName);
        AssertNothingStored(id);
    }

    [Fact]
    public async Task ANewerProposal_SavedWhilePreparing_RefusesAndStoresNothing()
    {
        var id = _f.ApproveWithBases(1, 1, 1);
        ChangeBasisWhileGenerating(id, FakeReviewBasisSource.Current(1, 1, 2));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("ProposalVersion", Assert.Single(result.Errors).FieldName);
        AssertNothingStored(id);
    }

    [Fact]
    public async Task ANewerSetOfAllThree_SavedWhilePreparing_RefusesAndStoresNothing()
    {
        var id = _f.ApproveWithBases(1, 1, 1);
        ChangeBasisWhileGenerating(id, FakeReviewBasisSource.Current(2, 2, 2));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("ProposalVersion", Assert.Single(result.Errors).FieldName);
        AssertNothingStored(id);
    }

    [Fact]
    public async Task ARecordThatDisappearsWhilePreparing_RefusesAndStoresNothing()
    {
        var id = _f.ApproveWithBases();
        ChangeBasisWhileGenerating(id, new ReviewBases(FakeReviewBasisSource.Requirements(), FakeReviewBasisSource.Design(), null));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Proposal", Assert.Single(result.Errors).FieldName);
        AssertNothingStored(id);
    }

    [Fact]
    public async Task TheSystemCheckComputedEarlier_IsDiscardedWithTheRefusedReview()
    {
        var id = _f.ApproveWithBases(1, 1, 1);
        ChangeBasisWhileGenerating(id, FakeReviewBasisSource.Current(1, 1, 2));

        await _f.Reviewing.ReviewAsync(id);

        Assert.NotNull(_f.Generator.LastCheck); // it was computed and shown to the model...
        Assert.Empty(_f.Reviewing.GetReviewHistory(id).Value!); // ...but no review, with or without a check, exists
    }

    [Fact]
    public async Task ARefusedAsOutdatedRequest_DoesNotUseUpAVersion()
    {
        var id = _f.ApproveWithBases(1, 1, 1);
        ChangeBasisWhileGenerating(id, FakeReviewBasisSource.Current(1, 1, 2));
        await _f.Reviewing.ReviewAsync(id);

        _f.Generator.Behavior = FakeReviewGenerator.Returns(FakeReviewGenerator.ValidContent());
        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Review!.Version);
        Assert.Equal(2, result.Review.ProposalVersion);
    }

    [Fact]
    public async Task WhenNothingChanged_TheReviewIsStored_WithTheVersionsTheModelWasGiven()
    {
        var id = _f.ApproveWithBases(1, 1, 1);
        _f.Generator.Behavior = _ =>
        {
            _f.Basis.Set(id, FakeReviewBasisSource.Current(1, 1, 1)); // set again to the same versions
            return Task.FromResult<ReviewContent?>(FakeReviewGenerator.ValidContent());
        };

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal((1, 1, 1), (result.Review!.RequirementsVersion, result.Review.DesignVersion, result.Review.ProposalVersion));
    }
}
