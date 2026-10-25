using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticChangeOps.Agents.Reviewer.Tests.Fakes;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>
/// The system's own re-check of the proposal edits (contracts/system-check-rules.md). Every row of
/// the outcome table, the order and numbering of results, availability, and what never leaves the checker.
/// </summary>
public sealed class SampleCheckerTests
{
    private const string File = "orders-service/config/orders.settings";

    private static ReviewProposedChange Change(string path, string? existing, string replacement = "new text") =>
        new(path, ReviewChangeKind.Change, existing, replacement);

    private static ReviewProposedChange Add(string path, string? existing = null, string text = "new file\n") =>
        new(path, ReviewChangeKind.Add, existing, text);

    private static EditCheckOutcome Single(FakeReviewSampleFiles sample, ReviewProposedChange change)
    {
        var result = new SampleChecker(sample).Check(new[] { change });

        Assert.Equal(SystemCheckStatus.Performed, result.Status);
        return Assert.Single(result.Edits).Outcome;
    }

    // ---- change: quote found ----------------------------------------------------------

    [Fact]
    public void Change_WithTheQuoteAppearingOnce_IsFound()
    {
        var sample = new FakeReviewSampleFiles().With(File, "page_size = 20\nretry_count = 3\n");

        Assert.Equal(EditCheckOutcome.Found, Single(sample, Change(File, "retry_count = 3")));
    }

    [Theory]
    [InlineData("alpha\r\nbeta\r\n", "alpha\nbeta")]
    [InlineData("alpha\nbeta\n", "alpha\r\nbeta")]
    [InlineData("alpha\rbeta\r", "alpha\nbeta")]
    [InlineData("alpha\r\nbeta\r\n", "alpha\rbeta")]
    public void Change_WithOnlyLineEndingsDiffering_IsStillFound(string fileText, string quote)
    {
        var sample = new FakeReviewSampleFiles().With(File, fileText);

        Assert.Equal(EditCheckOutcome.Found, Single(sample, Change(File, quote)));
    }

    // ---- change: quote not found ------------------------------------------------------

    [Theory]
    [InlineData("report_hour  = 2")] // one extra space
    [InlineData("report_hour =\t2")] // a tab instead of a space
    [InlineData("report_hour = 2 ")] // a trailing space
    [InlineData("Report_hour = 2")] // letter case
    [InlineData("report_hour = 3")] // a different letter
    public void Change_WithAQuoteThatDiffersInTheSmallestWay_IsQuoteNotFound(string quote)
    {
        var sample = new FakeReviewSampleFiles().With(File, "page_size = 20\nreport_hour = 2\n");

        Assert.Equal(EditCheckOutcome.QuoteNotFound, Single(sample, Change(File, quote)));
    }

    [Fact]
    public void Change_WithAQuoteThatAppearsTwice_IsQuoteAmbiguous()
    {
        var sample = new FakeReviewSampleFiles().With(File, "retry_count = 3\npage_size = 20\nretry_count = 3\n");

        Assert.Equal(EditCheckOutcome.QuoteAmbiguous, Single(sample, Change(File, "retry_count = 3")));
    }

    // ---- change: file problems --------------------------------------------------------

    [Fact]
    public void Change_ForAMissingFile_IsFileNotFound()
    {
        var sample = new FakeReviewSampleFiles().With(File, "x\n");

        Assert.Equal(EditCheckOutcome.FileNotFound, Single(sample, Change("orders-service/config/missing.settings", "x")));
    }

    [Fact]
    public void Change_ForAFolder_IsFileNotFound()
    {
        var sample = new FakeReviewSampleFiles().With(File, "x\n");

        Assert.Equal(EditCheckOutcome.FileNotFound, Single(sample, Change("orders-service/config", "x")));
    }

    [Fact]
    public void Change_ForAFileThatCannotBeRead_IsFileNotFound()
    {
        var sample = new FakeReviewSampleFiles().WithUnreadable(File);

        Assert.Equal(EditCheckOutcome.FileNotFound, Single(sample, Change(File, "x")));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("orders-service/../../outside.txt")]
    [InlineData("C:\\temp\\outside.txt")]
    [InlineData("orders-service\\config\\orders.settings")]
    public void EitherKind_WithAPathOutsideTheSample_IsPathOutsideSample(string path)
    {
        var sample = new FakeReviewSampleFiles().With(File, "x\n");

        Assert.Equal(EditCheckOutcome.PathOutsideSample, Single(sample, Change(path, "x")));
        Assert.Equal(EditCheckOutcome.PathOutsideSample, Single(sample, Add(path)));
    }

    // ---- malformed edits --------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Change_WithoutAQuote_IsMalformedEdit(string? quote)
    {
        var sample = new FakeReviewSampleFiles().With(File, "x\n");

        Assert.Equal(EditCheckOutcome.MalformedEdit, Single(sample, Change(File, quote)));
    }

    [Fact]
    public void Add_CarryingAQuote_IsMalformedEdit()
    {
        var sample = new FakeReviewSampleFiles();

        Assert.Equal(EditCheckOutcome.MalformedEdit, Single(sample, Add("orders-service/new.settings", "something")));
    }

