using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// US1 and US2 over HTTP, through the real composition: request, latest and history with the
/// conventions of the design operations (contracts/http-api.md).
/// </summary>
public sealed class ProposalEndpointTests
{
    // ---- request ----

    [Fact]
    public async Task Post_Returns201_WithLocation_AndTheCamelCaseBody()
    {
        using var host = new ProposalHost();
        var id = await host.ApprovedAsync();

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/change-requests/{id}/proposals/latest", response.Headers.Location!.OriginalString);
        var body = await response.ReadJsonAsync();
        Assert.Equal(id, body.GetProperty("changeRequestId").GetGuid());
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.Equal(1, body.GetProperty("requirementsVersion").GetInt32());
        Assert.Equal(1, body.GetProperty("designVersion").GetInt32());
        Assert.Equal("Draft", body.GetProperty("status").GetString());
        Assert.Equal("DeveloperAgent", body.GetProperty("agentName").GetString());
        Assert.Equal("Not compiled, run or tested.", body.GetProperty("verificationNote").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("summary").GetString()));
        var change = Assert.Single(body.GetProperty("changes").EnumerateArray());
        Assert.Equal(ProposalHost.OrdersSettings, change.GetProperty("path").GetString());
        Assert.Equal("Change", change.GetProperty("kind").GetString());
        Assert.Equal("orders.page.size = 25", change.GetProperty("existingText").GetString());
        Assert.Equal("orders.page.size = 50", change.GetProperty("replacementText").GetString());
        Assert.NotEmpty(body.GetProperty("risks").EnumerateArray());
        Assert.NotEmpty(body.GetProperty("testIdeas").EnumerateArray());
        Assert.Equal(new[] { ProposalHost.OrdersSettings }, body.GetProperty("sampleFilesRead").EnumerateArray().Select(f => f.GetString()));
        Assert.Equal(1, host.ToolClient.Calls);
    }

    [Fact]
    public async Task AnAdd_OmitsTheExistingText_InTheBody()
    {
        using var host = new ProposalHost(behavior: ProposalHost.ReadsAndProposes(ProposalHost.AddEdit()));
        var id = await host.ApprovedAsync();

        var body = await (await host.ProposeAsync(id)).ReadJsonAsync();

        var change = body.GetProperty("changes")[0];
        Assert.Equal("Add", change.GetProperty("kind").GetString());
        Assert.False(change.TryGetProperty("existingText", out _));
    }

    [Fact]
    public async Task Proposing_LeavesTheSampleByteIdentical_AndTheChangeRequestStatusUnchanged()
    {
        using var host = new ProposalHost(behavior: ProposalHost.ReadsAndProposes(ProposalHost.ChangeEdit(), ProposalHost.AddEdit()));
        var id = await host.ApprovedAsync();
        var before = host.SampleFingerprint();

        var response = await host.ProposeAsync(id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(before, host.SampleFingerprint());
        Assert.False(File.Exists(Path.Combine(host.SampleFolder, "orders-service", "notes", "page-size.md")));
        Assert.Equal("Approved", await host.StatusAsync(id));
    }

    [Fact]
    public async Task TheAudit_HasOneToolCalledEntryPerCall_ThenProposalGenerated_WithoutContent()
    {
        using var host = new ProposalHost();
        var id = await host.ApprovedAsync();

        await host.ProposeAsync(id);

        var audit = (await host.AuditAsync(id)).EnumerateArray().ToArray();
        Assert.Equal(
            new[] { "Submitted", "Approved", "ToolCalled", "ProposalGenerated" },
            audit.Select(e => e.GetProperty("eventType").GetString()));
        var call = audit[2];
        Assert.Equal("read_file", call.GetProperty("toolName").GetString());
        Assert.Equal(ProposalHost.OrdersSettings, call.GetProperty("toolTarget").GetString());
        Assert.Equal("Served", call.GetProperty("toolOutcome").GetString());
        Assert.True(call.GetProperty("resultBytes").GetInt32() > 0);
        Assert.Equal("DeveloperAgent", call.GetProperty("agentName").GetString());
        var generated = audit[3];
        Assert.Equal("DeveloperAgent", generated.GetProperty("agentName").GetString());
        Assert.Equal(1, generated.GetProperty("draftVersion").GetInt32());
        var raw = await (await host.Client.GetAsync($"/change-requests/{id}/audit")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("orders.page.size", raw);
    }

    // ---- retrieval ----

    [Fact]
    public async Task Latest_And_History_FollowTheDesignConventions()
    {
        using var host = new ProposalHost();
        var id = await host.ApprovedAsync();
        await host.ProposeAsync(id);
        await host.ProposeAsync(id);

        var latestResponse = await host.Client.GetAsync($"/change-requests/{id}/proposals/latest");
        var historyResponse = await host.Client.GetAsync($"/change-requests/{id}/proposals");

        Assert.Equal(HttpStatusCode.OK, latestResponse.StatusCode);
        Assert.Equal(2, (await latestResponse.ReadJsonAsync()).GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = (await historyResponse.ReadJsonAsync()).EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 2 }, history.Select(p => p.GetProperty("version").GetInt32()));
    }

    [Fact]
    public async Task Latest_WithNoProposal_Is404NoProposal_AndHistoryIsAnEmptyList()
    {
        using var host = new ProposalHost();
        var id = await host.ApprovedAsync();

        var latest = await host.Client.GetAsync($"/change-requests/{id}/proposals/latest");
        var history = await host.Client.GetAsync($"/change-requests/{id}/proposals");

        Assert.Equal(HttpStatusCode.NotFound, latest.StatusCode);
        Assert.Equal("no-proposal", (await latest.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Empty((await history.ReadJsonAsync()).EnumerateArray());
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("00000000-0000-0000-0000-00000000000Z")]
    public async Task AMalformedId_Is400InvalidIdentifier_OnAllThreeRoutes(string id)
    {
        using var host = new ProposalHost();

        var responses = new[]
        {
            await host.Client.PostAsync($"/change-requests/{id}/proposals", null),
            await host.Client.GetAsync($"/change-requests/{id}/proposals/latest"),
            await host.Client.GetAsync($"/change-requests/{id}/proposals")
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid-identifier", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task AnUnknownId_Is404NotFound_OnAllThreeRoutes_EvenWithTheToolsOff()
    {
        using var host = new ProposalHost(toolsOn: false);
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await host.Client.PostAsync($"/change-requests/{id}/proposals", null),
            await host.Client.GetAsync($"/change-requests/{id}/proposals/latest"),
            await host.Client.GetAsync($"/change-requests/{id}/proposals")
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("not-found", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Reads_AreAvailable_EvenWhenTheToolsAreOff()
    {
        using var host = new ProposalHost(toolsOn: false);
        var id = await host.ApprovedAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/change-requests/{id}/proposals/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync($"/change-requests/{id}/proposals")).StatusCode);
    }

    [Fact]
    public async Task TwoOverlappingRequests_GetDistinctVersions()
    {
        using var host = new ProposalHost();
        var id = await host.ApprovedAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => host.ProposeAsync(id)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var history = (await (await host.Client.GetAsync($"/change-requests/{id}/proposals")).ReadJsonAsync()).EnumerateArray().ToArray();
        Assert.Equal(Enumerable.Range(1, 6), history.Select(p => p.GetProperty("version").GetInt32()));
    }
}
