using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Requirement.Tests.Fakes;

namespace AgenticChangeOps.Agents.Requirement.Tests.Unit;

public sealed class DraftGenerationFailureTests
{
    private readonly DraftingTestFixture _f = new();

    private void AssertNothingChanged(Guid id, ChangeRequest? requestBefore, IReadOnlyList<ChangeRequestAuditEntry> auditBefore)
    {
        Assert.Equal(requestBefore, _f.Store.FindById(id));
        Assert.Equal(auditBefore, _f.Store.GetAuditEntries(id));
        Assert.Empty(_f.Store.GetDrafts(id));
    }

    [Theory]
    [InlineData(GenerationFailureCategory.Unreachable)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.ResponseTooLarge)]
    [InlineData(GenerationFailureCategory.MalformedOutput)]
    public async Task GeneratorReportsACategory_ErrorNamesTheCategoryAndNothingIsStored(
        GenerationFailureCategory category)
    {
        var id = _f.SubmitSynthetic();
        var requestBefore = _f.Store.FindById(id);
        var auditBefore = _f.Store.GetAuditEntries(id);
        _f.Generator.Behavior = FakeDraftGenerator.Throws(new DraftGenerationException(category));

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Generator", error.FieldName);
        Assert.Equal($"The content generator failed ({category}).", error.Message);
        AssertNothingChanged(id, requestBefore, auditBefore);
    }

    [Fact]
    public async Task CategoryFailureThenSuccess_NextDraftIsVersionOneWithNoGap()
    {
        var id = _f.SubmitSynthetic();
        _f.Generator.Behavior = FakeDraftGenerator.Throws(
            new DraftGenerationException(GenerationFailureCategory.TimedOut));
        await _f.Drafting.DraftAsync(id);
        _f.Generator.Behavior = FakeDraftGenerator.Returns(FakeDraftGenerator.ValidContent());

        var result = await _f.Drafting.DraftAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Draft!.Version);
    }

    [Fact]
    public async Task GeneratorThrows_ReturnsGeneratorErrorWithoutLeakingTheMessage()
    {
        var id = _f.SubmitSynthetic();
        var requestBefore = _f.Store.FindById(id);
        var auditBefore = _f.Store.GetAuditEntries(id);
        _f.Generator.Behavior = FakeDraftGenerator.Throws(
            new InvalidOperationException("secret-endpoint-value"));

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Generator", error.FieldName);
        Assert.Contains("InvalidOperationException", error.Message);
        Assert.DoesNotContain("secret-endpoint-value", error.Message);
        AssertNothingChanged(id, requestBefore, auditBefore);
    }

    [Fact]
    public async Task GeneratorReturnsNull_ReturnsGeneratorError()
    {
        var id = _f.SubmitSynthetic();
        var requestBefore = _f.Store.FindById(id);
        var auditBefore = _f.Store.GetAuditEntries(id);
        _f.Generator.Behavior = FakeDraftGenerator.ReturnsNull();

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Generator", Assert.Single(result.Errors).FieldName);
        AssertNothingChanged(id, requestBefore, auditBefore);
    }

    [Fact]
    public async Task GeneratorIsCancelled_ReturnsGeneratorErrorInsteadOfThrowing()
    {
        var id = _f.SubmitSynthetic();
        var requestBefore = _f.Store.FindById(id);
        var auditBefore = _f.Store.GetAuditEntries(id);
        _f.Generator.Behavior = FakeDraftGenerator.Throws(new OperationCanceledException());

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Generator", Assert.Single(result.Errors).FieldName);
        AssertNothingChanged(id, requestBefore, auditBefore);
    }

    [Fact]
    public async Task GeneratorTimesOut_ReturnsGeneratorError()
    {
        var id = _f.SubmitSynthetic();
        _f.Generator.Behavior = FakeDraftGenerator.Throws(new TimeoutException());

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Generator", Assert.Single(result.Errors).FieldName);
    }

    [Fact]
    public async Task FailureAfterEarlierSuccess_LeavesVersionOneUnchangedAndNextSuccessIsVersionTwo()
    {
        var id = _f.SubmitSynthetic();
        var first = (await _f.Drafting.DraftAsync(id)).Draft!;
        _f.Generator.Behavior = FakeDraftGenerator.Throws(new InvalidOperationException("boom"));

        var failed = await _f.Drafting.DraftAsync(id);

        Assert.False(failed.IsSuccess);
        Assert.Equal(first, Assert.Single(_f.Store.GetDrafts(id)));

        _f.Generator.Behavior = FakeDraftGenerator.Returns(FakeDraftGenerator.ValidContent());
        var next = await _f.Drafting.DraftAsync(id);

        Assert.Equal(2, next.Draft!.Version);
        Assert.Equal(2, _f.Store.GetAuditEntries(id).Count(e => e.EventType == AuditEventType.DraftGenerated));
    }

    [Fact]
    public async Task Failure_IsNotRetriedAutomatically()
    {
        var id = _f.SubmitSynthetic();
        _f.Generator.Behavior = FakeDraftGenerator.Throws(new InvalidOperationException("boom"));

        await _f.Drafting.DraftAsync(id);

        Assert.Equal(1, _f.Generator.CallCount);
    }

    [Fact]
    public async Task MalformedContent_LeavesRequestAuditAndDraftsUnchanged()
    {
        var id = _f.SubmitSynthetic();
        var requestBefore = _f.Store.FindById(id);
        var auditBefore = _f.Store.GetAuditEntries(id);
        _f.Generator.Behavior = FakeDraftGenerator.Returns(
            FakeDraftGenerator.ValidContent() with { FunctionalRequirements = Array.Empty<string>() });

        var result = await _f.Drafting.DraftAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("FunctionalRequirements", Assert.Single(result.Errors).FieldName);
        AssertNothingChanged(id, requestBefore, auditBefore);
    }
}
