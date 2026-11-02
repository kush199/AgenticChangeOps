using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;

namespace AgenticChangeOps.Api.Tests;

/// <summary>A decision is a recorded human action: it moves nothing else.</summary>
public sealed class DesignDecisionIsolationTests : IDisposable
{
    private readonly DesignHost _host = new();

    public void Dispose() => _host.Dispose();

    private async Task<string[]> SnapshotAsync(Guid id)
    {
        var paths = new[]
        {
            $"/change-requests/{id}",
            $"/change-requests/{id}/workflow",
            $"/change-requests/{id}/clearance",
            $"/change-requests/{id}/drafts",
            $"/change-requests/{id}/proposals",
            $"/change-requests/{id}/reviews"
        };
        var snapshot = new List<string>();
        foreach (var path in paths)
        {
            var response = await _host.Client.GetAsync(path);
            snapshot.Add((int)response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        }

        var advance = await _host.Client.PostAsync($"/change-requests/{id}/workflow/advance", null);
        snapshot.Add("advance " + (int)advance.StatusCode + " " + await advance.Content.ReadAsStringAsync());
        return snapshot.ToArray();
    }

    [Theory]
    [InlineData("Accept", null)]
    [InlineData("Reject", "Not convinced.")]
    public async Task WorkflowStatusProposalsReviewsClearanceAndAdvance_AreIdenticalBeforeAndAfter(string decision, string? reason)
    {
        var id = await _host.ApprovedWithDesignsAsync();
        await SnapshotAsync(id); // the first advance may run a pending step; settle before comparing
        var before = await SnapshotAsync(id);

        (await _host.DecideAsync(id, 1, decision, reason: reason)).EnsureSuccessStatusCode();

        Assert.Equal(before, await SnapshotAsync(id));
    }

    [Fact]
    public async Task ARejection_DoesNotMakeAnythingRequestANewDesign()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        var generatorCalls = _host.Generator.CallCount;

        (await _host.DecideAsync(id, 1, "Reject", reason: "No.")).EnsureSuccessStatusCode();
        await _host.Client.PostAsync($"/change-requests/{id}/workflow/advance", null);

        Assert.Equal(generatorCalls, _host.Generator.CallCount);
        var history = await (await _host.Client.GetAsync($"/change-requests/{id}/designs")).ReadJsonAsync();
        Assert.Equal(1, history.GetArrayLength());
    }

    [Fact]
    public async Task ARefusedDecision_ChangesNothingEither()
    {
        var id = await _host.ApprovedWithDesignsAsync();
        await SnapshotAsync(id); // the first advance may run a pending step; settle before comparing
        var before = await SnapshotAsync(id);

        var response = await _host.DecideAsync(id, 3, "Accept");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await SnapshotAsync(id));
    }
}
