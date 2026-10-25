using AgenticChangeOps.Agents.Reviewer.Tests.Fakes;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>
/// A reference is written <c>document.list[position]</c> (contracts/reference-and-limit-rules.md)
/// and resolves only to a real entry in the exact upstream records the review is based on. List
/// lengths of the test records: requirements (functionalRequirements 2, acceptanceCriteria 2,
/// assumptions 1, openQuestions 1), design (affectedComponents 2, impactAndRisks 1, dependencies 1,
/// rollbackConsiderations 1, openQuestions 1), proposal (changes 3, risks 2, testIdeas 1, openQuestions 1).
/// </summary>
public sealed class ReferenceResolverTests
{
    private static readonly ReviewBases Bases = FakeReviewBasisSource.Current();

    public static IEnumerable<object[]> Resolving() => new[]
    {
        new object[] { "requirements.functionalRequirements[1]", ReviewDocument.Requirements, ReferenceList.FunctionalRequirements, 1 },
        new object[] { "requirements.functionalRequirements[2]", ReviewDocument.Requirements, ReferenceList.FunctionalRequirements, 2 },
        new object[] { "requirements.acceptanceCriteria[2]", ReviewDocument.Requirements, ReferenceList.AcceptanceCriteria, 2 },
        new object[] { "requirements.assumptions[1]", ReviewDocument.Requirements, ReferenceList.Assumptions, 1 },
        new object[] { "requirements.openQuestions[1]", ReviewDocument.Requirements, ReferenceList.OpenQuestions, 1 },
        new object[] { "design.affectedComponents[2]", ReviewDocument.Design, ReferenceList.AffectedComponents, 2 },
        new object[] { "design.impactAndRisks[1]", ReviewDocument.Design, ReferenceList.ImpactAndRisks, 1 },
        new object[] { "design.dependencies[1]", ReviewDocument.Design, ReferenceList.Dependencies, 1 },
        new object[] { "design.rollbackConsiderations[1]", ReviewDocument.Design, ReferenceList.RollbackConsiderations, 1 },
        new object[] { "design.openQuestions[1]", ReviewDocument.Design, ReferenceList.OpenQuestions, 1 },
        new object[] { "proposal.changes[3]", ReviewDocument.Proposal, ReferenceList.Changes, 3 },
        new object[] { "proposal.risks[2]", ReviewDocument.Proposal, ReferenceList.Risks, 2 },
        new object[] { "proposal.testIdeas[1]", ReviewDocument.Proposal, ReferenceList.TestIdeas, 1 },
        new object[] { "proposal.openQuestions[1]", ReviewDocument.Proposal, ReferenceList.OpenQuestions, 1 }
    };

    [Theory]
    [MemberData(nameof(Resolving))]
    public void AReferenceToARealEntry_ResolvesToTheTypedReference(
        string text, ReviewDocument document, ReferenceList list, int position)
    {
        var reference = ReferenceResolver.Resolve(text, Bases);

        Assert.Equal(new ReviewReference(document, list, position), reference);
    }

    [Theory]
    [InlineData("PROPOSAL.CHANGES[2]")]
    [InlineData("Proposal.Changes[2]")]
    [InlineData("proposal.CHANGES[2]")]
    public void Names_AreCaseInsensitive(string text)
    {
        Assert.Equal(
            new ReviewReference(ReviewDocument.Proposal, ReferenceList.Changes, 2),
            ReferenceResolver.Resolve(text, Bases));
    }

