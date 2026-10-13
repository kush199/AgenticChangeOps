using System.Net;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

public class DesignEndpointTests : IDisposable
{
    private readonly DesignHost _host = new();

    private HttpClient Client => _host.Client;

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Design_ApprovedWithRequirementsDraft_Returns201WithLocationVersionOneAndOneAuditEntry()
    {
        var id = await _host.ApprovedWithBasisAsync(basisVersion: 2);

        var response = await Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/change-requests/{id}/designs/latest", response.Headers.Location?.OriginalString);
        var json = await response.ReadJsonAsync();
        Assert.Equal(1, json.GetProperty("version").GetInt32());
        Assert.Equal(2, json.GetProperty("requirementsVersion").GetInt32());
        Assert.Equal(id, json.GetProperty("changeRequestId").GetGuid());
        Assert.Equal("Draft", json.GetProperty("status").GetString());
        Assert.Equal("ArchitectAgent", json.GetProperty("agentName").GetString());
        foreach (var section in new[]
                 {
                     "approachSummary", "affectedComponents", "impactAndRisks",
                     "dependencies", "rollbackConsiderations", "openQuestions"
                 })
            Assert.True(json.TryGetProperty(section, out _), $"Missing section {section}.");
        Assert.Equal(new[] { "Submitted", "Approved", "DesignGenerated" }, await _host.AuditEventTypesAsync(id));
    }

    [Fact]
    public async Task Design_LeavesTheChangeRequestApproved()
    {
        var id = await _host.ApprovedWithBasisAsync();

        await Client.PostAsync($"/change-requests/{id}/designs", null);

        var request = await (await Client.GetAsync($"/change-requests/{id}")).ReadJsonAsync();
        Assert.Equal("Approved", request.GetProperty("status").GetString());
        var clearance = await (await Client.GetAsync($"/change-requests/{id}/clearance")).ReadJsonAsync();
        Assert.True(clearance.GetProperty("isCleared").GetBoolean());
    }

