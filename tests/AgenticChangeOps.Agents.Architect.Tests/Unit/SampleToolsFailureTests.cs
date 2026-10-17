using AgenticChangeOps.Agents.Architect.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

/// <summary>US4: tools unavailable or limits exhausted fail with their own category and store nothing.</summary>
public sealed class SampleToolsFailureTests
{
    private static void AssertNothingStored(SampleToolsRig rig, Guid id)
    {
        Assert.Empty(rig.Designs(id));
        Assert.Equal(
            new[] { AuditEventType.Submitted, AuditEventType.Approved },
            rig.Audit(id).Select(e => e.EventType).ToArray());
        Assert.Equal(ChangeRequestStatus.Approved, rig.Store.FindById(id)!.Status);
    }

    [Fact]
    public async Task ToolsUnavailable_FailsBeforeAnyModelCall_WithItsOwnCategory_AndStoresNothing()
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();
        rig.Sessions.FailWith = new DraftGenerationException(GenerationFailureCategory.ToolsUnavailable);

        var result = await rig.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureCategory.ToolsUnavailable, result.FailureCategory);
        Assert.Equal(0, rig.ToolClient.CallCount);
        Assert.Equal(0, rig.PlainClient.CallCount);
        AssertNothingStored(rig, id);
    }

    [Fact]
    public async Task LimitsExhausted_FailsWithItsOwnCategory_AndStoresNothing_NotEvenTheCalls()
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();
        rig.ToolClient.Behavior = (session, _) =>
        {
            session.Read("orders-service/src/OrderIntake").Read("orders-service/src/StockCheck");
            throw new DraftGenerationException(GenerationFailureCategory.LimitsExhausted);
        };

        var result = await rig.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureCategory.LimitsExhausted, result.FailureCategory);
        AssertNothingStored(rig, id);
    }

    [Fact]
    public async Task TheTwoCategories_AreDistinctFromEachOtherAndFromTheExistingOnes()
    {
        var names = Enum.GetNames<GenerationFailureCategory>();

        Assert.Equal(names.Length, names.Distinct().Count());
        Assert.Contains("ToolsUnavailable", names);
        Assert.Contains("LimitsExhausted", names);
        Assert.NotEqual(GenerationFailureCategory.ToolsUnavailable, GenerationFailureCategory.LimitsExhausted);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ALimitReachedButAValidAnswerProduced_IsStoredNormally()
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();
        rig.ToolClient.Behavior = (session, _) =>
        {
            session.Read("orders-service/src/OrderIntake")
                .Refused("read_file", "orders-service/src/StockCheck", ToolCallOutcome.RefusedCallLimit)
                .Refused("search_text", "page", ToolCallOutcome.RefusedCallLimit);
            return Task.FromResult(new ChatModelResponse(FakeChatModelClient.ValidAnswerJson(), false));
        };

        var result = await rig.Designing.DesignAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "orders-service/src/OrderIntake" }, result.Draft!.SampleFilesRead);
        Assert.Equal(3, rig.Audit(id).Count(e => e.EventType == AuditEventType.ToolCalled));
    }

    [Fact]
    public async Task ARequestAfterAFailedOne_BehavesNormally_WithNoStateCarriedOver()
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();
        rig.Sessions.FailWith = new DraftGenerationException(GenerationFailureCategory.ToolsUnavailable);
        await rig.Designing.DesignAsync(id);
        rig.Sessions.FailWith = null;
        rig.ToolClient.Behavior = (session, _) =>
        {
            session.Read("orders-service/src/OrderIntake");
            return Task.FromResult(new ChatModelResponse(FakeChatModelClient.ValidAnswerJson(), false));
        };

        var result = await rig.Designing.DesignAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Draft!.Version);
        Assert.Equal(new[] { "orders-service/src/OrderIntake" }, result.Draft.SampleFilesRead);
    }

    [Theory]
    [InlineData(GenerationFailureCategory.ToolsUnavailable)]
    [InlineData(GenerationFailureCategory.LimitsExhausted)]
    public async Task TheFailureText_IsFixed_AndCarriesNothingFromTheToolsOrTheModel(GenerationFailureCategory category)
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();
        rig.ToolClient.Behavior = (_, _) => throw new DraftGenerationException(category);

        var result = await rig.Designing.DesignAsync(id);

        var text = string.Join(" ", result.Errors.Select(e => e.FieldName + " " + e.Message));
        Assert.Contains(category.ToString(), text);
        foreach (var forbidden in new[]
                 { "sentinel-endpoint", "sentinel-deployment", "FILE-CONTENT-SENTINEL", "orders-service/", "C:\\", "/home/", "/tmp/" })
            Assert.DoesNotContain(forbidden, text);
    }

    [Fact]
    public async Task AnUnexpectedToolClientException_ReportsOnlyItsType_NeverItsMessage()
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();
        rig.ToolClient.Behavior = (_, _) =>
            throw new InvalidOperationException("boom FILE-CONTENT-SENTINEL-0001 at C:\\secret\\place sentinel-endpoint");

        var result = await rig.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        var text = string.Join(" ", result.Errors.Select(e => e.Message));
        Assert.DoesNotContain("FILE-CONTENT-SENTINEL", text);
        Assert.DoesNotContain("C:\\secret", text);
        Assert.DoesNotContain("sentinel-endpoint", text);
        AssertNothingStored(rig, id);
    }
}
