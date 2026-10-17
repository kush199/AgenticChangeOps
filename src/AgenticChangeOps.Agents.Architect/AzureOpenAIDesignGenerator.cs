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

    private readonly IToolSessionFactory? _toolSessions;
    private readonly IToolChatModelClient? _toolClient;

    public AzureOpenAIDesignGenerator(AzureOpenAIGeneratorOptions options, IChatModelClient client)
    {
        _caller = new ModelCaller(options, client);
    }

    /// <summary>
    /// The same generator with the sample codebase tools switched on. Without this constructor no
    /// tool is ever offered and the generator behaves exactly as before.
    /// </summary>
    public AzureOpenAIDesignGenerator(
        AzureOpenAIGeneratorOptions options,
        IChatModelClient client,
        IToolSessionFactory toolSessions,
        IToolChatModelClient toolClient)
        : this(options, client)
    {
        ArgumentNullException.ThrowIfNull(toolSessions);
        ArgumentNullException.ThrowIfNull(toolClient);
        _toolSessions = toolSessions;
        _toolClient = toolClient;
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
        if (_toolSessions is not null && _toolClient is not null)
            return await GenerateWithToolsAsync(changeRequest, basis, cancellationToken).ConfigureAwait(false);

        var (system, user) = DesignPromptBuilder.Build(changeRequest, basis);

        var text = await _caller.GetAnswerTextAsync(system, user, cancellationToken).ConfigureAwait(false);

        return DesignResponseParser.Parse(text);
    }

    private async Task<DesignContent?> GenerateWithToolsAsync(
        ChangeRequest changeRequest, RequirementsBasis basis, CancellationToken cancellationToken)
    {
        // Opening can only fail as ToolsUnavailable, and does so before any model call is made.
        await using var session = await _toolSessions!.OpenAsync(cancellationToken).ConfigureAwait(false);

        var (system, user) = DesignPromptBuilder.BuildWithTools(changeRequest, basis);

        var text = await _caller
            .GetAnswerTextAsync(system, user, session, _toolClient!, cancellationToken)
            .ConfigureAwait(false);

        var content = DesignResponseParser.Parse(text);

        // The list and the calls come from the session's own records, never from the model's text.
        return content with { SampleFilesRead = session.FilesRead.ToArray(), ToolCalls = session.Calls.ToArray() };
    }
}
