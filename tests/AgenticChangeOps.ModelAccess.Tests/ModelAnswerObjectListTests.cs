namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// The shared reader for a list of flat objects (slice 016, research R9): each item is read as a
/// map of the requested keys to strings, so an agent never parses JSON itself.
/// </summary>
public class ModelAnswerObjectListTests
{
    private const string LeakMarker = "synthetic-object-list-marker-7c1e";

    private static IReadOnlyList<IReadOnlyDictionary<string, string?>> Read(string text, params string[] keys) =>
        ModelAnswerReader.Read(text, a => a.ReadObjectList("items", keys));

    private static void AssertMalformed(string text, params string[] keys)
    {
        var ex = Assert.Throws<DraftGenerationException>(() => Read(text, keys));

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
        Assert.DoesNotContain(LeakMarker, ex.ToString());
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void EachObject_IsReadAsTheRequestedKeys()
    {
        var items = Read("{ \"items\": [ { \"a\": \"1\", \"b\": \"2\", \"c\": \"ignored\" }, { \"a\": \"3\", \"b\": \"4\" } ] }", "a", "b");

        Assert.Equal(2, items.Count);
        Assert.Equal("1", items[0]["a"]);
        Assert.Equal("2", items[0]["b"]);
        Assert.Equal("3", items[1]["a"]);
        Assert.False(items[0].ContainsKey("c"));
    }

    [Fact]
    public void AMissingKey_OrANullValue_IsReadAsNull()
    {
        var items = Read("{ \"items\": [ { \"a\": \"1\" }, { \"a\": \"1\", \"b\": null } ] }", "a", "b");

        Assert.Null(items[0]["b"]);
        Assert.Null(items[1]["b"]);
    }

    [Fact]
    public void AnEmptyString_IsPassedThroughUnchanged()
    {
        var items = Read("{ \"items\": [ { \"a\": \"\" } ] }", "a");

        Assert.Equal(string.Empty, items[0]["a"]);
    }

    [Fact]
    public void AnEmptyList_IsAnEmptyList()
    {
        Assert.Empty(Read("{ \"items\": [] }", "a"));
    }

    [Fact]
    public void ALineBreak_InsideAString_IsKeptExactly()
    {
        var items = Read("{ \"items\": [ { \"a\": \"one\\r\\ntwo\\n\" } ] }", "a");

        Assert.Equal("one\r\ntwo\n", items[0]["a"]);
    }

    [Theory]
    [InlineData("{ \"items\": \"not a list\" }")]
    [InlineData("{ \"items\": { \"a\": \"1\" } }")]
    [InlineData("{ \"items\": [ \"a string\" ] }")]
    [InlineData("{ \"items\": [ [ \"nested\" ] ] }")]
    [InlineData("{ \"items\": [ 7 ] }")]
    [InlineData("{ \"items\": [ { \"a\": 7 } ] }")]
    [InlineData("{ \"items\": [ { \"a\": [ \"x\" ] } ] }")]
    [InlineData("{ \"items\": [ { \"a\": { \"x\": \"y\" } } ] }")]
    [InlineData("{ \"other\": [] }")]
    public void AnythingElse_IsMalformed(string text) => AssertMalformed(text, "a");

    [Fact]
    public void Malformed_NeverCarriesTheAnswerText() =>
        AssertMalformed("{ \"items\": [ { \"a\": 7, \"b\": \"" + LeakMarker + "\" } ] }", "a", "b");

    [Fact]
    public void ARequestedKeyList_WithNoKeys_IsRejectedAsMisuse()
    {
        Assert.ThrowsAny<Exception>(() => Read("{ \"items\": [] }"));
    }
}
