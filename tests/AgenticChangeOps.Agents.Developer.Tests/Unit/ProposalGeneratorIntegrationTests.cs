using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>
/// The real generator over a scripted tool client and a fake session: one conversation with the
/// tools, the session always closed, and the files read and calls taken from the session only.
/// </summary>
public sealed class ProposalGeneratorIntegrationTests
{
    private static readonly AzureOpenAIGeneratorOptions Options = new(
        new Uri("https://sentinel-endpoint.invalid/"), "sentinel-deployment", TimeSpan.FromSeconds(30), 32000);

    private static readonly ChangeRequest Request = new(
        Guid.NewGuid(), "Move nightly report schedule", "Move the job.", "Avery Quill",
        "Sample Billing Portal", Priority.Medium, ChangeType.ConfigChange,
        ChangeRequestStatus.Approved, DateTime.UnixEpoch);

    private readonly FakeToolSessionFactory _sessions = new();
    private readonly FakeToolChatModelClient _toolClient = new();
    private readonly FakeChatModelClient _plainClient = new();

    private AzureOpenAIProposalGenerator Generator() => new(Options, _plainClient, _sessions, _toolClient);

    private Task<ProposalContent?> Generate() => Generator().GenerateAsync(
        Request, FakeProposalBasisSource.Requirements(), FakeProposalBasisSource.Design(), CancellationToken.None);

    [Fact]
    public void ThereIsNoToolLessConstruction()
    {
        var constructors = typeof(AzureOpenAIProposalGenerator).GetConstructors();

        var only = Assert.Single(constructors);
        Assert.Equal(4, only.GetParameters().Length);
        Assert.Throws<ArgumentNullException>(() => new AzureOpenAIProposalGenerator(Options, _plainClient, null!, _toolClient));
        Assert.Throws<ArgumentNullException>(() => new AzureOpenAIProposalGenerator(Options, _plainClient, _sessions, null!));
    }

    [Fact]
    public async Task OneSessionIsOpened_OneToolConversationHappens_AndTheSessionIsClosed()
    {
        var content = await Generate();

        Assert.NotNull(content);
        Assert.Single(_sessions.Opened);
        Assert.Equal(1, _toolClient.CallCount);
        Assert.Equal(0, _plainClient.CallCount);
        Assert.Equal(1, _sessions.Opened[0].Disposals);
    }

    [Fact]
    public async Task TheModelIsAskedWithTheProposalPrompt()
    {
        await Generate();

        Assert.Contains("read_file", _toolClient.SystemInstruction);
        Assert.StartsWith(ProposalPromptBuilder.DataDelimiter, _toolClient.UserContent);
        Assert.Contains("Change the report hour in the portal settings only.", _toolClient.UserContent);
    }

    [Fact]
    public async Task FilesRead_AndCalls_ComeFromTheSession_NeverFromTheAnswerText()
    {
        _toolClient.Behavior = (session, _) =>
        {
            session.Read("billing-portal/config/portal.settings", bytes: 200).List("billing-portal");
            return Task.FromResult(new ChatModelResponse("""
                { "summary": "s", "changes": [ { "path": "a/new.md", "kind": "add", "replacementText": "t" } ],
                  "risks": ["r"], "testIdeas": [], "openQuestions": [], "sampleFilesRead": ["lie/file"] }
                """, false));
        };

        var content = await Generate();

        Assert.Equal(new[] { "billing-portal/config/portal.settings" }, content!.SampleFilesRead);
        Assert.Equal(new[] { "read_file", "list_files" }, content.ToolCalls!.Select(c => c.ToolName));
        Assert.Equal(200, content.ToolCalls![0].ResultBytes);
    }

    [Fact]
    public async Task AFactoryThatCannotOpen_SurfacesToolsUnavailable_BeforeAnyModelCall()
    {
        _sessions.FailWith = new DraftGenerationException(GenerationFailureCategory.ToolsUnavailable);

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(Generate);

        Assert.Equal(GenerationFailureCategory.ToolsUnavailable, ex.Category);
        Assert.Equal(0, _toolClient.CallCount);
    }

    [Theory]
    [InlineData(GenerationFailureCategory.LimitsExhausted)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.Unreachable)]
    public async Task AClientFailure_SurfacesItsCategory_AndTheSessionIsStillClosed(GenerationFailureCategory category)
    {
        _toolClient.Behavior = (_, _) => throw new DraftGenerationException(category);

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(Generate);

        Assert.Equal(category, ex.Category);
        Assert.Equal(1, _sessions.Opened[0].Disposals);
    }

    [Fact]
    public async Task AnUnexpectedClientException_BecomesACategory_WithNoMessageAttached()
    {
        _toolClient.Behavior = (_, _) => throw new InvalidOperationException("https://leak.invalid secret");

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(Generate);

        Assert.DoesNotContain("leak.invalid", ex.ToString());
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public async Task AnUnusableAnswer_IsMalformedOutput()
    {
        _toolClient.Behavior = (_, _) => Task.FromResult(new ChatModelResponse("not json at all", false));

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(Generate);

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
    }

    [Fact]
    public async Task ACutOffAnswer_IsResponseTooLarge()
    {
        _toolClient.Behavior = (_, _) => Task.FromResult(new ChatModelResponse("{", true));

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(Generate);

        Assert.Equal(GenerationFailureCategory.ResponseTooLarge, ex.Category);
    }

    [Fact]
    public async Task ASecondRequest_OpensItsOwnSession()
    {
        await Generate();
        await Generate();

        Assert.Equal(2, _sessions.Opened.Count);
        Assert.NotSame(_sessions.Opened[0], _sessions.Opened[1]);
        Assert.All(_sessions.Opened, s => Assert.Equal(1, s.Disposals));
    }
}
