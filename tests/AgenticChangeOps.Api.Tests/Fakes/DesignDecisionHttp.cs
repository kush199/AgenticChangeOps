namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>Calls for the design decision tests, on top of <see cref="DesignHost"/>.</summary>
public static class DesignDecisionHttp
{
    public static Task<HttpResponseMessage> DecideAsync(
        this DesignHost host, Guid id, string version, object body) =>
        host.Client.PostJsonAsync($"/change-requests/{id}/designs/{version}/decisions", body);

    public static Task<HttpResponseMessage> DecideAsync(
        this DesignHost host, Guid id, int version, string decision, string name = "Jordan Reed", string? reason = null) =>
        host.DecideAsync(id, version.ToString(), Body(decision, name, reason));

    public static object Body(string decision, string name = "Jordan Reed", string? reason = null) =>
        new { approverName = name, decision, reason };

    /// <summary>Approves a request with a requirements draft and drafts the given number of designs over HTTP.</summary>
    public static async Task<Guid> ApprovedWithDesignsAsync(this DesignHost host, int designs = 1, int basisVersion = 1)
    {
        var id = await host.ApprovedWithBasisAsync(basisVersion);
        for (var i = 0; i < designs; i++)
            (await host.Client.PostAsync($"/change-requests/{id}/designs", null)).EnsureSuccessStatusCode();
        return id;
    }

    public static async Task<string> ErrorCodeAsync(this HttpResponseMessage response) =>
        (await response.ReadJsonAsync()).GetProperty("code").GetString()!;
}
