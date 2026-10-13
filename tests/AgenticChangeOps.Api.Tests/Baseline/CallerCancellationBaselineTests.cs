using System.Text;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests.Baseline;

/// <summary>
/// Convergence test for slice 009. Records what a caller sees when it abandons a request while
/// the model is still answering, for both real generators over HTTP, and what is left behind
/// (nothing stored, no audit entry). The golden was captured from the code as it stood before
/// the shared plumbing was extracted (commit a49108f) and is never rewritten from later code.
/// Rewrite only with <see cref="ApiBaselineTests.UpdateVariable"/> = 1 on reviewed, intended
/// change. Log text is scanned for the sentinels but is not part of the golden.
/// </summary>
public class CallerCancellationBaselineTests
{
    private const string SentinelEndpoint = "https://sentinel-endpoint.invalid/";
    private const string SentinelHost = "sentinel-endpoint.invalid";
    private const string SentinelDeployment = "sentinel-deployment";

    [Fact]
    public async Task CallerCancellation_LeavesTheRecordedOutcome_AndNothingStored()
    {
        var outcome = new StringBuilder();
        var log = new TranscriptBuilder();
        var seen = new List<string>();

        await RecordAsync("Requirement", "drafts", outcome, log, seen);
        await RecordAsync("Architect", "designs", outcome, log, seen);

        GoldenFile.Verify(
            "caller-cancellation-transcript.txt", (outcome + log.ToString()).Replace("\r\n", "\n"));

        foreach (var text in seen)
        {
            Assert.DoesNotContain(SentinelEndpoint, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SentinelHost, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SentinelDeployment, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task RecordAsync(
        string agent, string route, StringBuilder outcome, TranscriptBuilder log, List<string> seen)
    {
        var options = new AzureOpenAIGeneratorOptions(
            new Uri(SentinelEndpoint), SentinelDeployment, TimeSpan.FromSeconds(30), 32000);
        var model = new ScriptedChatModelClient { Behavior = ScriptedChatModelClient.DelaysUntilCancelled() };
        var basis = new FakeBasisSource();

        using var factory = agent == "Requirement"
            ? new ApiFactory(new AzureOpenAIDraftGenerator(options, model), draftingConfigured: true)
            : new ApiFactory(
                FakeDraftGenerator.ReturnsSyntheticContent(),
                draftingConfigured: true,
                configureServices: services =>
                {
                    services.AddSingleton<IDesignDraftGenerator>(new AzureOpenAIDesignGenerator(options, model));
                    services.AddSingleton(new DesignAvailability(true));
                    services.AddSingleton<IRequirementsBasisSource>(basis);
                });
        using var client = factory.CreateClient();

        var id = await client.SubmitAsync();
        if (agent == "Architect")
        {
            basis.Set(id, FakeBasisSource.Basis());
            (await client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()))
                .EnsureSuccessStatusCode();
        }

        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        try
        {
            var response = await client.PostAsync($"/change-requests/{id}/{route}", null, cancel.Token);
            seen.Add(await response.Content.ReadAsStringAsync());
            outcome.Append(agent).Append(" caller cancels mid-call: response ").AppendLine(((int)response.StatusCode).ToString());
        }
        catch (OperationCanceledException)
        {
            outcome.Append(agent).AppendLine(" caller cancels mid-call: client cancelled, no response");
        }

        outcome.AppendLine();

        await Observe(log, seen, $"{agent} latest after cancellation", await client.GetAsync($"/change-requests/{id}/{route}/latest"));
        await Observe(log, seen, $"{agent} audit after cancellation", await client.GetAsync($"/change-requests/{id}/audit"));

        seen.AddRange(factory.Logs.Lines);
    }

    private static async Task Observe(TranscriptBuilder log, List<string> seen, string label, HttpResponseMessage response)
    {
        seen.Add(await response.Content.ReadAsStringAsync());
        await log.RecordAsync(label, response);
    }
}
