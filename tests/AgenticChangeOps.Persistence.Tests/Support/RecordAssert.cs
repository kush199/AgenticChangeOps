using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Persistence.Tests.Support;

/// <summary>
/// Member-wise comparison. Records compare list members by reference, so plain record equality
/// would fail for a correct round-trip of a record that holds a list.
/// </summary>
public static class RecordAssert
{
    public static void Same(ChangeRequest? expected, ChangeRequest? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected, actual);
        Assert.Equal(expected!.SubmittedAt.Ticks, actual!.SubmittedAt.Ticks);
        Assert.Equal(expected.SubmittedAt.Kind, actual.SubmittedAt.Kind);
    }

    public static void Same(ChangeRequestAuditEntry? expected, ChangeRequestAuditEntry? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected, actual);
        Assert.Equal(expected!.OccurredAt.Ticks, actual!.OccurredAt.Ticks);
        Assert.Equal(expected.OccurredAt.Kind, actual.OccurredAt.Kind);
    }

    public static void Same(IReadOnlyList<ChangeRequestAuditEntry> expected, IReadOnlyList<ChangeRequestAuditEntry> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
            Same(expected[i], actual[i]);
    }

    public static void Same(FakeAgentOutput? expected, FakeAgentOutput? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected!.OutputId, actual!.OutputId);
        Assert.Equal(expected.ChangeRequestId, actual.ChangeRequestId);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.GeneratedAt.Ticks, actual.GeneratedAt.Ticks);
        Assert.Equal(expected.GeneratedAt.Kind, actual.GeneratedAt.Kind);
        Assert.Equal(expected.Priority, actual.Priority);
        Assert.Equal(expected.Summary, actual.Summary);
        Assert.True(expected.Items.SequenceEqual(actual.Items), "List members differ.");
    }

    public static void Same(IReadOnlyList<FakeAgentOutput> expected, IReadOnlyList<FakeAgentOutput> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
            Same(expected[i], actual[i]);
    }
}
