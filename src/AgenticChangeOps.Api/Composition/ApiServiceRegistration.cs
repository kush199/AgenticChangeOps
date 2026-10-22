using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;
using Microsoft.AspNetCore.Http.Json;
using AgenticChangeOps.ModelAccess;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Persistence;
using AgenticChangeOps.SampleCode;
using AgenticChangeOps.Workflow;

namespace AgenticChangeOps.Api.Composition;

public static class ApiServiceRegistration
{
    /// <summary>
    /// Registers the intake, approval and drafting services over one store (durable local storage
    /// by default, in-memory when the provider setting says so). Model
    /// settings are read lazily from <c>IConfiguration</c> (environment variables by default).
    /// </summary>
    public static IServiceCollection AddChangeOpsApi(this IServiceCollection services)
    {
        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddSingleton<IChangeRequestStore>(CreateStore);
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IIdGenerator, GuidIdGenerator>();
        services.AddSingleton<ChangeRequestValidator>();
        services.AddSingleton<IChangeRequestService, ChangeRequestService>();
        services.AddSingleton<ApprovalDecisionValidator>();
        services.AddSingleton<IApprovalService, ApprovalService>();

        // Drafting: the real generator when configuration is valid, otherwise a recorded
        // "not configured" state. Both are derived once from a single setup object so the
        // answer cannot disagree with the generator that is actually registered.
        services.AddSingleton<DraftingSetup>(CreateDraftingSetup);
        services.AddSingleton<IRequirementsDraftGenerator>(sp => sp.GetRequiredService<DraftingSetup>().Generator);
        services.AddSingleton(sp => new DraftingAvailability(sp.GetRequiredService<DraftingSetup>().IsConfigured));
        services.AddSingleton<DraftContentValidator>();
        services.AddSingleton<IRequirementsDraftingService, RequirementsDraftingService>();

        // Design drafting (Architect): same configuration keys, read independently so the
        // requirements registration above is untouched. The Architect never references the
        // Requirement agent; the adapter below supplies the latest requirements draft.
        // The tool-capable model call is only built when the sample tools are switched on and design
        // drafting asks for it; tests replace it.
        services.AddSingleton<IToolChatModelClient>(sp =>
            new AgentFrameworkToolChatModelClient(AzureOpenAIGeneratorOptions.FromEnvironment(
                name => sp.GetRequiredService<IConfiguration>()[name])));
        services.AddSingleton<DesignSetup>(CreateDesignSetup);
        services.AddSingleton<IDesignDraftGenerator>(sp => sp.GetRequiredService<DesignSetup>().Generator);
        services.AddSingleton(sp => new DesignAvailability(sp.GetRequiredService<DesignSetup>().IsConfigured));
        services.AddSingleton<DesignContentValidator>();
        services.AddSingleton<IRequirementsBasisSource, RequirementsBasisAdapter>();
        services.AddSingleton<IDesignDraftingService, DesignDraftingService>();

        // Change proposals (Developer): needs the sample tools, so everything about it is decided from
        // one setup object that reads the tools switch first. The workflow only ever shows the offer.
        services.AddSingleton<ProposalSetup>(CreateProposalSetup);
        services.AddSingleton(sp => new ProposalAvailability(sp.GetRequiredService<ProposalSetup>().State));
        services.AddSingleton<IProposalOffer, ProposalOffer>();
        services.AddSingleton<IProposalBasisSource, ProposalBasisAdapter>();
        services.AddSingleton<IProposalService>(sp =>
        {
            var setup = sp.GetRequiredService<ProposalSetup>();
            return new ProposalService(
                sp.GetRequiredService<IChangeRequestStore>(),
                sp.GetRequiredService<ISystemClock>(),
                sp.GetRequiredService<IIdGenerator>(),
                sp.GetRequiredService<IProposalBasisSource>(),
                setup.Generator,
                new ProposalContentValidator(),
                setup.Verifier);
        });

        // Reviews (Reviewer): an internal service only, no route. Nothing here is read at start-up: the
        // setup is built the first time the service is asked for, so a server that never uses the
        // Reviewer is unaffected by any review or model setting. A test can replace the generator or
        // register a plain chat client.
        services.AddSingleton<ReviewSetup>(CreateReviewSetup);
        services.AddSingleton<IReviewGenerator>(sp => sp.GetRequiredService<ReviewSetup>().Generator);
        services.AddSingleton<IReviewBasisSource, ReviewBasisAdapter>();
        services.AddSingleton<IReviewService>(sp =>
        {
            var setup = sp.GetRequiredService<ReviewSetup>();
            return new ReviewService(
                sp.GetRequiredService<IChangeRequestStore>(),
                sp.GetRequiredService<ISystemClock>(),
                sp.GetRequiredService<IIdGenerator>(),
                sp.GetRequiredService<IReviewBasisSource>(),
                sp.GetRequiredService<IReviewGenerator>(),
                new ReviewContentValidator(setup.Limits),
                new SampleChecker(setup.Sample));
        });

        // Workflow orchestration: derives status from recorded facts and runs the agents'
        // own services; the availability adapter reuses the two flags registered above.
        services.AddSingleton<IStepAvailability, StepAvailabilityAdapter>();
        // One WorkflowService answers both the manual and the automatic path, so they share one
        // per-request gate and at most one step runs for a request at a time.
        services.AddSingleton<WorkflowService>();
        services.AddSingleton<IWorkflowService>(sp => sp.GetRequiredService<WorkflowService>());
        services.AddSingleton<IAutomaticStepRunner>(sp => sp.GetRequiredService<WorkflowService>());

        // Automatic advancement: off unless an operator switches it on. Settings are read once.
        services.AddSingleton<AutomaticAdvancementSettings>(CreateAutomaticSettings);
        services.AddSingleton<CheckSummaryHolder>();
        services.AddSingleton<IAutomaticStatusSource, AutomaticStatusSource>();
        services.AddSingleton<AutomaticAdvancementService>();
        services.AddHostedService<AutomaticAdvancementHostedService>();

        return services;
    }

