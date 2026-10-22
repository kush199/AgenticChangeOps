using System.Security.Cryptography;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.ModelAccess;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>
/// The real composition (settings, adapters, services, store) with only the model calls scripted: the
/// requirements and design generators are fakes, proposals go through the real Developer service over
/// a throwaway copy of the real sample folder, and the Reviewer's own model call is a scripted plain
/// chat client. No route exists for reviews, so tests obtain the service from the provider.
/// </summary>
public sealed class ReviewHost : IDisposable
{
    public const string Endpoint = "https://sentinel-endpoint.example.invalid/";
    public const string Deployment = "sentinel-deployment";

    /// <summary>A review whose references exist in any synthetic chain this host builds.</summary>
    public const string ValidReviewJson = """
        {
          "findings": [
            { "category": "UnmentionedRisk", "severity": "Low", "statement": "Synthetic finding about a risk.",
              "references": ["proposal.changes[1]", "proposal.risks[1]"] }
          ],
          "openQuestions": ["Who confirms the change?"]
        }
        """;

    private readonly ApiFactory _factory;

    public ReviewHost(
        Dictionary<string, string?>? settings = null,
        bool toolsOn = true,
        bool modelConfigured = true,
        string? sampleFolder = null,
        IReviewGenerator? generator = null)
    {
        ReviewClient = new ScriptedChatModelClient { Behavior = ScriptedChatModelClient.ReturnsText(ValidReviewJson) };
        ToolClient = new ScriptedToolClient(ProposalHost.ReadsAndProposes(ProposalHost.ChangeEdit(), ProposalHost.AddEdit()));
        SampleFolder = sampleFolder ?? CopyRealSample();
        OwnsSampleFolder = sampleFolder is null;

        var all = new Dictionary<string, string?>
        {
            ["AGENTICCHANGEOPS_SAMPLE_CODEBASE_PATH"] = SampleFolder
        };
        if (modelConfigured)
        {
            all["AGENTICCHANGEOPS_MODEL_ENDPOINT"] = Endpoint;
            all["AGENTICCHANGEOPS_MODEL_DEPLOYMENT"] = Deployment;
        }
        if (toolsOn)
            all["AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED"] = "true";

        foreach (var pair in settings ?? new Dictionary<string, string?>())
            all[pair.Key] = pair.Value;

        Designs = FakeDesignGenerator.ReturnsSyntheticContent();
        _factory = new ApiFactory(
            FakeDraftGenerator.ReturnsSyntheticContent(), draftingConfigured: true, settings: all,
            configureServices: services =>
            {
                services.AddSingleton<IToolChatModelClient>(ToolClient);
                services.AddSingleton<IChatModelClient>(ReviewClient);
                services.AddSingleton<IDesignDraftGenerator>(Designs);
                services.AddSingleton(new DesignAvailability(true));
                if (generator is not null)
                    services.AddSingleton(generator);
            });
        Client = _factory.CreateClient();
    }

    public ScriptedChatModelClient ReviewClient { get; }
    public ScriptedToolClient ToolClient { get; }
    public FakeDesignGenerator Designs { get; }
    public HttpClient Client { get; }
    public string SampleFolder { get; }
    public bool OwnsSampleFolder { get; }
    public CapturingLoggerProvider Logs => _factory.Logs;
    public IServiceProvider Services => _factory.Services;

    public IReviewService Reviewing => Services.GetRequiredService<IReviewService>();
    public IChangeRequestStore Store => Services.GetRequiredService<IChangeRequestStore>();

    /// <summary>Submit, draft requirements, approve, design: leaves the request ready for a proposal.</summary>
    public async Task<Guid> ReadyForProposalAsync(string affectedSystem = "Orders Service")
    {
        var id = await Client.SubmitAsync(new AgenticChangeOps.Api.Contracts.SubmitChangeRequestBody(
            Title: "Raise the page size", Description: "Raise the number of rows shown per page.",
            Requester: "Alex Example", AffectedSystem: affectedSystem, Priority: "Medium", ChangeType: "ConfigChange"));
        (await Client.PostAsync($"/change-requests/{id}/drafts", null)).EnsureSuccessStatusCode();
        (await Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve())).EnsureSuccessStatusCode();
        (await Client.PostAsync($"/change-requests/{id}/designs", null)).EnsureSuccessStatusCode();
        return id;
    }

    /// <summary>The full real chain: requirements, design and a proposal made over HTTP (tools on).</summary>
    public async Task<Guid> ReadyForReviewAsync(string affectedSystem = "Orders Service")
    {
        var id = await ReadyForProposalAsync(affectedSystem);
        (await Client.PostAsync($"/change-requests/{id}/proposals", null)).EnsureSuccessStatusCode();
        return id;
    }

    /// <summary>
    /// A proposal stored directly for a request that already has requirements and design, for hosts
    /// where the tools are off and the proposal route is unavailable.
    /// </summary>
    public ChangeProposal SeedProposal(Guid id, params ProposedChange[] changes)
    {
        var requirements = Services.GetRequiredService<AgenticChangeOps.Agents.Requirement.IRequirementsDraftingService>()
            .GetLatestDraft(id).Value!;
        var design = Services.GetRequiredService<IDesignDraftingService>().GetLatestDesign(id).Value!;
        var proposal = new ChangeProposal(
            ProposalId: Guid.NewGuid(), ChangeRequestId: id, Version: 1,
            RequirementsVersion: requirements.Version, DesignVersion: design.Version,
            GeneratedAt: DateTime.UtcNow, Status: ProposalStatus.Draft, AgentName: ChangeProposal.DeveloperAgentName,
            Summary: "Raise the orders page size.",
            Changes: changes.Length == 0
                ? new[] { new ProposedChange(ProposalHost.OrdersSettings, ProposalChangeKind.Change, "orders.page.size = 25", "orders.page.size = 50") }
                : changes,
            Risks: new[] { "Longer pages load more rows." }, TestIdeas: Array.Empty<string>(),
            OpenQuestions: Array.Empty<string>(), SampleFilesRead: Array.Empty<string>(),
            VerificationNote: ChangeProposal.VerificationNoteText);
        Store.SaveProposal(proposal, new ChangeRequestAuditEntry(
            AuditEntryId: Guid.NewGuid(), ChangeRequestId: id, EventType: AuditEventType.ProposalGenerated,
            OccurredAt: proposal.GeneratedAt, Details: null, StatusBefore: ChangeRequestStatus.Approved,
            StatusAfter: ChangeRequestStatus.Approved, AgentName: proposal.AgentName, DraftVersion: proposal.Version));
        return proposal;
    }

    public async Task<string> AuditJsonAsync(Guid id) =>
        await (await Client.GetAsync($"/change-requests/{id}/audit")).Content.ReadAsStringAsync();

    // ---- the sample copy ----

    private static string CopyRealSample()
    {
        var target = Path.Combine(Path.GetTempPath(), "changeops-review-sample-" + Guid.NewGuid().ToString("N"));
        var source = ProposalHost.RealSampleFolder();
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        return target;
    }

    /// <summary>One hash over every path and byte under the sample copy, to prove nothing changed.</summary>
    public string SampleFingerprint()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        if (!Directory.Exists(SampleFolder))
            return "missing";

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
        if (OwnsSampleFolder)
        {
            try { Directory.Delete(SampleFolder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
