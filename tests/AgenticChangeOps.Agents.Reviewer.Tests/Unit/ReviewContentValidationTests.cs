using AgenticChangeOps.Agents.Reviewer.Tests.Fakes;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>
/// The model's findings are checked in a fixed order (contracts/reference-and-limit-rules.md): shape
/// first (errors), then the count, then the size, then each finding's references in order
/// (typed rejections). A rejected review carries a reason and nothing else.
/// </summary>
public sealed class ReviewContentValidationTests
{
    private static readonly ReviewBases Bases = FakeReviewBasisSource.Current();

    private static ReviewContentValidator Validator(ReviewLimits? limits = null) =>
        new(limits ?? ReviewLimits.Defaults);

    private static ReviewFindingContent Finding(
        string? category = "AcceptanceCriteria",
        string? severity = "Medium",
        string? statement = "The proposal does not mention the old hour in the notes.",
        params string[] references) =>
        new(category, severity, statement, references.Length == 0 ? new[] { "proposal.changes[1]" } : references);

    private static ReviewContent Content(
        IReadOnlyList<ReviewFindingContent>? findings, IReadOnlyList<string>? questions = null) =>
        new(findings, questions ?? Array.Empty<string>());

    private static ReviewValidation Validate(ReviewContent? content, ReviewLimits? limits = null) =>
        Validator(limits).Validate(content, Bases);

    private static void AssertShapeError(ReviewValidation result)
    {
        Assert.Null(result.Value);
        Assert.Null(result.Rejection);
        Assert.NotEmpty(result.Errors);
    }

    private static void AssertRejected(ReviewValidation result, ReviewRejection reason)
    {
        Assert.Null(result.Value);
        Assert.Equal(reason, result.Rejection);
        Assert.Empty(result.Errors);
    }

    // ---- a valid review ---------------------------------------------------------------

    [Fact]
    public void AWellFormedAnswer_IsValid_WithTypedFindingsAndResolvedReferences()
    {
        var content = Content(
            new[]
            {
                Finding("AcceptanceCriteria", "High", "No test covers the old hour.", "requirements.acceptanceCriteria[2]", "proposal.testIdeas[1]"),
                Finding("DesignConsistency", "Low", "The notes file is not in the design.", "design.affectedComponents[1]"),
                Finding("UnmentionedRisk", "Medium", "A reader in another time zone sees the report late.", "proposal.risks[1]")
            },
            new[] { "Who owns the notes file?" });

        var result = Validate(content);

        Assert.Empty(result.Errors);
        Assert.Null(result.Rejection);
        var value = Assert.IsType<ValidatedReview>(result.Value);
        Assert.Equal(3, value.Findings.Count);
        Assert.Equal(FindingCategory.AcceptanceCriteria, value.Findings[0].Category);
        Assert.Equal(FindingSeverity.High, value.Findings[0].Severity);
        Assert.Equal("No test covers the old hour.", value.Findings[0].Statement);
        Assert.Equal(
            new[]
            {
                new ReviewReference(ReviewDocument.Requirements, ReferenceList.AcceptanceCriteria, 2),
                new ReviewReference(ReviewDocument.Proposal, ReferenceList.TestIdeas, 1)
            },
            value.Findings[0].References);
        Assert.Equal(FindingCategory.DesignConsistency, value.Findings[1].Category);
        Assert.Equal(FindingSeverity.Low, value.Findings[1].Severity);
        Assert.Equal(FindingCategory.UnmentionedRisk, value.Findings[2].Category);
        Assert.Equal(new[] { "Who owns the notes file?" }, value.OpenQuestions);
    }

    [Fact]
    public void ZeroFindings_IsValid()
    {
        var result = Validate(Content(Array.Empty<ReviewFindingContent>()));

        Assert.Empty(result.Errors);
        Assert.Null(result.Rejection);
        Assert.Empty(result.Value!.Findings);
    }

    [Fact]
    public void OpenQuestions_MayBeEmpty()
    {
        var result = Validate(Content(new[] { Finding() }, Array.Empty<string>()));

        Assert.Empty(result.Value!.OpenQuestions);
    }

