using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

public sealed class DesignDecisionRetrievalTests : IDisposable
{
    private readonly DesignHost _host = new();

    public void Dispose() => _host.Dispose();

    private HttpClient Client => _host.Client;

    [Fact]
    public async Task Latest_ShowsTheDecision_OnlyOnceOneExists()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        var before = await (await Client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();
        Assert.False(before.TryGetProperty("decision", out _));

        await _host.DecideAsync(id, 1, "Reject", name: "Jordan Reed", reason: "Needs a rollback plan.");

        var after = await (await Client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();
        var decision = after.GetProperty("decision");
        Assert.Equal("Rejected", decision.GetProperty("outcome").GetString());
        Assert.Equal("Jordan Reed", decision.GetProperty("approverName").GetString());
        Assert.Equal("Needs a rollback plan.", decision.GetProperty("reason").GetString());
        Assert.True(decision.TryGetProperty("decidedAt", out _));
    }

    [Fact]
    public async Task AnAcceptedDecisionWithNoReason_OmitsTheReason()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        await _host.DecideAsync(id, 1, "Accept");

        var json = await (await Client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();

        var decision = json.GetProperty("decision");
        Assert.Equal("Accepted", decision.GetProperty("outcome").GetString());
        Assert.True(!decision.TryGetProperty("reason", out var reason) || reason.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task History_ShowsTheDecisionOnDecidedVersionsOnly_InAscendingOrder()
    {
        var id = await _host.ApprovedWithDesignsAsync(designs: 2);
        await _host.DecideAsync(id, 2, "Accept");
        (await Client.PostAsync($"/change-requests/{id}/designs", null)).EnsureSuccessStatusCode();

        var history = (await (await Client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync())
            .EnumerateArray().ToArray();

        Assert.Equal(new[] { 1, 2, 3 }, history.Select(d => d.GetProperty("version").GetInt32()));
        Assert.False(history[0].TryGetProperty("decision", out _));
        Assert.Equal("Accepted", history[1].GetProperty("decision").GetProperty("outcome").GetString());
        Assert.False(history[2].TryGetProperty("decision", out _));
    }

    [Fact]
    public async Task ADesignWithNoDecision_SerializesExactlyAsTheStoredDraftDoes()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        var store = _host.Services.GetRequiredService<IChangeRequestStore>();
        var draft = store.GetAgentOutputs<DesignDraft>(id).Single();

        var served = await (await Client.GetAsync($"/change-requests/{id}/designs/latest")).Content.ReadAsStringAsync();

        Assert.Equal(Serialize(draft), served);
        Assert.Equal(Serialize(DesignView.From(draft, null)), served);
        var list = await (await Client.GetAsync($"/change-requests/{id}/designs")).Content.ReadAsStringAsync();
        Assert.Equal("[" + Serialize(draft) + "]", list);
    }

    [Fact]
    public async Task EveryDesignMember_StaysTheSame_WhenADecisionIsAdded()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        var before = await (await Client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();

        await _host.DecideAsync(id, 1, "Accept");

        var after = await (await Client.GetAsync($"/change-requests/{id}/designs/latest")).ReadJsonAsync();
        foreach (var member in before.EnumerateObject())
            Assert.Equal(member.Value.GetRawText(), after.GetProperty(member.Name).GetRawText());
        Assert.Equal(before.EnumerateObject().Count() + 1, after.EnumerateObject().Count());
    }

    [Fact]
    public async Task RetrievalErrors_AreUnchanged()
    {
        var unknown = Guid.NewGuid();

        var latest = await Client.GetAsync($"/change-requests/{unknown}/designs/latest");
        var history = await Client.GetAsync($"/change-requests/{unknown}/designs");
        var invalid = await Client.GetAsync("/change-requests/not-a-guid/designs/latest");
        var id = await _host.ApprovedWithBasisAsync();
        var none = await Client.GetAsync($"/change-requests/{id}/designs/latest");

        Assert.Equal(HttpStatusCode.NotFound, latest.StatusCode);
        Assert.Equal("not-found", await latest.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
        Assert.Equal("no-design-draft", await none.ErrorCodeAsync());
    }

    private static string Serialize<T>(T value)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(value, options);
    }
}
