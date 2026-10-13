namespace AgenticChangeOps.ModelAccess;

/// <summary>
/// Makes the single, time-limited model call for a generator and reports every failure as a
/// <see cref="DraftGenerationException"/> carrying a category and nothing else. Makes one
/// model call per request and never retries.
/// </summary>
public sealed class ModelCaller
{
    // About three characters per token is a conservative bound for English prose.
    private const int CharactersPerToken = 3;

    private readonly AzureOpenAIGeneratorOptions _options;
    private readonly IChatModelClient _client;

    public ModelCaller(AzureOpenAIGeneratorOptions options, IChatModelClient client)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(client);

        _options = options;
        _client = client;
    }

    public async Task<string> GetAnswerTextAsync(
        string systemInstruction, string userContent, CancellationToken cancellationToken)
    {
        var maxOutputTokens = (_options.MaxResponseCharacters + CharactersPerToken - 1) / CharactersPerToken;

        var response = await CallModelAsync(systemInstruction, userContent, maxOutputTokens, cancellationToken)
            .ConfigureAwait(false);

        if (response.WasCutOff || response.Text.Length > _options.MaxResponseCharacters)
            throw new DraftGenerationException(GenerationFailureCategory.ResponseTooLarge);

        if (string.IsNullOrWhiteSpace(response.Text))
            throw new DraftGenerationException(GenerationFailureCategory.MalformedOutput);

        return response.Text;
    }

    private async Task<ChatModelResponse> CallModelAsync(
        string system, string user, int maxOutputTokens, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(_options.RequestTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            return await _client.CompleteAsync(system, user, maxOutputTokens, linked.Token).ConfigureAwait(false);
        }
        catch (DraftGenerationException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new DraftGenerationException(GenerationFailureCategory.TimedOut);
        }
        catch (Exception ex)
        {
            // The original exception is deliberately not attached: its message can carry the
            // endpoint, deployment name or request details.
            throw new DraftGenerationException(ModelFailureMapper.FromException(ex));
        }
    }
}
