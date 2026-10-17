using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Api.Tests.Automatic;

/// <summary>A restart over the same durable file keeps the hand-over and repeats nothing.</summary>
public sealed class RestartTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "changeops-auto-" + Guid.NewGuid().ToString("N"));

    public RestartTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private Dictionary<string, string?> Settings() => new()
    {
        [ApiFactory.StorageProviderKey] = "Sqlite",
        [ApiFactory.StoragePathKey] = Path.Combine(_folder, "changeops.db"),
        ["ChangeOps:AutoAdvance:Enabled"] = "true",
        ["ChangeOps:AutoAdvance:IntervalSeconds"] = "1",
        ["ChangeOps:AutoAdvance:MaxConsecutiveFailures"] = "1"
    };

    private static async Task<System.Text.Json.JsonElement> PollStatusAsync(HttpClient client, Guid id, string mode)
    {
        var deadline = DateTime.UtcNow.AddSeconds(12);
        System.Text.Json.JsonElement status = default;
        while (DateTime.UtcNow < deadline)
        {
            status = await (await client.GetAsync($"/change-requests/{id}/workflow")).ReadJsonAsync();
            if (status.GetProperty("automatic").GetProperty("mode").GetString() == mode) break;
            await Task.Delay(100);
        }
        return status;
    }

    [Fact]
    public async Task LeftForAPerson_SurvivesARestart_AndNoFurtherAttemptIsMade()
    {
        Guid id;
        using (var first = new ApiFactory(
                   FakeDraftGenerator.ThrowsCategory(GenerationFailureCategory.Unreachable),
                   draftingConfigured: true, settings: Settings()))
        using (var client = first.CreateClient())
        {
            id = await client.SubmitAsync();
            var left = await PollStatusAsync(client, id, "LeftForPerson");
            Assert.Equal("LeftForPerson", left.GetProperty("automatic").GetProperty("mode").GetString());
        }

        var counting = FakeDraftGenerator.ReturnsSyntheticContent();
        using var second = new ApiFactory(counting, draftingConfigured: true, settings: Settings());
        using var secondClient = second.CreateClient();

        var status = await (await secondClient.GetAsync($"/change-requests/{id}/workflow")).ReadJsonAsync();
        await Task.Delay(2500);

        var automatic = status.GetProperty("automatic");
        Assert.Equal("LeftForPerson", automatic.GetProperty("mode").GetString());
        Assert.Equal("Unreachable", automatic.GetProperty("failureCategory").GetString());
        Assert.Equal(0, counting.CallCount);
        var after = await (await secondClient.GetAsync($"/change-requests/{id}/workflow")).ReadJsonAsync();
        Assert.Equal(0, after.GetProperty("completedSteps").GetArrayLength());
    }

    [Fact]
    public async Task ACompletedDraft_IsNotRepeated_AfterARestart()
    {
        Guid id;
        using (var first = new ApiFactory(
                   FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true, settings: Settings()))
        using (var client = first.CreateClient())
        {
            id = await client.SubmitAsync();
            var deadline = DateTime.UtcNow.AddSeconds(12);
            while (DateTime.UtcNow < deadline)
            {
                var s = await (await client.GetAsync($"/change-requests/{id}/workflow")).ReadJsonAsync();
                if (s.GetProperty("completedSteps").GetArrayLength() == 1) break;
                await Task.Delay(100);
            }
        }

        var counting = FakeDraftGenerator.ReturnsSyntheticContent();
        using var second = new ApiFactory(counting, draftingConfigured: true, settings: Settings());
        using var secondClient = second.CreateClient();
        await Task.Delay(2500);

        Assert.Equal(0, counting.CallCount);
        var drafts = await (await secondClient.GetAsync($"/change-requests/{id}/drafts")).ReadJsonAsync();
        Assert.Equal(1, drafts.GetArrayLength());
    }
}
