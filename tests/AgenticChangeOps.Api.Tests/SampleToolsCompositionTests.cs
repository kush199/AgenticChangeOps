using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US1 end to end through the real composition: tools on, the real sample, a scripted model call.
/// SC-001 and SC-006: designs cite real files; one audit entry per call with no content.
/// </summary>
public sealed class SampleToolsCompositionTests
{
    [Fact]
    public async Task AnOrdersDesign_ListsRealFilesUnderTheOrdersService()
    {
        using var host = new SampleToolsHost(behavior: SampleToolsHost.Reads(
            "orders-service/config/orders.settings", "orders-service/src/OrderIntake"));
        var id = await host.ApprovedAsync("Orders Service");

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var files = (await response.ReadJsonAsync()).GetProperty("sampleFilesRead").EnumerateArray()
            .Select(f => f.GetString()!).ToArray();
        Assert.Equal(new[] { "orders-service/config/orders.settings", "orders-service/src/OrderIntake" }, files);
        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(SampleToolsHost.RealSampleFolder(), f.Replace('/', Path.DirectorySeparatorChar)))));
    }

    [Fact]
    public async Task ABillingDesign_ListsRealFilesUnderTheBillingPortal()
    {
        using var host = new SampleToolsHost(behavior: SampleToolsHost.Reads(
            "billing-portal/src/PaymentReminder", "billing-portal/notes/decisions.md"));
        var id = await host.ApprovedAsync("Billing Portal");

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        var files = (await response.ReadJsonAsync()).GetProperty("sampleFilesRead").EnumerateArray()
            .Select(f => f.GetString()!).ToArray();
        Assert.All(files, f => Assert.StartsWith("billing-portal/", f));
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public async Task TheLatestAndHistoryRoutes_ReturnTheSameFileList()
    {
        using var host = new SampleToolsHost(behavior: SampleToolsHost.Reads("orders-service/src/StockCheck"));
        var id = await host.ApprovedAsync();
        await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        var latest = await (await host.Client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();
        var history = await (await host.Client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync();

        Assert.Equal("orders-service/src/StockCheck", latest.GetProperty("sampleFilesRead")[0].GetString());
        Assert.Equal("orders-service/src/StockCheck", history[0].GetProperty("sampleFilesRead")[0].GetString());
    }

    [Fact]
    public async Task EachCall_IsAudited_WithoutContent_AndTheStatusStaysApproved()
    {
        using var host = new SampleToolsHost(behavior: SampleToolsHost.Reads(
            "orders-service/config/orders.settings", "../outside", "orders-service/src/OrderIntake"));
        var id = await host.ApprovedAsync();

        await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        var audit = await host.AuditAsync(id);
        var types = audit.EnumerateArray().Select(e => e.GetProperty("eventType").GetString()).ToArray();
        Assert.Equal(new[] { "Submitted", "Approved", "ToolCalled", "ToolCalled", "ToolCalled", "DesignGenerated" }, types);
        var calls = audit.EnumerateArray().Where(e => e.GetProperty("eventType").GetString() == "ToolCalled").ToArray();
        Assert.Equal(new[] { "Served", "RefusedOutsideSample", "Served" }, calls.Select(c => c.GetProperty("toolOutcome").GetString()));
        Assert.All(calls, c =>
        {
            Assert.Equal("read_file", c.GetProperty("toolName").GetString());
            Assert.True(c.GetProperty("resultBytes").GetInt32() >= 0);
            Assert.True(c.GetProperty("elapsedMilliseconds").GetInt32() >= 0);
        });

        var text = audit.GetRawText();
        Assert.DoesNotContain("orders.page.size", text);
        Assert.DoesNotContain("acceptOrder", text);
        var request = await (await host.Client.GetAsync($"/change-requests/{id}")).ReadJsonAsync();
        Assert.Equal("Approved", request.GetProperty("status").GetString());
    }

    [Fact]
    public async Task TheApiOutputFolder_ContainsTheSameSampleFilesAsTheRepository()
    {
        var published = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location)!, "sample-codebase");
        Assert.True(Directory.Exists(published), "The Api build output must contain sample-codebase/.");

        static string[] Files(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')).OrderBy(f => f, StringComparer.Ordinal).ToArray();

        Assert.Equal(Files(SampleToolsHost.RealSampleFolder()), Files(published));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task TwoConcurrentDesigns_GetIndependentBudgetsAndCallLists()
    {
        var gate = new TaskCompletionSource();
        var started = 0;
        using var host = new SampleToolsHost(behavior: async (session, ct) =>
        {
            var n = Interlocked.Increment(ref started);
            await session.Tools.InvokeAsync("read_file",
                n == 1 ? "{\"path\":\"orders-service/src/OrderIntake\"}" : "{\"path\":\"billing-portal/src/InvoiceList\"}", ct);
            if (n == 1)
                await gate.Task;
            else
                gate.TrySetResult();
            return new AgenticChangeOps.ModelAccess.ChatModelResponse(SampleToolsHost.ValidAnswer, false);
        });
        var first = await host.ApprovedAsync();
        var second = await host.ApprovedAsync();

        var responses = await Task.WhenAll(
            host.Client.PostAsync($"/change-requests/{first}/designs", null),
            host.Client.PostAsync($"/change-requests/{second}/designs", null));

        var lists = new List<string[]>();
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            lists.Add((await response.ReadJsonAsync()).GetProperty("sampleFilesRead").EnumerateArray().Select(f => f.GetString()!).ToArray());
        }

        Assert.All(lists, l => Assert.Single(l));
        Assert.NotEqual(lists[0][0], lists[1][0]);
    }
}
