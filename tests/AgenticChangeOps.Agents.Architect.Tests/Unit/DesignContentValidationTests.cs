namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignContentValidationTests
{
    private readonly DesignContentValidator _validator = new();

    private static DesignContent Valid() => new(
        ApproachSummary: "Change the schedule entry.",
        AffectedComponents: new[] { "Scheduler" },
        ImpactAndRisks: new[] { "Later delivery" },
        Dependencies: new[] { "Batch import" },
        RollbackConsiderations: new[] { "Restore the old entry" },
        OpenQuestions: new[] { "Who confirms the window?" });

    private static void AssertSingleError(
        (DesignContent? Normalised, IReadOnlyList<AgenticChangeOps.Intake.Models.ValidationError> Errors) outcome,
        string field)
    {
        Assert.Null(outcome.Normalised);
        var error = Assert.Single(outcome.Errors);
        Assert.Equal(field, error.FieldName);
    }

    [Fact]
    public void CompleteContent_IsAcceptedUnchanged()
    {
        var outcome = _validator.Validate(Valid());

        Assert.Empty(outcome.Errors);
        Assert.Equal(Valid().ApproachSummary, outcome.Normalised!.ApproachSummary);
        Assert.Equal(Valid().AffectedComponents, outcome.Normalised.AffectedComponents);
    }

    [Fact]
    public void NullContent_IsRejectedAsAGeneratorError()
    {
        var outcome = _validator.Validate(null);

        Assert.Null(outcome.Normalised);
        Assert.Equal("Generator", Assert.Single(outcome.Errors).FieldName);
    }

    [Fact]
    public void TextAndItems_AreTrimmedAndBlankItemsDropped()
    {
        var outcome = _validator.Validate(Valid() with
        {
            ApproachSummary = "  Change it.  ",
            AffectedComponents = new[] { "  Scheduler ", " ", "" }
        });

        Assert.Empty(outcome.Errors);
        Assert.Equal("Change it.", outcome.Normalised!.ApproachSummary);
        Assert.Equal(new[] { "Scheduler" }, outcome.Normalised.AffectedComponents);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingOrBlankApproachSummary_IsRejected(string? summary) =>
        AssertSingleError(_validator.Validate(Valid() with { ApproachSummary = summary }), "ApproachSummary");

    [Fact]
    public void ApproachSummaryOver2000Characters_IsRejected() =>
        AssertSingleError(
            _validator.Validate(Valid() with { ApproachSummary = new string('a', 2001) }), "ApproachSummary");

    [Fact]
    public void AffectedComponentsAbsentOrEmpty_IsRejected()
    {
        AssertSingleError(_validator.Validate(Valid() with { AffectedComponents = null }), "AffectedComponents");
        AssertSingleError(
            _validator.Validate(Valid() with { AffectedComponents = Array.Empty<string>() }), "AffectedComponents");
        AssertSingleError(
            _validator.Validate(Valid() with { AffectedComponents = new[] { " " } }), "AffectedComponents");
    }

    [Fact]
    public void ImpactAndRisksAbsentOrEmpty_IsRejected()
    {
        AssertSingleError(_validator.Validate(Valid() with { ImpactAndRisks = null }), "ImpactAndRisks");
        AssertSingleError(
            _validator.Validate(Valid() with { ImpactAndRisks = Array.Empty<string>() }), "ImpactAndRisks");
    }

    [Fact]
    public void RollbackConsiderationsAbsentOrEmpty_IsRejected()
    {
        AssertSingleError(
            _validator.Validate(Valid() with { RollbackConsiderations = null }), "RollbackConsiderations");
        AssertSingleError(
            _validator.Validate(Valid() with { RollbackConsiderations = Array.Empty<string>() }),
            "RollbackConsiderations");
    }

    [Fact]
    public void DependenciesAndOpenQuestions_MayBeEmptyButNotAbsent()
    {
        var emptyOk = _validator.Validate(Valid() with
        {
            Dependencies = Array.Empty<string>(),
            OpenQuestions = Array.Empty<string>()
        });
        Assert.Empty(emptyOk.Errors);
        Assert.Empty(emptyOk.Normalised!.Dependencies!);
        Assert.Empty(emptyOk.Normalised.OpenQuestions!);

        AssertSingleError(_validator.Validate(Valid() with { Dependencies = null }), "Dependencies");
        AssertSingleError(_validator.Validate(Valid() with { OpenQuestions = null }), "OpenQuestions");
    }

    [Fact]
    public void ItemOver2000Characters_IsRejected() =>
        AssertSingleError(
            _validator.Validate(Valid() with { ImpactAndRisks = new[] { new string('b', 2001) } }), "ImpactAndRisks");

    [Fact]
    public void MoreThan50Items_IsRejected() =>
        AssertSingleError(
            _validator.Validate(Valid() with
            {
                Dependencies = Enumerable.Range(0, 51).Select(i => $"Dependency {i}").ToArray()
            }),
            "Dependencies");

    [Fact]
    public void EveryFailingSection_IsReportedOnce()
    {
        var outcome = _validator.Validate(new DesignContent(null, null, null, null, null, null));

        Assert.Null(outcome.Normalised);
        Assert.Equal(
            new[]
            {
                "ApproachSummary", "AffectedComponents", "ImpactAndRisks",
                "Dependencies", "RollbackConsiderations", "OpenQuestions"
            }.OrderBy(n => n),
            outcome.Errors.Select(e => e.FieldName).OrderBy(n => n));
    }
}