    private static AutomaticAdvancementSettings CreateAutomaticSettings(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AgenticChangeOps.Api.AutoAdvance");

        // Warnings name the setting only, never its value.
        return AutomaticAdvancementSettings.FromConfiguration(
            name => configuration[name],
            message => logger.LogWarning("{Message}", message));
    }

    public const string StorageProviderKey = "ChangeOps:Storage:Provider";
    public const string StoragePathKey = "ChangeOps:Storage:Path";

    /// <summary>
    /// Durable storage unless the provider is explicitly <c>InMemory</c> (what tests choose).
    /// The file is opened lazily, so an unusable location never stops the host from starting.
    /// </summary>
    private static IChangeRequestStore CreateStore(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        if (string.Equals(configuration[StorageProviderKey], "InMemory", StringComparison.OrdinalIgnoreCase))
            return new InMemoryChangeRequestStore();

        var configured = configuration[StoragePathKey];
        var path = string.IsNullOrWhiteSpace(configured) ? SqliteStorageOptions.DefaultPath() : configured;
        return new SqliteChangeRequestStore(new SqliteStorageOptions(path));
    }

    private static DesignSetup CreateDesignSetup(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AgenticChangeOps.Api.Designing");

        try
        {
            var options = AzureOpenAIGeneratorOptions.FromEnvironment(name => configuration[name]);

            // The switch is read first. While it is off nothing else about the tools is read, so
            // the generator and everything it does are exactly what they were without the feature.
            var tools = SampleToolSettings.FromConfiguration(name => configuration[name]);
            if (!tools.Enabled)
                return new DesignSetup(
                    new AzureOpenAIDesignGenerator(options, new AgentFrameworkChatModelClient(options)),
                    isConfigured: true);

            var sampleFolder = tools.SampleFolder ?? Path.Combine(AppContext.BaseDirectory, "sample-codebase");
            var generator = new AzureOpenAIDesignGenerator(
                options,
                new AgentFrameworkChatModelClient(options),
                new CountingToolSessionFactory(new SampleToolSessionFactory(tools, sampleFolder), logger),
                services.GetRequiredService<IToolChatModelClient>());
            return new DesignSetup(generator, isConfigured: true);
        }
        catch (GeneratorConfigurationException)
        {
            // Only the fact is recorded: not the setting name, and never a value.
            logger.LogWarning("Design drafting is not configured; design requests will be refused.");
            return new DesignSetup(new NotConfiguredDesignGenerator(), isConfigured: false);
        }
    }

