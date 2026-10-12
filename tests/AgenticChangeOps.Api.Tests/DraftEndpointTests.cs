using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

public class DraftEndpointTests : IDisposable
{
    private readonly FakeDraftGenerator _generator = FakeDraftGenerator.ReturnsSyntheticContent();
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public DraftEndpointTests()
    {
        _factory = new ApiFactory(_generator, draftingConfigured: true);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<string[]> AuditEventTypesAsync(Guid id)
    {
        var json = await (await _client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();
        return json.EnumerateArray().Select(e => e.GetProperty("eventType").GetString()!).ToArray();
    }

    [Fact]
    public async Task Draft_Draftable_Returns201WithLocationVersionOneAndOneAuditEntry()
    {
        var id = await _client.SubmitAsync();

        var response = await _client.PostAsync($"/change-requests/{id}/drafts", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/change-requests/{id}/drafts/latest", response.Headers.Location?.OriginalString);
        var json = await response.ReadJsonAsync();
        Assert.Equal(1, json.GetProperty("version").GetInt32());
        Assert.Equal(id, json.GetProperty("changeRequestId").GetGuid());
        Assert.Equal("Draft", json.GetProperty("status").GetString());
        Assert.Equal(new[] { "Submitted", "DraftGenerated" }, await AuditEventTypesAsync(id));
    }

    [Fact]
    public async Task Draft_Twice_ProducesVersionTwo()
    {
        var id = await _client.SubmitAsync();
        await _client.PostAsync($"/change-requests/{id}/drafts", null);

        var response = await _client.PostAsync($"/change-requests/{id}/drafts", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal(2, json.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Latest_ReturnsHighestVersion_AndHistoryReturnsAllAscending()
    {
        var id = await _client.SubmitAsync();
        await _client.PostAsync($"/change-requests/{id}/drafts", null);
        await _client.PostAsync($"/change-requests/{id}/drafts", null);

        var latest = await _client.GetAsync($"/change-requests/{id}/drafts/latest");
        var history = await _client.GetAsync($"/change-requests/{id}/drafts");

        Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
        Assert.Equal(2, (await latest.ReadJsonAsync()).GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var versions = (await history.ReadJsonAsync()).EnumerateArray()
            .Select(d => d.GetProperty("version").GetInt32()).ToArray();
        Assert.Equal(new[] { 1, 2 }, versions);
    }

    [Fact]
    public async Task NoDrafts_LatestIs404NoDraft_AndHistoryIsEmptyArray()
    {
        var id = await _client.SubmitAsync();

        var latest = await _client.GetAsync($"/change-requests/{id}/drafts/latest");
        var history = await _client.GetAsync($"/change-requests/{id}/drafts");

        Assert.Equal(HttpStatusCode.NotFound, latest.StatusCode);
        Assert.Equal("no-draft", (await latest.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Empty((await history.ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task UnknownIdentifier_Returns404NotFoundOnAllThreeRoutes()
    {
        const string id = "00000000-0000-0000-0000-000000000001";

        var responses = new[]
        {
            await _client.PostAsync($"/change-requests/{id}/drafts", null),
            await _client.GetAsync($"/change-requests/{id}/drafts/latest"),
            await _client.GetAsync($"/change-requests/{id}/drafts")
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("not-found", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        }

        Assert.Equal(0, _generator.CallCount);
    }

    [Fact]
    public async Task InvalidIdentifier_Returns400InvalidIdentifierOnAllThreeRoutes()
    {
        var responses = new[]
        {
            await _client.PostAsync("/change-requests/not-a-guid/drafts", null),
            await _client.GetAsync("/change-requests/not-a-guid/drafts/latest"),
            await _client.GetAsync("/change-requests/not-a-guid/drafts")
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid-identifier", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Draft_OnApprovedRequest_Returns409WithSpecificReasonAndGeneratorNotCalled()
    {
        var id = await _client.SubmitAsync();
        await _client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve());

        var response = await _client.PostAsync($"/change-requests/{id}/drafts", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("conflict", json.GetProperty("code").GetString());
        var error = json.GetProperty("errors")[0];
        Assert.Equal("Status", error.GetProperty("field").GetString());
        Assert.Contains("cannot be drafted", error.GetProperty("message").GetString());
        Assert.Equal(0, _generator.CallCount);
        Assert.Equal(new[] { "Submitted", "Approved" }, await AuditEventTypesAsync(id));
    }
}
