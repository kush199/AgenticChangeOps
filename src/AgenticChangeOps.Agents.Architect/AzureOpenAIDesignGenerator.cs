using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Architect;

/// <summary>
/// Produces design content by asking a language model. Generates content only: it never changes
/// status, stores anything, or takes any decision. Makes one model call per request and never
/// retries; every failure is reported as a <see cref="DraftGenerationException"/> carrying a
/// category and nothing else. The call itself is shared (<see cref="ModelCaller"/>); this type
/// supplies the prompt and reads the answer into this agent's content. The default client
/// (<see cref="AgentFrameworkChatModelClient"/>) runs the request through the Microsoft Agent
/// Framework; the generator itself neither knows nor cares which client it is given.
/// </summary>
public sealed class AzureOpenAIDesignGenerator : IDesignDraftGenerator
{
    private readonly ModelCaller _caller;

    public AzureOpenAIDesignGenerator(AzureOpenAIGeneratorOptions options, IChatModelClient client)
    {
        _caller = new ModelCaller(options, client);
    }

    public static AzureOpenAIDesignGenerator FromProcessEnvironment()
    {
        // Validated once, here, before any request can be made. No key variable exists: the
        // client authenticates with the operator's cloud identity.
        var options = AzureOpenAIGeneratorOptions.FromEnvironment(Environment.GetEnvironmentVariable);
        return new AzureOpenAIDesignGenerator(options, new AgentFrameworkChatModelClient(options));
    }

    public async Task<DesignContent?> GenerateAsync(
        ChangeRequest changeRequest, RequirementsBasis basis, CancellationToken cancellationToken)
    {
        var (system, user) = DesignPromptBuilder.Build(changeRequest, basis);

        var text = await _caller.GetAnswerTextAsync(system, user, cancellationToken).ConfigureAwait(false);

        return DesignResponseParser.Parse(text);
    }
}
