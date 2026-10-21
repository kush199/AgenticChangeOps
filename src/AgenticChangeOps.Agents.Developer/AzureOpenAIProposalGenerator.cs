using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Developer;

/// <summary>
/// Produces proposal content through the shared model call with the read-only sample tools, which
/// are required: there is no tool-less path. Generates content only: it never changes status,
/// stores anything or takes any decision. One model conversation per request, never retried; every
/// failure is a <see cref="DraftGenerationException"/> carrying a category and nothing else.
/// </summary>
public sealed class AzureOpenAIProposalGenerator : IProposalGenerator
{
    private readonly ModelCaller _caller;
    private readonly IToolSessionFactory _toolSessions;
    private readonly IToolChatModelClient _toolClient;

    public AzureOpenAIProposalGenerator(
        AzureOpenAIGeneratorOptions options,
        IChatModelClient client,
        IToolSessionFactory toolSessions,
        IToolChatModelClient toolClient)
    {
        ArgumentNullException.ThrowIfNull(toolSessions);
        ArgumentNullException.ThrowIfNull(toolClient);
        _caller = new ModelCaller(options, client);
        _toolSessions = toolSessions;
        _toolClient = toolClient;
    }

    public async Task<ProposalContent?> GenerateAsync(
        ChangeRequest changeRequest,
        ProposalRequirementsBasis requirements,
        ProposalDesignBasis design,
        CancellationToken cancellationToken)
    {
        // Opening can only fail as ToolsUnavailable, and does so before any model call is made.
        await using var session = await _toolSessions.OpenAsync(cancellationToken).ConfigureAwait(false);

        var (system, user) = ProposalPromptBuilder.Build(changeRequest, requirements, design);

        var text = await _caller
            .GetAnswerTextAsync(system, user, session, _toolClient, cancellationToken)
            .ConfigureAwait(false);

        var content = ProposalResponseParser.Parse(text);

        // The list and the calls come from the session's own records, never from the model's text.
        return content with { SampleFilesRead = session.FilesRead.ToArray(), ToolCalls = session.Calls.ToArray() };
    }
}
