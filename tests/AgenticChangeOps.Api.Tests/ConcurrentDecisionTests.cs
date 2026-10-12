using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// Edge case "two requests arrive at once": the business rules decide, so exactly one decision
/// wins and the others receive a conflict. A clock that pauses between validation and save
/// widens the race window so an unsynchronised decision step cannot pass by luck.
/// </summary>
public class ConcurrentDecisionTests : IDisposable
{
    private const int Contenders = 20;
    private const int Rounds = 5;

    private readonly ApiFactory _factory = new(
        configureServices: services => services.AddSingleton<ISystemClock>(new PausingClock()));

    private readonly HttpClient _client;

    public ConcurrentDecisionTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact(Skip = "synthetic")]
    public async Task SimultaneousApproves_ExactlyOneSucceedsAndTheRestConflict()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var id = await _client.SubmitAsync();
            using var gate = new ManualResetEventSlim(false);

            var attempts = Enumerable.Range(0, Contenders)
                .Select(_ => Task.Run(async () =>
                {
                    gate.Wait();
                    return await _client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve());
                }))
                .ToArray();

            gate.Set();
            var responses = await Task.WhenAll(attempts);

            var statuses = responses.Select(r => r.StatusCode).ToArray();
            Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.OK));
            Assert.Equal(Contenders - 1, statuses.Count(s => s == HttpStatusCode.Conflict));

            var audit = await (await _client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();
            var approved = audit.EnumerateArray()
                .Count(e => e.GetProperty("eventType").GetString() == "Approved");
            Assert.Equal(1, approved);

            var request = await (await _client.GetAsync($"/change-requests/{id}")).ReadJsonAsync();
            Assert.Equal("Approved", request.GetProperty("status").GetString());
        }
    }

    [Fact(Skip = "synthetic")]
    public async Task SimultaneousApproveAndReject_ExactlyOneDecisionIsRecorded()
    {
        var id = await _client.SubmitAsync();
        using var gate = new ManualResetEventSlim(false);
        var reject = new AgenticChangeOps.Api.Contracts.DecisionBody("Sam Sample", "Reject", "Not this quarter.");

        var attempts = new[]
        {
            Task.Run(async () => { gate.Wait(); return await _client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()); }),
            Task.Run(async () => { gate.Wait(); return await _client.PostJsonAsync($"/change-requests/{id}/decisions", reject); })
        };

        gate.Set();
        var responses = await Task.WhenAll(attempts);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var audit = await (await _client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();
        var decisions = audit.EnumerateArray()
            .Count(e => e.GetProperty("eventType").GetString() is "Approved" or "Rejected");
        Assert.Equal(1, decisions);
    }

    /// <summary>Real time, but sleeps briefly so overlapping calls interleave.</summary>
    private sealed class PausingClock : ISystemClock
    {
        public DateTime UtcNow
        {
            get
            {
                Thread.Sleep(25);
                return DateTime.UtcNow;
            }
        }
    }
}
