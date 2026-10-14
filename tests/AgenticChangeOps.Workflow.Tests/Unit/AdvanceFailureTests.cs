using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Workflow.Tests.Fakes;

namespace AgenticChangeOps.Workflow.Tests.Unit;

public class AdvanceFailureTests
{
    private const string Sentinel = "https://sentinel-endpoint.invalid/ key=sentinel-secret-7f3a";

    private readonly WorkflowTestFixture _f = new();

    [Fact]
    public async Task UnknownId_IsNotFound_WithAChangeRequestIdError()
    {
        var result = await _f.Workflow.AdvanceAsync(Guid.NewGuid());

        Assert.Equal(AdvanceOutcome.NotFound, result.Outcome);
        Assert.Equal("ChangeRequestId", Assert.Single(result.Errors).FieldName);
        Assert.Null(result.Status);
    }

    [Theory]
    [InlineData(GenerationFailureCategory.Unreachable)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.ResponseTooLarge)]
    [InlineData(GenerationFailureCategory.MalformedOutput)]
    public async Task RequirementsStepFailure_CarriesOnlyTheCategory_AndStoresNothing(
        GenerationFailureCategory category)
    {
        var id = _f.SubmitSynthetic();
        var before = _f.Workflow.GetStatus(id).Value;
        _f.RequirementsGenerator.Behavior = (_, _) => throw new DraftGenerationException(category);

        var result = await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.StepFailed, result.Outcome);
        Assert.Equal(category, result.Requirements!.FailureCategory);
        Assert.Equal(0, _f.DraftCount(id));
        Assert.Equal(new[] { AuditEventType.Submitted }, _f.AuditTypes(id));
        Assert.Equal(before, result.Status);
        Assert.Equal(before, _f.Workflow.GetStatus(id).Value);
    }

    [Theory]
    [InlineData(GenerationFailureCategory.Unreachable)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.ResponseTooLarge)]
    [InlineData(GenerationFailureCategory.MalformedOutput)]
    public async Task DesignStepFailure_CarriesOnlyTheCategory_AndStoresNothing(
        GenerationFailureCategory category)
    {
        var id = await _f.ApprovedWithDraftAsync();
        var before = _f.Workflow.GetStatus(id).Value;
        _f.DesignGenerator.Behavior = (_, _, _) => throw new DraftGenerationException(category);

        var result = await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.StepFailed, result.Outcome);
        Assert.Equal(category, result.Design!.FailureCategory);
        Assert.Equal(0, _f.DesignCount(id));
        Assert.DoesNotContain(AuditEventType.DesignGenerated, _f.AuditTypes(id));
        Assert.Equal(before, _f.Workflow.GetStatus(id).Value);
    }

    [Fact]
    public async Task IncompleteContent_IsMalformedOutput_AndStoresNothing()
    {
        var id = await _f.ApprovedWithDraftAsync();
        _f.DesignGenerator.Behavior = (_, _, _) => Task.FromResult<AgenticChangeOps.Agents.Architect.DesignContent?>(
            FakeDesignGenerator.ValidContent() with { ApproachSummary = "   " });

        var result = await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.StepFailed, result.Outcome);
        Assert.Equal(GenerationFailureCategory.MalformedOutput, result.Design!.FailureCategory);
        Assert.Equal(0, _f.DesignCount(id));
    }

    [Fact]
    public async Task ArbitraryException_NeverLeaksItsMessage()
    {
        var id = _f.SubmitSynthetic();
        _f.RequirementsGenerator.Behavior = (_, _) => throw new InvalidOperationException(Sentinel);

        var result = await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.StepFailed, result.Outcome);
        var text = string.Join(" ", result.Requirements!.Errors.Select(e => e.FieldName + e.Message));
        Assert.DoesNotContain("sentinel", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sentinel", result.Status!.Reason?.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequirementsChangedWhileDesigning_IsTheAgentsOwnConflict_AndStoresNothing()
    {
        var id = await _f.ApprovedWithDraftAsync();
        _f.DesignGenerator.Behavior = async (_, _, _) =>
        {
            // A newer requirements draft appears while the design is being generated.
            _f.SeedStatus(id, ChangeRequestStatus.Submitted);
            await _f.Requirements.DraftAsync(id);
            _f.SeedStatus(id, ChangeRequestStatus.Approved);
            return FakeDesignGenerator.ValidContent();
        };

        var result = await _f.Workflow.AdvanceAsync(id);

        Assert.Equal(AdvanceOutcome.StepFailed, result.Outcome);
        Assert.Contains(result.Design!.Errors, e => e.FieldName == "RequirementsDraft");
        Assert.Equal(0, _f.DesignCount(id));
    }
}
