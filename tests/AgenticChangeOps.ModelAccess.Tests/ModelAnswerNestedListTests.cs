namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// The shared reader for a list of objects that each carry a list of strings (slice 018, plan
/// Complexity Tracking): each item is read as a map of the requested keys to strings plus one named
/// list, so an agent never parses JSON itself. Absent and empty lists stay distinguishable.
/// </summary>
public class ModelAnswerNestedListTests
{
    private const string LeakMarker = "synthetic-nested-list-marker-5d2a";

    private static IReadOnlyList<ObjectWithList> Read(string text, string listKey = "refs", params string[] keys) =>
        ModelAnswerReader.Read(text, a => a.ReadObjectListWithStringList("items", listKey, keys.Length == 0 ? new[] { "a" } : keys));

    private static void AssertMalformed(string text, string listKey = "refs", params string[] keys)
    {
        var ex = Assert.Throws<DraftGenerationException>(() => Read(text, listKey, keys));

        Assert.Equal(GenerationFailureCategory.MalformedOutput, ex.Category);
        Assert.DoesNotContain(LeakMarker, ex.ToString());
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void EachObject_IsReadAsTheRequestedKeys_AndItsList()
    {
        var items = Read(
            "{ \"items\": [ { \"a\": \"1\", \"b\": \"2\", \"c\": \"ignored\", \"refs\": [\"x\", \"y\"] }, { \"a\": \"3\", \"b\": \"4\", \"refs\": [\"z\"] } ] }",
            "refs", "a", "b");

        Assert.Equal(2, items.Count);
        Assert.Equal("1", items[0].Values["a"]);
        Assert.Equal("2", items[0].Values["b"]);
        Assert.False(items[0].Values.ContainsKey("c"));
        Assert.Equal(new[] { "x", "y" }, items[0].List);
        Assert.Equal(new[] { "z" }, items[1].List);
    }

    [Fact]
    public void AMissingKey_OrANullValue_IsReadAsNull()
    {
        var items = Read("{ \"items\": [ { \"refs\": [] }, { \"a\": null, \"refs\": [] } ] }", "refs", "a");

        Assert.Null(items[0].Values["a"]);
        Assert.Null(items[1].Values["a"]);
    }

    [Fact]
    public void AnAbsentList_IsNull_AnEmptyList_IsEmpty()
    {
        var items = Read("{ \"items\": [ { \"a\": \"1\" }, { \"a\": \"1\", \"refs\": null }, { \"a\": \"1\", \"refs\": [] } ] }");

        Assert.Null(items[0].List);
        Assert.Null(items[1].List);
        Assert.NotNull(items[2].List);
        Assert.Empty(items[2].List!);
    }

    [Fact]
    public void AnEmptyOuterList_IsAnEmptyList()
    {
        Assert.Empty(Read("{ \"items\": [] }"));
    }

    [Fact]
    public void AnEmptyString_IsPassedThroughUnchanged_InTheListToo()
    {
        var items = Read("{ \"items\": [ { \"a\": \"\", \"refs\": [\"\"] } ] }");

        Assert.Equal(string.Empty, items[0].Values["a"]);
        Assert.Equal(new[] { string.Empty }, items[0].List);
    }

    [Theory]
    [InlineData("{ \"items\": \"not a list\" }")]
    [InlineData("{ \"items\": { \"a\": \"1\" } }")]
    [InlineData("{ \"items\": [ \"a string\" ] }")]
    [InlineData("{ \"items\": [ [ \"nested\" ] ] }")]
    [InlineData("{ \"items\": [ 7 ] }")]
    [InlineData("{ \"items\": [ { \"a\": 7, \"refs\": [] } ] }")]
    [InlineData("{ \"items\": [ { \"a\": [ \"x\" ], \"refs\": [] } ] }")]
    [InlineData("{ \"items\": [ { \"a\": { \"x\": \"y\" }, \"refs\": [] } ] }")]
    [InlineData("{ \"items\": [ { \"a\": \"1\", \"refs\": \"not a list\" } ] }")]
    [InlineData("{ \"items\": [ { \"a\": \"1\", \"refs\": { \"x\": \"y\" } } ] }")]
    [InlineData("{ \"items\": [ { \"a\": \"1\", \"refs\": [ 7 ] } ] }")]
    [InlineData("{ \"items\": [ { \"a\": \"1\", \"refs\": [ [ \"deeper\" ] ] } ] }")]
    [InlineData("{ \"items\": [ { \"a\": \"1\", \"refs\": [ { \"x\": \"y\" } ] } ] }")]
    [InlineData("{ \"items\": [ { \"a\": \"1\", \"refs\": [ null ] } ] }")]
    [InlineData("{ \"other\": [] }")]
    public void AnythingElse_IsMalformed(string text) => AssertMalformed(text);

    [Fact]
    public void Malformed_NeverCarriesTheAnswerText() =>
        AssertMalformed("{ \"items\": [ { \"a\": 7, \"refs\": [ \"" + LeakMarker + "\", 5 ] } ] }");

    [Fact]
    public void TheExistingReaders_AreUnchanged()
    {
        var flat = ModelAnswerReader.Read(
            "{ \"items\": [ { \"a\": \"1\" } ], \"list\": [ \"x\" ] }",
            a => (a.ReadObjectList("items", "a"), a.ReadList("list")));

        Assert.Equal("1", flat.Item1[0]["a"]);
        Assert.Equal(new[] { "x" }, flat.Item2);
    }
}