    [Fact]
    public void Text_IsTrimmed_AndBlankOpenQuestionsAreDropped()
    {
        var result = Validate(Content(
            new[] { Finding(statement: "   A statement with spaces around it.   ", references: "  proposal.changes[2]  ") },
            new[] { "  First?  ", "", "   ", "Second?" }));

        Assert.Equal("A statement with spaces around it.", result.Value!.Findings[0].Statement);
        Assert.Equal(new[] { "First?", "Second?" }, result.Value.OpenQuestions);
        Assert.Equal(
            new ReviewReference(ReviewDocument.Proposal, ReferenceList.Changes, 2),
            Assert.Single(result.Value.Findings[0].References));
    }

    [Theory]
    [InlineData("AcceptanceCriteria", FindingCategory.AcceptanceCriteria)]
    [InlineData("acceptancecriteria", FindingCategory.AcceptanceCriteria)]
    [InlineData("ACCEPTANCECRITERIA", FindingCategory.AcceptanceCriteria)]
    [InlineData("  DesignConsistency ", FindingCategory.DesignConsistency)]
    [InlineData("unmentionedrisk", FindingCategory.UnmentionedRisk)]
    public void CategoryWords_AreCaseInsensitive(string word, FindingCategory expected)
    {
        var result = Validate(Content(new[] { Finding(category: word) }));

        Assert.Equal(expected, result.Value!.Findings[0].Category);
    }

    [Theory]
    [InlineData("Low", FindingSeverity.Low)]
    [InlineData("MEDIUM", FindingSeverity.Medium)]
    [InlineData(" high ", FindingSeverity.High)]
    public void SeverityWords_AreCaseInsensitive(string word, FindingSeverity expected)
    {
        var result = Validate(Content(new[] { Finding(severity: word) }));

        Assert.Equal(expected, result.Value!.Findings[0].Severity);
    }

    // ---- shape errors -----------------------------------------------------------------

    [Fact]
    public void NoContent_IsAShapeError()
    {
        var result = Validate(null);

        AssertShapeError(result);
        Assert.Contains(result.Errors, e => e.FieldName == "Generator");
    }

    [Fact]
    public void AMissingFindingsSection_IsAShapeError_ButAnEmptyOneIsNot()
    {
        var missing = Validate(new ReviewContent(null, Array.Empty<string>()));

        AssertShapeError(missing);
        Assert.Contains(missing.Errors, e => e.FieldName == "Findings");
    }

