using AgenticChangeOps.Agents.Architect.Tests.Fakes;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;

namespace AgenticChangeOps.Agents.Architect.Tests.Unit;

public sealed class DesignDecisionValidationTests
{
    private readonly DesigningTestFixture _f = new();

    private async Task<(Guid Id, DesignDecisionResult Result)> DecideAsync(
        string? name, DesignVerdict verdict, string? reason)
    {
        var id = await _f.ApproveAndDesignAsync();
        return (id, _f.Deciding.Decide(id, 1, name!, verdict, reason));
    }

    private void AssertNothingStored(Guid id)
    {
        Assert.Empty(_f.Deciding.GetDecisions(id));
        Assert.Equal(0, _f.AuditCount(id, AuditEventType.DesignDecided));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AMissingApprover_IsRefusedNamingTheApproverField(string? name)
    {
        var (id, result) = await DecideAsync(name, DesignVerdict.Accept, null);

        Assert.Equal(DesignDecisionRefusal.Invalid, result.Refusal);
        Assert.Contains(result.Errors, e => e.FieldName == "ApproverName");
        AssertNothingStored(id);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task TheApproverNameLimit_IsHundredCharactersAfterTrimming(int length, bool accepted)
    {
        var (id, result) = await DecideAsync("  " + new string('n', length) + "  ", DesignVerdict.Accept, null);

        Assert.Equal(accepted, result.IsSuccess);
        if (!accepted)
        {
            Assert.Contains(result.Errors, e => e.FieldName == "ApproverName");
            AssertNothingStored(id);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t ")]
    public async Task Reject_WithoutAReason_IsRefused_NamingReject(string? reason)
    {
        var (id, result) = await DecideAsync("Jordan Reed", DesignVerdict.Reject, reason);

        Assert.Equal(DesignDecisionRefusal.Invalid, result.Refusal);
        var error = Assert.Single(result.Errors, e => e.FieldName == "Reason");
        Assert.Contains("Reject", error.Message);
        AssertNothingStored(id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Accept_WithoutAReason_IsRecorded_WithNoReason(string? reason)
    {
        var (_, result) = await DecideAsync("Jordan Reed", DesignVerdict.Accept, reason);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Decision!.Reason);
    }

    [Theory]
    [InlineData(DesignVerdict.Accept, 1000, true)]
    [InlineData(DesignVerdict.Accept, 1001, false)]
    [InlineData(DesignVerdict.Reject, 1000, true)]
    [InlineData(DesignVerdict.Reject, 1001, false)]
    public async Task TheReasonLimit_IsAThousandCharactersAfterTrimming(DesignVerdict verdict, int length, bool accepted)
    {
        var (id, result) = await DecideAsync("Jordan Reed", verdict, " " + new string('r', length) + " ");

        Assert.Equal(accepted, result.IsSuccess);
        if (!accepted)
        {
            Assert.Contains(result.Errors, e => e.FieldName == "Reason");
            AssertNothingStored(id);
        }
    }

    [Fact]
    public async Task AnUnrecognisedVerdict_ListsTheAcceptedValues_AndNeverEchoesAnything()
    {
        var (id, result) = await DecideAsync("Jordan Reed", (DesignVerdict)(-1), "reason-sentinel");

        Assert.Equal(DesignDecisionRefusal.Invalid, result.Refusal);
        var error = Assert.Single(result.Errors, e => e.FieldName == "Decision");
        Assert.Contains("Accept, Reject", error.Message);
        Assert.DoesNotContain("-1", error.Message);
        Assert.All(result.Errors, e => Assert.DoesNotContain("sentinel", e.Message));
        AssertNothingStored(id);
    }

    [Fact]
    public async Task EveryFailingField_IsListedTogether()
    {
        var (_, result) = await DecideAsync("  ", (DesignVerdict)7, null);
        Assert.Equal(new[] { "ApproverName", "Decision" }, result.Errors.Select(e => e.FieldName).OrderBy(x => x));

        var (_, reject) = await DecideAsync("  ", DesignVerdict.Reject, null);
        Assert.Equal(new[] { "ApproverName", "Reason" }, reject.Errors.Select(e => e.FieldName).OrderBy(x => x));
    }

    [Theory]
    [InlineData(100, 1000)]
    [InlineData(101, 1000)]
    [InlineData(100, 1001)]
    [InlineData(0, 5)]
    public void TheLimits_AgreeWithTheApprovalDecisionRules(int nameLength, int reasonLength)
    {
        var name = new string('n', nameLength);
        var reason = new string('r', reasonLength);
        var approval = new ApprovalDecisionValidator().Validate(
            new ApprovalDecisionRequest(Guid.NewGuid(), name, ApprovalDecision.Reject, reason),
            new ChangeRequest(
                Guid.NewGuid(), "Title", "Description", "Requester", "System",
                Priority.Low, ChangeType.Feature, ChangeRequestStatus.Submitted, DateTime.UtcNow));

        var design = new DesignDecisionValidator().Validate(name, DesignVerdict.Reject, reason);

        Assert.Equal(
            approval.Where(e => e.FieldName is "ApproverName" or "Reason").Select(e => e.FieldName).OrderBy(x => x),
            design.Select(e => e.FieldName).OrderBy(x => x));
    }
}
