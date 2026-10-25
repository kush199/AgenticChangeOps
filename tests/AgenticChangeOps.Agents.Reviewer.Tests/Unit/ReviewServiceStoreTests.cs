using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticChangeOps.Agents.Reviewer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>US1: an eligible request stores a typed, versioned, audited, advisory review and changes nothing else.</summary>
public sealed class ReviewServiceStoreTests
{
    private readonly ReviewTestFixture _f = new();

    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    [Fact]
    public async Task Success_StoresATypedReview_WithEveryRequiredPart()
    {
        var id = _f.ApproveWithBases(requirementsVersion: 2, designVersion: 3, proposalVersion: 4);

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.True(result.IsSuccess);
        var r = result.Review!;
        Assert.Equal(id, r.ChangeRequestId);
        Assert.Equal(1, r.Version);
        Assert.Equal(2, r.RequirementsVersion);
        Assert.Equal(3, r.DesignVersion);
        Assert.Equal(4, r.ProposalVersion);
        Assert.Equal(_f.Clock.UtcNow, r.GeneratedAt);
        Assert.Equal(ReviewStatus.Draft, r.Status);
        Assert.Equal("ReviewerAgent", r.AgentName);
        Assert.Equal("Advisory only. Nothing acts on this review.", r.AdvisoryNote);
        Assert.Equal(2, r.Findings.Count);
        Assert.Equal(FindingCategory.AcceptanceCriteria, r.Findings[0].Category);
        Assert.Equal(FindingSeverity.Medium, r.Findings[0].Severity);
        Assert.Equal(FakeReviewGenerator.FirstStatement, r.Findings[0].Statement);
        Assert.Equal(
            new[]
            {
                new ReviewReference(ReviewDocument.Requirements, ReferenceList.AcceptanceCriteria, 2),
                new ReviewReference(ReviewDocument.Proposal, ReferenceList.TestIdeas, 1)
            },
            r.Findings[0].References);
        Assert.Equal(FindingCategory.UnmentionedRisk, r.Findings[1].Category);
        Assert.Equal(new[] { "Who owns the notes file?" }, r.OpenQuestions);
        Assert.NotEqual(Guid.Empty, r.ReviewId);
        Assert.Equal(r, Assert.Single(_f.Store.GetAgentOutputs<Review>(id)));
    }

    [Fact]
    public async Task TheAgentNameAndTheAdvisoryNote_AreSetBySystem()
    {
        var id = _f.ApproveWithBases();

        var r = (await _f.Reviewing.ReviewAsync(id)).Review!;

        Assert.Equal(Review.ReviewerAgentName, r.AgentName);
        Assert.Equal(Review.AdvisoryNoteText, r.AdvisoryNote);
    }

    [Fact]
    public async Task TheSystemCheck_IsComputedBeforeTheModelIsAsked_AndIsTheOneStored()
    {
        var id = _f.ApproveWithBases();
        _f.Generator.Behavior = token =>
        {
            // By the time the model is asked, the check has been made and handed over.
            Assert.NotNull(_f.Generator.LastCheck);
            return Task.FromResult<ReviewContent?>(FakeReviewGenerator.ValidContent());
        };

        var r = (await _f.Reviewing.ReviewAsync(id)).Review!;

        var expected = new SampleChecker(FakeReviewBasisSource.MatchingSample()).Check(FakeReviewBasisSource.Proposal().Changes);
        Assert.Equal(SystemCheckStatus.Performed, r.SystemCheck.Status);
        Assert.Equal(expected.Edits, r.SystemCheck.Edits);
        Assert.Equal(
            new[] { EditCheckOutcome.Found, EditCheckOutcome.NewFileFree, EditCheckOutcome.Found },
            r.SystemCheck.Edits.Select(e => e.Outcome));
        Assert.Equal(_f.Generator.LastCheck!.Edits, r.SystemCheck.Edits);
        Assert.Equal(_f.Generator.LastCheck.Status, r.SystemCheck.Status);
    }

    [Fact]
    public async Task AQuoteThatNoLongerMatchesTheSample_IsStoredAsQuoteNotFound_AndTheReviewStillSucceeds()
    {
        var id = _f.ApproveWithBases();
        _f.Sample.With(FakeReviewBasisSource.SettingsPath, "page_size = 20\nreport_hour = 9\n");

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(EditCheckOutcome.QuoteNotFound, result.Review!.SystemCheck.Edits[0].Outcome);
        Assert.Equal(EditCheckOutcome.QuoteNotFound, _f.Generator.LastCheck!.Edits[0].Outcome);
    }

