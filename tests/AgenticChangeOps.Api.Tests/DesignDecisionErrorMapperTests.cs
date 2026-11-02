using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Api.Errors;
using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Api.Tests;

public sealed class DesignDecisionErrorMapperTests
{
    private static (int StatusCode, string Code) Map(DesignDecisionRefusal refusal, params ValidationError[] errors)
    {
        var (status, body) = ErrorMapper.ForDesignDecision(DesignDecisionResult.Failure(refusal, errors));
        return (status, body.Code);
    }

    [Fact]
    public void EachRefusal_MapsToItsDocumentedStatusAndCode()
    {
        Assert.Equal((404, "not-found"), Map(DesignDecisionRefusal.NotFound, new ValidationError("ChangeRequestId", "x")));
        Assert.Equal((409, "conflict"), Map(DesignDecisionRefusal.NotApproved, new ValidationError("Status", "x")));
        Assert.Equal((404, "design-version-not-found"), Map(DesignDecisionRefusal.VersionNotFound, new ValidationError("DesignVersion", "x")));
        Assert.Equal((409, "design-outdated"), Map(DesignDecisionRefusal.Outdated, new ValidationError("DesignVersion", "x")));
        Assert.Equal((409, "design-already-decided"), Map(DesignDecisionRefusal.AlreadyDecided, new ValidationError("DesignVersion", "x")));
        Assert.Equal((400, "validation-failed"), Map(DesignDecisionRefusal.Invalid, new ValidationError("Decision", "x")));
    }

    [Fact]
    public void TheNewCodes_UseFixedMessages_ThatCarryNothingFromTheCaller()
    {
        foreach (var refusal in new[]
                 {
                     DesignDecisionRefusal.VersionNotFound, DesignDecisionRefusal.Outdated, DesignDecisionRefusal.AlreadyDecided
                 })
        {
            var (_, body) = ErrorMapper.ForDesignDecision(
                DesignDecisionResult.Failure(refusal, new[] { new ValidationError("DesignVersion", "caller-text-sentinel") }));

            Assert.DoesNotContain("sentinel", body.Message);
            Assert.True(body.Errors is null || body.Errors.All(e => !e.Message.Contains("sentinel")));
        }
    }

    [Fact]
    public void Invalid_ListsEveryError()
    {
        var (_, body) = ErrorMapper.ForDesignDecision(DesignDecisionResult.Failure(
            DesignDecisionRefusal.Invalid,
            new[] { new ValidationError("ApproverName", "a"), new ValidationError("Reason", "b") }));

        Assert.Equal(new[] { "ApproverName", "Reason" }, body.Errors!.Select(e => e.Field));
    }
}
