using AgenticChangeOps.Agents.Reviewer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>
/// US3: every ineligible request is refused with its own field, with no model call and nothing
/// stored. Order: existence, status, requirements draft, design draft, design current, proposal,
/// proposal current.
/// </summary>
public sealed class ReviewEligibilityTests
{
    private readonly ReviewTestFixture _f = new();

    private void AssertNothingHappened(Guid id)
    {
        Assert.Equal(0, _f.Generator.CallCount);
        Assert.Equal(0, _f.Sample.ProbeCount);
        Assert.Equal(0, _f.Sample.ReadCount);
        Assert.Empty(_f.Store.GetAgentOutputs<Review>(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ReviewGenerated));
    }

    private static string FieldOf(ReviewResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.FailureCategory);
        Assert.Null(result.Rejection);
        return Assert.Single(result.Errors).FieldName;
    }

    // ---- the id -----------------------------------------------------------------------

    [Fact]
    public async Task AnUnknownChangeRequest_IsReportedOnTheIdField()
    {
        var result = await _f.Reviewing.ReviewAsync(Guid.NewGuid());

        Assert.Equal("ChangeRequestId", FieldOf(result));
        Assert.Equal(0, _f.Generator.CallCount);
        Assert.Equal(0, _f.Basis.CallCount);
    }

    [Fact]
    public async Task AnEmptyIdentifier_IsTheMalformedCase_AtTheInternalService()
    {
        // A malformed text identifier can only arrive over HTTP, which is a later slice. Here a
        // malformed identifier is the empty one, and it is refused like an unknown one.
        var result = await _f.Reviewing.ReviewAsync(Guid.Empty);

        Assert.Equal("ChangeRequestId", FieldOf(result));
        Assert.Equal(0, _f.Generator.CallCount);
        Assert.Equal(0, _f.Basis.CallCount);
    }

    // ---- status -----------------------------------------------------------------------

    [Theory]
    [InlineData(ChangeRequestStatus.Submitted)]
    [InlineData(ChangeRequestStatus.UnderReview)]
    [InlineData(ChangeRequestStatus.Rejected)]
    [InlineData(ChangeRequestStatus.Deferred)]
    public async Task AnyStatusButApproved_IsRefused_AndTheBasisIsNotConsulted(ChangeRequestStatus status)
    {
        var id = _f.SubmitSynthetic();
        _f.Basis.Set(id, FakeReviewBasisSource.Current());
        _f.SeedStatus(id, status);

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.Equal("Status", FieldOf(result));
        Assert.Equal(0, _f.Basis.CallCount);
        AssertNothingHappened(id);
    }

    // ---- missing records, one distinct field each -------------------------------------

    [Fact]
    public async Task NoRequirementsDraft_IsRefused_OnTheRequirementsField()
    {
        var id = _f.ApproveWithBases();
        _f.Basis.Set(id, new ReviewBases(null, null, null));

        Assert.Equal("RequirementsDraft", FieldOf(await _f.Reviewing.ReviewAsync(id)));
        AssertNothingHappened(id);
    }

    [Fact]
    public async Task NoDesignDraft_IsRefused_OnTheDesignField()
    {
        var id = _f.ApproveWithBases();
        _f.Basis.Set(id, new ReviewBases(FakeReviewBasisSource.Requirements(), null, FakeReviewBasisSource.Proposal()));

        Assert.Equal("DesignDraft", FieldOf(await _f.Reviewing.ReviewAsync(id)));
        AssertNothingHappened(id);
    }

    [Fact]
    public async Task NoProposal_IsRefused_OnTheProposalField()
    {
        var id = _f.ApproveWithBases();
        _f.Basis.Set(id, new ReviewBases(FakeReviewBasisSource.Requirements(), FakeReviewBasisSource.Design(), null));

        Assert.Equal("Proposal", FieldOf(await _f.Reviewing.ReviewAsync(id)));
        AssertNothingHappened(id);
    }

    // ---- outdated records -------------------------------------------------------------

    [Fact]
    public async Task ADesignBasedOnOlderRequirements_IsRefused_OnTheDesignVersionField()
    {
        var id = _f.ApproveWithBases();
        _f.Basis.Set(id, new ReviewBases(
            FakeReviewBasisSource.Requirements(version: 3),
            FakeReviewBasisSource.Design(version: 1, requirementsVersion: 2),
            FakeReviewBasisSource.Proposal(version: 1, requirementsVersion: 2, designVersion: 1)));

        Assert.Equal("DesignDraftVersion", FieldOf(await _f.Reviewing.ReviewAsync(id)));
        AssertNothingHappened(id);
    }

    [Fact]
    public async Task AProposalBasedOnAnOlderDesign_IsRefused_OnTheProposalVersionField()
    {
        var id = _f.ApproveWithBases();
        _f.Basis.Set(id, new ReviewBases(
            FakeReviewBasisSource.Requirements(1),
            FakeReviewBasisSource.Design(version: 2, requirementsVersion: 1),
            FakeReviewBasisSource.Proposal(version: 1, requirementsVersion: 1, designVersion: 1)));

        Assert.Equal("ProposalVersion", FieldOf(await _f.Reviewing.ReviewAsync(id)));
        AssertNothingHappened(id);
    }

    [Fact]
    public async Task AProposalBasedOnOlderRequirements_IsRefused_OnTheProposalVersionField()
    {
        // The real services cannot produce this once the design is current, so it is built with a fake.
        var id = _f.ApproveWithBases();
        _f.Basis.Set(id, new ReviewBases(
            FakeReviewBasisSource.Requirements(version: 2),
            FakeReviewBasisSource.Design(version: 1, requirementsVersion: 2),
            FakeReviewBasisSource.Proposal(version: 1, requirementsVersion: 1, designVersion: 1)));

        Assert.Equal("ProposalVersion", FieldOf(await _f.Reviewing.ReviewAsync(id)));
        AssertNothingHappened(id);
    }

    // ---- order of the checks ----------------------------------------------------------

    [Fact]
    public async Task WhenEverythingIsMissing_TheRequirementsDraftIsReportedFirst()
    {
        var id = _f.ApproveWithBases();
        _f.Basis.Set(id, new ReviewBases(null, null, null));

        Assert.Equal("RequirementsDraft", FieldOf(await _f.Reviewing.ReviewAsync(id)));
    }

    [Fact]
    public async Task WhenTheDesignAndTheProposalAreMissing_TheDesignIsReportedFirst()
    {
        var id = _f.ApproveWithBases();
        _f.Basis.Set(id, new ReviewBases(FakeReviewBasisSource.Requirements(), null, null));

        Assert.Equal("DesignDraft", FieldOf(await _f.Reviewing.ReviewAsync(id)));
    }

    [Fact]
    public async Task WhenTheDesignIsOutdatedAndTheProposalIsMissing_TheDesignVersionIsReportedFirst()
    {
        var id = _f.ApproveWithBases();
        _f.Basis.Set(id, new ReviewBases(
            FakeReviewBasisSource.Requirements(version: 3),
            FakeReviewBasisSource.Design(version: 1, requirementsVersion: 2),
            null));

        Assert.Equal("DesignDraftVersion", FieldOf(await _f.Reviewing.ReviewAsync(id)));
    }

    [Fact]
    public async Task WhenTheProposalIsMissing_AndTheDesignIsCurrent_TheProposalIsReported()
    {
        var id = _f.ApproveWithBases();
        _f.Basis.Set(id, new ReviewBases(FakeReviewBasisSource.Requirements(), FakeReviewBasisSource.Design(), null));

        Assert.Equal("Proposal", FieldOf(await _f.Reviewing.ReviewAsync(id)));
    }

    // ---- messages ---------------------------------------------------------------------

    [Theory]
    [InlineData("Status")]
    [InlineData("RequirementsDraft")]
    [InlineData("DesignDraft")]
    [InlineData("DesignDraftVersion")]
    [InlineData("Proposal")]
    [InlineData("ProposalVersion")]
    public async Task TheMessages_AreFixedText_WithNoRecordContent(string field)
    {
        var id = _f.ApproveWithBases();
        switch (field)
        {
            case "Status":
                _f.SeedStatus(id, ChangeRequestStatus.Deferred);
                break;
            case "RequirementsDraft":
                _f.Basis.Set(id, new ReviewBases(null, null, null));
                break;
            case "DesignDraft":
                _f.Basis.Set(id, new ReviewBases(FakeReviewBasisSource.Requirements(), null, null));
                break;
            case "DesignDraftVersion":
                _f.Basis.Set(id, new ReviewBases(
                    FakeReviewBasisSource.Requirements(3), FakeReviewBasisSource.Design(1, 2), FakeReviewBasisSource.Proposal(1, 2, 1)));
                break;
            case "Proposal":
                _f.Basis.Set(id, new ReviewBases(FakeReviewBasisSource.Requirements(), FakeReviewBasisSource.Design(), null));
                break;
            default:
                _f.Basis.Set(id, new ReviewBases(
                    FakeReviewBasisSource.Requirements(), FakeReviewBasisSource.Design(2, 1), FakeReviewBasisSource.Proposal(1, 1, 1)));
                break;
        }

        var result = await _f.Reviewing.ReviewAsync(id);

        var error = Assert.Single(result.Errors);
        Assert.Equal(field, error.FieldName);
        foreach (var word in new[] { "report", "portal", "hour", "billing" })
            Assert.DoesNotContain(word, error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