    [Fact]
    public async Task AReviewWithNoFindings_IsStoredLikeAnyOther()
    {
        var id = _f.ApproveWithBases();
        _f.Generator.Behavior = FakeReviewGenerator.Returns(new ReviewContent(Array.Empty<ReviewFindingContent>(), Array.Empty<string>()));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Review!.Findings);
        Assert.Empty(result.Review.OpenQuestions);
        Assert.Equal(1, _f.AuditCount(id, AuditEventType.ReviewGenerated));
    }

    [Fact]
    public async Task OneReviewGeneratedEntry_IsAudited_WithNoToolCalledEntries()
    {
        var id = _f.ApproveWithBases();

        await _f.Reviewing.ReviewAsync(id);

        var added = _f.Store.GetAuditEntries(id).Skip(2).ToArray(); // after Submitted and Approved
        var entry = Assert.Single(added);
        Assert.Equal(AuditEventType.ReviewGenerated, entry.EventType);
        Assert.Equal("ReviewerAgent", entry.AgentName);
        Assert.Equal(1, entry.DraftVersion);
        Assert.Equal(entry.StatusBefore, entry.StatusAfter);
        Assert.Equal(ChangeRequestStatus.Approved, entry.StatusAfter);
        Assert.Null(entry.Details);
        Assert.Null(entry.Automatic);
        Assert.Equal(_f.Clock.UtcNow, entry.OccurredAt);
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ToolCalled));
    }

    [Fact]
    public async Task Reviewing_ChangesNoStatus_AndAddsNoOtherRecord()
    {
        var id = _f.ApproveWithBases();
        var before = _f.Store.FindById(id);
        var auditBefore = _f.Store.GetAuditEntries(id).Count;

        await _f.Reviewing.ReviewAsync(id);

        Assert.Equal(before, _f.Store.FindById(id));
        Assert.Equal(ChangeRequestStatus.Approved, _f.Store.FindById(id)!.Status);
        Assert.Equal(auditBefore + 1, _f.Store.GetAuditEntries(id).Count);
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.DesignGenerated));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.DraftGenerated));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ProposalGenerated));
    }

    [Fact]
    public async Task TheGenerator_IsCalledOnce_AndGivenTheLatestRecordsAndTheRequest()
    {
        var id = _f.ApproveWithBases(requirementsVersion: 4, designVersion: 2, proposalVersion: 5);

        await _f.Reviewing.ReviewAsync(id);

        Assert.Equal(1, _f.Generator.CallCount);
        Assert.Equal(4, _f.Generator.LastRequirements!.Version);
        Assert.Equal(2, _f.Generator.LastDesign!.Version);
        Assert.Equal(5, _f.Generator.LastProposal!.Version);
        Assert.Equal(id, _f.Generator.LastRequest!.ChangeRequestId);
    }

    [Fact]
    public async Task TheStoredReviewIsLinkedToTheVersionsTheModelWasGiven_NotToAnyOther()
    {
        var id = _f.ApproveWithBases(requirementsVersion: 7, designVersion: 6, proposalVersion: 5);

        var r = (await _f.Reviewing.ReviewAsync(id)).Review!;

        Assert.Equal(
            (_f.Generator.LastRequirements!.Version, _f.Generator.LastDesign!.Version, _f.Generator.LastProposal!.Version),
            (r.RequirementsVersion, r.DesignVersion, r.ProposalVersion));
    }

    [Fact]
    public async Task NothingInTheAuditTrail_CarriesAStatementAPathAQuoteOrFileText()
    {
        var id = _f.ApproveWithBases();

        await _f.Reviewing.ReviewAsync(id);

        var json = JsonSerializer.Serialize(_f.Store.GetAuditEntries(id), Options());
        Assert.DoesNotContain(FakeReviewGenerator.FirstStatement, json);
        Assert.DoesNotContain(FakeReviewGenerator.SecondStatement, json);
        Assert.DoesNotContain("billing-portal", json);
        Assert.DoesNotContain("report_hour", json);
        Assert.DoesNotContain("Who owns the notes file?", json);
    }

    [Fact]
    public async Task TheStoredReview_HoldsNoPathQuoteOrFileText_ThatTheProposalOrSampleDidNotAlreadyOwn()
    {
        var id = _f.ApproveWithBases();

        var r = (await _f.Reviewing.ReviewAsync(id)).Review!;

        var json = JsonSerializer.Serialize(r.SystemCheck, Options());
        Assert.DoesNotContain("billing-portal", json);
        Assert.DoesNotContain("report_hour", json);
        Assert.DoesNotContain("page_size", json);
    }
}
