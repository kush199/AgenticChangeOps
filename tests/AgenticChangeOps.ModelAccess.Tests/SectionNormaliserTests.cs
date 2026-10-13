namespace AgenticChangeOps.ModelAccess.Tests;

/// <summary>
/// Proves, once, the trimming, blank-item removal and size limits for text and list sections
/// (contracts/shared-api.md, SectionNormaliser), including the exact error wording.
/// </summary>
public class SectionNormaliserTests
{
    [Fact]
    public void Limits_AreTheRecordedOnes()
    {
        Assert.Equal(2000, SectionNormaliser.MaxTextLength);
        Assert.Equal(50, SectionNormaliser.MaxListItems);
    }

    // ---- text ----

    [Fact]
    public void Text_IsTrimmed()
    {
        var outcome = SectionNormaliser.Text("  some text \n", "Headline");

        Assert.Equal("some text", outcome.Value);
        Assert.Null(outcome.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Text_NullOrBlank_IsRequiredError(string? value)
    {
        var outcome = SectionNormaliser.Text(value, "Headline");

        Assert.Null(outcome.Value);
        Assert.Equal("Headline is required and must not be blank.", outcome.Error);
    }

    [Fact]
    public void Text_AtTheLimit_IsAccepted_AndOverItIsAnError()
    {
        var atLimit = SectionNormaliser.Text(new string('a', 2000), "Headline");
        var overLimit = SectionNormaliser.Text(new string('a', 2001), "Headline");

        Assert.Equal(new string('a', 2000), atLimit.Value);
        Assert.Null(atLimit.Error);
        Assert.Null(overLimit.Value);
        Assert.Equal("Headline must not exceed 2000 characters.", overLimit.Error);
    }

    [Fact]
    public void Text_LengthIsMeasuredAfterTrimming()
    {
        var outcome = SectionNormaliser.Text("  " + new string('a', 2000) + "  ", "Headline");

        Assert.Null(outcome.Error);
    }

    // ---- lists ----

    [Fact]
    public void List_Null_IsMissingError()
    {
        var outcome = SectionNormaliser.List(null, "Steps", requireItem: true);

        Assert.Null(outcome.Value);
        Assert.Equal("Steps section is missing.", outcome.Error);
    }

    [Fact]
    public void List_NullOptionalSection_IsAlsoMissingError()
    {
        var outcome = SectionNormaliser.List(null, "Notes", requireItem: false);

        Assert.Equal("Notes section is missing.", outcome.Error);
    }

    [Fact]
    public void List_DropsBlankItems_TrimsTheRest_AndKeepsOrder()
    {
        var outcome = SectionNormaliser.List(
            new[] { "  first ", "", "   ", "second", "\t", " third" }, "Steps", requireItem: true);

        Assert.Null(outcome.Error);
        Assert.Equal(new[] { "first", "second", "third" }, outcome.Value);
    }

    [Fact]
    public void List_RequiredWithNoItemLeft_IsAnError()
    {
        var empty = SectionNormaliser.List(Array.Empty<string>(), "Steps", requireItem: true);
        var blanks = SectionNormaliser.List(new[] { " ", "" }, "Steps", requireItem: true);

        Assert.Equal("Steps must contain at least one non-blank item.", empty.Error);
        Assert.Equal("Steps must contain at least one non-blank item.", blanks.Error);
        Assert.Null(empty.Value);
        Assert.Null(blanks.Value);
    }

    [Fact]
    public void List_OptionalWithNoItemLeft_IsAcceptedAsEmpty()
    {
        var empty = SectionNormaliser.List(Array.Empty<string>(), "Notes", requireItem: false);
        var blanks = SectionNormaliser.List(new[] { " ", "" }, "Notes", requireItem: false);

        Assert.Null(empty.Error);
        Assert.Empty(empty.Value!);
        Assert.Null(blanks.Error);
        Assert.Empty(blanks.Value!);
    }

    [Fact]
    public void List_AtTheItemLimit_IsAccepted_AndOverItIsAnError()
    {
        var atLimit = SectionNormaliser.List(Items(50), "Steps", requireItem: true);
        var overLimit = SectionNormaliser.List(Items(51), "Steps", requireItem: true);

        Assert.Null(atLimit.Error);
        Assert.Equal(50, atLimit.Value!.Count);
        Assert.Null(overLimit.Value);
        Assert.Equal("Steps must not exceed 50 items.", overLimit.Error);
    }

    [Fact]
    public void List_BlankItemsDoNotCountTowardTheItemLimit()
    {
        var items = Items(50).Concat(new[] { "", "  ", "\t" }).ToArray();

        var outcome = SectionNormaliser.List(items, "Steps", requireItem: true);

        Assert.Null(outcome.Error);
        Assert.Equal(50, outcome.Value!.Count);
    }

    [Fact]
    public void List_ItemAtTheLengthLimit_IsAccepted_AndOverItIsAnError()
    {
        var atLimit = SectionNormaliser.List(new[] { new string('a', 2000) }, "Steps", requireItem: true);
        var overLimit = SectionNormaliser.List(new[] { "ok", new string('a', 2001) }, "Steps", requireItem: true);

        Assert.Null(atLimit.Error);
        Assert.Null(overLimit.Value);
        Assert.Equal("Each Steps item must not exceed 2000 characters.", overLimit.Error);
    }

    [Fact]
    public void List_ItemLengthIsMeasuredAfterTrimming()
    {
        var outcome = SectionNormaliser.List(
            new[] { "  " + new string('a', 2000) + "  " }, "Steps", requireItem: true);

        Assert.Null(outcome.Error);
    }

    [Fact]
    public void List_CountIsCheckedBeforeLength()
    {
        var items = Items(51).Append(new string('a', 2001)).ToArray();

        var outcome = SectionNormaliser.List(items, "Steps", requireItem: true);

        Assert.Equal("Steps must not exceed 50 items.", outcome.Error);
    }

    private static string[] Items(int count) =>
        Enumerable.Range(1, count).Select(i => $"item {i}").ToArray();
}
