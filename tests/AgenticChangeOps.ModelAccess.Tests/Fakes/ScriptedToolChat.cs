using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgenticChangeOps.ModelAccess.Tests.Fakes;

/// <summary>
/// A stand-in chat client that follows a script, one step per model call, and records what it was
/// shown on every call: the instructions, the tool names and every message.
/// </summary>
public sealed class ScriptedToolChat : IChatClient
{
    public abstract record Step;

    public sealed record AsksForTool(string ToolName, string ArgumentsJson = "{}") : Step;

    public sealed record Answers(string Text) : Step;

    public sealed record Waits : Step;

    private readonly Func<int, Step> _script;
    private int _calls;

    public ScriptedToolChat(Func<int, Step> script)
    {
        _script = script;
    }

    /// <summary>Always asks for the same tool, forever.</summary>
    public static ScriptedToolChat AlwaysAsks(string toolName = "read_file") => new(_ => new AsksForTool(toolName));

    /// <summary>Asks for a tool until call number <paramref name="finalCall"/> (1-based), then answers.</summary>
    public static ScriptedToolChat AsksThenAnswers(int finalCall, string text, string toolName = "read_file") =>
        new(call => call < finalCall ? new AsksForTool(toolName) : new Answers(text));

    public int Calls => _calls;

    public List<string> InstructionsSeen { get; } = new();

    public List<string[]> ToolNamesSeen { get; } = new();

    public List<string> NonToolMessageTextSeen { get; } = new();

    public List<string> ToolResultTextSeen { get; } = new();

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var call = Interlocked.Increment(ref _calls);
        var all = messages.ToList();

        InstructionsSeen.Add(
            string.Join("\n", all.Where(m => m.Role == ChatRole.System).Select(m => m.Text))
            + "\n" + (options?.Instructions ?? string.Empty));
        ToolNamesSeen.Add(options?.Tools?.Select(t => t.Name).ToArray() ?? Array.Empty<string>());
        foreach (var message in all)
        {
            foreach (var content in message.Contents)
            {
                if (content is FunctionResultContent result)
                    ToolResultTextSeen.Add(result.Result?.ToString() ?? string.Empty);
            }

            if (message.Role != ChatRole.System && message.Contents.All(c => c is TextContent))
                NonToolMessageTextSeen.Add(message.Text);
        }

        switch (_script(call))
        {
            case AsksForTool ask:
                var arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(ask.ArgumentsJson)
                    ?? new Dictionary<string, object?>();
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, new List<AIContent>
                {
                    new FunctionCallContent("call-" + call, ask.ToolName, arguments)
                }));

            case Waits:
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("unreachable");

            default:
                var answer = (Answers)_script(call);
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, answer.Text));
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
