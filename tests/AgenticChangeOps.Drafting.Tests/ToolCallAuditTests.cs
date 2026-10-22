using AgenticChangeOps.Drafting.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Drafting.Tests;

/// <summary>The audit entry for one tool call, proven once for every agent that uses tools.</summary>
public sealed class ToolCallAuditTests
{
    private static readonly DateTime Generated = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    private readonly ThreadSafeIds _ids = new();
    private readonly Guid _id = Guid.NewGuid();

    private Intake.Models.ChangeRequestAuditEntry Create(ToolCallRecord call, string agent = "SomeAgent") =>
        ToolCallAuditEntries.Create(agent, _ids, _id, ChangeRequestStatus.Approved, call, Generated);

    private static ToolCallRecord Call(
        string target = "orders-service/config/orders.settings",
        ToolCallOutcome outcome = ToolCallOutcome.Served,
        TimeSpan? elapsed = null,
        DateTime? startedAt = null) =>
        new("read_file", target, outcome, 321, elapsed ?? TimeSpan.FromMilliseconds(17), startedAt ?? Generated.AddSeconds(-2));

    [Fact]
    public void TheEntryCarriesTheCallsFacts_AndTheAgentNameAsGiven()
    {
        var entry = Create(Call(), "AnyAgentName");

        Assert.Equal(AuditEventType.ToolCalled, entry.EventType);
        Assert.Equal(_id, entry.ChangeRequestId);
        Assert.Equal("AnyAgentName", entry.AgentName);
        Assert.Equal("read_file", entry.ToolName);
        Assert.Equal("orders-service/config/orders.settings", entry.ToolTarget);
        Assert.Equal("Served", entry.ToolOutcome);
        Assert.Equal(321, entry.ResultBytes);
        Assert.Equal(17, entry.ElapsedMilliseconds);
        Assert.Null(entry.Details);
        Assert.Equal(ChangeRequestStatus.Approved, entry.StatusBefore);
        Assert.Equal(ChangeRequestStatus.Approved, entry.StatusAfter);
        Assert.NotEqual(Guid.Empty, entry.AuditEntryId);
    }

    [Fact]
    public void ControlCharactersAreRemovedFromTheTarget_AndItIsCutTo120Characters()
    {
        var entry = Create(Call(target: "a\u0001b\nc" + new string('x', 200)));

        Assert.DoesNotContain(entry.ToolTarget!, char.IsControl);
        Assert.Equal(120, entry.ToolTarget!.Length);
        Assert.StartsWith("abcxxx", entry.ToolTarget);
    }

    [Fact]
    public void ACallIsNeverRecordedAsHavingHappenedAfterTheOutputItBelongsTo()
    {
        Assert.Equal(Generated, Create(Call(startedAt: Generated.AddMinutes(5))).OccurredAt);
        Assert.Equal(Generated.AddSeconds(-2), Create(Call()).OccurredAt);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(1234, 1234)]
    public void ElapsedTimeIsClampedToZeroAndInt32MaxMilliseconds(double milliseconds, int expected)
    {
        Assert.Equal(expected, Create(Call(elapsed: TimeSpan.FromMilliseconds(milliseconds))).ElapsedMilliseconds);
        Assert.Equal(int.MaxValue, Create(Call(elapsed: TimeSpan.FromDays(60))).ElapsedMilliseconds);
    }

    [Fact]
    public void TheOutcomeIsRecordedByItsName()
    {
        Assert.Equal("RefusedOutsideSample", Create(Call(outcome: ToolCallOutcome.RefusedOutsideSample)).ToolOutcome);
    }
}
