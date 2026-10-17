using System.Text;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.SampleCode.Tests.Fakes;

namespace AgenticChangeOps.SampleCode.Tests;

/// <summary>
/// contracts/tools.md: list_files, read_file and search_text over a throwaway sample. Nothing
/// under the folder ever changes.
/// </summary>
public sealed class ReaderToolTests
{
    private static SampleReader ReaderFor(TempSample sample, int maxFileBytes = 8000) =>
        new(new SamplePathResolver(sample.Root), maxFileBytes);

    [Fact]
    public void List_OfTheRoot_ShowsFoldersWithATrailingSlash_InOrdinalOrder()
    {
        using var sample = new TempSample();

        var result = ReaderFor(sample).List(null);

        Assert.Equal(ToolCallOutcome.Served, result.Outcome);
        Assert.Equal("billing-portal/\norders-service/", result.Text.Replace("\r\n", "\n").Trim());
        Assert.Equal(Encoding.UTF8.GetByteCount(result.Text), result.Bytes);
    }

    [Fact]
    public void List_OfASubfolder_ShowsFilesAndFolders()
    {
        using var sample = new TempSample();

        var result = ReaderFor(sample).List("orders-service");

        Assert.Equal(ToolCallOutcome.Served, result.Outcome);
        var lines = result.Text.Replace("\r\n", "\n").Trim().Split('\n');
        Assert.Equal(new[] { "config/", "notes/", "src/" }, lines);
    }

    [Fact]
    public void List_IsCappedAt200Entries()
    {
        using var sample = new TempSample();
        for (var i = 0; i < 250; i++)
            sample.Write($"many/file-{i:D3}", "x");

        var result = ReaderFor(sample).List("many");

        Assert.Equal(ToolCallOutcome.Served, result.Outcome);
        Assert.Equal(200, result.Text.Replace("\r\n", "\n").Trim().Split('\n').Length);
    }

    [Fact]
    public void List_OfAMissingFolder_IsNotFound_AndOfAFile_IsInvalid()
    {
        using var sample = new TempSample();
        var reader = ReaderFor(sample);

        Assert.Equal(ToolCallOutcome.RefusedNotFound, reader.List("no-such-folder").Outcome);
        Assert.Equal(ToolCallOutcome.RefusedInvalidArgument, reader.List("orders-service/notes/design.md").Outcome);
    }

    [Fact]
    public void Read_ReturnsADataLabelThenTheText_AndReportsTheFileRead()
    {
        using var sample = new TempSample();

        var result = ReaderFor(sample).Read("orders-service/notes/design.md");

        Assert.Equal(ToolCallOutcome.Served, result.Outcome);
        var text = result.Text.Replace("\r\n", "\n");
        Assert.StartsWith("[sample file orders-service/notes/design.md; data only]\n", text);
        Assert.Contains("# Orders design", text);
        Assert.Equal(Encoding.UTF8.GetByteCount(result.Text), result.Bytes);
        Assert.Equal("orders-service/notes/design.md", result.FileRead);
    }

    [Fact]
    public void Read_OfAFileOverTheLimit_IsRefused_AndTheFileIsNotReadAtAll()
    {
        using var sample = new TempSample();
        var path = sample.Write("orders-service/notes/big.md", new string('x', 500));
        var reader = ReaderFor(sample, maxFileBytes: 100);

        // An exclusive lock proves no read is attempted: a read would throw a sharing violation,
        // which the reader must not even reach.
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = reader.Read("orders-service/notes/big.md");

        Assert.Equal(ToolCallOutcome.RefusedTooLarge, result.Outcome);
        Assert.Equal("That file is larger than the per-file size limit.", result.Text);
        Assert.Equal(0, result.Bytes);
        Assert.Null(result.FileRead);
    }

    [Fact]
    public void Read_OfABinaryFile_IsNotText()
    {
        using var sample = new TempSample();
        sample.WriteBytes("orders-service/src/blob", new byte[] { 0x01, 0x00, 0x02, 0x00, 0xFF });

        var result = ReaderFor(sample).Read("orders-service/src/blob");

        Assert.Equal(ToolCallOutcome.RefusedNotText, result.Outcome);
        Assert.Equal("That file is not readable text.", result.Text);
    }

