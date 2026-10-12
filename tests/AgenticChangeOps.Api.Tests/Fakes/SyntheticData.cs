using AgenticChangeOps.Api.Contracts;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>Fully synthetic request bodies; no real names, systems or identifiers.</summary>
public static class SyntheticData
{
    public static SubmitChangeRequestBody ValidSubmission() => new(
        Title: "Rotate demo cache settings",
        Description: "Adjust the demo cache time-to-live for the sample catalogue.",
        Requester: "Alex Example",
        AffectedSystem: "Sample Catalogue",
        Priority: "Medium",
        ChangeType: "ConfigChange");

    public static DecisionBody Approve() => new("Sam Sample", "Approve", null);

    public static DecisionBody Defer() => new("Sam Sample", "Defer", "Needs a clearer rollback plan.");
}
