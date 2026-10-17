using System.Diagnostics;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.SampleCode.Tests.Fakes;

namespace AgenticChangeOps.SampleCode.Tests;

/// <summary>
/// FR-004 to FR-006: the tools are offered through an in-process Model Context Protocol server,
/// exactly three of them, with no process, socket, resource or prompt.
/// </summary>
public sealed class McpToolListTests
{
    private static SampleToolSettings Settings(int maxCalls = 8, int maxBytes = 24000, int maxFileBytes = 8000) =>
        new(Enabled: true, MaxToolCalls: maxCalls, MaxModelCalls: 6, MaxReturnedBytes: maxBytes,
            MaxFileBytes: maxFileBytes, TimeLimit: TimeSpan.FromSeconds(60), SampleFolder: null);

    private static Task<SampleToolSession> OpenAsync(TempSample sample, SampleToolSettings? settings = null) =>
        SampleToolSession.OpenAsync(settings ?? Settings(), sample.Root, time: null, CancellationToken.None);

    [Fact]
    public async Task TheServerOffersExactlyTheThreeTools_AndNoResourcesOrPrompts()
    {
        using var sample = new TempSample();
        await using var session = await OpenAsync(sample);

        var (tools, resources, prompts) = await session.DescribeServerAsync();

        Assert.Equal(new[] { "list_files", "read_file", "search_text" }, tools.OrderBy(t => t, StringComparer.Ordinal));
        Assert.Equal(0, resources);
        Assert.Equal(0, prompts);
        Assert.Equal(new[] { "list_files", "read_file", "search_text" }, session.Tools.Names.OrderBy(t => t, StringComparer.Ordinal));
    }

    [Fact]
    public async Task EachTool_ThroughMcp_ReturnsWhatTheReaderReturns()
    {
        using var sample = new TempSample();
        var reader = new SampleReader(new SamplePathResolver(sample.Root), 8000);
        await using var session = await OpenAsync(sample);

        var list = await session.CallAsync("list_files", new Dictionary<string, object?> { ["path"] = "orders-service" });
        var read = await session.CallAsync("read_file", new Dictionary<string, object?> { ["path"] = "orders-service/notes/design.md" });
        var search = await session.CallAsync("search_text", new Dictionary<string, object?> { ["term"] = "page" });

        Assert.Equal(reader.List("orders-service").Text, list);
        Assert.Equal(reader.Read("orders-service/notes/design.md").Text, read);
        Assert.Equal(reader.Search("page").Text, search);
    }

    [Fact]
    public async Task EveryCall_IsRecorded_WithToolTargetOutcomeSizeAndTime_AndNoContent()
    {
        using var sample = new TempSample();
        await using var session = await OpenAsync(sample);

        await session.CallAsync("read_file", new Dictionary<string, object?> { ["path"] = "orders-service/notes/design.md" });
        await session.CallAsync("read_file", new Dictionary<string, object?> { ["path"] = "../outside/secret.txt" });
        await session.CallAsync("search_text", new Dictionary<string, object?> { ["term"] = "Orders design" });

        Assert.Equal(3, session.Calls.Count);
        Assert.Equal(("read_file", "orders-service/notes/design.md", ToolCallOutcome.Served),
            (session.Calls[0].ToolName, session.Calls[0].Target, session.Calls[0].Outcome));
        Assert.True(session.Calls[0].ResultBytes > 0);
        Assert.Equal(ToolCallOutcome.RefusedOutsideSample, session.Calls[1].Outcome);
        Assert.Equal(0, session.Calls[1].ResultBytes);
        Assert.Equal(("search_text", "Orders design"), (session.Calls[2].ToolName, session.Calls[2].Target));
        Assert.All(session.Calls, c => Assert.True(c.Elapsed >= TimeSpan.Zero));
        Assert.Equal(new[] { "orders-service/notes/design.md" }, session.FilesRead);
    }

    [Fact]
    public async Task APathThatEscapes_IsNotRecordedAsAFileRead_AndItsTargetIsNeverAnAbsolutePath()
    {
        using var sample = new TempSample();
        await using var session = await OpenAsync(sample);

        await session.CallAsync("read_file", new Dictionary<string, object?> { ["path"] = Path.Combine(sample.Outside, "secret.txt") });

        Assert.Empty(session.FilesRead);
        Assert.DoesNotContain(sample.Outside, session.Calls.Single().Target);
    }

