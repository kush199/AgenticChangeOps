using System.Net;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

public class DecisionEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public DecisionEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<JsonElementWithStatus> DecideAsync(Guid id, DecisionBody body)
    {
        var response = await _client.PostJsonAsync($"/change-requests/{id}/decisions", body);
        return new JsonElementWithStatus(response.StatusCode, await response.ReadJsonAsync());
    }

    private async Task<string[]> AuditEventTypesAsync(Guid id)
    {
        var response = await _client.GetAsync($"/change-requests/{id}/audit");
        var json = await response.ReadJsonAsync();
        return json.EnumerateArray().Select(e => e.GetProperty("eventType").GetString()!).ToArray();
    }

    [Fact(Skip = "synthetic")]
    public async Task Approve_ValidDecision_Returns200WithApprovedStatusAndOneAuditEntry()
    {
        var id = await _client.SubmitAsync();

        var result = await DecideAsync(id, SyntheticData.Approve());

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Equal("Approved", result.Json.GetProperty("status").GetString());
        Assert.Equal(id, result.Json.GetProperty("changeRequestId").GetGuid());
        Assert.Equal(new[] { "Submitted", "Approved" }, await AuditEventTypesAsync(id));
    }

    [Fact(Skip = "synthetic")]
    public async Task Defer_WithoutReason_Returns400NamingReasonAndLeavesRequestUnchanged()
    {
        var id = await _client.SubmitAsync();

        var result = await DecideAsync(id, new DecisionBody("Sam Sample", "Defer", null));

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        Assert.Equal("validation-failed", result.Json.GetProperty("code").GetString());
        Assert.Contains("Reason", result.Json.FieldNames());

        var current = await (await _client.GetAsync($"/change-requests/{id}")).ReadJsonAsync();
        Assert.Equal("Submitted", current.GetProperty("status").GetString());
        Assert.Equal(new[] { "Submitted" }, await AuditEventTypesAsync(id));
    }

    [Fact(Skip = "synthetic")]
    public async Task Decision_UnknownDecisionValue_Returns400NamingDecision()
    {
        var id = await _client.SubmitAsync();

        var result = await DecideAsync(id, new DecisionBody("Sam Sample", "Maybe", null));

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        Assert.Equal("validation-failed", result.Json.GetProperty("code").GetString());
        Assert.Contains("Decision", result.Json.FieldNames());
    }

    [Fact(Skip = "synthetic")]
    public async Task Reject_OnFinalRequestWithoutReason_Returns400ListingReasonAndStatus()
    {
        var id = await _client.SubmitAsync();
        await DecideAsync(id, SyntheticData.Approve());

        var result = await DecideAsync(id, new DecisionBody("Sam Sample", "Reject", null));

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        Assert.Equal("validation-failed", result.Json.GetProperty("code").GetString());
        var fields = result.Json.FieldNames();
        Assert.Contains("Reason", fields);
        Assert.Contains("Status", fields);
    }

    [Fact(Skip = "synthetic")]
    public async Task Approve_OnAlreadyApprovedRequest_Returns409WithSpecificReason()
    {
        var id = await _client.SubmitAsync();
        await DecideAsync(id, SyntheticData.Approve());

        var result = await DecideAsync(id, SyntheticData.Approve());

        Assert.Equal(HttpStatusCode.Conflict, result.Status);
        Assert.Equal("conflict", result.Json.GetProperty("code").GetString());
        var error = result.Json.GetProperty("errors")[0];
        Assert.Equal("Status", error.GetProperty("field").GetString());
        Assert.Contains("final state", error.GetProperty("message").GetString());
        Assert.Equal(new[] { "Submitted", "Approved" }, await AuditEventTypesAsync(id));
    }

    [Fact(Skip = "synthetic")]
    public async Task Decision_UnknownIdentifier_Returns404NotFound()
    {
        var result = await DecideAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"), SyntheticData.Approve());

        Assert.Equal(HttpStatusCode.NotFound, result.Status);
        Assert.Equal("not-found", result.Json.GetProperty("code").GetString());
    }

    [Fact(Skip = "synthetic")]
    public async Task Decision_InvalidIdentifier_Returns400InvalidIdentifier()
    {
        var response = await _client.PostJsonAsync("/change-requests/not-a-guid/decisions", SyntheticData.Approve());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("invalid-identifier", json.GetProperty("code").GetString());
    }

    [Fact(Skip = "synthetic")]
    public async Task Decision_MalformedBody_Returns400InvalidBody()
    {
        var id = await _client.SubmitAsync();

        var response = await _client.PostRawAsync($"/change-requests/{id}/decisions", "{");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("invalid-body", json.GetProperty("code").GetString());
    }

    [Fact(Skip = "synthetic")]
    public async Task Decision_IdentifierInBody_IsIgnoredInFavourOfTheRoute()
    {
        var routeId = await _client.SubmitAsync();
        var otherId = await _client.SubmitAsync();
        var raw = $"{{\"changeRequestId\":\"{otherId}\",\"approverName\":\"Sam Sample\",\"decision\":\"Approve\"}}";

        var response = await _client.PostRawAsync($"/change-requests/{routeId}/decisions", raw);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { "Submitted", "Approved" }, await AuditEventTypesAsync(routeId));
        Assert.Equal(new[] { "Submitted" }, await AuditEventTypesAsync(otherId));
    }

    private sealed record JsonElementWithStatus(HttpStatusCode Status, System.Text.Json.JsonElement Json);
}
