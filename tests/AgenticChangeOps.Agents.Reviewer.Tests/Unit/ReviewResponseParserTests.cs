using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>
/// The model's answer becomes <see cref="ReviewContent"/> or malformed output; the answer text never
/// travels with the failure, and nothing but the findings and open questions is ever kept.
/// </summary>
public sealed class ReviewResponseParserTests
{
    private const string Marker = "HOSTILE-MARKER-0001";

    private const string Valid = """
        {
          "findings": [
            { "category": "AcceptanceCriteria", "severity": "Medium", "statement": "First.", "references": ["proposal.changes[1]", "design.dependencies[1]"] },
            { "category": "UnmentionedRisk", "severity": "Low", "statement": "Second.", "references": ["proposal.risks[2]"] }
          ],
          "openQuestions": ["Who owns it?"]
        }
        """;

    private static void AssertMalformed(string text)
    {
        var ex = Assert.Throws<DraftGenerationException>(() => ReviewResponseParser.Parse(text));

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
        Assert.DoesNotContain(Marker, ex.ToString());
        Assert.Null(ex.InnerException);
    }

    // ---- readable answers -------------------------------------------------------------

    [Fact]
    public void TheObjectAlone_IsRead()
    {
        var content = ReviewResponseParser.Parse(Valid);

        Assert.Equal(2, content.Findings!.Count);
        Assert.Equal("AcceptanceCriteria", content.Findings[0].Category);
        Assert.Equal("Medium", content.Findings[0].Severity);
        Assert.Equal("First.", content.Findings[0].Statement);
        Assert.Equal(new[] { "proposal.changes[1]", "design.dependencies[1]" }, content.Findings[0].References);
        Assert.Equal(new[] { "proposal.risks[2]" }, content.Findings[1].References);
        Assert.Equal(new[] { "Who owns it?" }, content.OpenQuestions);
    }

    [Theory]
    [InlineData("```json\n", "\n```")]
    [InlineData("```\n", "\n```")]
    [InlineData("  ```JSON\n", "\n```  ")]
    public void TheObjectInsideOneCodeFence_IsRead(string before, string after)
    {
        var content = ReviewResponseParser.Parse(before + Valid + after);

        Assert.Equal(2, content.Findings!.Count);
    }

    [Fact]
    public void ZeroFindings_AndNoOpenQuestions_AreReadAsEmptyLists()
    {
        var content = ReviewResponseParser.Parse("{ \"findings\": [], \"openQuestions\": [] }");

        Assert.Empty(content.Findings!);
        Assert.Empty(content.OpenQuestions!);
    }

    [Fact]
    public void AFindingWithoutAReferencesKey_IsReadWithNullReferences_SoTheValidatorCanRefuseIt()
    {
        var content = ReviewResponseParser.Parse("""
            { "findings": [ { "category": "UnmentionedRisk", "severity": "Low", "statement": "No refs." } ], "openQuestions": [] }
            """);

        Assert.Null(content.Findings![0].References);
    }

    [Fact]
    public void AFindingWithAnEmptyReferencesList_IsReadWithAnEmptyList()
    {
        var content = ReviewResponseParser.Parse("""
            { "findings": [ { "category": "UnmentionedRisk", "severity": "Low", "statement": "x", "references": [] } ], "openQuestions": [] }
            """);

        Assert.Empty(content.Findings![0].References!);
    }

    [Fact]
    public void AMissingCategoryOrSeverityOrStatement_IsReadAsNull_AndLeftToTheValidator()
    {
        var content = ReviewResponseParser.Parse("""
            { "findings": [ { "references": ["proposal.changes[1]"] } ], "openQuestions": [] }
            """);

        var finding = Assert.Single(content.Findings!);
        Assert.Null(finding.Category);
        Assert.Null(finding.Severity);
        Assert.Null(finding.Statement);
    }

    // ---- nothing but findings and questions is ever kept ------------------------------

