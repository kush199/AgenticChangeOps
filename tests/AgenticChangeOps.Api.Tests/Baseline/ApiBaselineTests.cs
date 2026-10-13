using System.Net;
using System.Text;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Requirement;

namespace AgenticChangeOps.Api.Tests.Baseline;

/// <summary>
/// Characterization test for slice 007 (organise agent code by agent). Drives every HTTP
/// operation over its success and error paths and compares status code, headers, normalised
/// body and error code with golden files captured before any code moved, plus the served API
/// description. Goldens live in <c>Baseline/golden</c>; set
/// <see cref="UpdateVariable"/> to 1 to rewrite them (only ever on reviewed, intended change).
/// </summary>
public class ApiBaselineTests
{
    public const string UpdateVariable = "AGENTICCHANGEOPS_UPDATE_BASELINE";

    private static readonly Dictionary<string, string?> ConfiguredSettings = new()
    {
        [AzureOpenAIGeneratorOptions.EndpointVariable] = "https://sentinel-endpoint.invalid/",
        [AzureOpenAIGeneratorOptions.DeploymentVariable] = "sentinel-deployment"
    };

    [Fact]
    public async Task EveryOperation_ReturnsTheRecordedStatusHeadersBodyAndErrorCode()
    {
        var log = new TranscriptBuilder();

        await RecordMainFlowAsync(log);
        await RecordGenerationFailuresAsync(log);
        await RecordNotConfiguredAsync(log);

        GoldenFile.Verify("transcript.txt", log.ToString());
    }

    [Fact]
    public async Task ApiDescription_IsTheRecordedOne()
    {
        using var factory = new ApiFactory(settings: new Dictionary<string, string?>(ConfiguredSettings));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        GoldenFile.Verify("openapi.json", text.Replace("\r\n", "\n"));
    }

