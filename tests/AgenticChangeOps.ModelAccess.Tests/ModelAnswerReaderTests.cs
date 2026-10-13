namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// Proves, once, how a model answer is read: bare object or one fenced block, then strict
/// text and list reads by name (contracts/shared-api.md, ModelAnswerReader).
/// </summary>
public class ModelAnswerReaderTests
{
    private const string Fence = "```";
    private const string LeakMarker = "synthetic-answer-marker-4b7d";

    private const string ObjectText =
        "{ \"title\": \"A title\", \"steps\": [ \"one\", \"two\" ], \"notes\": [] }";

    private sealed record Shape(string Title, IReadOnlyList<string> Steps, IReadOnlyList<string> Notes);

    private static Shape Read(string text) => ModelAnswerReader.Read(text, a => new Shape(
        a.ReadString("title"), a.ReadList("steps"), a.ReadList("notes")));

    private static void AssertMalformed(string text)
    {
        var ex = Assert.Throws<DraftGenerationException>(() => Read(text));

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
        Assert.DoesNotContain(LeakMarker, ex.ToString());
        Assert.Null(ex.InnerException);
    }

    // ---- accepted shapes ----

    [Fact]
    public void BareObject_IsRead()
    {
        var shape = Read(ObjectText);

        Assert.Equal("A title", shape.Title);
        Assert.Equal(new[] { "one", "two" }, shape.Steps);
        Assert.Empty(shape.Notes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("json")]
    [InlineData("JSON")]
    [InlineData("Json")]
    [InlineData("  json  ")]
    public void OneFencedBlock_UnlabelledOrLabelledJson_IsRead(string label)
    {
        var shape = Read($"{Fence}{label}\n{ObjectText}\n{Fence}");

        Assert.Equal("A title", shape.Title);
    }

    [Fact]
    public void SurroundingWhitespace_IsIgnored()
    {
        var shape = Read($"  \n {Fence}json\n{ObjectText}\n{Fence}  \n");

        Assert.Equal("A title", shape.Title);
    }

    [Fact]
    public void PresentButEmptyValues_ArePassedThroughUnchanged()
    {
        var shape = Read("{ \"title\": \"\", \"steps\": [ \"\", \"  \" ], \"notes\": [] }");

        Assert.Equal(string.Empty, shape.Title);
        Assert.Equal(new[] { string.Empty, "  " }, shape.Steps);
    }

    [Fact]
    public void ExtraKeys_AreIgnored()
    {
        var shape = Read("{ \"title\": \"T\", \"steps\": [], \"notes\": [], \"extra\": 1 }");

        Assert.Equal("T", shape.Title);
    }

    // ---- rejected fences ----

    [Fact]
    public void FenceWithAnotherLabel_IsMalformed() =>
        AssertMalformed($"{Fence}yaml\n{ObjectText}\n{Fence}");

    [Fact]
    public void FenceWithNoNewlineAfterTheOpening_IsMalformed() =>
        AssertMalformed($"{Fence}json {ObjectText} {Fence}");

    [Fact]
    public void FenceWithNoClosing_IsMalformed() =>
        AssertMalformed($"{Fence}json\n{ObjectText}");

    [Fact]
    public void FenceContainingAnotherFence_IsMalformed() =>
        AssertMalformed($"{Fence}json\n{ObjectText}\n{Fence}\nmore\n{Fence}");

    [Fact]
    public void TextAfterTheClosingFence_IsMalformed() =>
        AssertMalformed($"{Fence}json\n{ObjectText}\n{Fence}\ntrailing words");

    // ---- rejected content ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("plain prose")]
    [InlineData("[]")]
    [InlineData("[ \"a\" ]")]
    [InlineData("\"a string\"")]
    [InlineData("42")]
    [InlineData("null")]
    public void NotAnObject_IsMalformed(string text) => AssertMalformed(text);

    [Fact]
    public void InvalidJson_IsMalformed_WithoutTheAnswerText() =>
        AssertMalformed("{ \"title\": " + LeakMarker);

    [Fact]
    public void MissingKey_IsMalformed() =>
        AssertMalformed("{ \"title\": \"T\", \"steps\": [] }");

    [Theory]
    [InlineData("{ \"title\": 42, \"steps\": [], \"notes\": [] }")]
    [InlineData("{ \"title\": null, \"steps\": [], \"notes\": [] }")]
    [InlineData("{ \"title\": [], \"steps\": [], \"notes\": [] }")]
    [InlineData("{ \"title\": \"T\", \"steps\": \"one\", \"notes\": [] }")]
    [InlineData("{ \"title\": \"T\", \"steps\": { }, \"notes\": [] }")]
    [InlineData("{ \"title\": \"T\", \"steps\": [ 1 ], \"notes\": [] }")]
    [InlineData("{ \"title\": \"T\", \"steps\": [ null ], \"notes\": [] }")]
    [InlineData("{ \"title\": \"T\", \"steps\": [ [ \"a\" ] ], \"notes\": [] }")]
    public void WrongValueKind_IsMalformed(string text) => AssertMalformed(text);

    [Fact]
    public void ExceptionThrownByTheCallersMapping_IsMalformed_WithoutItsMessage()
    {
        var text = $"{{ \"title\": \"{LeakMarker}\", \"steps\": [], \"notes\": [] }}";

        var ex = Assert.Throws<DraftGenerationException>(() =>
            ModelAnswerReader.Read<Shape>(text, _ => throw new InvalidOperationException(LeakMarker)));

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
        Assert.DoesNotContain(LeakMarker, ex.ToString());
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void CategoryExceptionThrownByTheCallersMapping_PassesThroughUnchanged()
    {
        var ex = Assert.Throws<DraftGenerationException>(() =>
            ModelAnswerReader.Read<Shape>(ObjectText,
                _ => throw new DraftGenerationException(GenerationFailureCategory.TimedOut)));

        Assert.Equal(GenerationFailureCategory.TimedOut, ex.Category);
    }
}