    [Theory]
    [InlineData("proposal.changes[0]")]
    [InlineData("proposal.changes[-1]")]
    [InlineData("proposal.changes[+1]")]
    [InlineData("proposal.changes[1.0]")]
    [InlineData("proposal.changes[one]")]
    [InlineData("proposal.changes[]")]
    [InlineData("proposal.changes[99999999999999999999]")]
    [InlineData("proposal.changes")]
    [InlineData("proposal.changes[1")]
    [InlineData("proposal.changes1]")]
    [InlineData("proposal.changes[1]x")]
    [InlineData("x proposal.changes[1]")]
    [InlineData("proposal.changes [1]")]
    [InlineData("proposal . changes[1]")]
    [InlineData("proposal.changes[1][2]")]
    [InlineData("proposal[1]")]
    [InlineData("proposal.[1]")]
    [InlineData(".changes[1]")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AReferenceThatDoesNotParse_IsUnresolved(string? text)
    {
        Assert.Null(ReferenceResolver.Resolve(text, Bases));
    }

    [Theory]
    [InlineData("code.changes[1]")]
    [InlineData("review.changes[1]")]
    [InlineData("proposal.functionalRequirements[1]")] // a list of the requirements, not of the proposal
    [InlineData("proposal.summary[1]")]
    [InlineData("design.changes[1]")]
    [InlineData("design.risks[1]")]
    [InlineData("design.acceptanceCriteria[1]")]
    [InlineData("requirements.changes[1]")]
    [InlineData("requirements.dependencies[1]")]
    [InlineData("requirements.summary[1]")]
    [InlineData("design.approachSummary[1]")]
    public void AnUnknownDocument_OrAListThatBelongsToAnotherDocument_OrASummary_IsUnresolved(string text)
    {
        Assert.Null(ReferenceResolver.Resolve(text, Bases));
    }

    [Theory]
    [InlineData("requirements.functionalRequirements[3]")]
    [InlineData("requirements.acceptanceCriteria[3]")]
    [InlineData("requirements.assumptions[2]")]
    [InlineData("requirements.openQuestions[2]")]
    [InlineData("design.affectedComponents[3]")]
    [InlineData("design.dependencies[2]")]
    [InlineData("proposal.changes[4]")]
    [InlineData("proposal.risks[3]")]
    [InlineData("proposal.testIdeas[2]")]
    [InlineData("proposal.openQuestions[2]")]
    public void APositionOnePastTheEnd_IsUnresolved(string text)
    {
        Assert.Null(ReferenceResolver.Resolve(text, Bases));
    }

    [Fact]
    public void AReferenceToAnEmptyList_IsUnresolved()
    {
        var bases = Bases with
        {
            Requirements = FakeReviewBasisSource.Requirements() with { Assumptions = Array.Empty<string>() },
            Proposal = FakeReviewBasisSource.Proposal() with { TestIdeas = Array.Empty<string>() }
        };

        Assert.Null(ReferenceResolver.Resolve("requirements.assumptions[1]", bases));
        Assert.Null(ReferenceResolver.Resolve("proposal.testIdeas[1]", bases));
    }

    [Fact]
    public void ResolutionIsAgainstTheRecordsPassedIn_NotAnyOtherVersion()
    {
        var shorter = Bases with
        {
            Proposal = FakeReviewBasisSource.Proposal() with
            {
                Changes = FakeReviewBasisSource.Proposal().Changes.Take(1).ToArray()
            }
        };

        Assert.NotNull(ReferenceResolver.Resolve("proposal.changes[3]", Bases));
        Assert.Null(ReferenceResolver.Resolve("proposal.changes[3]", shorter));
        Assert.NotNull(ReferenceResolver.Resolve("proposal.changes[1]", shorter));
    }

    [Theory]
    [InlineData("requirements.acceptanceCriteria[1]")]
    [InlineData("design.dependencies[1]")]
    [InlineData("proposal.changes[1]")]
    public void WhenThePartIsMissing_AReferenceIntoItIsUnresolved(string text)
    {
        var none = new ReviewBases(null, null, null);

        Assert.Null(ReferenceResolver.Resolve(text, none));
    }

    [Fact]
    public void WhenOnlyOnePartIsMissing_ReferencesIntoTheOthersStillResolve()
    {
        var noDesign = Bases with { Design = null };

        Assert.Null(ReferenceResolver.Resolve("design.dependencies[1]", noDesign));
        Assert.NotNull(ReferenceResolver.Resolve("proposal.changes[1]", noDesign));
        Assert.NotNull(ReferenceResolver.Resolve("requirements.acceptanceCriteria[1]", noDesign));
    }
}