    /// <summary>
    /// Proposals need the sample tools. The tools switch is read first: while it is off nothing else
    /// about proposals is read. With it on, an invalid model, tool or proposal setting makes
    /// proposals "not configured" (only the fact is logged, never a name or a value).
    /// </summary>
    private static ProposalSetup CreateProposalSetup(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AgenticChangeOps.Api.Proposing");

        try
        {
            var tools = SampleToolSettings.FromConfiguration(name => configuration[name]);
            if (!tools.Enabled)
                return ProposalSetup.Unavailable(ProposalAvailabilityState.ToolsUnavailable);

            var options = AzureOpenAIGeneratorOptions.FromEnvironment(name => configuration[name]);
            var proposal = ProposalSettings.FromConfiguration(name => configuration[name]);

            var sampleFolder = tools.SampleFolder ?? Path.Combine(AppContext.BaseDirectory, "sample-codebase");
            var generator = new AzureOpenAIProposalGenerator(
                options,
                new AgentFrameworkChatModelClient(options),
                new CountingToolSessionFactory(new SampleToolSessionFactory(tools, sampleFolder), logger),
                services.GetRequiredService<IToolChatModelClient>());
            var verifier = new ProposalVerifier(new SampleFilesAdapter(sampleFolder, tools.MaxFileBytes), proposal.Limits);
            return new ProposalSetup(ProposalAvailabilityState.Available, generator, verifier);
        }
        catch (GeneratorConfigurationException)
        {
            logger.LogWarning("Change proposals are not configured; proposal requests will be refused.");
            return ProposalSetup.Unavailable(ProposalAvailabilityState.NotConfigured);
        }
    }

    /// <summary>
    /// Reviews need the model settings and the review limits, not the sample tools: the tools switch is
    /// never read, and the sample is looked at only by the system's own re-check. An invalid setting
    /// makes reviews "not configured" (only the fact is logged, never a name or a value).
    /// </summary>
    private static ReviewSetup CreateReviewSetup(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AgenticChangeOps.Api.Reviewing");

        var configuredFolder = configuration[SampleToolSettings.SampleFolderVariable];
        var sampleFolder = string.IsNullOrWhiteSpace(configuredFolder)
            ? Path.Combine(AppContext.BaseDirectory, "sample-codebase")
            : configuredFolder;
        var sample = new ReviewSampleFilesAdapter(sampleFolder, SampleToolSettings.DefaultMaxFileBytes);

        try
        {
            var options = AzureOpenAIGeneratorOptions.FromEnvironment(name => configuration[name]);
            var settings = ReviewSettings.FromConfiguration(name => configuration[name]);

            // The plain chat client, with no tool of any kind; a test may register its own.
            var client = services.GetService<IChatModelClient>() ?? new AzureOpenAIChatModelClient(options);
            return new ReviewSetup(new AzureOpenAIReviewGenerator(options, client), settings.Limits, sample);
        }
        catch (GeneratorConfigurationException)
        {
            logger.LogWarning("Reviews are not configured; review requests will fail.");
            return new ReviewSetup(new NotConfiguredReviewGenerator(), ReviewLimits.Defaults, sample);
        }
    }

    private sealed class ReviewSetup
    {
        public ReviewSetup(IReviewGenerator generator, ReviewLimits limits, IReviewSampleFiles sample)
        {
            Generator = generator;
            Limits = limits;
            Sample = sample;
        }

        public IReviewGenerator Generator { get; }
        public ReviewLimits Limits { get; }
        public IReviewSampleFiles Sample { get; }
    }

