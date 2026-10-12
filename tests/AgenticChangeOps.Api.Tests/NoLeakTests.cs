using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.AzureOpenAI;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// Drives every route, including each failure class, with sentinel model settings in place and
/// a generator that tries to leak them, then scans everything an observer could see.
/// </summary>
public class NoLeakTests
{
    private const string SentinelEndpoint = "https://sentinel-endpoint.invalid/";
    private const string SentinelHost = "sentinel-endpoint.invalid";
    private const string SentinelDeployment = "sentinel-deployment";
    private const string GeneratorText = "sentinel-model-output-5d1e";

    private static readonly string[] Sentinels =
    {
        SentinelEndpoint, SentinelHost, SentinelDeployment, GeneratorText, "Bearer ", "api-key"
    };

    private static Dictionary<string, string?> SentinelSettings() => new()
    {
        [AzureOpenAIGeneratorOptions.EndpointVariable] = SentinelEndpoint,
        [AzureOpenAIGeneratorOptions.DeploymentVariable] = SentinelDeployment
    };

    private static async Task Observe(HttpResponseMessage response, List<string> seen)
    {
        seen.Add(await response.Content.ReadAsStringAsync());
        foreach (var header in response.Headers)
            seen.Add($"{header.Key}: {string.Join(",", header.Value)}");
        foreach (var header in response.Content.Headers)
            seen.Add($"{header.Key}: {string.Join(",", header.Value)}");
    }

    private static async Task DriveAllRoutesAsync(HttpClient client, List<string> seen)
    {
        var id = await client.SubmitAsync();
        var missing = "00000000-0000-0000-0000-000000000001";

        await Observe(await client.GetAsync($"/change-requests/{id}"), seen);
        await Observe(await client.GetAsync($"/change-requests/{id}/audit"), seen);
        await Observe(await client.GetAsync($"/change-requests/{id}/clearance"), seen);
        await Observe(await client.PostAsync($"/change-requests/{id}/drafts", null), seen);
        await Observe(await client.GetAsync($"/change-requests/{id}/drafts/latest"), seen);
        await Observe(await client.GetAsync($"/change-requests/{id}/drafts"), seen);
        await Observe(await client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Defer()), seen);
        await Observe(await client.GetAsync($"/change-requests/{missing}"), seen);
        await Observe(await client.PostAsync($"/change-requests/{missing}/drafts", null), seen);
        await Observe(await client.GetAsync("/change-requests/not-a-guid"), seen);
        await Observe(await client.PostRawAsync("/change-requests", "{"), seen);
        await Observe(await client.GetAsync("/nope"), seen);
        await Observe(await client.GetAsync("/openapi/v1.json"), seen);
    }

    private static void AssertNoSentinel(IEnumerable<string> seen)
    {
        foreach (var text in seen)
            foreach (var sentinel in Sentinels)
                Assert.DoesNotContain(sentinel, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CategoryFailureAndSuccessPaths_LeakNothing()
    {
        var seen = new List<string>();
        var generators = new[]
        {
            FakeDraftGenerator.ReturnsSyntheticContent(),
            FakeDraftGenerator.ThrowsCategory(GenerationFailureCategory.Unreachable),
            FakeDraftGenerator.ThrowsCategory(GenerationFailureCategory.MalformedOutput)
        };

        foreach (var generator in generators)
        {
            using var factory = new ApiFactory(generator, settings: SentinelSettings());
            using var client = factory.CreateClient();
            await DriveAllRoutesAsync(client, seen);
            seen.AddRange(factory.Logs.Lines);
        }

        AssertNoSentinel(seen);
    }

    [Fact]
    public async Task ErrorResponses_AreLoggedWithRouteTemplateStatusAndCodeOnly()
    {
        using var factory = new ApiFactory(
            FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true, settings: SentinelSettings());
        using var client = factory.CreateClient();

        await client.GetAsync("/change-requests/not-a-guid");
        var raw = "{\"title\":\"" + GeneratorText + "\"}";
        await client.PostRawAsync("/change-requests", raw);

        var lines = factory.Logs.Lines;
        var invalidId = Assert.Single(lines, l => l.Contains("invalid-identifier"));
        Assert.Contains("/change-requests/{id}", invalidId);
        Assert.Contains("400", invalidId);
        var validation = Assert.Single(lines, l => l.Contains("validation-failed"));
        Assert.Contains("/change-requests", validation);

        // Only the template, status and code: never the raw path value, a body, or a setting.
        Assert.DoesNotContain(lines, l => l.Contains("not-a-guid"));
        Assert.DoesNotContain(lines, l => l.Contains(GeneratorText));
        AssertNoSentinel(lines);
    }

    [Fact]
    public async Task GeneratorThatEmbedsEndpointAndDeploymentInItsError_LeaksNothing()
    {
        var message = $"call to {SentinelEndpoint} deployment {SentinelDeployment} said {GeneratorText} Bearer abc api-key";
        var generator = FakeDraftGenerator.ThrowsArbitrary(new HttpRequestException(message));
        var seen = new List<string>();

        using var factory = new ApiFactory(generator, settings: SentinelSettings());
        using var client = factory.CreateClient();
        await DriveAllRoutesAsync(client, seen);
        seen.AddRange(factory.Logs.Lines);

        AssertNoSentinel(seen);
        var draft = await client.PostAsync($"/change-requests/{await client.SubmitAsync()}/drafts", null);
        Assert.Equal(HttpStatusCode.InternalServerError, draft.StatusCode);
    }

    [Fact]
    public async Task NotConfiguredWithSentinelNames_LeaksNothing()
    {
        var seen = new List<string>();

        using var factory = new ApiFactory(draftingConfigured: false, settings: SentinelSettings());
        using var client = factory.CreateClient();
        await DriveAllRoutesAsync(client, seen);
        seen.AddRange(factory.Logs.Lines);

        AssertNoSentinel(seen);
    }
}
