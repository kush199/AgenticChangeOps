using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

/// <summary>
/// The store's agent-neutral output point (spec 007, FR-014), exercised with synthetic output
/// records that belong to no agent.
/// </summary>
public sealed class AgentOutputStoreTests
{
    private sealed record SyntheticOutput(Guid ChangeRequestId, int Version, string Text) : IAgentOutput;

    private sealed record OtherSyntheticOutput(Guid ChangeRequestId, int Version) : IAgentOutput;

    private static SyntheticOutput Output(Guid id, int version) => new(id, version, $"output {version}");

    private static ChangeRequestAuditEntry Entry(Guid id, int version) => new(
        AuditEntryId: Guid.NewGuid(),
        ChangeRequestId: id,
        EventType: AuditEventType.DraftGenerated,
        OccurredAt: new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc).AddMinutes(version),
        Details: null,
        AgentName: "SyntheticAgent",
        DraftVersion: version);

    [Fact]
    public void SaveAgentOutput_StoresOutputAndAuditEntryTogether()
    {
        var store = new InMemoryChangeRequestStore();
        var id = Guid.NewGuid();
        var output = Output(id, 1);
        var entry = Entry(id, 1);

        store.SaveAgentOutput(output, entry);

        Assert.Equal(output, Assert.Single(store.GetAgentOutputs<SyntheticOutput>(id)));
        Assert.Equal(entry, Assert.Single(store.GetAuditEntries(id)));
    }

    [Fact]
    public void GetAgentOutputs_ReturnsAscendingVersionOrder()
    {
        var store = new InMemoryChangeRequestStore();
        var id = Guid.NewGuid();
        store.SaveAgentOutput(Output(id, 3), Entry(id, 3));
        store.SaveAgentOutput(Output(id, 1), Entry(id, 1));
        store.SaveAgentOutput(Output(id, 2), Entry(id, 2));

        Assert.Equal(new[] { 1, 2, 3 }, store.GetAgentOutputs<SyntheticOutput>(id).Select(o => o.Version));
    }

    [Fact]
    public void GetAgentOutputs_UnknownId_ReturnsEmptyList()
    {
        var store = new InMemoryChangeRequestStore();

        Assert.Empty(store.GetAgentOutputs<SyntheticOutput>(Guid.NewGuid()));
    }

    [Fact]
    public void GetAgentOutputs_OutputsOfTwoRequests_DoNotMix()
    {
        var store = new InMemoryChangeRequestStore();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        store.SaveAgentOutput(Output(first, 1), Entry(first, 1));
        store.SaveAgentOutput(Output(second, 1), Entry(second, 1));

        Assert.Equal(first, Assert.Single(store.GetAgentOutputs<SyntheticOutput>(first)).ChangeRequestId);
        Assert.Equal(second, Assert.Single(store.GetAgentOutputs<SyntheticOutput>(second)).ChangeRequestId);
    }

    [Fact]
    public void GetAgentOutputs_TwoOutputTypesForOneRequest_DoNotMix()
    {
        var store = new InMemoryChangeRequestStore();
        var id = Guid.NewGuid();
        store.SaveAgentOutput(Output(id, 1), Entry(id, 1));
        store.SaveAgentOutput(new OtherSyntheticOutput(id, 1), Entry(id, 1));

        Assert.Single(store.GetAgentOutputs<SyntheticOutput>(id));
        Assert.Single(store.GetAgentOutputs<OtherSyntheticOutput>(id));
    }

    [Fact]
    public void SaveAgentOutput_DuplicateVersion_ThrowsAndStoresNeitherItem()
    {
        var store = new InMemoryChangeRequestStore();
        var id = Guid.NewGuid();
        store.SaveAgentOutput(Output(id, 1), Entry(id, 1));

        Assert.Throws<InvalidOperationException>(() =>
            store.SaveAgentOutput(Output(id, 1), Entry(id, 1)));

        Assert.Single(store.GetAgentOutputs<SyntheticOutput>(id));
        Assert.Single(store.GetAuditEntries(id));
    }

    [Fact]
    public void SaveAgentOutput_MismatchedChangeRequestIds_ThrowsAndStoresNeitherItem()
    {
        var store = new InMemoryChangeRequestStore();
        var id = Guid.NewGuid();
        var other = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            store.SaveAgentOutput(Output(id, 1), Entry(other, 1)));

        Assert.Empty(store.GetAgentOutputs<SyntheticOutput>(id));
        Assert.Empty(store.GetAuditEntries(id));
        Assert.Empty(store.GetAuditEntries(other));
    }
}