    [Fact]
    public void KeysOtherThanFindingsAndOpenQuestions_AreIgnored_IncludingASystemCheckTheModelInvents()
    {
        var withExtras = Valid.TrimEnd().TrimEnd('}') + """
            , "systemCheck": { "status": "Performed", "edits": [ { "position": 1, "outcome": "Found" } ] },
              "agentName": "Somebody", "status": "Approved", "version": 99 }
            """;

        var plain = ReviewResponseParser.Parse(Valid);
        var extra = ReviewResponseParser.Parse(withExtras);

        Assert.Equal(plain.Findings!.Count, extra.Findings!.Count);
        Assert.Equal(plain.OpenQuestions, extra.OpenQuestions);
        Assert.Equal(plain.Findings[0], extra.Findings[0] with { References = plain.Findings[0].References });
    }

    [Fact]
    public void AFindingsOwnExtraKeys_AreIgnored()
    {
        var content = ReviewResponseParser.Parse("""
            { "findings": [ { "category": "UnmentionedRisk", "severity": "Low", "statement": "x",
                              "references": ["proposal.risks[1]"], "approved": true, "systemCheck": "Found" } ],
              "openQuestions": [] }
            """);

        Assert.Equal("x", Assert.Single(content.Findings!).Statement);
    }

    // ---- malformed --------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("\"a string\"")]
    [InlineData("{ \"findings\": [] }")] // no openQuestions
    [InlineData("{ \"openQuestions\": [] }")] // no findings
    [InlineData("{ \"findings\": {}, \"openQuestions\": [] }")]
    [InlineData("{ \"findings\": \"none\", \"openQuestions\": [] }")]
    [InlineData("{ \"findings\": [], \"openQuestions\": \"none\" }")]
    [InlineData("{ \"findings\": [ \"just text\" ], \"openQuestions\": [] }")]
    [InlineData("{ \"findings\": [ 7 ], \"openQuestions\": [] }")]
    [InlineData("{ \"findings\": [ [ \"nested\" ] ], \"openQuestions\": [] }")]
    [InlineData("{ \"findings\": [ { \"statement\": 7 } ], \"openQuestions\": [] }")]
    [InlineData("{ \"findings\": [ { \"category\": [ \"x\" ] } ], \"openQuestions\": [] }")]
    [InlineData("{ \"findings\": [ { \"severity\": { \"a\": \"b\" } } ], \"openQuestions\": [] }")]
    [InlineData("{ \"findings\": [ { \"references\": \"proposal.changes[1]\" } ], \"openQuestions\": [] }")]
    [InlineData("{ \"findings\": [ { \"references\": [ 7 ] } ], \"openQuestions\": [] }")]
    [InlineData("{ \"findings\": [ { \"references\": [ [ \"a\" ] ] } ], \"openQuestions\": [] }")]
    [InlineData("{ \"findings\": [ { \"references\": { \"a\": \"b\" } } ], \"openQuestions\": [] }")]
    [InlineData("{ \"findings\": [], \"openQuestions\": [ 7 ] }")]
    [InlineData("{ \"findings\": [], \"openQuestions\": [ { \"a\": \"b\" } ] }")]
    [InlineData("Here is the review: { \"findings\": [], \"openQuestions\": [] }")]
    [InlineData("```json\n{ \"findings\": [], \"openQuestions\": [] }\n```\nand more")]
    [InlineData("```json\n{ \"findings\": [], \"openQuestions\": [] }\n```\n```json\n{}\n```")]
    [InlineData("```yaml\n{ \"findings\": [], \"openQuestions\": [] }\n```")]
    public void AnythingElse_IsMalformedOutput(string text) => AssertMalformed(text);

    [Fact]
    public void Malformed_NeverCarriesTheAnswerText()
    {
        AssertMalformed("{ \"findings\": [ { \"statement\": 7, \"category\": \"" + Marker + "\" } ], \"openQuestions\": [] }");
        AssertMalformed("not json " + Marker);
    }
}
