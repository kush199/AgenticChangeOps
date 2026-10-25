using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Fakes;

/// <summary>
/// Scripted plain chat client: no tools exist on this seam. Counts calls, remembers what it was
/// asked, and answers with a valid review by default.
/// </summary>
public sealed class FakeReviewChatClient : IChatModelClient
{
    public const string ValidAnswerJson = """
        {
          "findings": [
            {
              "category": "AcceptanceCriteria",
              "severity": "Medium",
              "statement": "No test covers the old hour.",
              "references": ["requirements.acceptanceCriteria[2]", "proposal.testIdeas[1]"]
            },
            {
              "category": "UnmentionedRisk",
              "severity": "Low",
              "statement": "The notes file may drift from the settings.",
              "references": ["proposal.risks[2]"]
            }
          ],
          "openQuestions": ["Who owns the notes file?"]
        }
        """;

    public int CallCount { get; private set; }

    public string? SystemInstruction { get; private set; }

    public string? UserContent { get; private set; }

    public int? MaxOutputTokens { get; private set; }

    /// <summary>What to do for a call. Defaults to the valid answer.</summary>
    public Func<Task<ChatModelResponse>> Behavior { get; set; } =
        () => Task.FromResult(new ChatModelResponse(ValidAnswerJson, false));

    public Task<ChatModelResponse> CompleteAsync(
        string systemInstruction, string userContent, int maxOutputTokens, CancellationToken cancellationToken)
    {
        CallCount++;
        SystemInstruction = systemInstruction;
        UserContent = userContent;
        MaxOutputTokens = maxOutputTokens;
        return Behavior();
    }

    public FakeReviewChatClient Answering(string text, bool cutOff = false)
    {
        Behavior = () => Task.FromResult(new ChatModelResponse(text, cutOff));
        return this;
    }

    public FakeReviewChatClient Throwing(Exception exception)
    {
        Behavior = () => throw exception;
        return this;
    }
}

/// <summary>Synthetic change requests for the model-facing tests.</summary>
public static class ReviewTestRequests
{
    public static ChangeRequest Request(string description = "Move the nightly synthetic report job to a quieter window.") => new(
        ChangeRequestId: Guid.Parse("00000000-0000-0000-0000-0000000000c3"),
        Title: "Move nightly report schedule",
        Description: description,
        Requester: "Avery Quill",
        AffectedSystem: "Sample Billing Portal",
        Priority: Priority.Medium,
        ChangeType: ChangeType.ConfigChange,
        Status: ChangeRequestStatus.Approved,
        SubmittedAt: DateTime.UnixEpoch);

    public static SystemCheck Performed(params EditCheckOutcome[] outcomes) => new(
        SystemCheckStatus.Performed,
        outcomes.Select((o, i) => new EditCheck(i + 1, o)).ToArray());

    public static SystemCheck NotPerformed() => new(SystemCheckStatus.NotPerformed, Array.Empty<EditCheck>());

    public static AzureOpenAIGeneratorOptions Options() => new(
        new Uri("https://sentinel-endpoint.invalid/"), "sentinel-deployment", TimeSpan.FromSeconds(30), 32000);
}
