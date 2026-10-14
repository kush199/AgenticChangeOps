using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using OpenAI.Chat;

namespace AgenticChangeOps.ModelAccess;

/// <summary>
/// The only place that talks to the model service. Authenticates with the operator's cloud
/// identity (no key anywhere), makes exactly one attempt, asks for a JSON object, and reports
/// every failure as a <see cref="DraftGenerationException"/> carrying a category only. Nothing
/// is logged and no service or identity message is passed on.
/// </summary>
public sealed class AzureOpenAIChatModelClient : IChatModelClient
{
    private readonly ChatClient _chat;

    public AzureOpenAIChatModelClient(AzureOpenAIGeneratorOptions options)
        : this(
            options,
            new AzureOpenAIClientOptions
            {
                // One attempt per model request; the caller may simply ask again.
                RetryPolicy = new ClientRetryPolicy(0)
            },
            new DefaultAzureCredential())
    {
    }

    // Lets tests supply a fake transport and credential; production uses the public constructor.
    internal AzureOpenAIChatModelClient(
        AzureOpenAIGeneratorOptions options, AzureOpenAIClientOptions clientOptions, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(options);

        var client = new AzureOpenAIClient(options.Endpoint, credential, clientOptions);
        _chat = client.GetChatClient(options.DeploymentName);
    }

    public async Task<ChatModelResponse> CompleteAsync(
        string systemInstruction,
        string userContent,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        var completionOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = maxOutputTokens,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        try
        {
            ChatCompletion completion = (await _chat.CompleteChatAsync(
                new ChatMessage[] { new SystemChatMessage(systemInstruction), new UserChatMessage(userContent) },
                completionOptions,
                cancellationToken).ConfigureAwait(false)).Value;

            if (completion.FinishReason == ChatFinishReason.ContentFilter)
                throw new DraftGenerationException(GenerationFailureCategory.MalformedOutput);

            var text = completion.Content.Count > 0 ? completion.Content[0].Text ?? string.Empty : string.Empty;
            return new ChatModelResponse(text, completion.FinishReason == ChatFinishReason.Length);
        }
        catch (DraftGenerationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // Left to the generator, which knows whether the caller or the time limit cancelled.
            throw;
        }
        catch (ClientResultException ex)
        {
            var errorCode = ContentFilterProbe.ResponseMentions(ex) ? ContentFilterProbe.Marker : null;
            throw new DraftGenerationException(ModelFailureMapper.FromError(ex.Status, errorCode));
        }
        catch (Exception ex)
        {
            // Identity, network and any other failure. The exception itself is dropped: its
            // message can carry the endpoint, deployment name or identity detail.
            throw new DraftGenerationException(ModelFailureMapper.FromException(ex));
        }
    }
}
