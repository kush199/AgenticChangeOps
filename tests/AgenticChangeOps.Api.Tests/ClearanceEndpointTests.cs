using System.Net;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

public class ClearanceEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public ClearanceEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<bool> IsClearedAsync(Guid id)
    {
        var response = await _client.GetAsync($"/change-requests/{id}/clearance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal(id, json.GetProperty("changeRequestId").GetGuid());
        return json.GetProperty("isCleared").GetBoolean();
    }

    [Fact(Skip = "synthetic")]
    public async Task Submitted_IsNotCleared()
    {
        var id = await _client.SubmitAsync();

        Assert.False(await IsClearedAsync(id));
    }

    [Fact(Skip = "synthetic")]
    public async Task Approved_IsCleared()
    {
        var id = await _client.SubmitAsync();
        await _client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve());

        Assert.True(await IsClearedAsync(id));
    }

    [Fact(Skip = "synthetic")]
    public async Task Rejected_IsNotCleared()
    {
        var id = await _client.SubmitAsync();
        await _client.PostJsonAsync(
            $"/change-requests/{id}/decisions",
            new DecisionBody("Sam Sample", "Reject", "Out of scope for this quarter."));

        Assert.False(await IsClearedAsync(id));
    }

    [Fact(Skip = "synthetic")]
    public async Task Deferred_IsNotCleared()
    {
        var id = await _client.SubmitAsync();
        await _client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Defer());

        Assert.False(await IsClearedAsync(id));
    }

    [Fact(Skip = "synthetic")]
    public async Task UnknownIdentifier_Returns404NotFound()
    {
        var response = await _client.GetAsync("/change-requests/00000000-0000-0000-0000-000000000001/clearance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("not-found", json.GetProperty("code").GetString());
    }

    [Fact(Skip = "synthetic")]
    public async Task InvalidIdentifier_Returns400InvalidIdentifier()
    {
        var response = await _client.GetAsync("/change-requests/not-a-guid/clearance");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("invalid-identifier", json.GetProperty("code").GetString());
    }
}
