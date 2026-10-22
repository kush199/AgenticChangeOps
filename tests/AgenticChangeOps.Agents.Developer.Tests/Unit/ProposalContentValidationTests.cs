using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>Shape rules (verification step 1): what the model's content must look like before any file is checked.</summary>
public sealed class ProposalContentValidationTests
{
    private static readonly ProposalContentValidator Validator = new();

    private static ProposalContent Valid() => FakeProposalGenerator.ValidContent();

    [Fact]
    public void ValidContent_BecomesTypedEdits()
    {
        var result = Validator.Validate(Valid());

        Assert.Empty(result.Errors);
        Assert.Null(result.Rejection);
        var change = Assert.Single(result.Value!.Changes);
        Assert.Equal(ProposalChangeKind.Change, change.Kind);
        Assert.Equal(FakeProposalGenerator.SettingsPath, change.Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankOrMissingSummary_IsMalformedOutput(string? summary)
    {
        var result = Validator.Validate(Valid() with { Summary = summary });

        Assert.Contains(result.Errors, e => e.FieldName == "Summary");
        Assert.Null(result.Value);
    }

    [Fact]
    public void NoRisks_IsMalformedOutput()
    {
        var result = Validator.Validate(Valid() with { Risks = Array.Empty<string>() });

        Assert.Contains(result.Errors, e => e.FieldName == "Risks");
    }

    [Fact]
    public void AMissingChangesSection_IsMalformedOutput()
    {
        var result = Validator.Validate(Valid() with { Changes = null });

        Assert.Contains(result.Errors, e => e.FieldName == "Changes");
    }

    [Fact]
    public void TestIdeas_AndOpenQuestions_MayBeEmpty()
    {
        var result = Validator.Validate(Valid() with
        {
            TestIdeas = Array.Empty<string>(),
            OpenQuestions = Array.Empty<string>()
        });

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void BlankListItems_AreDropped_AndTextIsTrimmed()
    {
        var result = Validator.Validate(Valid() with
        {
            Summary = "  A summary.  ",
            Risks = new[] { "  one  ", "", "   " }
        });

        Assert.Equal("A summary.", result.Value!.Summary);
        Assert.Equal(new[] { "one" }, result.Value.Risks);
    }

    [Theory]
    [InlineData(null, "change", "q", "r")]
    [InlineData("", "change", "q", "r")]
    [InlineData("a/b", null, "q", "r")]
    [InlineData("a/b", "rename", "q", "r")]
    [InlineData("a/b", "change", "q", null)]
    public void AnEdit_MissingAPathAKindOrReplacementText_OrWithAnUnknownKind_IsMalformedOutput(
        string? path, string? kind, string? existing, string? replacement)
    {
        var result = Validator.Validate(Valid() with
        {
            Changes = new[] { new ProposedChangeContent(path, kind, existing, replacement) }
        });

        Assert.NotEmpty(result.Errors);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("change", null)]
    [InlineData("change", "")]
    [InlineData("add", "quoted text")]
    public void AChangeWithoutAQuote_OrAnAddWithOne_IsMalformedEdit(string kind, string? existing)
    {
        var result = Validator.Validate(Valid() with
        {
            Changes = new[] { new ProposedChangeContent("a/b", kind, existing, "replacement") }
        });

        Assert.Equal(ProposalRejection.MalformedEdit, result.Rejection);
        Assert.Null(result.Value);
    }

    [Fact]
    public void KindWords_AreReadWithoutRegardToCase()
    {
        var result = Validator.Validate(Valid() with
        {
            Changes = new[]
            {
                new ProposedChangeContent("a/b", "Change", "q", "r"),
                new ProposedChangeContent("a/c", "ADD", null, "r")
            }
        });

        Assert.Equal(new[] { ProposalChangeKind.Change, ProposalChangeKind.Add }, result.Value!.Changes.Select(c => c.Kind));
    }

    [Fact]
    public void TheQuoteAndReplacement_AreKeptExactly_NotTrimmed()
    {
        var result = Validator.Validate(Valid() with
        {
            Changes = new[] { new ProposedChangeContent("a/b", "change", "  quote\n", "  replacement\n") }
        });

        var change = Assert.Single(result.Value!.Changes);
        Assert.Equal("  quote\n", change.ExistingText);
        Assert.Equal("  replacement\n", change.ReplacementText);
    }

    [Fact]
    public void NullContent_IsAGeneratorError()
    {
        var result = Validator.Validate(null);

        Assert.Contains(result.Errors, e => e.FieldName == "Generator");
    }

    [Fact]
    public void FilesRead_AndToolCalls_AreCarriedThroughUnchanged()
    {
        var calls = new[]
        {
            new ToolCallRecord("read_file", "a", ToolCallOutcome.Served, 10, TimeSpan.Zero, DateTime.UnixEpoch)
        };

        var result = Validator.Validate(Valid() with { SampleFilesRead = new[] { "a" }, ToolCalls = calls });

        Assert.Equal(new[] { "a" }, result.Value!.SampleFilesRead);
        Assert.Equal(calls, result.Value.ToolCalls);
    }
}
