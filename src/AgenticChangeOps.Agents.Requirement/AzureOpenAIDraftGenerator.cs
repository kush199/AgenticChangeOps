using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Requirement;

/// <summary>
/// Produces draft content by asking a language model. Generates content only: it never changes
/// status, stores anything, or takes any decision. Makes one model call per request and never
/// retries; every failure is reported as a <see cref="DraftGenerationException"/> carrying a
/// category and nothing else. The call itself is shared (<see cref="ModelCaller"/>); this type
/// supplies the prompt and reads the answer into this agent's content.
/// </summary>
public sealed class AzureOpenAIDraftGenerator : IRequirementsDraftGenerator
{
    private readonly ModelCaller _caller;

    public AzureOpenAIDraftGenerator(AzureOpenAIGeneratorOptions options, IChatModelClient client)
    {
        _caller = new ModelCaller(options, client);
    }

    public static AzureOpenAIDraftGenerator CreateFromEnvironment()
    {
        // Validated once, here, before any draft request can be made. No key variable exists:
        // the client authenticates with the operator's cloud identity.
        var options = AzureOpenAIGeneratorOptions.FromEnvironment(Environment.GetEnvironmentVariable);
        return new AzureOpenAIDraftGenerator(options, new AzureOpenAIChatModelClient(options));
    }

    public async Task<DraftContent?> GenerateAsync(ChangeRequest changeRequest, CancellationToken cancellationToken)
    {
        var (system, user) = DraftPromptBuilder.Build(changeRequest);

        var text = await _caller.GetAnswerTextAsync(system, user, cancellationToken).ConfigureAwait(false);

        return DraftResponseParser.Parse(text);
    }
}
