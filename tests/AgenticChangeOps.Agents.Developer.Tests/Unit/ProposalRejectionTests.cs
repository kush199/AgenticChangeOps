using AgenticChangeOps.Agents.Developer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Developer.Tests.Unit;

/// <summary>
/// US4: a proposal the system will not accept, and every model-side failure, stores nothing and
/// reports a reason or a category only.
/// </summary>
public sealed class ProposalRejectionTests
{
    private const string Settings = FakeProposalGenerator.SettingsPath;
    private const string Secret = "SECRET-MODEL-TEXT-0042";

    private readonly ProposalTestFixture _f;

    public ProposalRejectionTests() : this(null) { }

    private ProposalRejectionTests(ProposalLimits? limits)
    {
        _f = new ProposalTestFixture(limits);
    }

    private static ProposalContent With(params ProposedChangeContent[] changes) =>
        FakeProposalGenerator.ValidContent() with { Changes = changes };

    private async Task<(Guid Id, ProposalResult Result)> RunAsync(ProposalContent content, ProposalTestFixture? fixture = null)
    {
        var f = fixture ?? _f;
        var id = f.ApproveWithDrafts();
        f.Generator.Behavior = FakeProposalGenerator.Returns(content);
        return (id, await f.Proposing.ProposeAsync(id));
    }

    private static void AssertNothingStored(ProposalTestFixture f, Guid id)
    {
        Assert.Empty(f.Store.GetAgentOutputs<ChangeProposal>(id));
        Assert.Equal(0, f.AuditCount(id, AuditEventType.ProposalGenerated));
        Assert.Equal(0, f.AuditCount(id, AuditEventType.ToolCalled));
    }

    public static IEnumerable<object[]> RejectedContent()
    {
        var ok = new ProposedChangeContent(Settings, "change", "report_hour = 2", "report_hour = 4");

        yield return new object[] { ProposalRejection.QuoteNotFound,
            new[] { ok, new ProposedChangeContent(Settings, "change", Secret, "x") } };
        yield return new object[] { ProposalRejection.QuoteAmbiguous,
            new[] { new ProposedChangeContent("orders-service/src/OrderIntake", "change", "order", "x") } };
        yield return new object[] { ProposalRejection.PathOutsideSample,
            new[] { ok, new ProposedChangeContent("../" + Secret, "change", "a", "b") } };
        yield return new object[] { ProposalRejection.FileNotFound,
            new[] { new ProposedChangeContent("billing-portal/" + Secret, "change", "a", "b") } };
        yield return new object[] { ProposalRejection.NewFileExists,
            new[] { new ProposedChangeContent(Settings, "add", null, "b") } };
        yield return new object[] { ProposalRejection.OverlappingEdits,
            new[] { ok, new ProposedChangeContent(Settings, "change", "report_hour = 2", "y") } };
        yield return new object[] { ProposalRejection.MalformedEdit,
            new[] { new ProposedChangeContent("a/b", "change", null, "b") } };
        yield return new object[] { ProposalRejection.NoChanges, Array.Empty<ProposedChangeContent>() };
    }

