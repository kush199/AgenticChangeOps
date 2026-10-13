namespace AgenticChangeOps.Intake.AzureOpenAI;

/// <summary>The answer from a chat model call. WasCutOff is true when the service stopped at the output limit.</summary>
public sealed record ChatModelResponse(string Text, bool WasCutOff);

/// <summary>
/// The only way production code reaches a language model. Implementations return an answer or
/// throw <see cref="AgenticChangeOps.Intake.Models.DraftGenerationException"/> with a category.
/// </summary>
public interface IChatModelClient
{
    Task<ChatModelResponse> CompleteAsync(
        string systemInstruction,
        string userContent,
        int maxOutputTokens,
        CancellationToken cancellationToken);
}