    // ---- add --------------------------------------------------------------------------

    [Fact]
    public void Add_ForAnExistingFile_IsNewFileExists()
    {
        var sample = new FakeReviewSampleFiles().With(File, "x\n");

        Assert.Equal(EditCheckOutcome.NewFileExists, Single(sample, Add(File)));
    }

    [Fact]
    public void Add_ForAnExistingFolder_IsNewFileExists()
    {
        var sample = new FakeReviewSampleFiles().With(File, "x\n");

        Assert.Equal(EditCheckOutcome.NewFileExists, Single(sample, Add("orders-service/config")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")] // an empty quote on an add is the same as none
    public void Add_ForAFreePath_IsNewFileFree(string? emptyQuote)
    {
        var sample = new FakeReviewSampleFiles().With(File, "x\n");

        Assert.Equal(EditCheckOutcome.NewFileFree, Single(sample, Add("orders-service/config/new.settings", emptyQuote)));
    }

    // ---- order, numbering, independence ----------------------------------------------

    [Fact]
    public void Results_AreInProposalOrder_WithOneBasedChangeNumbers()
    {
        var sample = new FakeReviewSampleFiles().With(File, "page_size = 20\nretry_count = 3\n");
        var changes = new[]
        {
            Change(File, "page_size = 20"),
            Add(File),
            Change(File, "no such text"),
            Add("orders-service/config/new.settings")
        };

        var result = new SampleChecker(sample).Check(changes);

        Assert.Equal(
            new[]
            {
                new EditCheck(1, EditCheckOutcome.Found),
                new EditCheck(2, EditCheckOutcome.NewFileExists),
                new EditCheck(3, EditCheckOutcome.QuoteNotFound),
                new EditCheck(4, EditCheckOutcome.NewFileFree)
            },
            result.Edits);
    }

    [Fact]
    public void EveryEdit_IsCheckedAgainstTheFileAsItIs_NeverAgainstAnotherEditsResult()
    {
        var sample = new FakeReviewSampleFiles().With(File, "page_size = 20\n");
        var changes = new[]
        {
            Change(File, "page_size = 20", "brand_new_line = 1"),
            Change(File, "brand_new_line = 1", "other"), // exists only in the first edit's replacement
            Change(File, "page_size = 20", "again") // the same quote as the first: still found, overlap is not re-checked
        };

        var result = new SampleChecker(sample).Check(changes);

        Assert.Equal(
            new[] { EditCheckOutcome.Found, EditCheckOutcome.QuoteNotFound, EditCheckOutcome.Found },
            result.Edits.Select(e => e.Outcome));
    }

    [Fact]
    public void AProposalWithNoEdits_IsPerformedWithAnEmptyList()
    {
        var result = new SampleChecker(new FakeReviewSampleFiles()).Check(Array.Empty<ReviewProposedChange>());

        Assert.Equal(SystemCheckStatus.Performed, result.Status);
        Assert.Empty(result.Edits);
    }

    // ---- availability -----------------------------------------------------------------

    [Fact]
    public void WhenTheSampleIsUnavailable_TheCheckIsNotPerformed_WithAnEmptyList_AndReadsNothing()
    {
        var sample = new FakeReviewSampleFiles().With(File, "x\n");
        sample.IsAvailable = false;

        var result = new SampleChecker(sample).Check(new[] { Change(File, "x"), Add("orders-service/new.settings") });

        Assert.Equal(SystemCheckStatus.NotPerformed, result.Status);
        Assert.Empty(result.Edits);
        Assert.Equal(0, sample.ProbeCount);
        Assert.Equal(0, sample.ReadCount);
    }

    // ---- what never leaves the checker -----------------------------------------------

    [Fact]
    public void TheResult_NeverContainsAPathAQuoteOrFileText()
    {
        var sample = FakeReviewBasisSource.MatchingSample();
        var basis = FakeReviewBasisSource.Proposal();

        var result = new SampleChecker(sample).Check(basis.Changes);

        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        var json = JsonSerializer.Serialize(result, options);

        Assert.DoesNotContain("billing-portal", json);
        Assert.DoesNotContain("report_hour", json);
        Assert.DoesNotContain("run at configured hour", json);
        Assert.DoesNotContain("page_size", json);
    }

    [Fact]
    public void TheSampleInterface_OffersNoWayToWriteAnything()
    {
        var members = typeof(IReviewSampleFiles)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "IsAvailable", "Probe", "ReadText", "get_IsAvailable" }, members);
    }

    [Fact]
    public void ACompleteProposal_AgainstAMatchingSample_IsFoundAndNewFileFree()
    {
        var result = new SampleChecker(FakeReviewBasisSource.MatchingSample())
            .Check(FakeReviewBasisSource.Proposal().Changes);

        Assert.Equal(
            new[] { EditCheckOutcome.Found, EditCheckOutcome.NewFileFree, EditCheckOutcome.Found },
            result.Edits.Select(e => e.Outcome));
    }
}
