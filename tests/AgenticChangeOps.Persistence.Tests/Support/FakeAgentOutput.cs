using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Persistence.Tests.Support;

/// <summary>A test-only output of an agent the storage layer has never heard of.</summary>
public sealed record FakeAgentOutput(
    Guid OutputId,
    Guid ChangeRequestId,
    int Version,
    DateTime GeneratedAt,
    Priority Priority,
    string Summary,
    IReadOnlyList<string> Items) : IAgentOutput;

/// <summary>A second, unrelated output type, to prove versions are kept per type.</summary>
public sealed record OtherFakeOutput(
    Guid ChangeRequestId,
    int Version,
    string Note) : IAgentOutput;