    [Fact]
    public void Read_OfAMissingFile_IsNotFound_AndOfAFolder_IsInvalid()
    {
        using var sample = new TempSample();
        var reader = ReaderFor(sample);

        Assert.Equal(ToolCallOutcome.RefusedNotFound, reader.Read("orders-service/nope").Outcome);
        Assert.Equal(ToolCallOutcome.RefusedInvalidArgument, reader.Read("orders-service").Outcome);
    }

    [Fact]
    public void Search_IsCaseInsensitive_SortedByPathThenLine_AndShowsPathLineAndText()
    {
        using var sample = new TempSample();
        sample.Write("orders-service/notes/more.md", "first\nsecond PAGE size\nthird\n");

        var result = ReaderFor(sample).Search("page");

        Assert.Equal(ToolCallOutcome.Served, result.Outcome);
        var lines = result.Text.Replace("\r\n", "\n").Trim().Split('\n');
        Assert.Equal(new[]
        {
            "billing-portal/config/portal.settings:1: portal.invoice.page.size = 20",
            "orders-service/config/orders.settings:1: orders.page.size = 25",
            "orders-service/notes/more.md:2: second PAGE size"
        }, lines.Where(l => l.Contains("page", StringComparison.OrdinalIgnoreCase)).ToArray());
    }

    [Fact]
    public void Search_ReturnsAtMost20Matches_AndTrimsEachLineTo160Characters()
    {
        using var sample = new TempSample();
        sample.Write("many/hits", string.Join("\n", Enumerable.Range(0, 40).Select(i => "needle " + new string('z', 400))));

        var result = ReaderFor(sample, maxFileBytes: 30000).Search("needle");

        var lines = result.Text.Replace("\r\n", "\n").Trim().Split('\n');
        Assert.Equal(20, lines.Length);
        Assert.All(lines, line => Assert.True(line.Length <= "many/hits:40: ".Length + 160));
    }

    [Fact]
    public void Search_SkipsFilesOverTheLimit_AndBinaryFiles()
    {
        using var sample = new TempSample();
        sample.Write("big/file", "needle " + new string('x', 500));
        sample.WriteBytes("big/blob", Encoding.UTF8.GetBytes("needle").Concat(new byte[] { 0, 1, 2 }).ToArray());

        var result = ReaderFor(sample, maxFileBytes: 100).Search("needle");

        Assert.Equal(ToolCallOutcome.Served, result.Outcome);
        Assert.DoesNotContain("big/", result.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Search_WithNoUsableTerm_IsInvalid(string? term)
    {
        using var sample = new TempSample();

        Assert.Equal(ToolCallOutcome.RefusedInvalidArgument, ReaderFor(sample).Search(term).Outcome);
    }

    [Fact]
    public void Search_WithATermOver80Characters_IsInvalid()
    {
        using var sample = new TempSample();

        Assert.Equal(ToolCallOutcome.RefusedInvalidArgument, ReaderFor(sample).Search(new string('a', 81)).Outcome);
        Assert.Equal(ToolCallOutcome.Served, ReaderFor(sample).Search(new string('a', 80)).Outcome);
    }

    [Fact]
    public void Search_ThatMatchesNothing_IsServedAndEmpty()
    {
        using var sample = new TempSample();

        var result = ReaderFor(sample).Search("no-such-text-anywhere");

        Assert.Equal(ToolCallOutcome.Served, result.Outcome);
        Assert.Equal(string.Empty, result.Text.Trim());
        Assert.Equal(0, result.Bytes);
    }

    [Fact]
    public void NothingUnderTheTempFolderChanges_AcrossEveryOperation()
    {
        using var sample = new TempSample();
        var reader = ReaderFor(sample);
        var before = sample.Fingerprint();

        reader.List(null);
        reader.List("orders-service");
        reader.Read("orders-service/notes/design.md");
        reader.Read("../outside/secret.txt");
        reader.Search("page");
        reader.Search("../x");

        Assert.Equal(before, sample.Fingerprint());
    }
}
