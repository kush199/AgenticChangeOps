using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

public sealed class ProposalResponseParserTests
{
    private const string LeakMarker = "synthetic-proposal-answer-marker-91ad";
    private static readonly string Fence = new('`', 3);

    private static void AssertMalformed(string text)
    {
        var ex = Assert.Throws<DraftGenerationException>(() => ProposalResponseParser.Parse(text));

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
        Assert.DoesNotContain(LeakMarker, ex.ToString());
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void ABareObject_IsRead_IncludingNestedChanges()
    {
        var content = ProposalResponseParser.Parse(FakeProposalGenerator.ValidAnswerJson());

        Assert.Equal("Move the nightly report hour from 2 to 4 in the portal settings.", content.Summary);
        var change = Assert.Single(content.Changes!);
        Assert.Equal(FakeProposalGenerator.SettingsPath, change.Path);
        Assert.Equal("change", change.Kind);
        Assert.Equal("report_hour = 2", change.ExistingText);
        Assert.Equal("report_hour = 4", change.ReplacementText);
        Assert.Single(content.Risks!);
        Assert.Single(content.TestIdeas!);
        Assert.Single(content.OpenQuestions!);
    }

    [Fact]
    public void OneFencedBlock_IsRead()
    {
        var content = ProposalResponseParser.Parse($"{Fence}json\n{FakeProposalGenerator.ValidAnswerJson()}\n{Fence}");

        Assert.NotNull(content.Summary);
    }

    [Fact]
    public void AnAddWithoutAQuote_IsReadWithANullQuote()
    {
        var content = ProposalResponseParser.Parse("""
            { "summary": "s", "changes": [ { "path": "a/new.md", "kind": "add", "replacementText": "text" } ],
              "risks": ["r"], "testIdeas": [], "openQuestions": [] }
            """);

        Assert.Null(content.Changes![0].ExistingText);
    }

    [Fact]
    public void FilesRead_ToolCalls_AndAgentFields_AreNeverTakenFromTheAnswer()
    {
        var content = ProposalResponseParser.Parse("""
            { "summary": "s", "changes": [], "risks": [], "testIdeas": [], "openQuestions": [],
              "sampleFilesRead": ["fake/file"], "verificationNote": "Verified and tested.", "agentName": "Other" }
            """);

        Assert.Null(content.SampleFilesRead);
        Assert.Null(content.ToolCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain prose")]
    [InlineData("[]")]
    [InlineData("{ \"summary\": \"s\" }")]
    [InlineData("{ \"summary\": 7, \"changes\": [], \"risks\": [], \"testIdeas\": [], \"openQuestions\": [] }")]
    [InlineData("{ \"summary\": \"s\", \"changes\": [ \"x\" ], \"risks\": [], \"testIdeas\": [], \"openQuestions\": [] }")]
    [InlineData("{ \"summary\": \"s\", \"changes\": [ { \"path\": 3 } ], \"risks\": [], \"testIdeas\": [], \"openQuestions\": [] }")]
    public void AnythingElse_IsMalformed(string text) => AssertMalformed(text);

    [Fact]
    public void Malformed_NeverCarriesTheAnswerText() =>
        AssertMalformed("{ \"summary\": " + LeakMarker);
}
