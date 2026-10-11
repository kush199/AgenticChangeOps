using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;

namespace AgenticChangeOps.Intake.AzureOpenAI;

/// <summary>
/// Produces draft content by asking a language model. Generates content only: it never changes
/// status, stores anything, or takes any decision. Makes one model call per request and never
/// retries; every failure is reported as a <see cref="DraftGenerationException"/> carrying a
/// category and nothing else.
/// </summary>
public sealed class AzureOpenAIDraftGenerator : IRequirementsDraftGenerator
{
    // About three characters per token is a conservative bound for English prose.
    private const int CharactersPerToken = 3;

    private readonly AzureOpenAIGeneratorOptions _options;
    private readonly IChatModelClient _client;

    public AzureOpenAIDraftGenerator(AzureOpenAIGeneratorOptions options, IChatModelClient client)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(client);

        _options = options;
        _client = client;
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
        var maxOutputTokens = (_options.MaxResponseCharacters + CharactersPerToken - 1) / CharactersPerToken;

        var response = await CallModelAsync(system, user, maxOutputTokens, cancellationToken).ConfigureAwait(false);

        if (response.WasCutOff || response.Text.Length > _options.MaxResponseCharacters)
            throw new DraftGenerationException(GenerationFailureCategory.ResponseTooLarge);

        if (string.IsNullOrWhiteSpace(response.Text))
            throw new DraftGenerationException(GenerationFailureCategory.MalformedOutput);

        return DraftResponseParser.Parse(response.Text);
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
