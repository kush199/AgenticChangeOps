using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignRetrievalTests
{
    private readonly DesigningTestFixture _f = new();

    [Fact]
    public async Task Latest_ReturnsTheHighestVersion()
    {
        var id = _f.ApproveWithBasis();
        await _f.Designing.DesignAsync(id);
        await _f.Designing.DesignAsync(id);
        await _f.Designing.DesignAsync(id);

        var result = _f.Designing.GetLatestDesign(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Version);
    }

    [Fact]
    public async Task History_ReturnsEveryVersionInAscendingOrderUnaltered()
    {
        var id = _f.ApproveWithBasis();
        await _f.Designing.DesignAsync(id);
        await _f.Designing.DesignAsync(id);
        await _f.Designing.DesignAsync(id);

        var result = _f.Designing.GetDesignHistory(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 1, 2, 3 }, result.Value!.Select(d => d.Version));
        Assert.Equal(_f.Store.GetDesigns(id), result.Value);
    }

    [Fact]
    public void ExistingRequestWithNoDesigns_LatestIsNullAndHistoryIsEmpty()
    {
        var id = _f.ApproveWithBasis();

        var latest = _f.Designing.GetLatestDesign(id);
        var history = _f.Designing.GetDesignHistory(id);

        Assert.True(latest.IsSuccess);
        Assert.Null(latest.Value);
        Assert.True(history.IsSuccess);
        Assert.Empty(history.Value!);
    }

    [Fact]
    public void UnknownId_ReturnsAChangeRequestIdErrorForBothQueries()
    {
        var unknown = Guid.NewGuid();

        var latest = _f.Designing.GetLatestDesign(unknown);
        var history = _f.Designing.GetDesignHistory(unknown);

        Assert.False(latest.IsSuccess);
        Assert.False(history.IsSuccess);
        foreach (var error in new[] { Assert.Single(latest.Errors), Assert.Single(history.Errors) })
        {
            Assert.Equal("ChangeRequestId", error.FieldName);
            Assert.Contains(unknown.ToString(), error.Message);
        }
    }

    [Fact]
    public async Task Retrieval_WritesNoAuditEntryAndNeverCallsTheGenerator()
    {
        var id = _f.ApproveWithBasis();
        await _f.Designing.DesignAsync(id);
        var auditBefore = _f.Store.GetAuditEntries(id);
        var callsBefore = _f.Generator.CallCount;

        _f.Designing.GetLatestDesign(id);
        _f.Designing.GetDesignHistory(id);

        Assert.Equal(auditBefore, _f.Store.GetAuditEntries(id));
        Assert.Equal(callsBefore, _f.Generator.CallCount);
    }
}
