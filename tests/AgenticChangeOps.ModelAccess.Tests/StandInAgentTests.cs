using AgenticChangeOps.ModelAccess.Tests.Fakes;

namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// Proves FR-011 / SC-006: a further model-backed agent can use every shared piece by adding
/// only itself. The stand-in below has its own content type, section names and required
/// choices, lives only in this test project, and needs no change to the shared code or to
/// either shipped agent. This file references nothing but the shared project.
/// </summary>
public class StandInAgentTests
{
    private sealed record StandInContent(string Headline, IReadOnlyList<string> Steps, IReadOnlyList<string> Notes);

    private sealed record StandInResult(StandInContent? Content, IReadOnlyList<string> Errors);

    private sealed class StandInAgent
    {
        private readonly ModelCaller _caller;

        public StandInAgent(AzureOpenAIGeneratorOptions options, IChatModelClient client) =>
            _caller = new ModelCaller(options, client);

        public async Task<StandInResult> RunAsync(CancellationToken cancellationToken)
        {
            // The agent's own prompt, the shared call.
            var text = await _caller
                .GetAnswerTextAsync("Describe the steps.", "A synthetic change.", cancellationToken)
                .ConfigureAwait(false);

            // The agent's own section names, the shared reader.
            var raw = ModelAnswerReader.Read(text, a => new StandInContent(
                a.ReadString("headline"), a.ReadList("steps"), a.ReadList("notes")));

            // The agent's own required choices, the shared normaliser.
            var errors = new List<string>();
            var headline = SectionNormaliser.Text(raw.Headline, "Headline");
            var steps = SectionNormaliser.List(raw.Steps, "Steps", requireItem: true);
            var notes = SectionNormaliser.List(raw.Notes, "Notes", requireItem: false);

            foreach (var error in new[] { headline.Error, steps.Error, notes.Error })
                if (error is not null)
                    errors.Add(error);

            return errors.Count > 0
                ? new StandInResult(null, errors)
                : new StandInResult(new StandInContent(headline.Value!, steps.Value!, notes.Value!), errors);
        }
    }

    private static (StandInAgent Agent, ScriptedChatModelClient Client) Create()
    {
        var client = new ScriptedChatModelClient();
        var options = new AzureOpenAIGeneratorOptions(
            new Uri("https://synthetic-models.invalid/"), "synthetic-deployment", TimeSpan.FromSeconds(30), 32000);
        return (new StandInAgent(options, client), client);
    }

    private const string Valid =
        "{ \"headline\": \" Roll out in two waves \", \"steps\": [ \" wave one \", \"\", \"wave two\" ], \"notes\": [] }";

    [Fact]
    public async Task ValidAnswer_IsCalledReadAndNormalisedWithTheAgentsOwnSections()
    {
        var (agent, client) = Create();
        client.Behavior = ScriptedChatModelClient.ReturnsText(Valid);

        var result = await agent.RunAsync(CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal("Roll out in two waves", result.Content!.Headline);
        Assert.Equal(new[] { "wave one", "wave two" }, result.Content.Steps);
        Assert.Empty(result.Content.Notes);
        Assert.Equal("Describe the steps.", client.SystemInstruction);
    }

    [Fact]
    public async Task FencedAnswer_IsRead()
    {
        var (agent, client) = Create();
        client.Behavior = ScriptedChatModelClient.ReturnsText("```json\n" + Valid + "\n```");

        var result = await agent.RunAsync(CancellationToken.None);

        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task OwnRequiredChoice_IsApplied_PerSection()
    {
        var (agent, client) = Create();
        client.Behavior = ScriptedChatModelClient.ReturnsText(
            "{ \"headline\": \"  \", \"steps\": [ \"\" ], \"notes\": [] }");

        var result = await agent.RunAsync(CancellationToken.None);

        Assert.Null(result.Content);
        Assert.Equal(
            new[]
            {
                "Headline is required and must not be blank.",
                "Steps must contain at least one non-blank item."
            },
            result.Errors);
    }

    [Fact]
    public async Task MalformedAnswer_IsReportedAsMalformedOutput()
    {
        var (agent, client) = Create();
        client.Behavior = ScriptedChatModelClient.ReturnsText("{ \"headline\": \"only this\" }");

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(() => agent.RunAsync(CancellationToken.None));

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
    }

    [Fact]
    public async Task ModelFailure_IsReportedWithItsCategory()
    {
        var (agent, client) = Create();
        client.Behavior = ScriptedChatModelClient.Throws(new HttpRequestException("synthetic"));

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(() => agent.RunAsync(CancellationToken.None));

        Assert.Equal(GenerationFailureCategory.Unreachable, ex.Category);
    }

    [Fact]
    public async Task EmptyAnswer_IsReportedAsMalformedOutput()
    {
        var (agent, client) = Create();
        client.Behavior = ScriptedChatModelClient.ReturnsText(string.Empty);

        var ex = await Assert.ThrowsAsync<DraftGenerationException>(() => agent.RunAsync(CancellationToken.None));

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
    }
}
