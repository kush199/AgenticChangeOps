using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Persistence;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

/// <summary>
/// The typed decision record through both stores, as the other agents' round-trip tests do: saved with its
/// audit entry, read back in version order, exclusive per version, and no schema change.
/// </summary>
public sealed class DesignDecisionStorageRoundTripTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "changeops-decision-" + Guid.NewGuid().ToString("N"));

    public DesignDecisionStorageRoundTripTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string DbPath => Path.Combine(_folder, "changeops.db");

    private static DesignDecision Decision(Guid id, int version, DesignDecisionOutcome outcome, string? reason) => new(
        DecisionId: new Guid(version, 0, 0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
        ChangeRequestId: id,
        Version: version,
        DesignId: new Guid(version + 100, 0, 0, new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }),
        Outcome: outcome,
        ApproverName: "Jordan Reed é",
        Reason: reason,
        DecidedAt: new DateTime(2026, 1, 15, 9, 0, version, DateTimeKind.Utc));

    private static ChangeRequestAuditEntry Entry(Guid id, int version) => new(
        AuditEntryId: Guid.NewGuid(),
        ChangeRequestId: id,
        EventType: AuditEventType.DesignDecided,
        OccurredAt: new DateTime(2026, 1, 15, 9, 0, version, DateTimeKind.Utc),
        Details: "Accepted",
        ApproverName: "Jordan Reed é",
        DraftVersion: version);

    public static IEnumerable<object[]> Stores() => new[] { new object[] { "memory" }, new object[] { "sqlite" } };

    private IChangeRequestStore Open(string kind) =>
        kind == "memory" ? new InMemoryChangeRequestStore() : new SqliteChangeRequestStore(new SqliteStorageOptions(DbPath));

    [Theory]
    [MemberData(nameof(Stores))]
    public void ADecision_RoundTripsWithItsAuditEntry_AndKeepsEveryMember(string kind)
    {
        var store = Open(kind);
        var id = Guid.NewGuid();
        var accepted = Decision(id, 1, DesignDecisionOutcome.Accepted, null);
        var rejected = Decision(id, 2, DesignDecisionOutcome.Rejected, "Needs a rollback plan.");

        store.SaveAgentOutput(rejected, Entry(id, 2));
        store.SaveAgentOutput(accepted, Entry(id, 1));

        Assert.Equal(new[] { accepted, rejected }, store.GetAgentOutputs<DesignDecision>(id));
        Assert.Equal(2, store.GetAuditEntries(id).Count(e => e.EventType == AuditEventType.DesignDecided));
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public void ASecondDecision_OfTheSameVersion_IsRejectedByTheStore_AndStoresNeitherItemTwice(string kind)
    {
        var store = Open(kind);
        var id = Guid.NewGuid();
        store.SaveAgentOutput(Decision(id, 1, DesignDecisionOutcome.Accepted, null), Entry(id, 1));

        Assert.ThrowsAny<Exception>(() =>
            store.SaveAgentOutput(Decision(id, 1, DesignDecisionOutcome.Rejected, "No."), Entry(id, 1)));

        Assert.Equal(DesignDecisionOutcome.Accepted, Assert.Single(store.GetAgentOutputs<DesignDecision>(id)).Outcome);
        Assert.Single(store.GetAuditEntries(id));
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public void ADecision_WithAnAuditEntryOfAnotherRequest_IsRejected_AndStoresNothing(string kind)
    {
        var store = Open(kind);
        var id = Guid.NewGuid();

        Assert.ThrowsAny<Exception>(() =>
            store.SaveAgentOutput(Decision(id, 1, DesignDecisionOutcome.Accepted, null), Entry(Guid.NewGuid(), 1)));

        Assert.Empty(store.GetAgentOutputs<DesignDecision>(id));
    }

    [Fact]
    public void TheDurableStore_ReturnsTheSameDecisionsAfterARestart()
    {
        var id = Guid.NewGuid();
        var decision = Decision(id, 1, DesignDecisionOutcome.Rejected, "Not now.");
        using (var first = new SqliteChangeRequestStore(new SqliteStorageOptions(DbPath)))
            first.SaveAgentOutput(decision, Entry(id, 1));

        using var second = new SqliteChangeRequestStore(new SqliteStorageOptions(DbPath));

        Assert.Equal(decision, Assert.Single(second.GetAgentOutputs<DesignDecision>(id)));
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public void DecisionsOfOneRequest_AreNotVisibleToAnother_OrToOtherOutputTypes(string kind)
    {
        var store = Open(kind);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        store.SaveAgentOutput(Decision(a, 1, DesignDecisionOutcome.Accepted, null), Entry(a, 1));

        Assert.Empty(store.GetAgentOutputs<DesignDecision>(b));
        Assert.Empty(store.GetAgentOutputs<DesignDraft>(a));
    }
}
