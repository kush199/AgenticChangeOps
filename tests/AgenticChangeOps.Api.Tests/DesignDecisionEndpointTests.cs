using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

public sealed class DesignDecisionEndpointTests : IDisposable
{
    private readonly DesignHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Accept_Returns201WithTheStoredDecision_AndALocationToTheLatestDesign()
    {
        var id = await _host.ApprovedWithDesignsAsync();

        var response = await _host.DecideAsync(id, 1, "Accept");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/change-requests/{id}/designs/latest", response.Headers.Location?.OriginalString);
        var json = await response.ReadJsonAsync();
        Assert.Equal(id, json.GetProperty("changeRequestId").GetGuid());
        Assert.Equal(1, json.GetProperty("version").GetInt32());
        Assert.Equal("Accepted", json.GetProperty("outcome").GetString());
        Assert.Equal("Jordan Reed", json.GetProperty("approverName").GetString());
        Assert.True(json.TryGetProperty("decisionId", out _));
        Assert.True(json.TryGetProperty("designId", out _));
        Assert.True(json.TryGetProperty("decidedAt", out _));
        var design = await (await _host.Client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();
        Assert.Equal(design.GetProperty("designId").GetGuid(), json.GetProperty("designId").GetGuid());
    }

    [Fact]
    public async Task Reject_WithAReason_Returns201Rejected()
    {
        var id = await _host.ApprovedWithDesignsAsync();

        var response = await _host.DecideAsync(id, 1, "Reject", reason: "Rollback plan is missing.");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("Rejected", json.GetProperty("outcome").GetString());
        Assert.Equal("Rollback plan is missing.", json.GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("ACCEPT")]
    [InlineData(" Accept ")]
    public async Task TheDecisionValue_IsMatchedIgnoringCaseAndSurroundingSpace(string value)
    {
        var id = await _host.ApprovedWithDesignsAsync();

        var response = await _host.DecideAsync(id, 1, value);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AnIdOrVersionInTheBody_IsIgnored()
    {
        var id = await _host.ApprovedWithDesignsAsync(designs: 2);

        var response = await _host.DecideAsync(id, "2", new
        {
            approverName = "Jordan Reed",
            decision = "Accept",
            changeRequestId = Guid.NewGuid(),
            version = 1
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, (await response.ReadJsonAsync()).GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task ADecision_ChangesNoStatus_AndLeavesTheWorkflowAndClearanceAsTheyWere()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        var workflowBefore = await (await _host.Client.GetAsync($"/change-requests/{id}/workflow")).Content.ReadAsStringAsync();
        var clearanceBefore = await (await _host.Client.GetAsync($"/change-requests/{id}/clearance")).Content.ReadAsStringAsync();

        (await _host.DecideAsync(id, 1, "Reject", reason: "Not yet.")).EnsureSuccessStatusCode();

        var request = await (await _host.Client.GetAsync($"/change-requests/{id}")).ReadJsonAsync();
        Assert.Equal("Approved", request.GetProperty("status").GetString());
        Assert.Equal(workflowBefore, await (await _host.Client.GetAsync($"/change-requests/{id}/workflow")).Content.ReadAsStringAsync());
        Assert.Equal(clearanceBefore, await (await _host.Client.GetAsync($"/change-requests/{id}/clearance")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AfterARejection_ANewDesignCanBeRequested_AsTheNextVersion()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        (await _host.DecideAsync(id, 1, "Reject", reason: "Not yet.")).EnsureSuccessStatusCode();

        var response = await _host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, (await response.ReadJsonAsync()).GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.Created, (await _host.DecideAsync(id, 2, "Accept")).StatusCode);
    }

    [Fact]
    public async Task EachDecision_AddsOneDesignDecidedAuditEntry()
    {
        var id = await _host.ApprovedWithDesignsAsync();

        await _host.DecideAsync(id, 1, "Accept");

        Assert.Equal(
            new[] { "Submitted", "Approved", "DesignGenerated", "DesignDecided" },
            await _host.AuditEventTypesAsync(id));
        var audit = await (await _host.Client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();
        var entry = audit.EnumerateArray().Last();
        Assert.Equal("Jordan Reed", entry.GetProperty("approverName").GetString());
        Assert.Equal(1, entry.GetProperty("draftVersion").GetInt32());
    }
}
