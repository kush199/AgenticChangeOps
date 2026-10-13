using AgenticChangeOps.Intake.AzureOpenAI;

namespace AgenticChangeOps.Intake.Tests.Fakes;

/// <summary>Stand-in model client: never touches a network. Records what it was asked.</summary>
public sealed class FakeChatModelClient : IChatModelClient
{
    private int _callCount;

    public int CallCount => _callCount;
    public string? SystemInstruction { get; private set; }
    public string? UserContent { get; private set; }
    public int MaxOutputTokens { get; private set; }

    public Func<CancellationToken, Task<ChatModelResponse>> Behavior { get; set; } =
        _ => Task.FromResult(new ChatModelResponse(ValidAnswerJson(), false));

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

    /// <summary>A complete, fictional five-key answer about a schedule change.</summary>
    public static string ValidAnswerJson() => """
        {
          "summary": "Move the nightly Sample Billing Portal report job to a quieter window.",
          "functionalRequirements": [
            "The report job runs at the newly configured time each night.",
            "The previous schedule entry is removed once the new one is active."
          ],
          "acceptanceCriteria": [
            "The nightly report is produced within the new window on three consecutive nights.",
            "No report is produced at the old time."
          ],
          "assumptions": [ "The new window has no competing batch jobs." ],
          "openQuestions": [ "Should weekend runs keep the old schedule?" ]
        }
        """;
}
