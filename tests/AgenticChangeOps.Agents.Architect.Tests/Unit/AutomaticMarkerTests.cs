using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

/// <summary>The audit entry shows a step was run automatically only when the caller says so.</summary>
public sealed class AutomaticMarkerTests
{
    private readonly DesigningTestFixture _f = new();

    [Fact]
    public async Task Design_ByDefault_WritesAnEntryWithNoAutomaticMarker()
    {
        var id = _f.ApproveWithBasis();

        var result = await _f.Designing.DesignAsync(id);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(_f.Store.GetAuditEntries(id), e => e.EventType == AuditEventType.DesignGenerated);
        Assert.Null(entry.Automatic);
    }

    [Fact]
    public async Task Design_RunAutomatically_WritesTheSameEntryPlusTheMarker()
    {
        var manual = new DesigningTestFixture();
        var manualId = manual.ApproveWithBasis();
        await manual.Designing.DesignAsync(manualId);
        var manualEntry = manual.Store.GetAuditEntries(manualId).Single(e => e.EventType == AuditEventType.DesignGenerated);

        var id = _f.ApproveWithBasis();
        var result = await _f.Designing.DesignAsync(id, CancellationToken.None, automatic: true);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(_f.Store.GetAuditEntries(id), e => e.EventType == AuditEventType.DesignGenerated);
        Assert.True(entry.Automatic);
        Assert.Equal(
            manualEntry with { ChangeRequestId = id, Automatic = true },
            entry);
    }
}
