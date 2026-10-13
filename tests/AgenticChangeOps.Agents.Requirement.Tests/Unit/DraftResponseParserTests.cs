using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Tests.Fakes;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Requirement.Tests.Fakes;

namespace AgenticChangeOps.Agents.Requirement.Tests.Unit;

public class DraftResponseParserTests
{
    [Fact]
    public void BareObject_ParsesToMatchingDraftContent()
    {
        var content = DraftResponseParser.Parse(FakeChatModelClient.ValidAnswerJson());

        Assert.Equal("Move the nightly Sample Billing Portal report job to a quieter window.", content.Summary);
        Assert.Equal(2, content.FunctionalRequirements!.Count);
        Assert.Equal("The report job runs at the newly configured time each night.", content.FunctionalRequirements[0]);
        Assert.Equal(2, content.AcceptanceCriteria!.Count);
        Assert.Equal(new[] { "The new window has no competing batch jobs." }, content.Assumptions);
        Assert.Equal(new[] { "Should weekend runs keep the old schedule?" }, content.OpenQuestions);
    }

    [Theory]
    [InlineData("```\n{0}\n```")]
    [InlineData("```json\n{0}\n```")]
    [InlineData("  \n```JSON\n{0}\n```  \n")]
    public void ObjectInsideOneCodeFence_ParsesIdentically(string wrapper)
    {
        var bare = DraftResponseParser.Parse(FakeChatModelClient.ValidAnswerJson());

        var fenced = DraftResponseParser.Parse(wrapper.Replace("{0}", FakeChatModelClient.ValidAnswerJson()));

        Assert.Equal(bare.Summary, fenced.Summary);
        Assert.Equal(bare.FunctionalRequirements, fenced.FunctionalRequirements);
        Assert.Equal(bare.AcceptanceCriteria, fenced.AcceptanceCriteria);
        Assert.Equal(bare.Assumptions, fenced.Assumptions);
        Assert.Equal(bare.OpenQuestions, fenced.OpenQuestions);
    }

    [Fact]
    public void UnknownExtraKey_IsIgnored()
    {
        var json = """
            { "summary": "S", "functionalRequirements": ["F"], "acceptanceCriteria": ["A"],
              "assumptions": [], "openQuestions": [], "confidence": "high" }
            """;

        var content = DraftResponseParser.Parse(json);

        Assert.Equal("S", content.Summary);
        Assert.Equal(new[] { "F" }, content.FunctionalRequirements);
    }

    [Fact]
    public void PresentButEmptyAssumptionsAndQuestions_PassThroughAsEmptyLists()
    {
        var json = """
            { "summary": "S", "functionalRequirements": ["F"], "acceptanceCriteria": ["A"],
              "assumptions": [], "openQuestions": [] }
            """;

        var content = DraftResponseParser.Parse(json);

        Assert.NotNull(content.Assumptions);
        Assert.Empty(content.Assumptions!);
        Assert.NotNull(content.OpenQuestions);
        Assert.Empty(content.OpenQuestions!);
    }

    private static void AssertMalformed(string text)
    {
        var ex = Assert.Throws<DraftGenerationException>(() => DraftResponseParser.Parse(text));
        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
        if (text.Length > 12)
            Assert.DoesNotContain(text.Trim(), ex.Message);
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("")]
    [InlineData("   \n  ")]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"just a string\"")]
    [InlineData("{ \"summary\": 5, \"functionalRequirements\": [\"F\"], \"acceptanceCriteria\": [\"A\"], \"assumptions\": [], \"openQuestions\": [] }")]
    [InlineData("{ \"summary\": \"S\", \"functionalRequirements\": \"F\", \"acceptanceCriteria\": [\"A\"], \"assumptions\": [], \"openQuestions\": [] }")]
    [InlineData("{ \"summary\": \"S\", \"functionalRequirements\": [1, 2], \"acceptanceCriteria\": [\"A\"], \"assumptions\": [], \"openQuestions\": [] }")]
    [InlineData("{ \"summary\": \"S\", \"functionalRequirements\": [\"F\"], \"acceptanceCriteria\": [\"A\"], \"assumptions\": null, \"openQuestions\": [] }")]
    public void UnusableShapes_AreMalformedOutput(string text) => AssertMalformed(text);

    [Theory]
    [InlineData("summary")]
    [InlineData("functionalRequirements")]
    [InlineData("acceptanceCriteria")]
    [InlineData("assumptions")]
    [InlineData("openQuestions")]
    public void EachMissingKey_IsMalformedOutput(string missingKey)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(FakeChatModelClient.ValidAnswerJson())!.AsObject();
        node.Remove(missingKey);

        AssertMalformed(node.ToJsonString());
    }

    [Fact]
    public void ProseBeforeOrAfterTheObject_IsMalformedOutput()
    {
        var json = FakeChatModelClient.ValidAnswerJson();

        AssertMalformed("Here is the draft you asked for:\n" + json);
        AssertMalformed(json + "\nI hope this helps.");
        AssertMalformed("Sure!\n```json\n" + json + "\n```");
    }

    [Fact]
    public void MoreThanOneFencedBlock_IsMalformedOutput()
    {
        var json = FakeChatModelClient.ValidAnswerJson();

        AssertMalformed("```json\n" + json + "\n```\n```json\n" + json + "\n```");
    }

    [Fact]
    public void UnclosedFence_IsMalformedOutput()
    {
        AssertMalformed("```json\n" + FakeChatModelClient.ValidAnswerJson());
    }

    [Fact]
    public void EmptySummaryAndEmptyRequirementLists_ArePassedThroughForSliceThreeValidation()
    {
        var json = """
            { "summary": "", "functionalRequirements": [], "acceptanceCriteria": [],
              "assumptions": [], "openQuestions": [] }
            """;

        var content = DraftResponseParser.Parse(json);

        Assert.Equal(string.Empty, content.Summary);
        Assert.Empty(content.FunctionalRequirements!);
        Assert.Empty(content.AcceptanceCriteria!);
    }
}
