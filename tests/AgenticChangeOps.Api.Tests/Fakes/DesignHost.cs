using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Api.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>
/// In-process host for design tests: a fake design generator, a fake requirements source
/// (unless the real adapter is wanted), and a chosen configured state. The requirements
/// generator is the usual synthetic one so requirements drafts can be requested over HTTP.
/// </summary>
public sealed class DesignHost : IDisposable
{
    private readonly ApiFactory _factory;

    public DesignHost(
        FakeDesignGenerator? generator = null,
        bool configured = true,
        bool useFakeBasis = true)
    {
        Generator = generator ?? FakeDesignGenerator.ReturnsSyntheticContent();
        Requirements = FakeDraftGenerator.ReturnsSyntheticContent();
        Basis = new FakeBasisSource();
        _factory = new ApiFactory(Requirements, draftingConfigured: true, configureServices: services =>
        {
            services.AddSingleton<IDesignDraftGenerator>(Generator);
            services.AddSingleton(new DesignAvailability(configured));
            if (useFakeBasis)
                services.AddSingleton<IRequirementsBasisSource>(Basis);
        });
        Client = _factory.CreateClient();
    }

    public FakeDesignGenerator Generator { get; }
    public FakeDraftGenerator Requirements { get; }
    public FakeBasisSource Basis { get; }
    public HttpClient Client { get; }
    public CapturingLoggerProvider Logs => _factory.Logs;
    public IServiceProvider Services => _factory.Services;

    /// <summary>Submits a request, gives the fake source a requirements draft, and approves it.</summary>
    public async Task<Guid> ApprovedWithBasisAsync(int basisVersion = 1)
    {
        var id = await Client.SubmitAsync();
        Basis.Set(id, FakeBasisSource.Basis(basisVersion));
        (await Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()))
            .EnsureSuccessStatusCode();
        return id;
    }

    /// <summary>Approves a request that has no requirements draft.</summary>
    public async Task<Guid> ApprovedWithoutBasisAsync()
    {
        var id = await Client.SubmitAsync();
        (await Client.PostJsonAsync($"/change-requests/{id}/decisions", SyntheticData.Approve()))
            .EnsureSuccessStatusCode();
        return id;
    }

    public async Task<string[]> AuditEventTypesAsync(Guid id)
    {
        var json = await (await Client.GetAsync($"/change-requests/{id}/audit")).ReadJsonAsync();
        return json.EnumerateArray().Select(e => e.GetProperty("eventType").GetString()!).ToArray();
    }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
    }
}
