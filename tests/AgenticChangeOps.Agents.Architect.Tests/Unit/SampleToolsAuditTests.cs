using AgenticChangeOps.Agents.Architect.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

/// <summary>
/// FR-018 and contracts/audit-and-record.md: one typed entry per call, in call order, just before
/// the design's own entry, and never any file content.
/// </summary>
public sealed class SampleToolsAuditTests
{
    private const string Sentinel = "FILE-CONTENT-SENTINEL-0001";

    private static async Task<(SampleToolsRig Rig, Guid Id)> DesignAsync(Action<FakeToolSession> calls)
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();
        rig.ToolClient.Behavior = (session, _) =>
        {
            calls(session);
            return Task.FromResult(new ChatModelResponse(FakeChatModelClient.ValidAnswerJson(), false));
        };

        var result = await rig.Designing.DesignAsync(id);
        Assert.True(result.IsSuccess);
        return (rig, id);
    }

    [Fact]
    public async Task OneToolCalledEntry_PerCall_InCallOrder_ImmediatelyBeforeDesignGenerated()
    {
        var (rig, id) = await DesignAsync(s => s
            .List("orders-service")
            .Read("orders-service/src/OrderIntake")
            .Search("page"));

        var types = rig.Audit(id).Select(e => e.EventType).ToArray();
        Assert.Equal(
            new[]
            {
                AuditEventType.Submitted, AuditEventType.Approved,
                AuditEventType.ToolCalled, AuditEventType.ToolCalled, AuditEventType.ToolCalled,
                AuditEventType.DesignGenerated
            },
            types);

        var entries = rig.Audit(id).Where(e => e.EventType == AuditEventType.ToolCalled).ToArray();
        Assert.Equal(new[] { "list_files", "read_file", "search_text" }, entries.Select(e => e.ToolName));
        Assert.Equal(new[] { "orders-service", "orders-service/src/OrderIntake", "page" }, entries.Select(e => e.ToolTarget));
    }

    [Fact]
    public async Task EachEntry_CarriesToolTargetOutcomeSizeAndTime_AndTheUsualFields()
    {
        var (rig, id) = await DesignAsync(s => s.Read("orders-service/src/OrderIntake", bytes: 321, millis: 17));

        var entry = rig.Audit(id).Single(e => e.EventType == AuditEventType.ToolCalled);
        Assert.Equal("read_file", entry.ToolName);
        Assert.Equal("orders-service/src/OrderIntake", entry.ToolTarget);
        Assert.Equal("Served", entry.ToolOutcome);
        Assert.Equal(321, entry.ResultBytes);
        Assert.Equal(17, entry.ElapsedMilliseconds);
        Assert.Equal(DesignDraft.ArchitectAgentName, entry.AgentName);
        Assert.Equal(id, entry.ChangeRequestId);
        Assert.Equal(ChangeRequestStatus.Approved, entry.StatusBefore);
        Assert.Equal(ChangeRequestStatus.Approved, entry.StatusAfter);
        Assert.Null(entry.Details);
        Assert.Equal(FakeToolSession.FixedStart, entry.OccurredAt);
    }

    [Fact]
    public async Task ARefusedCall_IsAudited_WithItsOutcomeName_AndZeroBytes()
    {
        var (rig, id) = await DesignAsync(s => s.Refused("read_file", "../secret", ToolCallOutcome.RefusedOutsideSample));

        var entry = rig.Audit(id).Single(e => e.EventType == AuditEventType.ToolCalled);
        Assert.Equal("RefusedOutsideSample", entry.ToolOutcome);
        Assert.Equal(0, entry.ResultBytes);
    }

    [Fact]
    public async Task ASearchTerm_IsCleanedAndTruncatedTo120Characters_BeforeItIsAudited()
    {
        var term = "needle\r\n\t" + new string('z', 300) + "\u0007";
        var (rig, id) = await DesignAsync(s => s.Search(term));

        var target = rig.Audit(id).Single(e => e.EventType == AuditEventType.ToolCalled).ToolTarget!;
        Assert.True(target.Length <= 120);
        Assert.StartsWith("needle", target);
        Assert.DoesNotContain(target, ch => char.IsControl(ch));
    }

    [Fact]
    public async Task NoFileContent_AppearsInAnyAuditEntryOrStoredDraft()
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();
        rig.ToolClient.Behavior = (session, _) =>
        {
            // The content the tools returned lives only in the client conversation; the session
            // keeps records, which have nowhere to hold it.
            session.Read("orders-service/src/OrderIntake");
            return Task.FromResult(new ChatModelResponse(FakeChatModelClient.ValidAnswerJson(), false));
        };
        await rig.Designing.DesignAsync(id);

        var everything = System.Text.Json.JsonSerializer.Serialize(rig.Audit(id)) + System.Text.Json.JsonSerializer.Serialize(rig.Designs(id));
        Assert.DoesNotContain(Sentinel, everything);
        Assert.DoesNotContain(nameof(ToolCallRecord), everything);
        var members = typeof(ToolCallRecord).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain(members, m => m.Contains("Content", StringComparison.OrdinalIgnoreCase) || m.Contains("Text", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WithToolsOn_AndNothingCalled_ThereAreNoToolCalledEntries()
    {
        var (rig, id) = await DesignAsync(_ => { });

        Assert.DoesNotContain(rig.Audit(id), e => e.EventType == AuditEventType.ToolCalled);
        Assert.Equal(1, rig.Audit(id).Count(e => e.EventType == AuditEventType.DesignGenerated));
    }

    [Fact]
    public async Task TheCallsOfARequestThatFails_AreNeverAudited()
    {
        var rig = new SampleToolsRig();
        var id = rig.ApprovedOrdersRequest();
        rig.ToolClient.Behavior = (session, _) =>
        {
            session.Read("orders-service/src/OrderIntake").Refused("read_file", "x", ToolCallOutcome.RefusedCallLimit);
            throw new DraftGenerationException(GenerationFailureCategory.LimitsExhausted);
        };

        var result = await rig.Designing.DesignAsync(id);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain(rig.Audit(id), e => e.EventType == AuditEventType.ToolCalled);
        Assert.Empty(rig.Designs(id));
    }
}
