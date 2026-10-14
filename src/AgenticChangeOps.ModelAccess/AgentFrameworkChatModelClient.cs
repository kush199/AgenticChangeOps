using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgenticChangeOps.ModelAccess;

/// <summary>
/// Runs one model request through the Microsoft Agent Framework's agent abstraction. It is a
/// content generator only: the agent has no tools, no session and no memory, and a request is
/// built entirely from its own arguments, so no request can see another. Makes exactly one
/// attempt and reports every failure as a <see cref="DraftGenerationException"/> carrying a
/// category only; no service, identity or model text is passed on and nothing is logged.
/// </summary>
public sealed class AgentFrameworkChatModelClient : IChatModelClient
{
    private readonly IChatClient _chat;

    public AgentFrameworkChatModelClient(AzureOpenAIGeneratorOptions options)
        : this(options, new AzureOpenAIClientOptions(), new DefaultAzureCredential())
    {
    }

    // Lets tests supply a fake transport and credential; production uses the public constructor.
    internal AgentFrameworkChatModelClient(
        AzureOpenAIGeneratorOptions options, AzureOpenAIClientOptions clientOptions, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);
        ArgumentNullException.ThrowIfNull(credential);

        // One attempt per model request; the caller may simply ask again. Set here, not by the
        // caller, so the guarantee belongs to this client.
        clientOptions.RetryPolicy = new ClientRetryPolicy(0);

        var client = new AzureOpenAIClient(options.Endpoint, credential, clientOptions);

        // No middleware is added to the chat client: nothing here can retry, call a tool, cache
        // or log.
        _chat = client.GetChatClient(options.DeploymentName).AsIChatClient();
    }

    public async Task<ChatModelResponse> CompleteAsync(
        string systemInstruction,
        string userContent,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        // A fresh agent per request, built from this request's own instruction and given no
        // session: nothing is carried from one request to the next.
        var agent = new ChatClientAgent(_chat, instructions: systemInstruction);

        var runOptions = new ChatClientAgentRunOptions(new ChatOptions
        {
            MaxOutputTokens = maxOutputTokens,
            ResponseFormat = ChatResponseFormat.Json
        });

        try
        {
            AgentResponse response = await agent
                .RunAsync(userContent, session: null, runOptions, cancellationToken)
                .ConfigureAwait(false);

            if (response.FinishReason == ChatFinishReason.ContentFilter)
                throw new DraftGenerationException(GenerationFailureCategory.MalformedOutput);

            return new ChatModelResponse(response.Text ?? string.Empty, response.FinishReason == ChatFinishReason.Length);
        }
        catch (DraftGenerationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // Left to the caller, which knows whether the caller or the time limit cancelled.
            throw;
        }
        catch (Exception ex)
        {
            // The exception itself is dropped: its message can carry the endpoint, deployment
            // name or identity detail. Only a status or a content-filter marker is read from it.
            var service = FindServiceError(ex);
            if (service is not null)
            {
                var errorCode = ContentFilterProbe.ResponseMentions(service) ? ContentFilterProbe.Marker : null;
                throw new DraftGenerationException(ModelFailureMapper.FromError(service.Status, errorCode));
            }

            throw new DraftGenerationException(ModelFailureMapper.FromException(ex));
        }
    }

    // The framework may wrap what the service client raised; look through the chain for it.
    private static ClientResultException? FindServiceError(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ClientResultException service)
                return service;
        }

        return null;
    }
}