    [Fact]
    public void AMissingOpenQuestionsSection_IsAShapeError()
    {
        var result = Validate(new ReviewContent(new[] { Finding() }, null));

        AssertShapeError(result);
        Assert.Contains(result.Errors, e => e.FieldName == "OpenQuestions");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AFindingWithABlankOrMissingStatement_IsAShapeError(string? statement)
    {
        var result = Validate(Content(new[] { Finding(statement: statement) }));

        AssertShapeError(result);
        Assert.Contains(result.Errors, e => e.FieldName == "Findings");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Style")]
    [InlineData("Acceptance Criteria")]
    [InlineData("critical")]
    public void AFindingWithABlankOrUnknownCategory_IsAShapeError(string? category)
    {
        var result = Validate(Content(new[] { Finding(category: category) }));

        AssertShapeError(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Critical")]
    [InlineData("1")]
    [InlineData("Medium-High")]
    public void AFindingWithABlankOrUnknownSeverity_IsAShapeError(string? severity)
    {
        var result = Validate(Content(new[] { Finding(severity: severity) }));

        AssertShapeError(result);
    }

    [Fact]
    public void AStatementOverTheSharedTextLimit_IsAShapeError()
    {
        var result = Validate(Content(
            new[] { Finding(statement: new string('a', 2001)) }),
            new ReviewLimits(8, 100000));

        AssertShapeError(result);
    }

    [Fact]
    public void AShapeError_IsReportedEvenWhenTheCountIsAlsoOver_BecauseShapeComesFirst()
    {
        var tooMany = new[] { Finding(), Finding(), Finding(statement: " ") };

        var result = Validate(Content(tooMany), new ReviewLimits(2, 8000));

        AssertShapeError(result);
    }

    [Fact]
    public void TheContentTypes_HaveNoPlaceForASystemCheck()
    {
        // The model cannot supply, and the validator cannot pass on, a system check or any status.
        var content = typeof(ReviewContent).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
        var validated = typeof(ValidatedReview).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(new[] { "Findings", "OpenQuestions" }, content);
        Assert.Equal(new[] { "Findings", "OpenQuestions" }, validated);
    }

    // ---- reference rejections ---------------------------------------------------------

    [Fact]
    public void AFindingWithNoReference_IsRejectedAsMissingReference()
    {
        var finding = new ReviewFindingContent("UnmentionedRisk", "Low", "Something is not covered.", Array.Empty<string>());

        AssertRejected(Validate(Content(new[] { finding })), ReviewRejection.MissingReference);
    }

    [Fact]
    public void AFindingWhoseReferenceListIsAbsent_IsRejectedAsMissingReference()
    {
        var finding = new ReviewFindingContent("UnmentionedRisk", "Low", "Something is not covered.", null);

        AssertRejected(Validate(Content(new[] { finding })), ReviewRejection.MissingReference);
    }

    [Fact]
    public void AFindingWhoseOnlyReferencesAreBlank_IsRejectedAsMissingReference()
    {
        var finding = new ReviewFindingContent("UnmentionedRisk", "Low", "Something is not covered.", new[] { "", "   " });

        AssertRejected(Validate(Content(new[] { finding })), ReviewRejection.MissingReference);
    }

    [Theory]
    [InlineData("proposal.changes[9]")]
    [InlineData("proposal.nothing[1]")]
    [InlineData("not a reference")]
    [InlineData("requirements.summary[1]")]
    public void AnyUnresolvedReference_RejectsTheWholeReview_EvenWhenOtherFindingsAreValid(string dangling)
    {
        var content = Content(new[]
        {
            Finding(),
            Finding(references: new[] { "design.dependencies[1]", dangling }),
            Finding()
        });

        AssertRejected(Validate(content), ReviewRejection.UnresolvedReference);
    }

    [Fact]
    public void WhenManyReferencesAreOffered_OneBadOneAmongGoodOnes_RejectsTheReview()
    {
        var content = Content(new[]
        {
            Finding(references: new[] { "proposal.changes[1]", "proposal.changes[2]", "proposal.changes[3]", "proposal.changes[4]" })
        });

        AssertRejected(Validate(content), ReviewRejection.UnresolvedReference);
    }

    [Fact]
    public void AReferenceToAnItemOfAnotherVersionOfTheRecords_IsUnresolved()
    {
        // The review is validated against the records the model was given: a shorter set cannot satisfy a longer reference.
        var shorter = Bases with
        {
            Proposal = FakeReviewBasisSource.Proposal() with { Changes = FakeReviewBasisSource.Proposal().Changes.Take(1).ToArray() }
        };

        var result = Validator().Validate(Content(new[] { Finding(references: "proposal.changes[3]") }), shorter);

        AssertRejected(result, ReviewRejection.UnresolvedReference);
    }

    // ---- limits -----------------------------------------------------------------------

    [Fact]
    public void ExactlyTheMaximumNumberOfFindings_Passes_OneMoreIsTooMany()
    {
        var limits = new ReviewLimits(MaxFindings: 3, MaxTotalBytes: 100000);

        var atMax = Validate(Content(new[] { Finding(), Finding(), Finding() }), limits);
        var overMax = Validate(Content(new[] { Finding(), Finding(), Finding(), Finding() }), limits);

        Assert.Equal(3, atMax.Value!.Findings.Count);
        AssertRejected(overMax, ReviewRejection.TooManyFindings);
    }

    [Fact]
    public void ExactlyTheMaximumNumberOfBytes_Passes_OneMoreByteIsTooLarge()
    {
        var limits = new ReviewLimits(MaxFindings: 3, MaxTotalBytes: 20);

        var atMax = Validate(Content(
            new[] { Finding(statement: new string('a', 10)) }, new[] { new string('b', 10) }), limits);
        var overMax = Validate(Content(
            new[] { Finding(statement: new string('a', 11)) }, new[] { new string('b', 10) }), limits);

        Assert.Null(atMax.Rejection);
        Assert.NotNull(atMax.Value);
        AssertRejected(overMax, ReviewRejection.TooLarge);
    }

    [Fact]
    public void Size_IsCountedInUtf8Bytes_NotCharacters()
    {
        var limits = new ReviewLimits(MaxFindings: 3, MaxTotalBytes: 20);

        // Ten two-byte characters in a statement plus ten bytes in a question: exactly 30 bytes, 20 characters.
        var result = Validate(Content(
            new[] { Finding(statement: new string('é', 10)) }, new[] { new string('b', 10) }), limits);

        AssertRejected(result, ReviewRejection.TooLarge);
    }

    [Fact]
    public void Size_CountsStatementsAndOpenQuestionsTogether()
    {
        var limits = new ReviewLimits(MaxFindings: 5, MaxTotalBytes: 30);

        var result = Validate(Content(
            new[] { Finding(statement: new string('a', 10)), Finding(statement: new string('a', 10)) },
            new[] { new string('b', 11) }), limits);

        AssertRejected(result, ReviewRejection.TooLarge);
    }

    // ---- order of checks, and what a rejection reveals --------------------------------

    [Fact]
    public void TheCountIsCheckedBeforeTheSize_AndBeforeAnyReference()
    {
        var limits = new ReviewLimits(MaxFindings: 1, MaxTotalBytes: 5);
        var content = Content(new[]
        {
            Finding(statement: "A long statement over the size.", references: "proposal.changes[9]"),
            Finding(references: "proposal.changes[9]")
        });

        AssertRejected(Validate(content, limits), ReviewRejection.TooManyFindings);
    }

    [Fact]
    public void TheSizeIsCheckedBeforeAnyReference()
    {
        var limits = new ReviewLimits(MaxFindings: 5, MaxTotalBytes: 5);
        var content = Content(new[] { Finding(statement: "A long statement over the size.", references: "proposal.changes[9]") });

        AssertRejected(Validate(content, limits), ReviewRejection.TooLarge);
    }

    [Fact]
    public void ReferencesAreCheckedFindingByFinding_AndTheFirstReasonFoundIsReturned()
    {
        var missingFirst = Content(new[]
        {
            new ReviewFindingContent("UnmentionedRisk", "Low", "No reference at all.", Array.Empty<string>()),
            Finding(references: "proposal.changes[9]")
        });
        var unresolvedFirst = Content(new[]
        {
            Finding(references: "proposal.changes[9]"),
            new ReviewFindingContent("UnmentionedRisk", "Low", "No reference at all.", Array.Empty<string>())
        });

        AssertRejected(Validate(missingFirst), ReviewRejection.MissingReference);
        AssertRejected(Validate(unresolvedFirst), ReviewRejection.UnresolvedReference);
    }

    [Fact]
    public void ARejection_CarriesTheReasonOnly_NoStatementReferenceOrText()
    {
        var result = Validate(Content(new[] { Finding(statement: "HOSTILE-MARKER-0001 secret statement", references: "proposal.changes[9]") }));

        AssertRejected(result, ReviewRejection.UnresolvedReference);
        Assert.DoesNotContain("HOSTILE-MARKER-0001", result.ToString());
        Assert.DoesNotContain("proposal.changes[9]", result.ToString());
    }

    [Fact]
    public void ShapeErrorMessages_NeverContainTheModelText()
    {
        var result = Validate(Content(new[] { Finding(category: "HOSTILE-MARKER-0001") }));

        AssertShapeError(result);
        Assert.All(result.Errors, e => Assert.DoesNotContain("HOSTILE-MARKER-0001", e.Message + e.FieldName));
    }
}
