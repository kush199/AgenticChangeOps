using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Developer.Tests.Fakes;

/// <summary>
/// Stand-in content generator: never calls a model. Behaviour is configurable per test and every
/// call is counted so tests can prove the generator was (or was not) used.
/// </summary>
public sealed class FakeProposalGenerator : IProposalGenerator
{
    private int _callCount;

    public int CallCount => _callCount;

    public ChangeRequest? LastRequest { get; private set; }
    public ProposalRequirementsBasis? LastRequirements { get; private set; }
    public ProposalDesignBasis? LastDesign { get; private set; }

    public Func<CancellationToken, Task<ProposalContent?>> Behavior { get; set; } =
        _ => Task.FromResult<ProposalContent?>(ValidContent());

    public Task<ProposalContent?> GenerateAsync(
        ChangeRequest changeRequest,
        ProposalRequirementsBasis requirements,
        ProposalDesignBasis design,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        LastRequest = changeRequest;
        LastRequirements = requirements;
        LastDesign = design;
        return Behavior(cancellationToken);
    }

    public static Func<CancellationToken, Task<ProposalContent?>> Returns(ProposalContent content) =>
        _ => Task.FromResult<ProposalContent?>(content);

    public static Func<CancellationToken, Task<ProposalContent?>> Throws(Exception exception) =>
        _ => Task.FromException<ProposalContent?>(exception);

    public static Func<CancellationToken, Task<ProposalContent?>> ReturnsNull() =>
        _ => Task.FromResult<ProposalContent?>(null);

    /// <summary>The path and text the valid content edits; the fake sample holds this file.</summary>
    public const string SettingsPath = "billing-portal/config/portal.settings";
    public const string SettingsText = "report_hour = 2\r\nreport_format = summary\r\n";

    /// <summary>One valid change against <see cref="SettingsPath"/>, with fictional prose.</summary>
    public static ProposalContent ValidContent() => new(
        Summary: "Move the nightly report hour from 2 to 4 in the portal settings.",
        Changes: new[]
        {
            new ProposedChangeContent(SettingsPath, "change", "report_hour = 2", "report_hour = 4")
        },
        Risks: new[] { "Readers get the nightly report two hours later." },
        TestIdeas: new[] { "Check the settings parse with the new hour." },
        OpenQuestions: new[] { "Should weekends keep the old hour?" },
        SampleFilesRead: new[] { SettingsPath },
        ToolCalls: null);

    /// <summary>A complete answer in the JSON shape the real model is asked for.</summary>
    public static string ValidAnswerJson() => """
        {
          "summary": "Move the nightly report hour from 2 to 4 in the portal settings.",
          "changes": [
            { "path": "billing-portal/config/portal.settings", "kind": "change",
              "existingText": "report_hour = 2", "replacementText": "report_hour = 4" }
          ],
          "risks": [ "Readers get the nightly report two hours later." ],
          "testIdeas": [ "Check the settings parse with the new hour." ],
          "openQuestions": [ "Should weekends keep the old hour?" ]
        }
        """;

}
