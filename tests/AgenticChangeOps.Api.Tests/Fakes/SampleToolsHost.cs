using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.ModelAccess;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>
/// The real composition (settings, session factory, design generator, store) with only the model
/// call replaced by a scripted tool client. The tools are the real ones over a real sample folder.
/// </summary>
public sealed class SampleToolsHost : IDisposable
{
    public const string Endpoint = "https://sentinel-endpoint.example.invalid/";
    public const string Deployment = "sentinel-deployment";

    public const string ValidAnswer = """
        {
          "approachSummary": "Change the page size setting only.",
          "affectedComponents": [ "Orders settings" ],
          "impactAndRisks": [ "Longer pages load more rows." ],
          "dependencies": [],
          "rollbackConsiderations": [ "Restore the previous page size." ],
          "openQuestions": []
        }
        """;

    private readonly ApiFactory _factory;

    public SampleToolsHost(
        Dictionary<string, string?>? settings = null,
        Func<IToolSession, CancellationToken, Task<ChatModelResponse>>? behavior = null,
        bool toolsOn = true,
        bool useFakeBasis = true)
    {
        Basis = new FakeBasisSource();
        ToolClient = new ScriptedToolClient(behavior ?? ReadsTheOrdersDesignNotes);

        var all = new Dictionary<string, string?>
        {
            ["AGENTICCHANGEOPS_MODEL_ENDPOINT"] = Endpoint,
            ["AGENTICCHANGEOPS_MODEL_DEPLOYMENT"] = Deployment
        };
        if (toolsOn)
        {
            all["AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED"] = "true";
            all["AGENTICCHANGEOPS_SAMPLE_CODEBASE_PATH"] = RealSampleFolder();
        }

        foreach (var pair in settings ?? new Dictionary<string, string?>())
            all[pair.Key] = pair.Value;

        _factory = new ApiFactory(
            FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true, settings: all,
            configureServices: services =>
            {
                services.AddSingleton<IToolChatModelClient>(ToolClient);
                if (useFakeBasis)
                    services.AddSingleton<IRequirementsBasisSource>(Basis);
            });
        Client = _factory.CreateClient();
    }

    public FakeBasisSource Basis { get; }
    public ScriptedToolClient ToolClient { get; }
    public HttpClient Client { get; }
    public CapturingLoggerProvider Logs => _factory.Logs;
    public IServiceProvider Services => _factory.Services;

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }

    public static string RealSampleFolder() => Path.Combine(RepositoryRoot(), "sample-codebase");

    /// <summary>The default scripted model: reads the orders service design notes, then answers.</summary>
    public static async Task<ChatModelResponse> ReadsTheOrdersDesignNotes(IToolSession session, CancellationToken ct)
    {
        await session.Tools.InvokeAsync("read_file", "{\"path\":\"orders-service/notes/design.md\"}", ct);
        return new ChatModelResponse(ValidAnswer, false);
    }

    public static Func<IToolSession, CancellationToken, Task<ChatModelResponse>> Reads(params string[] paths) =>
        async (session, ct) =>
        {
            foreach (var path in paths)
                await session.Tools.InvokeAsync("read_file", $"{{\"path\":\"{path}\"}}", ct);
            return new ChatModelResponse(ValidAnswer, false);
        };

    public async Task<Guid> ApprovedAsync(string affectedSystem = "Orders Service", bool withBasis = true)
    {
        var id = await Client.SubmitAsync(new SubmitChangeRequestBody(
            Title: "Raise the page size", Description: "Raise the number of rows shown per page.",
            Requester: "Alex Example", AffectedSystem: affectedSystem, Priority: "Medium", ChangeType: "ConfigChange"));
        if (withBasis)
            Basis.Set(id, FakeBasisSource.Basis());
        (await Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve())).EnsureSuccessStatusCode();
        return id;
    }

    public async Task<System.Text.Json.JsonElement> AuditAsync(Guid id) =>
        await (await Client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
    }
}

/// <summary>Stand-in tool client: runs the scripted behaviour against the real session.</summary>
public sealed class ScriptedToolClient : IToolChatModelClient
{
    private int _calls;

    public ScriptedToolClient(Func<IToolSession, CancellationToken, Task<ChatModelResponse>> behavior)
    {
        Behavior = behavior;
    }

    public Func<IToolSession, CancellationToken, Task<ChatModelResponse>> Behavior { get; set; }

    public int Calls => _calls;

    public Task<ChatModelResponse> CompleteAsync(
        string systemInstruction, string userContent, int maxOutputTokens, IToolSession session, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return Behavior(session, cancellationToken);
    }
}
