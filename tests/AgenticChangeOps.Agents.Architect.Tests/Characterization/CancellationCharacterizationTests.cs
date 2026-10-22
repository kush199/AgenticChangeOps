using AgenticChangeOps.Agents.Architect.Tests.Fakes;

namespace AgenticChangeOps.Agents.Architect.Tests.Characterization;

/// <summary>
/// Slice 017 characterization (written on the code before the drafting engine was extracted):
/// the Architect service does NOT rethrow cancellation from the generator, even when the
/// caller's token is cancelled. It reports a generator failure naming the exception type only,
/// with no category, and stores nothing. The Developer service differs on purpose.
/// </summary>
public sealed class CancellationCharacterizationTests
{
    private readonly DesigningTestFixture _f = new();

    [Fact]
    public async Task CallerCancelledToken_AndGeneratorThrowsCancellation_IsReportedByTypeNameOnly()
    {
        var id = _f.ApproveWithBasis();
        _f.Generator.Behavior = FakeDesignGenerator.Throws(new OperationCanceledException("sentinel-cancel-text"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _f.Designing.DesignAsync(id, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Null(result.FailureCategory);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Generator", error.FieldName);
        Assert.Equal("The content generator failed (OperationCanceledException).", error.Message);
        Assert.Empty(_f.Store.GetDesigns(id));
    }
}