    [Fact]
    public async Task Design_Twice_ProducesVersionTwo()
    {
        var id = await _host.ApprovedWithBasisAsync();
        await Client.PostAsync($"/change-requests/{id}/designs", null);

        var response = await Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, (await response.ReadJsonAsync()).GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Latest_ReturnsHighestVersion_AndHistoryReturnsAllAscending()
    {
        var id = await _host.ApprovedWithBasisAsync();
        await Client.PostAsync($"/change-requests/{id}/designs", null);
        await Client.PostAsync($"/change-requests/{id}/designs", null);

        var latest = await Client.GetAsync($"/change-requests/{id}/designs/latest");
        var history = await Client.GetAsync($"/change-requests/{id}/designs");

        Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
        Assert.Equal(2, (await latest.ReadJsonAsync()).GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var versions = (await history.ReadJsonAsync()).EnumerateArray()
            .Select(d => d.GetProperty("version").GetInt32()).ToArray();
        Assert.Equal(new[] { 1, 2 }, versions);
    }

    [Fact]
    public async Task NoDesigns_LatestIs404NoDesignDraft_AndHistoryIsEmptyArray()
    {
        var id = await _host.ApprovedWithBasisAsync();

        var latest = await Client.GetAsync($"/change-requests/{id}/designs/latest");
        var history = await Client.GetAsync($"/change-requests/{id}/designs");

        Assert.Equal(HttpStatusCode.NotFound, latest.StatusCode);
        Assert.Equal("no-design-draft", (await latest.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Empty((await history.ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task UnknownIdentifier_Returns404NotFoundOnAllThreeRoutes()
    {
        const string id = "00000000-0000-0000-0000-000000000001";

        var responses = new[]
        {
            await Client.PostAsync($"/change-requests/{id}/designs", null),
            await Client.GetAsync($"/change-requests/{id}/designs/latest"),
            await Client.GetAsync($"/change-requests/{id}/designs")
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("not-found", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        }

        Assert.Equal(0, _host.Generator.CallCount);
    }

    [Fact]
    public async Task InvalidIdentifier_Returns400InvalidIdentifierOnAllThreeRoutes()
    {
        var responses = new[]
        {
            await Client.PostAsync("/change-requests/not-a-guid/designs", null),
            await Client.GetAsync("/change-requests/not-a-guid/designs/latest"),
            await Client.GetAsync("/change-requests/not-a-guid/designs")
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid-identifier", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        }

        Assert.Equal(0, _host.Generator.CallCount);
    }

    [Fact]
    public async Task Design_OnSubmittedRequest_Returns409StatusAndGeneratorNotCalled()
    {
        var id = await Client.SubmitAsync();
        _host.Basis.Set(id, FakeBasisSource.Basis());

        var response = await Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("conflict", json.GetProperty("code").GetString());
        Assert.Equal(new[] { "Status" }, json.FieldNames());
        Assert.Contains("Submitted", json.GetProperty("errors")[0].GetProperty("message").GetString());
        Assert.Equal(0, _host.Generator.CallCount);
        Assert.Equal(new[] { "Submitted" }, await _host.AuditEventTypesAsync(id));
    }

    [Theory]
    [InlineData("Defer")]
    [InlineData("Reject")]
    public async Task Design_OnDeferredOrRejectedRequest_Returns409Status(string decision)
    {
        var id = await Client.SubmitAsync();
        _host.Basis.Set(id, FakeBasisSource.Basis());
        (await Client.PostJsonAsync(
            $"/change-requests/{id}/decisions",
            new DecisionBody("Sam Sample", decision, "Synthetic reason for the decision.")))
            .EnsureSuccessStatusCode();

        var response = await Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(new[] { "Status" }, (await response.ReadJsonAsync()).FieldNames());
        Assert.Equal(0, _host.Generator.CallCount);
        var history = await (await Client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync();
        Assert.Empty(history.EnumerateArray());
    }

    [Fact]
    public async Task Design_ApprovedWithNoRequirementsDraft_Returns409RequirementsDraftAndNothingStored()
    {
        var id = await _host.ApprovedWithoutBasisAsync();

        var response = await Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("conflict", json.GetProperty("code").GetString());
        Assert.Equal(new[] { "RequirementsDraft" }, json.FieldNames());
        Assert.Equal(0, _host.Generator.CallCount);
        Assert.Equal(new[] { "Submitted", "Approved" }, await _host.AuditEventTypesAsync(id));
    }

    [Fact]
    public async Task Design_RequirementsDraftedOverHttpThenApproved_UsesTheRealAdapterEndToEnd()
    {
        using var host = new DesignHost(useFakeBasis: false);
        var id = await host.Client.SubmitAsync();
        (await host.Client.PostAsync($"/change-requests/{id}/drafts", null)).EnsureSuccessStatusCode();
        (await host.Client.PostAsync($"/change-requests/{id}/drafts", null)).EnsureSuccessStatusCode();
        (await host.Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()))
            .EnsureSuccessStatusCode();

        var response = await host.Client.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal(1, json.GetProperty("version").GetInt32());
        Assert.Equal(2, json.GetProperty("requirementsVersion").GetInt32());
        Assert.Equal(
            new[] { "Submitted", "DraftGenerated", "DraftGenerated", "Approved", "DesignGenerated" },
            await host.AuditEventTypesAsync(id));
    }

    [Fact]
    public async Task Description_ListsTheThreeDesignOperations()
    {
        var json = await (await Client.GetAsync("/openapi/v1.json")).ReadJsonAsync();

        var paths = json.GetProperty("paths");
        Assert.True(paths.GetProperty("/change-requests/{id}/designs").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/change-requests/{id}/designs").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/change-requests/{id}/designs/latest").TryGetProperty("get", out _));
        var text = json.GetRawText();
        Assert.Contains("no-design-draft", text);
        Assert.Contains("design-not-configured", text);
    }
}
