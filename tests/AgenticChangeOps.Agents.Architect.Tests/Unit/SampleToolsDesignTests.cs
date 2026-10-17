using AgenticChangeOps.Agents.Architect.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

/// <summary>
/// US1: with tools on, the design is stored with the list of sample files actually read, as an
/// ordinary design draft otherwise. Clarification 1: reading nothing is valid.
/// </summary>
public sealed class SampleToolsDesignTests
{
    private static async Task<(SampleToolsRig Rig, Guid Id, DesignDraftingResult Result)> DesignWithReadsAsync(
        Action<FakeToolSession> reads)
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();
        rig.ToolClient.Behavior = (session, _) =>
        {
            reads(session);
            return Task.FromResult(new ChatModelResponse(FakeChatModelClient.ValidAnswerJson(), false));
        };

        var result = await rig.Designing.DesignAsync(id);
        return (rig, id, result);
    }

    [Fact]
    public async Task TheDraft_ListsTheFilesTheSessionSaysWereRead()
    {
        var (rig, id, result) = await DesignWithReadsAsync(s => s
            .List("orders-service")
            .Read("orders-service/config/orders.settings")
            .Read("orders-service/src/OrderIntake")
            .Search("page"));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { "orders-service/config/orders.settings", "orders-service/src/OrderIntake" },
            result.Draft!.SampleFilesRead);
        Assert.Equal(result.Draft.SampleFilesRead, rig.Designs(id).Single().SampleFilesRead);
    }

    [Fact]
    public async Task AFileTheDesignTextNamesButWasNotRead_IsNotListed()
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();
        rig.ToolClient.Behavior = (session, _) =>
        {
            session.Read("orders-service/src/OrderIntake");
            var answer = FakeChatModelClient.ValidAnswerJson().Replace(
                "Report scheduler configuration", "billing-portal/src/InvoiceList");
            return Task.FromResult(new ChatModelResponse(answer, false));
        };

        var result = await rig.Designing.DesignAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Contains("billing-portal/src/InvoiceList", result.Draft!.AffectedComponents);
        Assert.Equal(new[] { "orders-service/src/OrderIntake" }, result.Draft.SampleFilesRead);
    }

    [Fact]
    public async Task ARefusedRead_IsNotListed_AndListingsAndSearchesDoNotCount()
    {
        var (_, _, result) = await DesignWithReadsAsync(s => s
            .Refused("read_file", "../secret", ToolCallOutcome.RefusedOutsideSample)
            .List("orders-service")
            .Search("needle"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Draft!.SampleFilesRead);
        Assert.Empty(result.Draft.SampleFilesRead!);
    }

    [Fact]
    public async Task ReadingNothing_IsValid_AndStoresAnEmptyList_NotNull()
    {
        var (_, _, result) = await DesignWithReadsAsync(_ => { });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Draft!.SampleFilesRead);
        Assert.Empty(result.Draft.SampleFilesRead!);
    }

    [Fact]
    public async Task TheDraft_IsAnOrdinaryDesignDraft_OtherwiseAndTheStatusIsUnchanged()
    {
        var (rig, id, result) = await DesignWithReadsAsync(s => s.Read("orders-service/src/OrderIntake"));

        var draft = result.Draft!;
        Assert.Equal(1, draft.Version);
        Assert.Equal(1, draft.RequirementsVersion);
        Assert.Equal(DesignDraftStatus.Draft, draft.Status);
        Assert.Equal(DesignDraft.ArchitectAgentName, draft.AgentName);
        Assert.Equal(ChangeRequestStatus.Approved, rig.Store.FindById(id)!.Status);
        Assert.Equal(1, rig.Audit(id).Count(e => e.EventType == AuditEventType.DesignGenerated));
    }

    [Fact]
    public async Task ASecondDesign_IsVersionTwo_WithItsOwnFileList()
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();
        var reads = new Queue<string>(new[] { "orders-service/src/OrderIntake", "orders-service/src/StockCheck" });
        rig.ToolClient.Behavior = (session, _) =>
        {
            session.Read(reads.Dequeue());
            return Task.FromResult(new ChatModelResponse(FakeChatModelClient.ValidAnswerJson(), false));
        };

        await rig.Designing.DesignAsync(id);
        var second = await rig.Designing.DesignAsync(id);

        Assert.Equal(2, second.Draft!.Version);
        Assert.Equal(new[] { "orders-service/src/StockCheck" }, second.Draft.SampleFilesRead);
        Assert.Equal(2, rig.Designs(id).Count);
    }

    [Fact]
    public async Task TheToolsOnSystemText_StartsWithTheUnchangedText_AndNamesTheThreeTools()
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();

        await rig.Designing.DesignAsync(id);

        var (plainSystem, _) = DesignPromptBuilder.Build(rig.Store.FindById(id)!, FakeRequirementsBasisSource.Basis(1));
        var toolsSystem = rig.ToolClient.SystemInstruction!;
        Assert.StartsWith(plainSystem, toolsSystem);
        Assert.NotEqual(plainSystem, toolsSystem);
        foreach (var tool in new[] { "list_files", "read_file", "search_text" })
            Assert.Contains(tool, toolsSystem);
        Assert.Equal(0, rig.PlainClient.CallCount);
        Assert.Equal(1, rig.ToolClient.CallCount);
    }

    [Fact]
    public async Task EverySession_IsClosed_Success_Failure_OrCancellation()
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();

        await rig.Designing.DesignAsync(id);
        rig.ToolClient.Behavior = (_, _) => throw new DraftGenerationException(GenerationFailureCategory.LimitsExhausted);
        await rig.Designing.DesignAsync(id);
        rig.ToolClient.Behavior = (_, _) => Task.FromResult(new ChatModelResponse("not json", false));
        await rig.Designing.DesignAsync(id);

        Assert.Equal(3, rig.Sessions.Opened.Count);
        Assert.All(rig.Sessions.Opened, s => Assert.Equal(1, s.Disposals));
    }

    [Fact]
    public async Task TwoConcurrentDesigns_GetTheirOwnSessions_AndTheirOwnFileLists()
    {
        var rig = new SampleToolsRig();
        var first = rig.ApprovedOrdersRequest();
        var second = rig.ApprovedOrdersRequest();
        var gate = new TaskCompletionSource();
        var count = 0;
        rig.ToolClient.Behavior = async (session, _) =>
        {
            var n = Interlocked.Increment(ref count);
            session.Read(n == 1 ? "orders-service/src/OrderIntake" : "billing-portal/src/InvoiceList");
            if (n == 1)
                await gate.Task;
            else
                gate.TrySetResult();
            return new ChatModelResponse(FakeChatModelClient.ValidAnswerJson(), false);
        };

        var results = await Task.WhenAll(rig.Designing.DesignAsync(first), rig.Designing.DesignAsync(second));

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Equal(2, rig.Sessions.Opened.Count);
        Assert.NotEqual(results[0].Draft!.SampleFilesRead!.Single(), results[1].Draft!.SampleFilesRead!.Single());
    }
}
