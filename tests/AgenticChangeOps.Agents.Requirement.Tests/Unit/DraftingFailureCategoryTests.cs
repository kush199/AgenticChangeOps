using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Requirement.Tests.Fakes;

namespace AgenticChangeOps.Agents.Requirement.Tests.Unit;

/// <summary>
/// The additive typed failure category on <see cref="DraftingResult"/> (web API slice): set only
/// when generation failed or produced unusable content, never for eligibility failures.
/// </summary>
public sealed class DraftingFailureCategoryTests
{
    private readonly DraftingTestFixture _f = new();

    [Theory]
    [InlineData(GenerationFailureCategory.Unreachable)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.ResponseTooLarge)]
    [InlineData(GenerationFailureCategory.MalformedOutput)]
    public async Task GeneratorReportsACategory_ResultCarriesThatCategory(GenerationFailureCategory category)
    {
        var id = _f.SubmitSynthetic();
        _f.Generator.Behavior = FakeDraftGenerator.Throws(new DraftGenerationException(category));

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(category, result.FailureCategory);
        Assert.Empty(_f.Store.GetDrafts(id));
    }

    [Fact]
    public async Task BlankSummary_ResultCarriesMalformedOutput()
    {
        var id = _f.SubmitSynthetic();
        var content = FakeDraftGenerator.ValidContent() with { Summary = "   " };
        _f.Generator.Behavior = FakeDraftGenerator.Returns(content);

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureCategory.MalformedOutput, result.FailureCategory);
        Assert.Empty(_f.Store.GetDrafts(id));
    }

    [Fact]
    public async Task GeneratorReturnsNothing_ResultCarriesMalformedOutput()
    {
        var id = _f.SubmitSynthetic();
        _f.Generator.Behavior = FakeDraftGenerator.ReturnsNull();

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureCategory.MalformedOutput, result.FailureCategory);
    }

    [Fact]
    public async Task GeneratorThrowsArbitraryException_CategoryIsNullAndErrorIsUnchanged()
    {
        var id = _f.SubmitSynthetic();
        _f.Generator.Behavior = FakeDraftGenerator.Throws(new InvalidOperationException("synthetic detail"));

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Null(result.FailureCategory);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Generator", error.FieldName);
        Assert.Equal("The content generator failed (InvalidOperationException).", error.Message);
    }

    [Fact]
    public async Task EligibilityFailure_CategoryIsNull()
    {
        var id = _f.SubmitSynthetic();
        _f.SeedStatus(id, ChangeRequestStatus.Approved);

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Null(result.FailureCategory);
        Assert.Equal(0, _f.Generator.CallCount);
    }

    [Fact]
    public async Task UnknownRequest_CategoryIsNull()
    {
        var result = await _f.Drafting.DraftAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Null(result.FailureCategory);
    }

    [Fact]
    public async Task Success_CategoryIsNull()
    {
        var id = _f.SubmitSynthetic();

        var result = await _f.Drafting.DraftAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Null(result.FailureCategory);
    }
}
