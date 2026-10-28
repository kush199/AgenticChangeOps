using System.Text.Json;
using AgenticChangeOps.Agents.Reviewer;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>Small shared checks for the review route tests.</summary>
public static class ReviewHttp
{
    /// <summary>An answer with one valid finding per reference given.</summary>
    public static string Answer(int findings = 1, string reference = "proposal.changes[1]", string statement = "Synthetic finding.")
    {
        var items = Enumerable.Range(0, findings).Select(i => new
        {
            category = "UnmentionedRisk",
            severity = "Low",
            statement = $"{statement} {i}",
            references = new[] { reference }
        });
        return JsonSerializer.Serialize(new { findings = items, openQuestions = Array.Empty<string>() });
    }

    /// <summary>The JSON element carries exactly the members of the stored review.</summary>
    public static void AssertMatchesStored(JsonElement json, Review stored)
    {
        Assert.Equal(stored.ReviewId, json.GetProperty("reviewId").GetGuid());
        Assert.Equal(stored.ChangeRequestId, json.GetProperty("changeRequestId").GetGuid());
        Assert.Equal(stored.Version, json.GetProperty("version").GetInt32());
        Assert.Equal(stored.RequirementsVersion, json.GetProperty("requirementsVersion").GetInt32());
        Assert.Equal(stored.DesignVersion, json.GetProperty("designVersion").GetInt32());
        Assert.Equal(stored.ProposalVersion, json.GetProperty("proposalVersion").GetInt32());
        Assert.Equal(stored.GeneratedAt, json.GetProperty("generatedAt").GetDateTime());
        Assert.Equal(stored.Status.ToString(), json.GetProperty("status").GetString());
        Assert.Equal(stored.AgentName, json.GetProperty("agentName").GetString());
        Assert.Equal(stored.SystemCheck.Status.ToString(), json.GetProperty("systemCheck").GetProperty("status").GetString());
        Assert.Equal(stored.Findings.Select(f => f.Statement),
            json.GetProperty("findings").EnumerateArray().Select(f => f.GetProperty("statement").GetString()));
        Assert.Equal(stored.OpenQuestions, json.GetProperty("openQuestions").EnumerateArray().Select(q => q.GetString()));
        Assert.Equal(stored.AdvisoryNote, json.GetProperty("advisoryNote").GetString());
    }

    public static async Task<string[]> AuditEventsAsync(ReviewHost host, Guid id)
    {
        var audit = await (await host.Client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();
        return audit.EnumerateArray().Select(e => e.GetProperty("eventType").GetString()!).ToArray();
    }

    public static async Task<string> StatusAsync(ReviewHost host, Guid id) =>
        (await (await host.Client.GetAsync($"/change-requests/{id}")).ReadJsonAsync()).GetProperty("status").GetString()!;

    /// <summary>No review and no ReviewGenerated entry exist for the change request.</summary>
    public static async Task AssertNothingStoredAsync(ReviewHost host, Guid id)
    {
        Assert.Empty(host.Store.GetAgentOutputs<Review>(id));
        Assert.DoesNotContain("ReviewGenerated", await AuditEventsAsync(host, id));
    }

    public static async Task AssertErrorAsync(HttpResponseMessage response, System.Net.HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }
}
