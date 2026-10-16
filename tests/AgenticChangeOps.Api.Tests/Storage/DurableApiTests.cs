using System.Text.RegularExpressions;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests.Storage;

/// <summary>
/// SC-003 / US1 / US2: over HTTP, the durable store gives the same status codes, bodies and
/// headers as the in-memory store, and the same answers after the host is rebuilt.
/// </summary>
public sealed class DurableApiTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "changeops-api-tests-" + Guid.NewGuid().ToString("N"));

    public DurableApiTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static readonly Regex Guids = new(
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);

    private static readonly Regex Timestamps = new(
        @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?", RegexOptions.Compiled);

    private static ApiFactory NewFactory(string provider, string? path) =>
        new(
            FakeDraftGenerator.ReturnsSyntheticContent(),
            draftingConfigured: true,
            settings: new Dictionary<string, string?>
            {
                [ApiFactory.StorageProviderKey] = provider,
                [ApiFactory.StoragePathKey] = path
            },
            configureServices: services =>
            {
                services.AddSingleton<IDesignDraftGenerator>(FakeDesignGenerator.ReturnsSyntheticContent());
                services.AddSingleton(new DesignAvailability(true));
            });

    private sealed record Seen(string Label, int Status, string? ContentType, string? Location, string Body);

    private static async Task<Seen> Get(HttpClient client, string label, string path)
    {
        var response = await client.GetAsync(path);
        return new Seen(
            label,
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.ToString(),
            response.Headers.Location?.OriginalString,
            await response.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> BuildHistory(HttpClient client)
    {
        var id = await client.SubmitAsync();
        // Requirements are drafted before approval; designs need an Approved request.
        (await client.PostAsync($"/change-requests/{id}/drafts", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/change-requests/{id}/drafts", null)).EnsureSuccessStatusCode();
        (await client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()))
            .EnsureSuccessStatusCode();
        (await client.PostAsync($"/change-requests/{id}/designs", null)).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<List<Seen>> ReadEverything(HttpClient client, Guid id)
    {
        var unknown = "00000000-0000-0000-0000-000000000001";
        return new List<Seen>
        {
            await Get(client, "request", $"/change-requests/{id}"),
            await Get(client, "audit", $"/change-requests/{id}/audit"),
            await Get(client, "clearance", $"/change-requests/{id}/clearance"),
            await Get(client, "draft-latest", $"/change-requests/{id}/drafts/latest"),
            await Get(client, "drafts", $"/change-requests/{id}/drafts"),
            await Get(client, "design-latest", $"/change-requests/{id}/designs/latest"),
            await Get(client, "designs", $"/change-requests/{id}/designs"),
            await Get(client, "workflow", $"/change-requests/{id}/workflow"),
            await Get(client, "unknown", $"/change-requests/{unknown}"),
            await Get(client, "unknown-audit", $"/change-requests/{unknown}/audit")
        };
    }

    [Fact]
    public async Task EverythingRecorded_IsReturnedIdentically_AfterTheHostIsRebuilt()
    {
        var path = Path.Combine(_folder, "restart.db");
        Guid id;
        List<Seen> before;
        using (var factory = NewFactory("Sqlite", path))
        using (var client = factory.CreateClient())
        {
            id = await BuildHistory(client);
            before = await ReadEverything(client, id);
        }

        using var rebuilt = NewFactory("Sqlite", path);
        using var rebuiltClient = rebuilt.CreateClient();
        var after = await ReadEverything(rebuiltClient, id);

        Assert.Equal(before, after);
        Assert.Equal(200, after.Single(s => s.Label == "request").Status);
        Assert.Equal(2, System.Text.Json.JsonDocument.Parse(after.Single(s => s.Label == "drafts").Body)
            .RootElement.GetArrayLength());
    }

    [Fact]
    public async Task ANewDesign_AfterTheRebuild_ContinuesTheVersionSequence()
    {
        var path = Path.Combine(_folder, "versions.db");
        Guid id;
        using (var factory = NewFactory("Sqlite", path))
        using (var client = factory.CreateClient())
            id = await BuildHistory(client);

        using var rebuilt = NewFactory("Sqlite", path);
        using var rebuiltClient = rebuilt.CreateClient();
        var response = await rebuiltClient.PostAsync($"/change-requests/{id}/designs", null);

        Assert.Equal(201, (int)response.StatusCode);
        Assert.Equal(2, (await response.ReadJsonAsync()).GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task TheSameFlow_GivesTheSameStatusesHeadersAndBodies_OnBothStores()
    {
        static string Mask(string text, Guid id) =>
            Timestamps.Replace(Guids.Replace(text.Replace(id.ToString(), "<id>"), "<guid>"), "<time>");

        List<Seen> Normalise(List<Seen> seen, Guid id) => seen
            .Select(s => s with { Body = Mask(s.Body, id), Location = s.Location is null ? null : Mask(s.Location, id) })
            .ToList();

        using var memory = NewFactory("InMemory", null);
        using var memoryClient = memory.CreateClient();
        var memoryId = await BuildHistory(memoryClient);
        var inMemory = Normalise(await ReadEverything(memoryClient, memoryId), memoryId);

        using var durable = NewFactory("Sqlite", Path.Combine(_folder, "parity.db"));
        using var durableClient = durable.CreateClient();
        var durableId = await BuildHistory(durableClient);
        var onDisk = Normalise(await ReadEverything(durableClient, durableId), durableId);

        Assert.Equal(inMemory, onDisk);
    }
}