    [Fact]
    public async Task AFileReadTwice_IsListedOnce_ButCountsAgainstTheBudgetEachTime()
    {
        using var sample = new TempSample();
        await using var session = await OpenAsync(sample, Settings(maxCalls: 2));

        await session.CallAsync("read_file", new Dictionary<string, object?> { ["path"] = "orders-service/src/OrderIntake" });
        await session.CallAsync("read_file", new Dictionary<string, object?> { ["path"] = "orders-service/src/OrderIntake" });
        var third = await session.CallAsync("read_file", new Dictionary<string, object?> { ["path"] = "orders-service/src/OrderIntake" });

        Assert.Equal(new[] { "orders-service/src/OrderIntake" }, session.FilesRead);
        Assert.Equal("The tool-call limit for this request has been reached.", third);
        Assert.Equal(ToolCallOutcome.RefusedCallLimit, session.Calls[2].Outcome);
    }

    [Fact]
    public async Task TheDataLimit_RefusesAWholeResult_WithTheFixedMessage()
    {
        using var sample = new TempSample();
        sample.Write("orders-service/notes/long.md", new string('x', 300));
        await using var session = await OpenAsync(sample, Settings(maxBytes: 200, maxFileBytes: 1000));

        var result = await session.CallAsync("read_file", new Dictionary<string, object?> { ["path"] = "orders-service/notes/long.md" });

        Assert.Equal("The returned-data limit for this request has been reached.", result);
        Assert.Equal(ToolCallOutcome.RefusedDataLimit, session.Calls.Single().Outcome);
        Assert.Empty(session.FilesRead);
    }

    [Fact]
    public async Task OpeningASession_StartsNoProcess_AndUsesAnInMemoryTransport()
    {
        using var sample = new TempSample();
        var before = Process.GetProcesses().Select(p => p.Id).ToHashSet();

        await using var session = await OpenAsync(sample);

        Assert.Equal("in-memory", session.TransportKind);
        var started = Process.GetProcesses()
            .Where(p => !before.Contains(p.Id))
            .Select(p => p.ProcessName)
            .Where(n => n is not ("conhost" or "dotnet" or "testhost" or "VBCSCompiler"))
            .ToList();
        Assert.Empty(started);
    }

    [Fact]
    public async Task AfterDisposal_ACallFailsCleanly_AndReadsNothing()
    {
        using var sample = new TempSample();
        var session = await OpenAsync(sample);
        await session.DisposeAsync();
        await session.DisposeAsync();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            session.CallAsync("read_file", new Dictionary<string, object?> { ["path"] = "orders-service/src/OrderIntake" }));
        Assert.Empty(session.Calls);
    }

    [Fact]
    public async Task ATool_NotInTheFixedSet_CannotBeCalled()
    {
        using var sample = new TempSample();
        await using var session = await OpenAsync(sample);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            session.Tools.InvokeAsync("delete_file", "{\"path\":\"orders-service/src/OrderIntake\"}", CancellationToken.None));
        Assert.True(File.Exists(Path.Combine(sample.Root, "orders-service", "src", "OrderIntake")));
    }

    [Fact]
    public async Task TheFactory_WithAMissingOrEmptySampleFolder_ReportsToolsUnavailable_AndNothingElse()
    {
        using var sample = new TempSample();
        var missing = Path.Combine(sample.Outside, "no-such-folder");
        var empty = Path.Combine(sample.Outside, "empty");
        Directory.CreateDirectory(empty);

        foreach (var root in new[] { missing, empty })
        {
            var factory = new SampleToolSessionFactory(Settings(), root);

            var error = await Assert.ThrowsAsync<DraftGenerationException>(() => factory.OpenAsync(CancellationToken.None));

            Assert.Equal(GenerationFailureCategory.ToolsUnavailable, error.Category);
            Assert.DoesNotContain(root, error.Message);
            Assert.Null(error.InnerException);
        }
    }

    [Theory]
    [InlineData("list_files,read_file,search_text", true)]
    [InlineData("search_text,list_files,read_file", true)]
    [InlineData("list_files,read_file", false)]
    [InlineData("list_files,read_file,search_text,delete_file", false)]
    [InlineData("list_files,read_file,read_file", false)]
    [InlineData("", false)]
    public void ASessionIsOnlyAccepted_WhenTheServerListsExactlyTheExpectedTools(string names, bool expected)
    {
        var list = names.Length == 0 ? Array.Empty<string>() : names.Split(',');

        Assert.Equal(expected, SampleMcpServer.IsExpectedToolSet(list));
    }

    [Fact]
    public async Task TheFactory_OpensAWorkingSession_ForAValidSample()
    {
        using var sample = new TempSample();
        var factory = new SampleToolSessionFactory(Settings(), sample.Root);

        await using var session = await factory.OpenAsync(CancellationToken.None);

        Assert.Equal(6, session.MaxModelCalls);
        Assert.Equal(TimeSpan.FromSeconds(60), session.TimeLimit);
        Assert.Equal(3, session.Tools.Names.Count);
    }
}
