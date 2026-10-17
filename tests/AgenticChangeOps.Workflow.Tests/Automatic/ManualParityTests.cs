using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Workflow.Tests.Automatic.Support;

namespace AgenticChangeOps.Workflow.Tests.Automatic;

public class ManualParityTests
{
    private static ChangeRequestAuditEntry Normalise(ChangeRequestAuditEntry e) =>
        e with { AuditEntryId = Guid.Empty, ChangeRequestId = Guid.Empty, Automatic = null };

    [Fact]
    public async Task ARequirementsDraft_StoredAutomatically_MatchesTheOneStoredByAManualAdvance()
    {
        var manual = new AutomaticFixture();
        var manualId = manual.Base.SubmitSynthetic();
        await manual.Workflow.AdvanceAsync(manualId);

        var auto = new AutomaticFixture();
        var autoId = auto.Base.SubmitSynthetic();
        await auto.Automatic.RunCheckAsync();

        var m = Assert.Single(manual.Store.GetDrafts(manualId));
        var a = Assert.Single(auto.Store.GetDrafts(autoId));
        Assert.Equal(m.Version, a.Version);
        Assert.Equal(m.Status, a.Status);
        Assert.Equal(m.AgentName, a.AgentName);
        Assert.Equal(m.Summary, a.Summary);
        Assert.True(m.FunctionalRequirements.SequenceEqual(a.FunctionalRequirements));
        Assert.True(m.AcceptanceCriteria.SequenceEqual(a.AcceptanceCriteria));

        Assert.Equal(
            manual.Store.GetAuditEntries(manualId).Select(Normalise).ToArray(),
            auto.Store.GetAuditEntries(autoId).Select(Normalise).ToArray());
        Assert.Null(manual.Store.GetAuditEntries(manualId).Last().Automatic);
        Assert.True(auto.Store.GetAuditEntries(autoId).Last().Automatic);
        Assert.Null(auto.Store.GetAuditEntries(autoId).First().Automatic);
    }

    [Fact]
    public async Task ADesignDraft_StoredAutomatically_MatchesTheOneStoredByAManualAdvance()
    {
        var manual = new AutomaticFixture();
        var manualId = await manual.Base.ApprovedWithDraftAsync();
        await manual.Workflow.AdvanceAsync(manualId);

        var auto = new AutomaticFixture();
        var autoId = await auto.Base.ApprovedWithDraftAsync();
        await auto.Automatic.RunCheckAsync();

        var m = Assert.Single(manual.Store.GetDesigns(manualId));
        var a = Assert.Single(auto.Store.GetDesigns(autoId));
        Assert.Equal(m.Version, a.Version);
        Assert.Equal(m.RequirementsVersion, a.RequirementsVersion);
        Assert.Equal(m.ApproachSummary, a.ApproachSummary);
        Assert.True(m.AffectedComponents.SequenceEqual(a.AffectedComponents));

        Assert.Equal(
            manual.Store.GetAuditEntries(manualId).Select(Normalise).ToArray(),
            auto.Store.GetAuditEntries(autoId).Select(Normalise).ToArray());
        Assert.Null(manual.Store.GetAuditEntries(manualId).Last().Automatic);
        Assert.True(auto.Store.GetAuditEntries(autoId).Last().Automatic);
    }
}
