using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Agents.Requirement.Tests.Fakes;

namespace AgenticChangeOps.Agents.Requirement.Tests.Unit;

/// <summary>The audit entry shows a step was run automatically only when the caller says so.</summary>
public sealed class AutomaticMarkerTests
{
    private readonly DraftingTestFixture _f = new();

    [Fact]
    public async Task Draft_ByDefault_WritesAnEntryWithNoAutomaticMarker()
    {
        var id = _f.SubmitSynthetic();

        var result = await _f.Drafting.DraftAsync(id);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(_f.Store.GetAuditEntries(id), e => e.EventType == AuditEventType.DraftGenerated);
        Assert.Null(entry.Automatic);
    }

    [Fact]
    public async Task Draft_RunAutomatically_WritesTheSameEntryPlusTheMarker()
    {
        var manual = new DraftingTestFixture();
        var manualId = manual.SubmitSynthetic();
        await manual.Drafting.DraftAsync(manualId);
        var manualEntry = manual.Store.GetAuditEntries(manualId).Single(e => e.EventType == AuditEventType.DraftGenerated);

        var id = _f.SubmitSynthetic();
        var result = await _f.Drafting.DraftAsync(id, CancellationToken.None, automatic: true);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(_f.Store.GetAuditEntries(id), e => e.EventType == AuditEventType.DraftGenerated);
        Assert.True(entry.Automatic);
        Assert.Equal(
            manualEntry with { ChangeRequestId = id, Automatic = true },
            entry);
    }
}
