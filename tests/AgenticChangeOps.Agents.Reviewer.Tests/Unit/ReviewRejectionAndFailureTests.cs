using AgenticChangeOps.Agents.Reviewer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>
/// US4: any rejected review, unusable answer or model failure stores nothing, saves no audit entry
/// and reports a reason or a category only: never a statement, a reference or any model text.
/// </summary>
public sealed class ReviewRejectionAndFailureTests
{
    private const string Marker = "HOSTILE-MARKER-0001";

    private readonly ReviewTestFixture _f = new();

    private void AssertNothingStored(Guid id)
    {
        Assert.Empty(_f.Store.GetAgentOutputs<Review>(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.ReviewGenerated));
        Assert.Equal(ChangeRequestStatus.Approved, _f.Store.FindById(id)!.Status);
    }

    private static ReviewContent Content(params ReviewFindingContent[] findings) =>
        new(findings, Array.Empty<string>());

    // ---- rejections -------------------------------------------------------------------

    [Fact]
    public async Task AFindingWithNoReference_IsRejected_AsMissingReference()
    {
        var id = _f.ApproveWithBases();
        _f.Generator.Behavior = FakeReviewGenerator.Returns(Content(
            new ReviewFindingContent("UnmentionedRisk", "Low", Marker + " no refs", Array.Empty<string>())));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReviewRejection.MissingReference, result.Rejection);
        AssertNothingStored(id);
    }

    [Fact]
    public async Task OneDanglingReferenceAmongValidFindings_RejectsTheWholeReview()
    {
        var id = _f.ApproveWithBases();
        _f.Generator.Behavior = FakeReviewGenerator.Returns(Content(
            FakeReviewGenerator.Finding(),
            FakeReviewGenerator.Finding(references: new[] { "design.dependencies[1]", "proposal.changes[42]" }),
            FakeReviewGenerator.Finding()));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.Equal(ReviewRejection.UnresolvedReference, result.Rejection);
        AssertNothingStored(id);
    }

    [Fact]
    public async Task MoreFindingsThanTheMaximum_IsRejected_AsTooManyFindings()
    {
        var f = new ReviewTestFixture(new ReviewLimits(MaxFindings: 2, MaxTotalBytes: 8000));
        var id = f.ApproveWithBases();
        f.Generator.Behavior = FakeReviewGenerator.Returns(Content(
            FakeReviewGenerator.Finding(), FakeReviewGenerator.Finding(), FakeReviewGenerator.Finding()));

        var result = await f.Reviewing.ReviewAsync(id);

        Assert.Equal(ReviewRejection.TooManyFindings, result.Rejection);
        Assert.Empty(f.Store.GetAgentOutputs<Review>(id));
        Assert.Equal(0, f.AuditCount(id, AuditEventType.ReviewGenerated));
    }

    [Fact]
    public async Task AReviewOverTheMaximumSize_IsRejected_AsTooLarge()
    {
        var f = new ReviewTestFixture(new ReviewLimits(MaxFindings: 8, MaxTotalBytes: 40));
        var id = f.ApproveWithBases();
        f.Generator.Behavior = FakeReviewGenerator.Returns(Content(
            FakeReviewGenerator.Finding(statement: new string('a', 41))));

        var result = await f.Reviewing.ReviewAsync(id);

        Assert.Equal(ReviewRejection.TooLarge, result.Rejection);
        Assert.Empty(f.Store.GetAgentOutputs<Review>(id));
        Assert.Equal(0, f.AuditCount(id, AuditEventType.ReviewGenerated));
    }

    [Fact]
    public async Task ARejection_CarriesTheReasonOnly_AFixedMessageAndNoCategory()
    {
        var id = _f.ApproveWithBases();
        _f.Generator.Behavior = FakeReviewGenerator.Returns(Content(
            FakeReviewGenerator.Finding(statement: Marker + " secret statement", references: "proposal.changes[42]")));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.Null(result.FailureCategory);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Review", error.FieldName);
        Assert.Equal("The review was rejected (UnresolvedReference).", error.Message);
        Assert.DoesNotContain(Marker, error.Message + error.FieldName);
        Assert.DoesNotContain("proposal.changes", error.Message);
    }

    [Fact]
    public async Task EveryRejection_ComesBackAs_ARejectedResult_NeverAsAnException()
    {
        foreach (var finding in new[]
        {
            new ReviewFindingContent("UnmentionedRisk", "Low", "x", Array.Empty<string>()),
            FakeReviewGenerator.Finding(references: "nothing")
        })
        {
            var id = _f.ApproveWithBases();
            _f.Generator.Behavior = FakeReviewGenerator.Returns(Content(finding));

            var result = await _f.Reviewing.ReviewAsync(id);

            Assert.NotNull(result.Rejection);
            AssertNothingStored(id);
        }
    }

    // ---- unusable answers: errors with the MalformedOutput category -------------------

    [Theory]
    [InlineData("blank statement")]
    [InlineData("unknown severity")]
    [InlineData("unknown category")]
    [InlineData("missing findings section")]
    [InlineData("missing open questions section")]
    public async Task ShapeProblems_AreErrors_WithTheMalformedOutputCategory(string problem)
    {
        var id = _f.ApproveWithBases();
        ReviewContent content = problem switch
        {
            "blank statement" => Content(FakeReviewGenerator.Finding(statement: "   ")),
            "unknown severity" => Content(FakeReviewGenerator.Finding(severity: "Critical")),
            "unknown category" => Content(new ReviewFindingContent("Style", "Low", "x", new[] { "proposal.changes[1]" })),
            "missing findings section" => new ReviewContent(null, Array.Empty<string>()),
            _ => new ReviewContent(Array.Empty<ReviewFindingContent>(), null)
        };
        _f.Generator.Behavior = FakeReviewGenerator.Returns(content);

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureCategory.MalformedOutput, result.FailureCategory);
        Assert.Null(result.Rejection);
        Assert.NotEmpty(result.Errors);
        AssertNothingStored(id);
    }

    [Fact]
    public async Task NullContent_IsMalformedOutput()
    {
        var id = _f.ApproveWithBases();
        _f.Generator.Behavior = FakeReviewGenerator.ReturnsNull();

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.Equal(GenerationFailureCategory.MalformedOutput, result.FailureCategory);
        AssertNothingStored(id);
    }

    // ---- model failures: a category and nothing else ----------------------------------

    public static IEnumerable<object[]> Categories() =>
        Enum.GetValues<GenerationFailureCategory>().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Categories))]
    public async Task AGeneratorFailure_ReportsItsCategory_AndAFixedMessage(GenerationFailureCategory category)
    {
        var id = _f.ApproveWithBases();
        _f.Generator.Behavior = FakeReviewGenerator.Throws(new DraftGenerationException(category));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(category, result.FailureCategory);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Generator", error.FieldName);
        Assert.Equal($"The content generator failed ({category}).", error.Message);
        Assert.Equal(1, _f.Generator.CallCount);
        AssertNothingStored(id);
    }

    [Fact]
    public async Task AnyOtherException_IsReportedByTypeNameOnly_WithNoCategoryAndNoMessage()
    {
        var id = _f.ApproveWithBases();
        _f.Generator.Behavior = FakeReviewGenerator.Throws(
            new InvalidOperationException("boom at https://sentinel-endpoint.invalid/ " + Marker));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.Null(result.FailureCategory);
        var error = Assert.Single(result.Errors);
        Assert.Equal("The content generator failed (InvalidOperationException).", error.Message);
        Assert.DoesNotContain("sentinel-endpoint", error.Message);
        Assert.DoesNotContain(Marker, error.Message);
        AssertNothingStored(id);
    }

    [Fact]
    public async Task ACancelledRequest_StoresNothing_AndTheCancellationIsNotTurnedIntoAFailure()
    {
        var id = _f.ApproveWithBases();
        using var cts = new CancellationTokenSource();
        _f.Generator.Behavior = token =>
        {
            cts.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult<ReviewContent?>(FakeReviewGenerator.ValidContent());
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _f.Reviewing.ReviewAsync(id, cts.Token));

        AssertNothingStored(id);
    }

    [Fact]
    public async Task ACancellationFromTheGenerator_WhileTheCallersTokenIsLive_IsReportedByTypeNameOnly()
    {
        // Intentional difference, as for the Developer: only the caller's own cancellation is rethrown.
        var id = _f.ApproveWithBases();
        _f.Generator.Behavior = FakeReviewGenerator.Throws(new OperationCanceledException("sentinel-cancel-text"));

        var result = await _f.Reviewing.ReviewAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Null(result.FailureCategory);
        Assert.Equal("The content generator failed (OperationCanceledException).", Assert.Single(result.Errors).Message);
        AssertNothingStored(id);
    }

    // ---- the common guarantees --------------------------------------------------------

    [Fact]
    public async Task AFailedRequest_ChangesNothing_AndASuccessfulOneStillWorksAfterwards()
    {
        var id = _f.ApproveWithBases();
        var before = _f.Store.FindById(id);
        var auditBefore = _f.Store.GetAuditEntries(id).Count;
        _f.Generator.Behavior = FakeReviewGenerator.Throws(new DraftGenerationException(GenerationFailureCategory.TimedOut));
        await _f.Reviewing.ReviewAsync(id);

        Assert.Equal(before, _f.Store.FindById(id));
        Assert.Equal(auditBefore, _f.Store.GetAuditEntries(id).Count);

        _f.Generator.Behavior = FakeReviewGenerator.Returns(FakeReviewGenerator.ValidContent());
        var next = await _f.Reviewing.ReviewAsync(id);

        Assert.True(next.IsSuccess);
        Assert.Equal(1, next.Review!.Version); // a failure never uses up a version
    }

    [Fact]
    public async Task NoErrorMessage_AnywhereInTheseResults_CarriesFindingTextOrAReference()
    {
        var id = _f.ApproveWithBases();
        _f.Generator.Behavior = FakeReviewGenerator.Returns(Content(
            FakeReviewGenerator.Finding(statement: Marker + " statement", references: "proposal.changes[42]")));

        var result = await _f.Reviewing.ReviewAsync(id);

        var text = string.Join(" ", result.Errors.Select(e => e.FieldName + " " + e.Message));
        Assert.DoesNotContain(Marker, text);
        Assert.DoesNotContain("proposal.changes[42]", text);
        Assert.DoesNotContain("billing-portal", text);
    }
}