    /// <summary>Fails before any model call when reviews are not configured.</summary>
    private sealed class NotConfiguredReviewGenerator : IReviewGenerator
    {
        public Task<ReviewContent?> GenerateAsync(
            ChangeRequest changeRequest,
            ReviewRequirementsBasis requirements,
            ReviewDesignBasis design,
            ReviewProposalBasis proposal,
            SystemCheck systemCheck,
            CancellationToken cancellationToken) =>
            throw new DraftGenerationException(GenerationFailureCategory.Unreachable);
    }

    private sealed class ProposalSetup
    {
        public ProposalSetup(ProposalAvailabilityState state, IProposalGenerator generator, ProposalVerifier verifier)
        {
            State = state;
            Generator = generator;
            Verifier = verifier;
        }

        public ProposalAvailabilityState State { get; }
        public IProposalGenerator Generator { get; }
        public ProposalVerifier Verifier { get; }

        public static ProposalSetup Unavailable(ProposalAvailabilityState state) =>
            new(state, new UnavailableProposalGenerator(), new ProposalVerifier(new NoSampleFiles(), ProposalLimits.Defaults));
    }

    /// <summary>Never reached through the API (the route checks availability first).</summary>
    private sealed class UnavailableProposalGenerator : IProposalGenerator
    {
        public Task<ProposalContent?> GenerateAsync(
            ChangeRequest changeRequest,
            ProposalRequirementsBasis requirements,
            ProposalDesignBasis design,
            CancellationToken cancellationToken) =>
            throw new DraftGenerationException(GenerationFailureCategory.Unreachable);
    }

    /// <summary>The sample as seen when proposals are unavailable: nothing is reachable.</summary>
    private sealed class NoSampleFiles : ISampleFiles
    {
        public SamplePathState Probe(string path) => SamplePathState.OutsideSample;

        public SampleTextResult ReadText(string path) => new(null, ProposalRejection.PathOutsideSample);
    }

    private sealed class DesignSetup
    {
        public DesignSetup(IDesignDraftGenerator generator, bool isConfigured)
        {
            Generator = generator;
            IsConfigured = isConfigured;
        }

        public IDesignDraftGenerator Generator { get; }
        public bool IsConfigured { get; }
    }

    /// <summary>Never reached through the API (the route checks availability first).</summary>
    private sealed class NotConfiguredDesignGenerator : IDesignDraftGenerator
    {
        public Task<DesignContent?> GenerateAsync(
            ChangeRequest changeRequest, RequirementsBasis basis, CancellationToken cancellationToken) =>
            throw new DraftGenerationException(GenerationFailureCategory.Unreachable);
    }

    private static DraftingSetup CreateDraftingSetup(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AgenticChangeOps.Api.Drafting");

        try
        {
            var options = AzureOpenAIGeneratorOptions.FromEnvironment(name => configuration[name]);
            var generator = new AzureOpenAIDraftGenerator(options, new AzureOpenAIChatModelClient(options));
            return new DraftingSetup(generator, isConfigured: true);
        }
        catch (GeneratorConfigurationException)
        {
            // Only the fact is recorded: not the setting name, and never a value.
            logger.LogWarning("Requirements drafting is not configured; drafting requests will be refused.");
            return new DraftingSetup(new NotConfiguredGenerator(), isConfigured: false);
        }
    }

    private sealed class DraftingSetup
    {
        public DraftingSetup(IRequirementsDraftGenerator generator, bool isConfigured)
        {
            Generator = generator;
            IsConfigured = isConfigured;
        }

        public IRequirementsDraftGenerator Generator { get; }
        public bool IsConfigured { get; }
    }

    /// <summary>Never reached through the API (the route checks availability first).</summary>
    private sealed class NotConfiguredGenerator : IRequirementsDraftGenerator
    {
        public Task<DraftContent?> GenerateAsync(ChangeRequest changeRequest, CancellationToken cancellationToken) =>
            throw new DraftGenerationException(GenerationFailureCategory.Unreachable);
    }
}
