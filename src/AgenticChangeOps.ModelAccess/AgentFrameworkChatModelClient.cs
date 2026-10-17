using System.ClientModel;
using System.Text.Json;
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

        _chat = CreateChatClient(options, clientOptions, credential);
    }

    // Shared with the tool-capable client so both build the service client the same way.
    internal static IChatClient CreateChatClient(
        AzureOpenAIGeneratorOptions options, AzureOpenAIClientOptions clientOptions, TokenCredential credential)
    {
        // One attempt per model request; the caller may simply ask again. Set here, not by the
        // caller, so the guarantee belongs to this client.
        clientOptions.RetryPolicy = new ClientRetryPolicy(0);

        var client = new AzureOpenAIClient(options.Endpoint, credential, clientOptions);

        // No middleware is added to the chat client: nothing here can retry, call a tool, cache
        // or log.
        return client.GetChatClient(options.DeploymentName).AsIChatClient();
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

/// <summary>
/// Runs one model request, with the session's tools, through the Microsoft Agent Framework. Lives
/// in this file because this is the one file that may name framework types. The tool set is fixed
/// by the session; a tool result is only ever handed back to the model as a tool result. The
/// number of model calls is limited exactly here (the framework's own cap allows one extra call
/// and ends quietly), a failed tool call is reported to the model as fixed text and never
/// retried, and every failure reaches the caller as a <see cref="DraftGenerationException"/>
/// carrying a category only.
/// </summary>
public sealed class AgentFrameworkToolChatModelClient : IToolChatModelClient
{
    private const string ToolFailedText = "The tool call failed.";

    private static readonly TimeSpan DefaultCallTimeout = AzureOpenAIGeneratorOptions.DefaultRequestTimeout;

    private readonly IChatClient _inner;
    private readonly TimeSpan _callTimeout;

    public AgentFrameworkToolChatModelClient(AzureOpenAIGeneratorOptions options)
        : this(options, new AzureOpenAIClientOptions(), new DefaultAzureCredential())
    {
    }

    // Lets tests supply a fake transport and credential; production uses the public constructor.
    internal AgentFrameworkToolChatModelClient(
        AzureOpenAIGeneratorOptions options, AzureOpenAIClientOptions clientOptions, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);
        ArgumentNullException.ThrowIfNull(credential);

        _inner = AgentFrameworkChatModelClient.CreateChatClient(options, clientOptions, credential);
        _callTimeout = options.RequestTimeout;
    }

    // Lets tests supply a scripted chat client.
    internal AgentFrameworkToolChatModelClient(IChatClient inner, TimeSpan? callTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _callTimeout = callTimeout ?? DefaultCallTimeout;
    }

    public async Task<ChatModelResponse> CompleteAsync(
        string systemInstruction,
        string userContent,
        int maxOutputTokens,
        IToolSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        using var timeLimit = new CancellationTokenSource(session.TimeLimit);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeLimit.Token);

        var counting = new CountingChatClient(_inner, session.MaxModelCalls, _callTimeout);

        // The framework's own cap is only a backstop; the exact limit is enforced by the wrapper.
        IChatClient chat = new ChatClientBuilder(counting)
            .UseFunctionInvocation(configure: f => f.MaximumIterationsPerRequest = Math.Max(1, session.MaxModelCalls))
            .Build();

        var tools = session.Tools.Definitions
            .Select(d => (AITool)new ToolFunction(d, session.Tools))
            .ToList();

        var agent = new ChatClientAgent(chat, new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = systemInstruction,
                Tools = tools,
                MaxOutputTokens = maxOutputTokens,
                ResponseFormat = ChatResponseFormat.Json
            },
            UseProvidedChatClientAsIs = true
        });

        try
        {
            AgentResponse response = await agent
                .RunAsync(userContent, session: null, options: null, linked.Token)
                .ConfigureAwait(false);

            if (response.FinishReason == ChatFinishReason.ContentFilter)
                throw new DraftGenerationException(GenerationFailureCategory.MalformedOutput);

            var text = response.Text ?? string.Empty;

            // The framework ends quietly when its cap is hit while the model still wants a tool.
            if (text.Length == 0 && response.Messages.Any(m => m.Contents.Any(c => c is FunctionCallContent)))
                throw new DraftGenerationException(GenerationFailureCategory.LimitsExhausted);

            return new ChatModelResponse(text, response.FinishReason == ChatFinishReason.Length);
        }
        catch (DraftGenerationException)
        {
            throw;
        }
        catch (ModelCallLimitReachedException)
        {
            throw new DraftGenerationException(GenerationFailureCategory.LimitsExhausted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up: not a limit.
            throw;
        }
        catch (OperationCanceledException) when (timeLimit.IsCancellationRequested)
        {
            throw new DraftGenerationException(GenerationFailureCategory.LimitsExhausted);
        }
        catch (Exception ex)
        {
            // The exception itself is dropped: its message can carry the endpoint, deployment
            // name or identity detail.
            var service = FindServiceError(ex);
            if (service is not null)
            {
                var errorCode = ContentFilterProbe.ResponseMentions(service) ? ContentFilterProbe.Marker : null;
                throw new DraftGenerationException(ModelFailureMapper.FromError(service.Status, errorCode));
            }

            throw new DraftGenerationException(ModelFailureMapper.FromException(ex));
        }
    }

    private static ClientResultException? FindServiceError(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ClientResultException service)
                return service;
        }

        return null;
    }

    private sealed class ModelCallLimitReachedException : Exception
    {
    }

    // Counts model calls exactly, and gives each its own time limit.
    private sealed class CountingChatClient : DelegatingChatClient
    {
        private readonly int _limit;
        private readonly TimeSpan _callTimeout;
        private int _calls;

        public CountingChatClient(IChatClient inner, int limit, TimeSpan callTimeout)
            : base(inner)
        {
            _limit = limit;
            _callTimeout = callTimeout;
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) > _limit)
                throw new ModelCallLimitReachedException();

            using var perCall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            perCall.CancelAfter(_callTimeout);

            try
            {
                return await base.GetResponseAsync(messages, options, perCall.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && perCall.IsCancellationRequested)
            {
                throw new DraftGenerationException(GenerationFailureCategory.TimedOut);
            }
        }
    }

    // One offered tool, run through the session's tool set. A tool that fails is reported to the
    // model as fixed text: the exception, which could carry a path, goes no further.
    private sealed class ToolFunction : AIFunction
    {
        private readonly ToolDefinition _definition;
        private readonly ToolSet _tools;

        public ToolFunction(ToolDefinition definition, ToolSet tools)
        {
            _definition = definition;
            _tools = tools;
        }

        public override string Name => _definition.Name;

        public override string Description => _definition.Description;

        public override JsonElement JsonSchema => _definition.InputSchema;

        protected override async ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            try
            {
                var json = JsonSerializer.Serialize(arguments.ToDictionary(a => a.Key, a => a.Value));
                return await _tools.InvokeAsync(Name, json, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return ToolFailedText;
            }
        }
    }
}
