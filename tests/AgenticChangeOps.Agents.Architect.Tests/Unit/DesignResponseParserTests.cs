using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignResponseParserTests
{
    private static readonly string Valid = FakeChatModelClient.ValidAnswerJson();

    private static void AssertMalformed(string text)
    {
        var ex = Assert.Throws<DraftGenerationException>(() => DesignResponseParser.Parse(text));
        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void JsonObjectAlone_IsParsedIntoTheSixSections()
    {
        var content = DesignResponseParser.Parse(Valid);

        ContentAssert.Equal(FakeDesignGenerator.ValidContent(), content);
    }

    [Theory]
    [InlineData("```json\n{0}\n```")]
    [InlineData("```\n{0}\n```")]
    [InlineData("  ```JSON\n{0}\n```  ")]
    public void JsonInsideOneCodeFence_IsParsed(string template)
    {
        var content = DesignResponseParser.Parse(template.Replace("{0}", Valid));

        ContentAssert.Equal(FakeDesignGenerator.ValidContent(), content);
    }

    [Fact]
    public void PresentButEmptyLists_ArePassedThroughUnchanged()
    {
        var json = """
            { "approachSummary": "x", "affectedComponents": [], "impactAndRisks": [],
              "dependencies": [], "rollbackConsiderations": [], "openQuestions": [] }
            """;

        var content = DesignResponseParser.Parse(json);

        Assert.Equal("x", content.ApproachSummary);
        Assert.Empty(content.AffectedComponents!);
        Assert.Empty(content.OpenQuestions!);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("[ \"a\" ]")]
    [InlineData("\"just a string\"")]
    [InlineData("Here is the design: {}")]
    [InlineData("```python\n{}\n```")]
    [InlineData("```json\n{ }\n``` trailing")]
    public void UnusableText_IsMalformedOutput(string text) => AssertMalformed(text);

    [Fact]
    public void MissingKey_IsMalformedOutput() =>
        AssertMalformed("""{ "approachSummary": "x", "affectedComponents": [] }""");

    [Fact]
    public void WrongValueKinds_AreMalformedOutput()
    {
        AssertMalformed(Valid.Replace("\"approachSummary\": \"Move", "\"approachSummary\": 5, \"x\": \"Move"));
        AssertMalformed("""
            { "approachSummary": "x", "affectedComponents": "not a list", "impactAndRisks": [],
              "dependencies": [], "rollbackConsiderations": [], "openQuestions": [] }
            """);
        AssertMalformed("""
            { "approachSummary": "x", "affectedComponents": [ 1, 2 ], "impactAndRisks": [],
              "dependencies": [], "rollbackConsiderations": [], "openQuestions": [] }
            """);
    }

    [Fact]
    public void ExtraUnrecognisedKeys_AreIgnoredAndOnlyTheSixSectionsAreRead()
    {
        var withExtras = Valid.Replace(
            "\"openQuestions\"",
            "\"costEstimate\": [ \"never stored\" ], \"approved\": true, \"openQuestions\"");
        Assert.Contains("costEstimate", withExtras);

        var content = DesignResponseParser.Parse(withExtras);

        ContentAssert.Equal(FakeDesignGenerator.ValidContent(), content);
    }

    [Fact]
    public async Task ExtraUnrecognisedKeys_NeverReachAStoredDesign()
    {
        var f = new DesigningTestFixture();
        var client = new FakeChatModelClient();
        client.Behavior = FakeChatModelClient.ReturnsText(Valid.Replace(
            "\"openQuestions\"",
            "\"costEstimate\": [ \"never stored\" ], \"openQuestions\""));
        var options = new AzureOpenAIGeneratorOptions(
            new Uri("https://synthetic-models.invalid/"), "synthetic-deployment", TimeSpan.FromSeconds(30), 32000);
        var service = new DesignDraftingService(
            f.Store, f.Clock, f.Ids, f.Basis,
            new AzureOpenAIDesignGenerator(options, client), new DesignContentValidator());
        var id = f.ApproveWithBasis();

        var result = await service.DesignAsync(id);

        Assert.True(result.IsSuccess);
        var stored = System.Text.Json.JsonSerializer.Serialize(result.Draft);
        Assert.DoesNotContain("costEstimate", stored);
        Assert.DoesNotContain("never stored", stored);
        Assert.Equal(FakeDesignGenerator.ValidContent().ApproachSummary, result.Draft!.ApproachSummary);
    }

    [Fact]
    public void TheAnswerText_IsNeverEchoedInTheFailure()
    {
        const string secret = "https://synthetic-models.invalid/secret-deployment";

        var ex = Assert.Throws<DraftGenerationException>(() => DesignResponseParser.Parse("oops " + secret));

        Assert.DoesNotContain(secret, ex.Message);
        Assert.DoesNotContain("oops", ex.Message);
    }
}
