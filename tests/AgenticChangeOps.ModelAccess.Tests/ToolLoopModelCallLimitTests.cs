using AgenticChangeOps.ModelAccess.Tests.Fakes;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// FR-012, FR-013, FR-016: the model-call limit is exact, a limit alone is not a failure, the time
/// limit ends the request, and nothing is retried. Per T004 the framework's own iteration cap allows
/// one call too many and returns quietly with no text, so the limit is enforced by a counting wrapper.
/// </summary>
public sealed class ToolLoopModelCallLimitTests
{
    private static Task<ChatModelResponse> RunAsync(
        ScriptedToolChat chat, FakeToolSession session, CancellationToken cancellationToken = default) =>
        new AgentFrameworkToolChatModelClient(chat)
            .CompleteAsync("system text", "user text", 1000, session, cancellationToken);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task AModelThatKeepsAskingForTools_IsStoppedAtExactlyTheLimit_AsLimitsExhausted(int limit)
    {
        var chat = ScriptedToolChat.AlwaysAsks();
        var session = new FakeToolSession(maxModelCalls: limit);

        var error = await Assert.ThrowsAsync<DraftGenerationException>(() => RunAsync(chat, session));

        Assert.Equal(GenerationFailureCategory.LimitsExhausted, error.Category);
        Assert.Equal(limit, chat.Calls);
    }

    [Fact]
    public async Task AFinalAnswerOnTheLastAllowedCall_IsReturned_NotDiscarded()
    {
        var chat = ScriptedToolChat.AsksThenAnswers(finalCall: 3, text: "{\"ok\":true}");
        var session = new FakeToolSession(maxModelCalls: 3);

        var response = await RunAsync(chat, session);

        Assert.Equal("{\"ok\":true}", response.Text);
        Assert.False(response.WasCutOff);
        Assert.Equal(3, chat.Calls);
        Assert.Equal(2, session.Invoked.Count);
    }

    [Fact]
    public async Task AnImmediateAnswer_IsOneModelCall_AndNoToolCall()
    {
        var chat = ScriptedToolChat.AsksThenAnswers(finalCall: 1, text: "{}");
        var session = new FakeToolSession(maxModelCalls: 6);

        await RunAsync(chat, session);

        Assert.Equal(1, chat.Calls);
        Assert.Empty(session.Invoked);
    }

    [Fact]
    public async Task AFailedToolCall_IsNeverRetried_AndTheLoopCarriesOn()
    {
        var chat = ScriptedToolChat.AsksThenAnswers(finalCall: 3, text: "{\"ok\":true}");
        var session = new FakeToolSession(
            maxModelCalls: 6, failure: (_, _) => new InvalidOperationException("tool broke at C:\\somewhere\\secret"));

        var response = await RunAsync(chat, session);

        Assert.Equal("{\"ok\":true}", response.Text);
        Assert.Equal(2, session.Invoked.Count);
        Assert.Equal(3, chat.Calls);
    }

    [Fact]
    public async Task TheTimeLimit_EndsARequestThatHasNoFinalAnswer_AsLimitsExhausted()
    {
        var chat = new ScriptedToolChat(call => call == 1 ? new ScriptedToolChat.AsksForTool("read_file") : new ScriptedToolChat.Waits());
        var session = new FakeToolSession(maxModelCalls: 6, timeLimit: TimeSpan.FromMilliseconds(300));

        var error = await Assert.ThrowsAsync<DraftGenerationException>(() => RunAsync(chat, session));

        Assert.Equal(GenerationFailureCategory.LimitsExhausted, error.Category);
    }

    [Fact]
    public async Task TheCallersOwnCancellation_IsNotReportedAsALimit()
    {
        var chat = new ScriptedToolChat(_ => new ScriptedToolChat.Waits());
        var session = new FakeToolSession(timeLimit: TimeSpan.FromSeconds(60));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(chat, session, cts.Token));
    }

    [Fact]
    public async Task TheFailureMessages_NeverCarryToolOrModelText()
    {
        var chat = ScriptedToolChat.AlwaysAsks();
        var session = new FakeToolSession(maxModelCalls: 2, result: (_, _) => "TOOL-SENTINEL-0001");

        var error = await Assert.ThrowsAsync<DraftGenerationException>(() => RunAsync(chat, session));

        Assert.DoesNotContain("TOOL-SENTINEL-0001", error.Message);
        Assert.Null(error.InnerException);
    }
}
