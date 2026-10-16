using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Intake.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AgenticChangeOps.Agents.Requirement;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>
/// In-process host for tests. Can replace the generator and the drafting availability, supply
/// settings through in-memory configuration, and captures all log output.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string StorageProviderKey = "ChangeOps:Storage:Provider";
    public const string StoragePathKey = "ChangeOps:Storage:Path";

    private readonly IRequirementsDraftGenerator? _generator;
    private readonly bool? _draftingConfigured;
    private readonly Dictionary<string, string?> _settings;

    public ApiFactory(
        IRequirementsDraftGenerator? generator = null,
        bool? draftingConfigured = null,
        Dictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configureServices = null)
    {
        _generator = generator;
        _draftingConfigured = draftingConfigured;
        _settings = settings is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?>(settings);

        // Tests run on the in-memory store unless they ask for the durable one.
        if (!_settings.ContainsKey(StorageProviderKey))
            _settings[StorageProviderKey] = "InMemory";
        _configureServices = configureServices;
    }

    private readonly Action<IServiceCollection>? _configureServices;

    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(_settings));
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            if (_generator is not null)
            {
                services.AddSingleton(_generator);
                services.AddSingleton<IRequirementsDraftGenerator>(_generator);
            }

            if (_draftingConfigured is not null)
                services.AddSingleton(new DraftingAvailability(_draftingConfigured.Value));

            _configureServices?.Invoke(services);
        });
    }
}
