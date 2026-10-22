using AgenticChangeOps.Agents.Developer.Tests.Fakes;

namespace AgenticChangeOps.Agents.Developer.Tests.Characterization;

/// <summary>
/// Slice 017 characterization (written on the code before the drafting engine was extracted):
/// the Developer service rethrows cancellation only when the caller's own token is cancelled
/// (covered by <c>ProposalRejectionTests.ACancelledRequest_StoresNothing</c>). A cancellation
/// exception raised while the caller's token is NOT cancelled is an ordinary generator failure,
/// reported by exception type name only, with no category. The Requirement and Architect
/// services differ on purpose and never rethrow.
/// </summary>
public sealed class CancellationCharacterizationTests
{
    private readonly ProposalTestFixture _f = new();

    [Fact]
    public async Task CancellationFromTheGenerator_WhileTheCallersTokenIsLive_IsReportedByTypeNameOnly()
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = FakeProposalGenerator.Throws(new OperationCanceledException("sentinel-cancel-text"));

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Null(result.FailureCategory);
        Assert.Null(result.Rejection);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Generator", error.FieldName);
        Assert.Equal("The content generator failed (OperationCanceledException).", error.Message);
        Assert.Empty(_f.Store.GetProposals(id));
    }
}
