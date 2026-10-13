using System.Text.Json;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Requirement.Tests;

/// <summary>
/// Slice 007 (FR-012): types that moved projects keep their members, numeric values and
/// serialized property names. The literals below were taken from the pre-move source.
/// </summary>
public sealed class MovedTypeShapeTests
{
    [Fact]
    public void DraftStatus_KeepsItsMembersAndValues()
    {
        var actual = Enum.GetValues<DraftStatus>().Select(v => (v.ToString(), (int)v)).ToArray();

        Assert.Equal(new[] { ("Draft", 0) }, actual);
    }

    [Fact]
    public void GenerationFailureCategory_KeepsItsMembersAndValues()
    {
        var actual = Enum.GetValues<GenerationFailureCategory>().Select(v => (v.ToString(), (int)v)).ToArray();

        Assert.Equal(
            new[] { ("Unreachable", 0), ("TimedOut", 1), ("ResponseTooLarge", 2), ("MalformedOutput", 3) },
            actual);
    }

    [Fact]
    public void RequirementsDraft_SerializesWithTheRecordedPropertyNames()
    {
        var draft = new RequirementsDraft(
            DraftId: Guid.Empty,
            ChangeRequestId: Guid.Empty,
            Version: 1,
            GeneratedAt: new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc),
            Status: DraftStatus.Draft,
            AgentName: RequirementsDraft.RequirementAgentName,
            Summary: "s",
            FunctionalRequirements: new[] { "f" },
            AcceptanceCriteria: new[] { "a" },
            Assumptions: Array.Empty<string>(),
            OpenQuestions: Array.Empty<string>());

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(draft));
        var names = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(
            new[]
            {
                "DraftId", "ChangeRequestId", "Version", "GeneratedAt", "Status", "AgentName", "Summary",
                "FunctionalRequirements", "AcceptanceCriteria", "Assumptions", "OpenQuestions"
            },
            names);
    }

    [Fact]
    public void RequirementAgentName_KeepsItsValue() =>
        Assert.Equal("RequirementAgent", RequirementsDraft.RequirementAgentName);
}
