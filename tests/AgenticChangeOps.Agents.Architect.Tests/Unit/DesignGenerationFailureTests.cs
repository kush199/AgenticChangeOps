using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignGenerationFailureTests
{
    private readonly DesigningTestFixture _f = new();

    private void AssertNothingChanged(
        Guid id, ChangeRequest? requestBefore, IReadOnlyList<ChangeRequestAuditEntry> auditBefore)
    {
        Assert.Equal(requestBefore, _f.Store.FindById(id));
        Assert.Equal(auditBefore, _f.Store.GetAuditEntries(id));
        Assert.Empty(_f.Store.GetDesigns(id));
    }

    [Theory]
    [InlineData(GenerationFailureCategory.Unreachable)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.ResponseTooLarge)]
    [InlineData(GenerationFailureCategory.MalformedOutput)]
    public async Task GeneratorReportsACategory_ErrorCarriesOnlyTheCategoryAndNothingIsStored(
        GenerationFailureCategory category)
    {
        var id = _f.ApproveWithBasis();
        var requestBefore = _f.Store.FindById(id);
        var auditBefore = _f.Store.GetAuditEntries(id);
        _f.Generator.Behavior = FakeDesignGenerator.Throws(new DraftGenerationException(category));

        var result = await _f.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(category, result.FailureCategory);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Generator", error.FieldName);
        Assert.Equal($"The content generator failed ({category}).", error.Message);
        AssertNothingChanged(id, requestBefore, auditBefore);
    }

    [Fact]
    public async Task GeneratorThrowsOtherException_ErrorNamesOnlyTheExceptionType()
    {
        var id = _f.ApproveWithBasis();
        var requestBefore = _f.Store.FindById(id);
        var auditBefore = _f.Store.GetAuditEntries(id);
        _f.Generator.Behavior = FakeDesignGenerator.Throws(
            new InvalidOperationException("secret-endpoint-value"));

        var result = await _f.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Null(result.FailureCategory);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Generator", error.FieldName);
        Assert.Contains("InvalidOperationException", error.Message);
        Assert.DoesNotContain("secret-endpoint-value", error.Message);
        AssertNothingChanged(id, requestBefore, auditBefore);
    }

    [Fact]
    public async Task GeneratorReturnsNull_IsMalformedOutputAndNothingIsStored()
    {
        var id = _f.ApproveWithBasis();
        var requestBefore = _f.Store.FindById(id);
        var auditBefore = _f.Store.GetAuditEntries(id);
        _f.Generator.Behavior = FakeDesignGenerator.ReturnsNull();

        var result = await _f.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureCategory.MalformedOutput, result.FailureCategory);
        AssertNothingChanged(id, requestBefore, auditBefore);
    }

    [Fact]
    public async Task IncompleteContent_IsMalformedOutputNamingTheSectionAndNothingIsStored()
    {
        var id = _f.ApproveWithBasis();
        var requestBefore = _f.Store.FindById(id);
        var auditBefore = _f.Store.GetAuditEntries(id);
        _f.Generator.Behavior = FakeDesignGenerator.Returns(
            FakeDesignGenerator.ValidContent() with { RollbackConsiderations = Array.Empty<string>() });

        var result = await _f.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureCategory.MalformedOutput, result.FailureCategory);
        Assert.Contains(result.Errors, e => e.FieldName == "RollbackConsiderations");
        AssertNothingChanged(id, requestBefore, auditBefore);
    }

    [Fact]
    public async Task FailureThenSuccess_NextDesignIsVersionOneWithNoGap()
    {
        var id = _f.ApproveWithBasis();
        _f.Generator.Behavior = FakeDesignGenerator.Throws(
            new DraftGenerationException(GenerationFailureCategory.TimedOut));
        await _f.Designing.DesignAsync(id);
        _f.Generator.Behavior = FakeDesignGenerator.Returns(FakeDesignGenerator.ValidContent());

        var result = await _f.Designing.DesignAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Draft!.Version);
        Assert.Equal(1, _f.AuditCount(id, AuditEventType.DesignGenerated));
    }
}
