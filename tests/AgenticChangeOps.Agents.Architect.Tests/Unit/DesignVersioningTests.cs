using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignVersioningTests
{
    private readonly DesigningTestFixture _f = new();

    [Fact]
    public async Task Redesign_CreatesVersionTwoAndKeepsVersionOneUnchanged()
    {
        var id = _f.ApproveWithBasis();
        var first = (await _f.Designing.DesignAsync(id)).Draft!;

        var second = (await _f.Designing.DesignAsync(id)).Draft!;

        Assert.Equal(2, second.Version);
        Assert.Equal(first, _f.Store.GetDesigns(id)[0]);
    }

    [Fact]
    public async Task Redesign_NTimes_YieldsVersionsOneToNWithoutGaps()
    {
        var id = _f.ApproveWithBasis();

        for (var i = 0; i < 5; i++)
            await _f.Designing.DesignAsync(id);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, _f.Store.GetDesigns(id).Select(d => d.Version));
    }

    [Fact]
    public async Task Redesign_EachStoredDesignHasItsOwnAuditEntry()
    {
        var id = _f.ApproveWithBasis();
        await _f.Designing.DesignAsync(id);
        await _f.Designing.DesignAsync(id);
        await _f.Designing.DesignAsync(id);

        var versions = _f.Store.GetAuditEntries(id)
            .Where(e => e.EventType == AuditEventType.DesignGenerated)
            .Select(e => e.DraftVersion);

        Assert.Equal(new int?[] { 1, 2, 3 }, versions);
    }

    [Fact]
    public async Task Redesign_TwoRequests_NumberVersionsIndependently()
    {
        var a = _f.ApproveWithBasis();
        var b = _f.ApproveWithBasis();
        await _f.Designing.DesignAsync(a);
        await _f.Designing.DesignAsync(a);

        var firstForB = (await _f.Designing.DesignAsync(b)).Draft!;

        Assert.Equal(1, firstForB.Version);
        Assert.Equal(2, _f.Store.GetDesigns(a).Count);
    }

    [Fact]
    public async Task Redesign_WithDifferentContent_KeepsEachVersionsOwnContent()
    {
        var id = _f.ApproveWithBasis();
        await _f.Designing.DesignAsync(id);
        _f.Generator.Behavior = FakeDesignGenerator.Returns(
            FakeDesignGenerator.ValidContent() with { ApproachSummary = "A revised approach." });

        await _f.Designing.DesignAsync(id);

        var designs = _f.Store.GetDesigns(id);
        Assert.Equal(FakeDesignGenerator.ValidContent().ApproachSummary, designs[0].ApproachSummary);
        Assert.Equal("A revised approach.", designs[1].ApproachSummary);
    }

    [Fact]
    public async Task Redesign_AfterANewRequirementsVersion_LinksTheNewVersionAndKeepsTheOldLink()
    {
        var id = _f.ApproveWithBasis(basisVersion: 1);
        await _f.Designing.DesignAsync(id);
        _f.Basis.Set(id, FakeRequirementsBasisSource.Basis(2));

        var second = (await _f.Designing.DesignAsync(id)).Draft!;

        Assert.Equal(2, second.RequirementsVersion);
        var designs = _f.Store.GetDesigns(id);
        Assert.Equal(1, designs[0].RequirementsVersion);
        Assert.Equal(2, designs[1].RequirementsVersion);
    }
}
