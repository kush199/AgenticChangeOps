using AgenticChangeOps.Agents.Reviewer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>US5: the review continues when the sample cannot be looked at, and says the check was not performed.</summary>
public sealed class ReviewSampleUnavailableTests
{
    private readonly ReviewTestFixture _f = new();

    [Fact]
    public async Task WhenTheSampleIsUnavailable_TheReviewIsStored_WithTheCheckNotPerformed()
    {
        var id = _f.ApproveWithBases();
        _f.Sample.IsAvailable = false;

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SystemCheckStatus.NotPerformed, result.Review!.SystemCheck.Status);
        Assert.Empty(result.Review.SystemCheck.Edits);
        Assert.Equal(2, result.Review.Findings.Count);
        Assert.Equal(1, _f.AuditCount(id, AuditEventType.ReviewGenerated));
    }

    [Fact]
    public async Task TheModelIsToldTheCheckWasNotPerformed_AndNothingWasRead()
    {
        var id = _f.ApproveWithBases();
        _f.Sample.IsAvailable = false;

        await _f.Reviewing.ReviewAsync(id);

        Assert.Equal(1, _f.Generator.CallCount);
        Assert.Equal(SystemCheckStatus.NotPerformed, _f.Generator.LastCheck!.Status);
        Assert.Empty(_f.Generator.LastCheck.Edits);
        Assert.Equal(0, _f.Sample.ProbeCount);
        Assert.Equal(0, _f.Sample.ReadCount);
    }

    [Fact]
    public async Task OneFileThatCannotBeRead_IsFileNotFoundForThatEdit_AndTheCheckIsStillPerformed()
    {
        var id = _f.ApproveWithBases();
        _f.Sample.WithUnreadable(FakeReviewBasisSource.SettingsPath);

        var r = (await _f.Reviewing.ReviewAsync(id)).Review!;

        Assert.Equal(SystemCheckStatus.Performed, r.SystemCheck.Status);
        Assert.Equal(
            new[] { EditCheckOutcome.FileNotFound, EditCheckOutcome.NewFileFree, EditCheckOutcome.Found },
            r.SystemCheck.Edits.Select(e => e.Outcome));
    }

    [Fact]
    public async Task AnUnavailableSample_NeverMakesAnyOtherRuleLooser()
    {
        var id = _f.ApproveWithBases();
        _f.Sample.IsAvailable = false;
        _f.Generator.Behavior = FakeReviewGenerator.Returns(new ReviewContent(
            new[] { FakeReviewGenerator.Finding(references: "proposal.changes[99]") }, Array.Empty<string>()));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReviewRejection.UnresolvedReference, result.Rejection);
        Assert.Empty(_f.Store.GetAgentOutputs<Review>(id));
    }
}