    private static async Task RecordMainFlowAsync(TranscriptBuilder log)
    {
        using var factory = new ApiFactory(FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true);
        using var client = factory.CreateClient();

        // Intake
        var submitted = await client.PostJsonAsync("/change-requests", SyntheticData.ValidSubmission());
        var id = (await log.RecordAsync("submit valid", submitted)).GetProperty("changeRequestId").GetGuid();

        await log.RecordAsync("submit not json", await client.PostRawAsync("/change-requests", "not json"));
        await log.RecordAsync("submit empty object", await client.PostRawAsync("/change-requests", "{}"));
        await log.RecordAsync("submit bad enum", await client.PostJsonAsync("/change-requests", new
        {
            title = "t", description = "d", requester = "r", affectedSystem = "s",
            priority = "Nope", changeType = "ConfigChange"
        }));
        await log.RecordAsync("submit non-json media type", await client.PostAsync(
            "/change-requests", new StringContent("title=x", Encoding.UTF8, "text/plain")));
        await log.RecordAsync("submit oversized body", await client.PostRawAsync(
            "/change-requests", $"{{\"title\":\"{new string('x', 70 * 1024)}\"}}"));

        await log.RecordAsync("get change request", await client.GetAsync($"/change-requests/{id}"));
        await log.RecordAsync("get unknown change request", await client.GetAsync($"/change-requests/{Guid.NewGuid()}"));
        await log.RecordAsync("get invalid identifier", await client.GetAsync("/change-requests/not-a-guid"));
        await log.RecordAsync("get audit", await client.GetAsync($"/change-requests/{id}/audit"));
        await log.RecordAsync("get clearance before decision", await client.GetAsync($"/change-requests/{id}/clearance"));

        // Drafting
        await log.RecordAsync("latest draft before any", await client.GetAsync($"/change-requests/{id}/drafts/latest"));
        await log.RecordAsync("draft history before any", await client.GetAsync($"/change-requests/{id}/drafts"));
        await log.RecordAsync("draft first", await client.PostAsync($"/change-requests/{id}/drafts", null));
        await log.RecordAsync("draft second", await client.PostAsync($"/change-requests/{id}/drafts", null));
        await log.RecordAsync("latest draft", await client.GetAsync($"/change-requests/{id}/drafts/latest"));
        await log.RecordAsync("draft history", await client.GetAsync($"/change-requests/{id}/drafts"));
        await log.RecordAsync("audit after drafting", await client.GetAsync($"/change-requests/{id}/audit"));
        await log.RecordAsync("draft unknown change request",
            await client.PostAsync($"/change-requests/{Guid.NewGuid()}/drafts", null));
        await log.RecordAsync("draft invalid identifier", await client.PostAsync("/change-requests/not-a-guid/drafts", null));
        await log.RecordAsync("latest draft unknown change request",
            await client.GetAsync($"/change-requests/{Guid.NewGuid()}/drafts/latest"));
        await log.RecordAsync("draft history unknown change request",
            await client.GetAsync($"/change-requests/{Guid.NewGuid()}/drafts"));

        // Approval
        await log.RecordAsync("decision invalid body",
            await client.PostJsonAsync($"/change-requests/{id}/decisions", new { approverName = "", decision = "Maybe" }));
        await log.RecordAsync("decision unknown change request",
            await client.PostJsonAsync($"/change-requests/{Guid.NewGuid()}/decisions", SyntheticData.Approve()));
        await log.RecordAsync("decision approve",
            await client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()));
        await log.RecordAsync("decision again after approval",
            await client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Defer()));
        await log.RecordAsync("draft after approval", await client.PostAsync($"/change-requests/{id}/drafts", null));
        await log.RecordAsync("get clearance after approval", await client.GetAsync($"/change-requests/{id}/clearance"));
        await log.RecordAsync("audit after approval", await client.GetAsync($"/change-requests/{id}/audit"));

        // Deferred request can still be drafted
        var deferredId = await client.SubmitAsync();
        await log.RecordAsync("decision defer",
            await client.PostJsonAsync($"/change-requests/{deferredId}/decisions", SyntheticData.Defer()));
        await log.RecordAsync("draft after defer", await client.PostAsync($"/change-requests/{deferredId}/drafts", null));

        // Edges
        await log.RecordAsync("unknown route", await client.GetAsync("/nope"));
        await log.RecordAsync("unsupported method", await client.PutAsync(
            "/change-requests", new StringContent("{}", Encoding.UTF8, "application/json")));

        using var foreignHost = new HttpRequestMessage(HttpMethod.Get, "/change-requests/not-a-guid");
        foreignHost.Headers.Host = "evil.example";
        await log.RecordAsync("foreign host header", await client.SendAsync(foreignHost));
    }

    private static async Task RecordGenerationFailuresAsync(TranscriptBuilder log)
    {
        foreach (var category in Enum.GetValues<GenerationFailureCategory>())
        {
            var generator = FakeDraftGenerator.ThrowsCategory(category);
            using var factory = new ApiFactory(generator, draftingConfigured: true);
            using var client = factory.CreateClient();
            var id = await client.SubmitAsync();

            await log.RecordAsync($"generator failure {category}",
                await client.PostAsync($"/change-requests/{id}/drafts", null));
            await log.RecordAsync($"audit after failure {category}", await client.GetAsync($"/change-requests/{id}/audit"));
        }

        await RecordOneAsync(log, "generator returns null",
            FakeDraftGenerator.ReturnsContent(null));
        await RecordOneAsync(log, "generator returns blank summary",
            FakeDraftGenerator.ReturnsContent(
                new DraftContent("   ", new[] { "a" }, new[] { "b" }, new[] { "c" }, new[] { "d" })));
        await RecordOneAsync(log, "generator returns missing section",
            FakeDraftGenerator.ReturnsContent(
                new DraftContent("S", null, new[] { "b" }, new[] { "c" }, new[] { "d" })));
        await RecordOneAsync(log, "generator throws arbitrary exception",
            FakeDraftGenerator.ThrowsArbitrary(new InvalidOperationException("synthetic failure")));
    }

    private static async Task RecordOneAsync(TranscriptBuilder log, string label, FakeDraftGenerator generator)
    {
        using var factory = new ApiFactory(generator, draftingConfigured: true);
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();

        await log.RecordAsync(label, await client.PostAsync($"/change-requests/{id}/drafts", null));
    }

    private static async Task RecordNotConfiguredAsync(TranscriptBuilder log)
    {
        using var factory = new ApiFactory(FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: false);
        using var client = factory.CreateClient();
        var id = await client.SubmitAsync();

        await log.RecordAsync("not configured draft", await client.PostAsync($"/change-requests/{id}/drafts", null));
        await log.RecordAsync("not configured invalid identifier",
            await client.PostAsync("/change-requests/not-a-guid/drafts", null));
        await log.RecordAsync("not configured latest draft",
            await client.GetAsync($"/change-requests/{id}/drafts/latest"));
        await log.RecordAsync("not configured draft history", await client.GetAsync($"/change-requests/{id}/drafts"));
    }
}