    [Theory]
    [MemberData(nameof(RejectedContent))]
    public async Task ARejectedProposal_StoresNothing_AndReportsOnlyTheReason(
        ProposalRejection expected, ProposedChangeContent[] changes)
    {
        var (id, result) = await RunAsync(With(changes));

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Rejection);
        Assert.Null(result.FailureCategory);
        AssertNothingStored(_f, id);
        foreach (var error in result.Errors)
        {
            Assert.DoesNotContain(Secret, error.Message);
            Assert.DoesNotContain("report_hour", error.Message);
            Assert.DoesNotContain("billing-portal", error.Message);
        }
    }

    [Fact]
    public async Task TooManyChanges_AndTooMuchText_AreRejected()
    {
        var small = new ProposalTestFixture(new ProposalLimits(MaxChanges: 1, MaxTotalBytes: 6000));
        var two = With(
            new ProposedChangeContent("a/new1.md", "add", null, "x"),
            new ProposedChangeContent("a/new2.md", "add", null, "y"));
        var (id1, r1) = await RunAsync(two, small);

        var tiny = new ProposalTestFixture(new ProposalLimits(MaxChanges: 5, MaxTotalBytes: 10));
        var (id2, r2) = await RunAsync(With(new ProposedChangeContent("a/new.md", "add", null, new string('z', 11))), tiny);

        Assert.Equal(ProposalRejection.TooManyChanges, r1.Rejection);
        Assert.Equal(ProposalRejection.TooLarge, r2.Rejection);
        AssertNothingStored(small, id1);
        AssertNothingStored(tiny, id2);
    }

    [Fact]
    public async Task ARejectedRequest_DoesNotUseUpAVersionNumber()
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = FakeProposalGenerator.Returns(With(new ProposedChangeContent(Settings, "change", Secret, "x")));
        await _f.Proposing.ProposeAsync(id);
        _f.Generator.Behavior = _ => Task.FromResult<ProposalContent?>(FakeProposalGenerator.ValidContent());

        var good = await _f.Proposing.ProposeAsync(id);

        Assert.Equal(1, good.Proposal!.Version);
    }

    // ---- model-side failures ----

    public static IEnumerable<object[]> Categories() =>
        Enum.GetValues<GenerationFailureCategory>().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Categories))]
    public async Task AGeneratorFailure_StoresNothing_AndReportsItsCategoryOnly(GenerationFailureCategory category)
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = FakeProposalGenerator.Throws(new DraftGenerationException(category));

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(category, result.FailureCategory);
        Assert.Null(result.Rejection);
        AssertNothingStored(_f, id);
        Assert.Equal(ChangeRequestStatus.Approved, _f.Store.FindById(id)!.Status);
    }

    [Fact]
    public async Task AnUnexpectedException_IsReportedByTypeNameOnly()
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = FakeProposalGenerator.Throws(new InvalidOperationException(Secret + " https://endpoint.invalid"));

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Null(result.FailureCategory);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Generator", error.FieldName);
        Assert.Contains("InvalidOperationException", error.Message);
        Assert.DoesNotContain(Secret, error.Message);
        Assert.DoesNotContain("endpoint", error.Message);
        AssertNothingStored(_f, id);
    }

    [Fact]
    public async Task NullContent_StoresNothing_AsAGeneratorError()
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = FakeProposalGenerator.ReturnsNull();

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureCategory.MalformedOutput, result.FailureCategory);
        AssertNothingStored(_f, id);
    }

    [Fact]
    public async Task UnusableContent_IsMalformedOutput_AndItsMessagesAreNotForwarded()
    {
        var (id, result) = await RunAsync(FakeProposalGenerator.ValidContent() with { Summary = Secret + "\u0000", Risks = Array.Empty<string>() });

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureCategory.MalformedOutput, result.FailureCategory);
        AssertNothingStored(_f, id);
    }

    [Fact]
    public async Task ACancelledRequest_StoresNothing()
    {
        var id = _f.ApproveWithDrafts();
        using var cts = new CancellationTokenSource();
        _f.Generator.Behavior = token =>
        {
            cts.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult<ProposalContent?>(FakeProposalGenerator.ValidContent());
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _f.Proposing.ProposeAsync(id, cts.Token));

        AssertNothingStored(_f, id);
    }

    [Fact]
    public async Task AValidProposal_ProducedAfterALimitWasReached_IsStoredNormally()
    {
        var id = _f.ApproveWithDrafts();
        _f.Generator.Behavior = FakeProposalGenerator.Returns(FakeProposalGenerator.ValidContent() with
        {
            ToolCalls = new[]
            {
                new ToolCallRecord("read_file", "a", ToolCallOutcome.RefusedCallLimit, 0, TimeSpan.Zero, DateTime.UnixEpoch)
            }
        });

        var result = await _f.Proposing.ProposeAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal("RefusedCallLimit", _f.Store.GetAuditEntries(id).Single(e => e.EventType == AuditEventType.ToolCalled).ToolOutcome);
    }
}
