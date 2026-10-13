namespace AgenticChangeOps.ModelAccess.Tests.Fakes;

/// <summary>
/// Stand-in model client for the shared-code tests: never touches a network, records what it
/// was asked, and behaves as the test scripts it. Holds no agent-specific sample text.
/// </summary>
public sealed class ScriptedChatModelClient : IChatModelClient
{
    private int _callCount;

    public int CallCount => _callCount;
    public string? SystemInstruction { get; private set; }
    public string? UserContent { get; private set; }
    public int MaxOutputTokens { get; private set; }

    public Func<CancellationToken, Task<ChatModelResponse>> Behavior { get; set; } =
        _ => Task.FromResult(new ChatModelResponse(string.Empty, false));

    public Task<ChatModelResponse> CompleteAsync(
        string systemInstruction, string userContent, int maxOutputTokens, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        SystemInstruction = systemInstruction;
        UserContent = userContent;
        MaxOutputTokens = maxOutputTokens;
        return Behavior(cancellationToken);
    }

    public static Func<CancellationToken, Task<ChatModelResponse>> ReturnsText(string text, bool wasCutOff = false) =>
        _ => Task.FromResult(new ChatModelResponse(text, wasCutOff));

    public static Func<CancellationToken, Task<ChatModelResponse>> Throws(Exception exception) =>
        _ => Task.FromException<ChatModelResponse>(exception);

    public static Func<CancellationToken, Task<ChatModelResponse>> DelaysUntilCancelled() =>
        async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new ChatModelResponse(string.Empty, false);
        };
}
