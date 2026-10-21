using System.Security.Cryptography;
using System.Text.Json;
using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.ModelAccess;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>Stand-in source of the latest requirements and design drafts, keyed by change request.</summary>
public sealed class FakeProposalBasis : IProposalBasisSource
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ProposalBasis> _bases = new();

    public void Set(Guid id, ProposalBasis basis)
    {
        lock (_gate)
            _bases[id] = basis;
    }

    public ProposalBasis GetLatest(Guid changeRequestId)
    {
        lock (_gate)
            return _bases.TryGetValue(changeRequestId, out var basis) ? basis : new ProposalBasis(null, null);
    }

    public static ProposalBasis Current(int requirementsVersion = 1, int designVersion = 1) => new(
        new ProposalRequirementsBasis(
            requirementsVersion, "Raise the number of orders shown per page.",
            new[] { "The page size is configurable." }, new[] { "Pages show the new size." },
            new[] { "Only the orders service is affected." }, new[] { "Should the maximum be capped?" }),
        new ProposalDesignBasis(
            designVersion, requirementsVersion, "Change the page size setting only.",
            new[] { "Orders settings" }, new[] { "Longer pages load more rows." }, Array.Empty<string>(),
            new[] { "Restore the previous page size." }, Array.Empty<string>()));
}

/// <summary>
/// The real composition (settings, session factory, proposal generator, verifier, store) with only
/// the model call replaced by a scripted tool client and the drafts by a fake basis. The tools and
/// the verification read a throwaway copy of the real sample folder, so tests can prove it is never changed.
/// </summary>
public sealed class ProposalHost : IDisposable
{
    public const string Endpoint = "https://sentinel-endpoint.example.invalid/";
    public const string Deployment = "sentinel-deployment";
    public const string OrdersSettings = "orders-service/config/orders.settings";

    private readonly ApiFactory _factory;

    public ProposalHost(
        Dictionary<string, string?>? settings = null,
        Func<IToolSession, CancellationToken, Task<ChatModelResponse>>? behavior = null,
        bool toolsOn = true,
        bool useFakeBasis = true)
    {
        Basis = new FakeProposalBasis();
        ToolClient = new ScriptedToolClient(behavior ?? ReadsAndProposes());
        SampleFolder = CopyRealSample();

        var all = new Dictionary<string, string?>
        {
            ["AGENTICCHANGEOPS_MODEL_ENDPOINT"] = Endpoint,
            ["AGENTICCHANGEOPS_MODEL_DEPLOYMENT"] = Deployment
        };
        if (toolsOn)
        {
            all["AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED"] = "true";
            all["AGENTICCHANGEOPS_SAMPLE_CODEBASE_PATH"] = SampleFolder;
        }

        foreach (var pair in settings ?? new Dictionary<string, string?>())
            all[pair.Key] = pair.Value;

        _factory = new ApiFactory(
            FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true, settings: all,
            configureServices: services =>
            {
                services.AddSingleton<IToolChatModelClient>(ToolClient);
                if (useFakeBasis)
                    services.AddSingleton<IProposalBasisSource>(Basis);
            });
        Client = _factory.CreateClient();
    }

    public FakeProposalBasis Basis { get; }
    public ScriptedToolClient ToolClient { get; }
    public HttpClient Client { get; }
    public string SampleFolder { get; }
    public CapturingLoggerProvider Logs => _factory.Logs;
    public IServiceProvider Services => _factory.Services;

    public static string RealSampleFolder() => SampleToolsHost.RealSampleFolder();

    // ---- scripted answers ----

    public sealed record Edit(string Path, string Kind, string? ExistingText, string ReplacementText);

    public static Edit ChangeEdit(string path = OrdersSettings, string existing = "orders.page.size = 25", string replacement = "orders.page.size = 50") =>
        new(path, "change", existing, replacement);

    public static Edit AddEdit(string path = "orders-service/notes/page-size.md", string text = "# Page size\nRaised to 50.") =>
        new(path, "add", null, text);

    public static string Answer(params Edit[] edits) => JsonSerializer.Serialize(new
    {
        summary = "Raise the orders page size from 25 to 50.",
        changes = edits.Select(e => new { path = e.Path, kind = e.Kind, existingText = e.ExistingText, replacementText = e.ReplacementText }),
        risks = new[] { "Longer pages load more rows." },
        testIdeas = new[] { "Check a page shows fifty rows." },
        openQuestions = new[] { "Should there be a maximum?" }
    });

    /// <summary>The default scripted model: reads the orders settings, then proposes one valid change.</summary>
    public static Func<IToolSession, CancellationToken, Task<ChatModelResponse>> ReadsAndProposes(params Edit[] edits) =>
        async (session, ct) =>
        {
            await session.Tools.InvokeAsync("read_file", $"{{\"path\":\"{OrdersSettings}\"}}", ct);
            return new ChatModelResponse(Answer(edits.Length == 0 ? new[] { ChangeEdit() } : edits), false);
        };

    /// <summary>Like <see cref="ReadsAndProposes"/>, but proposes exactly these edits, even none at all.</summary>
    public static Func<IToolSession, CancellationToken, Task<ChatModelResponse>> ReadsAndProposesExactly(Edit[] edits) =>
        async (session, ct) =>
        {
            await session.Tools.InvokeAsync("read_file", $"{{\"path\":\"{OrdersSettings}\"}}", ct);
            return new ChatModelResponse(Answer(edits), false);
        };

    public static Func<IToolSession, CancellationToken, Task<ChatModelResponse>> Returns(string text) =>
        (_, _) => Task.FromResult(new ChatModelResponse(text, false));

    public static Func<IToolSession, CancellationToken, Task<ChatModelResponse>> Throws(Exception exception) =>
        (_, _) => throw exception;

    // ---- change requests ----

    public async Task<Guid> SubmittedAsync(string affectedSystem = "Orders Service")
    {
        return await Client.SubmitAsync(new SubmitChangeRequestBody(
            Title: "Raise the page size", Description: "Raise the number of rows shown per page.",
            Requester: "Alex Example", AffectedSystem: affectedSystem, Priority: "Medium", ChangeType: "ConfigChange"));
    }

    /// <summary>Submitted, approved, with current requirements and design drafts in the fake basis.</summary>
    public async Task<Guid> ApprovedAsync(string affectedSystem = "Orders Service", bool withDrafts = true)
    {
        var id = await SubmittedAsync(affectedSystem);
        if (withDrafts)
            Basis.Set(id, FakeProposalBasis.Current());
        (await Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve())).EnsureSuccessStatusCode();
        return id;
    }

    public Task<HttpResponseMessage> ProposeAsync(Guid id) => Client.PostAsync($"/change-requests/{id}/proposals", null);

    public async Task<JsonElement> AuditAsync(Guid id) =>
        await (await Client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();

    public async Task<string> StatusAsync(Guid id) =>
        (await (await Client.GetAsync($"/change-requests/{id}")).ReadJsonAsync()).GetProperty("status").GetString()!;

    // ---- the sample copy ----

    private static string CopyRealSample()
    {
        var target = Path.Combine(Path.GetTempPath(), "changeops-proposal-sample-" + Guid.NewGuid().ToString("N"));
        var source = RealSampleFolder();
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        return target;
    }

    /// <summary>One hash over every path and byte under the sample copy, to prove nothing changed.</summary>
    public string SampleFingerprint()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFileSystemEntries(SampleFolder, "*", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(SampleFolder, path) + "\n"));
            if (File.Exists(path))
                hash.AppendData(File.ReadAllBytes(path));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
        try { Directory.Delete(SampleFolder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
