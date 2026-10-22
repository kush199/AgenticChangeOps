using AgenticChangeOps.Agents.Reviewer.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>
/// The real generator over a scripted plain chat client: one model call, never a retry, every failure
/// a category and nothing else, and no way to be given a tool.
/// </summary>
public sealed class ReviewGeneratorIntegrationTests
{
    private const string Endpoint = "sentinel-endpoint.invalid";
    private const string Deployment = "sentinel-deployment";

    private readonly FakeReviewChatClient _client = new();

    private AzureOpenAIReviewGenerator Generator() => new(ReviewTestRequests.Options(), _client);

    private Task<ReviewContent?> Generate(SystemCheck? check = null) => Generator().GenerateAsync(
        ReviewTestRequests.Request(),
        FakeReviewBasisSource.Requirements(),
        FakeReviewBasisSource.Design(),
        FakeReviewBasisSource.Proposal(),
        check ?? ReviewTestRequests.Performed(EditCheckOutcome.Found, EditCheckOutcome.NewFileFree, EditCheckOutcome.Found),
        CancellationToken.None);

    // ---- the happy path ---------------------------------------------------------------

    [Fact]
    public async Task OneModelCallIsMade_AndTheAnswerIsReadIntoContent()
    {
        var content = await Generate();

        Assert.Equal(1, _client.CallCount);
        Assert.Equal(2, content!.Findings!.Count);
        Assert.Equal("AcceptanceCriteria", content.Findings[0].Category);
        Assert.Equal(new[] { "requirements.acceptanceCriteria[2]", "proposal.testIdeas[1]" }, content.Findings[0].References);
        Assert.Equal(new[] { "Who owns the notes file?" }, content.OpenQuestions);
    }

    [Fact]
    public async Task TheModelIsAskedWithThePromptTheBuilderMakes()
    {
        var check = ReviewTestRequests.Performed(EditCheckOutcome.QuoteNotFound, EditCheckOutcome.NewFileFree, EditCheckOutcome.Found);

        await Generate(check);

        var (system, user) = ReviewPromptBuilder.Build(
            ReviewTestRequests.Request(),
            FakeReviewBasisSource.Requirements(),
            FakeReviewBasisSource.Design(),
            FakeReviewBasisSource.Proposal(),
            check);
        Assert.Equal(system, _client.SystemInstruction);
        Assert.Equal(user, _client.UserContent);
        Assert.StartsWith(ReviewPromptBuilder.DataDelimiter, _client.UserContent);
    }

    [Fact]
    public async Task TheSystemCheckReachesTheModelAsData_ButNothingTheModelWritesChangesIt()
    {
        var check = ReviewTestRequests.Performed(EditCheckOutcome.QuoteAmbiguous, EditCheckOutcome.NewFileExists, EditCheckOutcome.Found);
        _client.Answering("""
            { "findings": [], "openQuestions": [],
              "systemCheck": { "status": "NotPerformed", "edits": [] } }
            """);

        var content = await Generate(check);

        Assert.Contains("QuoteAmbiguous", _client.UserContent);
        Assert.Contains("NewFileExists", _client.UserContent);
        Assert.NotNull(content);
        Assert.Equal(new[] { "Findings", "OpenQuestions" }, typeof(ReviewContent).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public async Task TheOutputBudget_IsDerivedFromTheOptions()
    {
        await Generate();

        Assert.Equal((ReviewTestRequests.Options().MaxResponseCharacters + 2) / 3, _client.MaxOutputTokens);
    }

    // ---- failures are categories and nothing else ------------------------------------

    public static IEnumerable<object[]> Categories() =>
        Enum.GetValues<GenerationFailureCategory>().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Categories))]
    public async Task AClientFailureWithACategory_SurfacesAsThatCategory_AfterExactlyOneCall(GenerationFailureCategory category)
    {
        _client.Throwing(new DraftGenerationException(category));

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(() => Generate());

        Assert.Equal(category, ex.Category);
        Assert.Equal(1, _client.CallCount);
    }

    [Fact]
    public async Task AnUnexpectedClientException_BecomesACategory_NeverCarryingItsMessageOrItself()
    {
        _client.Throwing(new InvalidOperationException("boom at https://" + Endpoint + "/deployments/" + Deployment));

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(() => Generate());

        Assert.Null(ex.InnerException);
        Assert.DoesNotContain(Endpoint, ex.ToString());
        Assert.DoesNotContain(Deployment, ex.ToString());
        Assert.Equal(1, _client.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("I could not review this.")]
    [InlineData("{ \"findings\": [], \"openQuestions\": ")] // cut off in the middle
    [InlineData("{ \"findings\": 3, \"openQuestions\": [] }")]
    public async Task AnUnusableAnswer_IsMalformedOutput_AfterOneCall(string answer)
    {
        _client.Answering(answer);

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(() => Generate());

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
        // A blank answer has no text to leak; for the others, compare ordinally.
        if (answer.Trim().Length > 0)
            Assert.DoesNotContain(answer, ex.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, _client.CallCount);
    }

    [Fact]
    public async Task AnAnswerThatWasCutOff_IsResponseTooLarge()
    {
        _client.Answering(FakeReviewChatClient.ValidAnswerJson, cutOff: true);

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(() => Generate());

        Assert.Equal(GenerationFailureCategory.ResponseTooLarge, ex.Category);
    }

    [Fact]
    public async Task AnAnswerOverTheCharacterLimit_IsResponseTooLarge()
    {
        _client.Answering(new string('x', ReviewTestRequests.Options().MaxResponseCharacters + 1));

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(() => Generate());

        Assert.Equal(GenerationFailureCategory.ResponseTooLarge, ex.Category);
    }

    [Fact]
    public async Task ACallerCancellation_IsNotTurnedIntoACategory()
    {
        using var source = new CancellationTokenSource();
        _client.Behavior = () =>
        {
            source.Cancel();
            throw new OperationCanceledException(source.Token);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Generator().GenerateAsync(
            ReviewTestRequests.Request(),
            FakeReviewBasisSource.Requirements(),
            FakeReviewBasisSource.Design(),
            FakeReviewBasisSource.Proposal(),
            ReviewTestRequests.Performed(),
            source.Token));
    }

    // ---- no tool path -----------------------------------------------------------------

    [Fact]
    public void TheGenerator_HasOneConstruction_TakingOnlyOptionsAndAPlainChatClient()
    {
        var constructor = Assert.Single(typeof(AzureOpenAIReviewGenerator).GetConstructors());

        Assert.Equal(
            new[] { typeof(AzureOpenAIGeneratorOptions), typeof(IChatModelClient) },
            constructor.GetParameters().Select(p => p.ParameterType));
    }

    [Fact]
    public void NoPublicMemberOfTheGenerator_MentionsATool()
    {
        var names = typeof(AzureOpenAIReviewGenerator).GetMembers().Select(m => m.Name)
            .Concat(typeof(AzureOpenAIReviewGenerator).GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType.Name));

        Assert.DoesNotContain(names, n => n.Contains("Tool", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheConstruction_RefusesMissingParts()
    {
        Assert.Throws<ArgumentNullException>(() => new AzureOpenAIReviewGenerator(null!, _client));
        Assert.Throws<ArgumentNullException>(() => new AzureOpenAIReviewGenerator(ReviewTestRequests.Options(), null!));
    }
}
