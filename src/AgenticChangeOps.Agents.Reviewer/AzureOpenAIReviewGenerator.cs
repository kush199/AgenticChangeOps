using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Reviewer;

/// <summary>
/// Produces review content by asking a language model. Generates content only. Makes one model
/// call per request and never retries; every failure is reported as a
/// <see cref="DraftGenerationException"/> carrying a category and nothing else. There is no tool
/// path: the only construction takes the options and a plain chat client, so the generator can
/// neither offer nor use a tool. The call itself is shared (<see cref="ModelCaller"/>); this type
/// supplies the prompt and reads the answer into this agent's content.
/// </summary>
public sealed class AzureOpenAIReviewGenerator : IReviewGenerator
{
    private readonly ModelCaller _caller;

    public AzureOpenAIReviewGenerator(AzureOpenAIGeneratorOptions options, IChatModelClient client)
    {
        _caller = new ModelCaller(options, client);
    }

    public async Task<ReviewContent?> GenerateAsync(
        ChangeRequest changeRequest,
        ReviewRequirementsBasis requirements,
        ReviewDesignBasis design,
        ReviewProposalBasis proposal,
        SystemCheck systemCheck,
        CancellationToken cancellationToken)
    {
        var (system, user) = ReviewPromptBuilder.Build(changeRequest, requirements, design, proposal, systemCheck);

        var text = await _caller.GetAnswerTextAsync(system, user, cancellationToken).ConfigureAwait(false);

        return ReviewResponseParser.Parse(text);
    }
}
