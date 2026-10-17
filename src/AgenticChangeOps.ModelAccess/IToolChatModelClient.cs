namespace AgenticChangeOps.ModelAccess;

/// <summary>
/// The only way production code makes a model request that may use tools. Implementations return
/// an answer or throw <see cref="DraftGenerationException"/> with a category.
/// </summary>
public interface IToolChatModelClient
{
    Task<ChatModelResponse> CompleteAsync(
        string systemInstruction,
        string userContent,
        int maxOutputTokens,
        IToolSession session,
        CancellationToken cancellationToken);
}
