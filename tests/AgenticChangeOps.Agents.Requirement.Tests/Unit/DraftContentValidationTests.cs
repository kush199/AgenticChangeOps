using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

public sealed class DraftContentValidationTests
{
    private readonly DraftingTestFixture _f = new();
    private readonly DraftContentValidator _validator = new();

    private static DraftContent Valid() => FakeDraftGenerator.ValidContent();

    private async Task<DraftingResult> DraftWith(DraftContent content)
    {
        var id = _f.SubmitSynthetic();
        _f.Generator.Behavior = FakeDraftGenerator.Returns(content);
        var result = await _f.Drafting.DraftAsync(id);
        if (!result.IsSuccess)
            Assert.Empty(_f.Store.GetDrafts(id));
        return result;
    }

    private async Task AssertRejectedFor(DraftContent content, params string[] expectedFields)
    {
        var result = await DraftWith(content);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            expectedFields.OrderBy(f => f),
            result.Errors.Select(e => e.FieldName).OrderBy(f => f));
    }

    // ---- required sections ----

    [Fact]
    public Task NullSummary_IsRejected() =>
        AssertRejectedFor(Valid() with { Summary = null }, "Summary");

    [Fact]
    public Task WhitespaceSummary_IsRejected() =>
        AssertRejectedFor(Valid() with { Summary = "   " }, "Summary");

    [Fact]
    public Task NullFunctionalRequirements_IsRejected() =>
        AssertRejectedFor(Valid() with { FunctionalRequirements = null }, "FunctionalRequirements");

    [Fact]
    public Task EmptyFunctionalRequirements_IsRejected() =>
        AssertRejectedFor(Valid() with { FunctionalRequirements = Array.Empty<string>() }, "FunctionalRequirements");

    [Fact]
    public Task OnlyBlankFunctionalRequirements_IsRejected() =>
        AssertRejectedFor(Valid() with { FunctionalRequirements = new[] { " ", "" } }, "FunctionalRequirements");

    [Fact]
    public Task NullAcceptanceCriteria_IsRejected() =>
        AssertRejectedFor(Valid() with { AcceptanceCriteria = null }, "AcceptanceCriteria");

    [Fact]
    public Task EmptyAcceptanceCriteria_IsRejected() =>
        AssertRejectedFor(Valid() with { AcceptanceCriteria = Array.Empty<string>() }, "AcceptanceCriteria");

    [Fact]
    public Task OnlyBlankAcceptanceCriteria_IsRejected() =>
        AssertRejectedFor(Valid() with { AcceptanceCriteria = new[] { "\t" } }, "AcceptanceCriteria");

    [Fact]
    public Task NullAssumptions_IsRejected() =>
        AssertRejectedFor(Valid() with { Assumptions = null }, "Assumptions");

    [Fact]
    public Task NullOpenQuestions_IsRejected() =>
        AssertRejectedFor(Valid() with { OpenQuestions = null }, "OpenQuestions");

    [Fact]
    public async Task EmptyAssumptionsAndOpenQuestions_AreValidAndStored()
    {
        var result = await DraftWith(Valid() with
        {
            Assumptions = Array.Empty<string>(),
            OpenQuestions = Array.Empty<string>()
        });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Draft!.Assumptions);
        Assert.Empty(result.Draft.OpenQuestions);
    }

    // ---- every failing section is reported ----

    [Fact]
    public Task SeveralFailingSections_AreAllReported() =>
        AssertRejectedFor(
            Valid() with { Summary = " ", FunctionalRequirements = null, OpenQuestions = null },
            "Summary", "FunctionalRequirements", "OpenQuestions");

    [Fact]
    public void Validator_ReportsOneErrorPerFailingSection()
    {
        var (normalised, errors) = _validator.Validate(
            Valid() with { Summary = null, AcceptanceCriteria = Array.Empty<string>() });

        Assert.Null(normalised);
        Assert.Equal(2, errors.Count);
    }

    // ---- limits ----

    [Fact]
    public Task SummaryOver2000Characters_IsRejected() =>
        AssertRejectedFor(Valid() with { Summary = new string('x', 2001) }, "Summary");

    [Fact]
    public Task ListItemOver2000Characters_IsRejected() =>
        AssertRejectedFor(
            Valid() with { FunctionalRequirements = new[] { new string('y', 2001) } },
            "FunctionalRequirements");

    [Fact]
    public Task ListWithMoreThan50Items_IsRejected() =>
        AssertRejectedFor(
            Valid() with { OpenQuestions = Enumerable.Range(1, 51).Select(i => $"Question {i}").ToArray() },
            "OpenQuestions");

    [Fact]
    public async Task ExactlyAtLimits_IsAcceptedAndStoredWithoutTruncation()
    {
        var summary = new string('s', 2000);
        var item = new string('i', 2000);
        var fifty = Enumerable.Range(1, 50).Select(i => $"Question {i}").ToArray();

        var result = await DraftWith(Valid() with
        {
            Summary = summary,
            FunctionalRequirements = new[] { item },
            OpenQuestions = fifty
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(summary, result.Draft!.Summary);
        Assert.Equal(item, Assert.Single(result.Draft.FunctionalRequirements));
        Assert.Equal(fifty, result.Draft.OpenQuestions);
    }

    [Fact]
    public async Task ListWithBlankItemsAmongValidOnes_IsAcceptedWithOnlyValidItems()
    {
        var withBlanks = new[] { "Keep this", " ", "", "And this" };

        var result = await DraftWith(Valid() with { AcceptanceCriteria = withBlanks });

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Keep this", "And this" }, result.Draft!.AcceptanceCriteria);
    }

    [Fact]
    public async Task FiftyValidItemsPlusBlanks_IsAcceptedBecauseTheCapAppliesAfterBlanksAreDropped()
    {
        var items = Enumerable.Range(1, 50).Select(i => $"Question {i}").Concat(new[] { " ", "" }).ToArray();

        var result = await DraftWith(Valid() with { OpenQuestions = items });

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Draft!.OpenQuestions.Count);
    }
}
