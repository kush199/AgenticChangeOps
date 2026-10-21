using AgenticChangeOps.Agents.Developer.Tests.Fakes;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>
/// contracts/verification-rules.md steps 2 to 7: count, size, path, existence, quote, overlap.
/// One bad edit rejects the whole proposal and the reason carries no content.
/// </summary>
public sealed class ProposalVerifierTests
{
    private const string Settings = FakeProposalGenerator.SettingsPath;

    private static ProposalVerifier Verifier(FakeSampleFiles? sample = null, ProposalLimits? limits = null) =>
        new(sample ?? FakeSampleFiles.Standard(), limits ?? ProposalLimits.Defaults);

    private static ProposedChange Change(string path, string existing, string replacement = "x") =>
        new(path, ProposalChangeKind.Change, existing, replacement);

    private static ProposedChange Add(string path, string text = "new file text") =>
        new(path, ProposalChangeKind.Add, null, text);

    [Fact]
    public void AValidChange_AndAValidAdd_Pass()
    {
        var result = Verifier().Verify(new[]
        {
            Change(Settings, "report_hour = 2", "report_hour = 4"),
            Add("billing-portal/notes/schedule.md")
        });

        Assert.Null(result);
    }

    [Fact]
    public void ALineEndingDifference_OnEitherSide_IsNotSignificant()
    {
        // The sample text uses CRLF; the quote uses LF, and the other way round.
        Assert.Null(Verifier().Verify(new[] { Change(Settings, "report_hour = 2\nreport_format = summary") }));

        var lf = new FakeSampleFiles().With("a/file", "one\ntwo\n");
        Assert.Null(Verifier(lf).Verify(new[] { Change("a/file", "one\r\ntwo") }));
    }

    [Theory]
    [InlineData("report_hour  = 2")]
    [InlineData("report_hour =\t2")]
    [InlineData("report_hour = 2 ")]
    [InlineData("Report_hour = 2")]
    [InlineData("report_hour = 3")]
    public void AQuoteThatDiffersInSpacing_OrCase_OrText_IsNotFound(string quote)
    {
        Assert.Equal(ProposalRejection.QuoteNotFound, Verifier().Verify(new[] { Change(Settings, quote) }));
    }

    [Fact]
    public void AQuoteFoundTwice_IsAmbiguous()
    {
        var sample = new FakeSampleFiles().With("a/file", "retry = 2\nother = 1\nretry = 2\n");

        Assert.Equal(ProposalRejection.QuoteAmbiguous, Verifier(sample).Verify(new[] { Change("a/file", "retry = 2") }));
    }

    [Fact]
    public void AMissingFile_IsRejected_AsFileNotFound()
    {
        Assert.Equal(
            ProposalRejection.FileNotFound,
            Verifier().Verify(new[] { Change("billing-portal/config/missing.settings", "a") }));
    }

