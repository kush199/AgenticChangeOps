using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticChangeOps.Intake.AzureOpenAI;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;
using Microsoft.AspNetCore.Http.Json;

namespace AgenticChangeOps.Api.Composition;

public static class ApiServiceRegistration
{
    /// <summary>
    /// Registers the intake, approval and drafting services over one in-memory store. Model
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

        services.AddSingleton<IChangeRequestStore, InMemoryChangeRequestStore>();
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

        return services;
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
