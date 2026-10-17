using System.Net;
using AgenticChangeOps.Api.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests.Storage;

/// <summary>
/// US4 / FR-011 / FR-013: unusable storage does not stop the host, the operation is refused with
/// a specific error and no internal detail, and a lost version race is a clean conflict.
/// </summary>
public sealed class StorageErrorTests : IDisposable
{
    private const string SecretFolderName = "secret-storage-folder";

    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "changeops-api-errors-" + Guid.NewGuid().ToString("N"));

    public StorageErrorTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static ApiFactory Factory(string path, Action<IServiceCollection>? configure = null) =>
        new(
            settings: new Dictionary<string, string?>
            {
                [ApiFactory.StorageProviderKey] = "Sqlite",
                [ApiFactory.StoragePathKey] = path
            },
            configureServices: configure);

    private string BlockedPath()
    {
        var blocking = Path.Combine(_folder, SecretFolderName);
        File.WriteAllText(blocking, "synthetic");
        return Path.Combine(blocking, "inner", "changeops.db");
    }

    private string StampedPath(int version)
    {
        var path = Path.Combine(_folder, "stamped.db");
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE TABLE t (a INTEGER); PRAGMA user_version = {version};";
        command.ExecuteNonQuery();
        return path;
    }

    private static async Task AssertNothingLeaks(HttpResponseMessage response, ApiFactory factory, string path)
    {
        var seen = new List<string> { await response.Content.ReadAsStringAsync() };
        seen.AddRange(response.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}"));
        seen.AddRange(response.Content.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}"));
        seen.AddRange(factory.Logs.Lines);

        foreach (var text in seen)
        {
            Assert.DoesNotContain(path, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SecretFolderName, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("changeops-api-errors-", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sqlite", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task UnusableLocation_HostStarts_AndAnOperationNeedingStorageGets503StorageUnavailable()
    {
        var path = BlockedPath();
        using var factory = Factory(path);
        using var client = factory.CreateClient();

        var response = await client.PostJsonAsync("/change-requests", SyntheticData.ValidSubmission());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.ReadJsonAsync();
        Assert.Equal("storage-unavailable", json.GetProperty("code").GetString());
        Assert.Equal("Unavailable", json.GetProperty("category").GetString());
        Assert.Equal("The change request storage is unavailable.", json.GetProperty("message").GetString());
        Assert.False(json.TryGetProperty("errors", out _));
        await AssertNothingLeaks(response, factory, path);
    }

    [Fact]
    public async Task UnusableLocation_ReadsAreRefusedTheSameWay_AndRoutesNeedingNoStorageStillAnswer()
    {
        var path = BlockedPath();
        using var factory = Factory(path);
        using var client = factory.CreateClient();

        var read = await client.GetAsync($"/change-requests/{Guid.NewGuid()}");
        var unknownRoute = await client.GetAsync("/no-such-route");
        var swagger = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, read.StatusCode);
        Assert.Equal("storage-unavailable", (await read.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);
        Assert.Equal("route-not-found", (await unknownRoute.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
    }

    [Fact]
    public async Task UnsupportedLayout_Gets503WithCategoryUnsupportedLayout_AndTheFileIsUntouched()
    {
        var path = StampedPath(99);
        var before = File.ReadAllBytes(path);
        using var factory = Factory(path);
        using var client = factory.CreateClient();

        var response = await client.PostJsonAsync("/change-requests", SyntheticData.ValidSubmission());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("storage-unavailable", json.GetProperty("code").GetString());
        Assert.Equal("UnsupportedLayout", json.GetProperty("category").GetString());
        await AssertNothingLeaks(response, factory, path);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task ALostVersionRace_Gets409VersionConflict_InTheExistingErrorShape()
    {
        using var factory = Factory(
            Path.Combine(_folder, "unused.db"),
            services => services.AddSingleton<IChangeRequestStore>(
                new ThrowingStore(new StorageVersionConflictException())));
        using var client = factory.CreateClient();

        var response = await client.PostJsonAsync("/change-requests", SyntheticData.ValidSubmission());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("version-conflict", json.GetProperty("code").GetString());
        Assert.Equal("The request conflicts with a concurrent change; try again.", json.GetProperty("message").GetString());
        Assert.False(json.TryGetProperty("category", out _));
    }

    [Fact]
    public async Task AThrownStorageUnavailable_FromAnyStore_IsMappedTheSameWay()
    {
        using var factory = Factory(
            Path.Combine(_folder, "unused.db"),
            services => services.AddSingleton<IChangeRequestStore>(
                new ThrowingStore(new StorageUnavailableException(StorageFailureCategory.UnsupportedLayout))));
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/change-requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal("storage-unavailable", json.GetProperty("code").GetString());
        Assert.Equal("UnsupportedLayout", json.GetProperty("category").GetString());
    }

    /// <summary>A store whose every operation fails with the given exception.</summary>
    private sealed class ThrowingStore : IChangeRequestStore
    {
        private readonly Exception _exception;

        public ThrowingStore(Exception exception) => _exception = exception;

        public object SyncRoot { get; } = new();
        public void Save(ChangeRequest changeRequest) => throw _exception;
        public void Update(ChangeRequest changeRequest) => throw _exception;
        public ChangeRequest? FindById(Guid id) => throw _exception;
        public void SaveAuditEntry(ChangeRequestAuditEntry entry) => throw _exception;
        public IReadOnlyList<ChangeRequestAuditEntry> GetAuditEntries(Guid changeRequestId) => throw _exception;
        public void SaveWithAudit(ChangeRequest changeRequest, ChangeRequestAuditEntry auditEntry) => throw _exception;
        public void UpdateWithAudit(ChangeRequest changeRequest, ChangeRequestAuditEntry auditEntry) => throw _exception;
        public void SaveAgentOutput<T>(T output, ChangeRequestAuditEntry auditEntry) where T : IAgentOutput => throw _exception;
        public IReadOnlyList<T> GetAgentOutputs<T>(Guid changeRequestId) where T : IAgentOutput => throw _exception;
        public IReadOnlyList<Guid> ListChangeRequestIds() => throw _exception;
        public AutomaticAttempt? GetAutomaticAttempt(Guid changeRequestId) => throw _exception;
        public void SaveAutomaticAttempt(AutomaticAttempt attempt) => throw _exception;
        public void ClearAutomaticAttempt(Guid changeRequestId) => throw _exception;
    }
}