    [Theory]
    [InlineData("billing-portal/config/portal.settings")]
    [InlineData("billing-portal/config")]
    public void AnAdd_ForAnExistingFileOrFolder_IsRejected(string path)
    {
        Assert.Equal(ProposalRejection.NewFileExists, Verifier().Verify(new[] { Add(path) }));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\x")]
    [InlineData("billing-portal/../../x")]
    [InlineData("a\\b")]
    public void APath_OutsideTheSample_IsRejected_ForAChangeAndForAnAdd(string path)
    {
        Assert.Equal(ProposalRejection.PathOutsideSample, Verifier().Verify(new[] { Change(path, "a") }));
        Assert.Equal(ProposalRejection.PathOutsideSample, Verifier().Verify(new[] { Add(path) }));
    }

    [Fact]
    public void TwoOverlappingChanges_InOneFile_AreRejected()
    {
        var result = Verifier().Verify(new[]
        {
            Change(Settings, "report_hour = 2"),
            Change(Settings, "hour = 2\nreport_format")
        });

        Assert.Equal(ProposalRejection.OverlappingEdits, result);
    }

    [Fact]
    public void TwoEditsWithTheSameQuote_AreRejected_AsOverlapping()
    {
        var result = Verifier().Verify(new[]
        {
            Change(Settings, "report_hour = 2", "a"),
            Change(Settings, "report_hour = 2", "b")
        });

        Assert.Equal(ProposalRejection.OverlappingEdits, result);
    }

    [Fact]
    public void TwoAdds_ToOnePath_AreRejected_AsOverlapping()
    {
        var result = Verifier().Verify(new[] { Add("billing-portal/notes/new.md"), Add("billing-portal/notes/new.md") });

        Assert.Equal(ProposalRejection.OverlappingEdits, result);
    }

    [Fact]
    public void TwoNonOverlappingChanges_InOneFile_BothPass_EachCheckedAgainstTheOriginal()
    {
        var result = Verifier().Verify(new[]
        {
            // The second quote would not exist if the first edit had been applied first.
            Change(Settings, "report_hour = 2", "something else"),
            Change(Settings, "report_format = summary", "report_format = full")
        });

        Assert.Null(result);
    }

    [Fact]
    public void NoChanges_IsRejected()
    {
        Assert.Equal(ProposalRejection.NoChanges, Verifier().Verify(Array.Empty<ProposedChange>()));
    }

    [Fact]
    public void OneMoreThanTheMaximumNumberOfChanges_IsRejected()
    {
        var sample = new FakeSampleFiles();
        var changes = Enumerable.Range(0, 3).Select(i => Add($"a/new{i}.md")).ToArray();

        Assert.Equal(
            ProposalRejection.TooManyChanges,
            Verifier(sample, new ProposalLimits(MaxChanges: 2, MaxTotalBytes: 1000)).Verify(changes));
        Assert.Null(Verifier(sample, new ProposalLimits(MaxChanges: 3, MaxTotalBytes: 1000)).Verify(changes));
    }

    [Fact]
    public void TheSizeCeiling_CountsBytesOfQuotedAndReplacementText()
    {
        var sample = new FakeSampleFiles().With("a/file", "é");
        var change = Change("a/file", "é", "ab");   // 2 bytes quoted + 2 bytes replacement

        Assert.Null(Verifier(sample, new ProposalLimits(5, 4)).Verify(new[] { change }));
        Assert.Equal(
            ProposalRejection.TooLarge,
            Verifier(sample, new ProposalLimits(5, 3)).Verify(new[] { change }));
    }

    [Fact]
    public void AChangeWithoutAQuote_OrAnAddWithOne_IsMalformed()
    {
        Assert.Equal(
            ProposalRejection.MalformedEdit,
            Verifier().Verify(new[] { new ProposedChange(Settings, ProposalChangeKind.Change, null, "x") }));
        Assert.Equal(
            ProposalRejection.MalformedEdit,
            Verifier().Verify(new[] { new ProposedChange("a/new.md", ProposalChangeKind.Add, "quote", "x") }));
    }

    [Fact]
    public void OneBadEdit_AmongGoodOnes_RejectsTheWholeProposal()
    {
        var result = Verifier().Verify(new[]
        {
            Change(Settings, "report_hour = 2"),
            Add("billing-portal/notes/a.md"),
            Change(Settings, "not in the file"),
            Add("billing-portal/notes/b.md"),
            Add("billing-portal/notes/c.md")
        });

        Assert.Equal(ProposalRejection.QuoteNotFound, result);
    }

    [Fact]
    public void TheVerifier_OnlyReads_AndTheRejection_IsOnlyAReasonCode()
    {
        var sample = FakeSampleFiles.Standard();

        var result = Verifier(sample).Verify(new[] { Change(Settings, "SECRET-QUOTE-TEXT") });

        Assert.Equal(ProposalRejection.QuoteNotFound, result);
        Assert.DoesNotContain("SECRET-QUOTE-TEXT", result!.Value.ToString());
        Assert.True(sample.ReadCount >= 1);
    }
}
