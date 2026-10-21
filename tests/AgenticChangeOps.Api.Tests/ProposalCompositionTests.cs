using System.Net;
using System.Text.Json;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// End to end through the real composition with no fake basis: requirements draft, approval,
/// design and proposal all go through the real services and the real adapters, with only the model
/// call scripted. SC-001, SC-008 and the workflow offer.
/// </summary>
public sealed class ProposalCompositionTests
{
    private const string DesignJson = """
        {
          "approachSummary": "Change the page size setting only.",
          "affectedComponents": [ "Settings" ],
          "impactAndRisks": [ "Longer pages load more rows." ],
          "dependencies": [],
          "rollbackConsiderations": [ "Restore the previous value." ],
          "openQuestions": []
        }
        """;

    private const string PortalSettings = "billing-portal/config/portal.settings";

    private static string ReadSample(ProposalHost host, string path) =>
        File.ReadAllText(Path.Combine(host.SampleFolder, path.Replace('/', Path.DirectorySeparatorChar)));

    private static int Count(string text, string quote) =>
        text.Replace("\r\n", "\n").Split(quote.Replace("\r\n", "\n")).Length - 1;

    /// <summary>Submit, draft requirements, approve, design: leaves the request ready for a proposal.</summary>
    private static async Task<Guid> ReadyAsync(ProposalHost host, string system, Func<IToolSession, CancellationToken, Task<ChatModelResponse>> proposalBehavior)
    {
        var id = await host.SubmittedAsync(system);
        (await host.Client.PostAsync($"/change-requests/{id}/drafts", null)).EnsureSuccessStatusCode();
        (await host.Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve())).EnsureSuccessStatusCode();
        host.ToolClient.Behavior = ProposalHost.Returns(DesignJson);
        (await host.Client.PostAsync($"/change-requests/{id}/designs", null)).EnsureSuccessStatusCode();
        host.ToolClient.Behavior = proposalBehavior;
        return id;
    }

    private static void AssertEveryEditChecksOut(ProposalHost host, JsonElement proposal)
    {
        foreach (var change in proposal.GetProperty("changes").EnumerateArray())
        {
            var path = change.GetProperty("path").GetString()!;
            var exists = File.Exists(Path.Combine(host.SampleFolder, path.Replace('/', Path.DirectorySeparatorChar)));
            if (change.GetProperty("kind").GetString() == "Change")
            {
                Assert.True(exists, $"{path} should exist");
                Assert.Equal(1, Count(ReadSample(host, path), change.GetProperty("existingText").GetString()!));
            }
            else
            {
                Assert.False(exists, $"{path} should not exist");
            }
        }
    }

    [Fact]
    public async Task AnOrdersProposal_GoesThroughTheRealDraftAndDesign_AndEveryEditChecksOut()
    {
        using var host = new ProposalHost(useFakeBasis: false);
        var id = await ReadyAsync(host, "Orders Service",
            ProposalHost.ReadsAndProposes(ProposalHost.ChangeEdit(), ProposalHost.AddEdit()));

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var proposal = await response.ReadJsonAsync();
        Assert.Equal(1, proposal.GetProperty("requirementsVersion").GetInt32());
        Assert.Equal(1, proposal.GetProperty("designVersion").GetInt32());
        AssertEveryEditChecksOut(host, proposal);
        Assert.All(
            proposal.GetProperty("sampleFilesRead").EnumerateArray(),
            f => Assert.StartsWith("orders-service/", f.GetString()));
    }

    [Fact]
    public async Task ABillingProposal_EditsTheBillingPortalSettings_AndChecksOut()
    {
        using var host = new ProposalHost(useFakeBasis: false);
        var edit = ProposalHost.ChangeEdit(PortalSettings, "portal.invoice.page.size = 20", "portal.invoice.page.size = 40");
        var id = await ReadyAsync(host, "Billing Portal", async (session, ct) =>
        {
            await session.Tools.InvokeAsync("read_file", $"{{\"path\":\"{PortalSettings}\"}}", ct);
            return new ChatModelResponse(ProposalHost.Answer(edit), false);
        });

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var proposal = await response.ReadJsonAsync();
        AssertEveryEditChecksOut(host, proposal);
        Assert.Equal(new[] { PortalSettings }, proposal.GetProperty("sampleFilesRead").EnumerateArray().Select(f => f.GetString()));
    }

    [Fact]
    public async Task TheWorkflowOffersTheProposal_AtDesignComplete_WithoutMakingItTheNextStep()
    {
        using var host = new ProposalHost(useFakeBasis: false);
        var id = await ReadyAsync(host, "Orders Service", ProposalHost.ReadsAndProposes());

        var status = await (await host.Client.GetAsync($"/change-requests/{id}/workflow")).ReadJsonAsync();

        Assert.Equal("DesignComplete", status.GetProperty("stage").GetString());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("nextStep").ValueKind);
        Assert.Equal("Proposal", status.GetProperty("offeredStep").GetString());

        var advance = await host.Client.PostAsync($"/change-requests/{id}/workflow/advance", null);
        Assert.Equal(HttpStatusCode.Conflict, advance.StatusCode);
        Assert.Equal(1, host.ToolClient.Calls);   // only the design used the model
        var history = await (await host.Client.GetAsync($"/change-requests/{id}/proposals")).ReadJsonAsync();
        Assert.Empty(history.EnumerateArray());
    }


    [Fact]
    public async Task TwoSequentialRequests_GiveVersionsOneAndTwo_AndOnlyTheSecondIsLatest()
    {
        using var host = new ProposalHost(useFakeBasis: false);
        var id = await ReadyAsync(host, "Orders Service", ProposalHost.ReadsAndProposes());

        var first = await (await host.ProposeAsync(id)).ReadJsonAsync();
        var second = await (await host.ProposeAsync(id)).ReadJsonAsync();

        Assert.Equal(1, first.GetProperty("version").GetInt32());
        Assert.Equal(2, second.GetProperty("version").GetInt32());
        var latest = await (await host.Client.GetAsync($"/change-requests/{id}/proposals/latest")).ReadJsonAsync();
        Assert.Equal(2, latest.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task TheApiDescription_ListsTheThreeOperations_WithTheDocumentedResponses()
    {
        using var host = new ProposalHost();

        var doc = await (await host.Client.GetAsync("/openapi/v1.json")).ReadJsonAsync();
        var paths = doc.GetProperty("paths");

        var post = paths.GetProperty("/change-requests/{id}/proposals").GetProperty("post");
        var codes = post.GetProperty("responses").EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "201", "400", "404", "409", "500", "502", "503" }, codes);
        Assert.Equal("RequestProposal", post.GetProperty("operationId").GetString());
        Assert.True(paths.GetProperty("/change-requests/{id}/proposals").TryGetProperty("get", out _));
        var latest = paths.GetProperty("/change-requests/{id}/proposals/latest").GetProperty("get");
        Assert.Equal("GetLatestProposal", latest.GetProperty("operationId").GetString());
        Assert.Equal(
            new[] { "200", "400", "404" },
            latest.GetProperty("responses").EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
    }
}
