using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Workflow.Tests.Unit;

public class WorkflowRulesTests
{
    private static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly DateTime T1 = new(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = T1.AddHours(1);

    private static RequirementsFact[] NoReq => Array.Empty<RequirementsFact>();
    private static DesignFact[] NoDesign => Array.Empty<DesignFact>();
    private static RequirementsFact[] Req(params int[] versions) =>
        versions.Select(v => new RequirementsFact(v, T1.AddMinutes(v))).ToArray();
    private static DesignFact[] Design(params (int Version, int Basis)[] items) =>
        items.Select(i => new DesignFact(i.Version, T2.AddMinutes(i.Version), i.Basis)).ToArray();

    private static WorkflowStatus Derive(
        ChangeRequestStatus status, RequirementsFact[] req, DesignFact[]? designs = null) =>
        WorkflowRules.Derive(Id, status, req, designs ?? NoDesign);

    private static void AssertNext(WorkflowStatus s, WorkflowStage stage, WorkflowStep step)
    {
        Assert.Equal(stage, s.Stage);
        Assert.Equal(step, s.NextStep);
        Assert.Null(s.Reason);
    }

    private static void AssertReason(WorkflowStatus s, WorkflowStage stage, WorkflowReasonCode code)
    {
        Assert.Equal(stage, s.Stage);
        Assert.Null(s.NextStep);
        Assert.NotNull(s.Reason);
        Assert.Equal(code, s.Reason!.Code);
        Assert.False(string.IsNullOrWhiteSpace(s.Reason.Message));
    }

    [Fact]
    public void Row1_Rejected_NothingFurther()
    {
        AssertReason(Derive(ChangeRequestStatus.Rejected, NoReq), WorkflowStage.Rejected, WorkflowReasonCode.Rejected);
        AssertReason(Derive(ChangeRequestStatus.Rejected, Req(1)), WorkflowStage.Rejected, WorkflowReasonCode.Rejected);
    }

    [Fact]
    public void Row2_DeferredWithoutRequirements_OffersRequirementsDraft() =>
        AssertNext(Derive(ChangeRequestStatus.Deferred, NoReq), WorkflowStage.Deferred, WorkflowStep.RequirementsDraft);

    [Fact]
    public void Row3_DeferredWithRequirements_NothingFurther() =>
        AssertReason(Derive(ChangeRequestStatus.Deferred, Req(1)), WorkflowStage.Deferred, WorkflowReasonCode.Deferred);

    [Fact]
    public void Row4_SubmittedWithoutRequirements_OffersRequirementsDraft() =>
        AssertNext(
            Derive(ChangeRequestStatus.Submitted, NoReq),
            WorkflowStage.AwaitingRequirementsDraft, WorkflowStep.RequirementsDraft);

    [Fact]
    public void Row5_SubmittedWithRequirements_AwaitsDecision() =>
        AssertReason(
            Derive(ChangeRequestStatus.Submitted, Req(1)),
            WorkflowStage.AwaitingDecision, WorkflowReasonCode.AwaitingDecision);

    [Fact]
    public void Row6_UnderReview_AwaitsDecision_WithOrWithoutRequirements()
    {
        AssertReason(
            Derive(ChangeRequestStatus.UnderReview, NoReq),
            WorkflowStage.AwaitingDecision, WorkflowReasonCode.AwaitingDecision);
        AssertReason(
            Derive(ChangeRequestStatus.UnderReview, Req(1)),
            WorkflowStage.AwaitingDecision, WorkflowReasonCode.AwaitingDecision);
    }

    [Fact]
    public void Row7_ApprovedWithoutRequirements_NeedsRequirementsDraft() =>
        AssertReason(
            Derive(ChangeRequestStatus.Approved, NoReq),
            WorkflowStage.AwaitingRequirementsDraft, WorkflowReasonCode.RequirementsDraftRequired);

    [Fact]
    public void Row8_ApprovedWithRequirementsAndNoDesign_OffersDesign() =>
        AssertNext(Derive(ChangeRequestStatus.Approved, Req(1)), WorkflowStage.ReadyForDesign, WorkflowStep.DesignDraft);

    [Fact]
    public void Row8_ApprovedWithDesignBasedOnOlderRequirements_OffersDesignAgain() =>
        AssertNext(
            Derive(ChangeRequestStatus.Approved, Req(1, 2), Design((1, 1))),
            WorkflowStage.ReadyForDesign, WorkflowStep.DesignDraft);

    [Fact]
    public void Row9_ApprovedWithDesignBasedOnLatestRequirements_NothingDue() =>
        AssertReason(
            Derive(ChangeRequestStatus.Approved, Req(1, 2), Design((1, 1), (2, 2))),
            WorkflowStage.DesignComplete, WorkflowReasonCode.NothingDue);

    [Fact]
    public void CompletedSteps_ListRequirementsThenDesigns_AscendingWithVersionsAndTimes()
    {
        var status = Derive(ChangeRequestStatus.Approved, Req(2, 1), Design((2, 2), (1, 1)));

        Assert.Equal(
            new[]
            {
                (WorkflowStep.RequirementsDraft, 1, (int?)null),
                (WorkflowStep.RequirementsDraft, 2, null),
                (WorkflowStep.DesignDraft, 1, 1),
                (WorkflowStep.DesignDraft, 2, 2)
            },
            status.CompletedSteps.Select(c => (c.Step, c.Version, c.RequirementsVersion)).ToArray());
        Assert.Equal(T1.AddMinutes(1), status.CompletedSteps[0].GeneratedAt);
        Assert.Equal(T2.AddMinutes(2), status.CompletedSteps[3].GeneratedAt);
    }

    [Fact]
    public void Status_CopiesChangeRequestIdAndStatus()
    {
        var status = Derive(ChangeRequestStatus.Approved, Req(1));

        Assert.Equal(Id, status.ChangeRequestId);
        Assert.Equal(ChangeRequestStatus.Approved, status.ChangeRequestStatus);
    }

    public static IEnumerable<object[]> EveryStatus() =>
        Enum.GetValues<ChangeRequestStatus>().Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void EveryStatus_AndEveryFactShape_HasExactlyOneOfNextStepAndReason(ChangeRequestStatus status)
    {
        var shapes = new (RequirementsFact[] Req, DesignFact[] Designs)[]
        {
            (NoReq, NoDesign),
            (Req(1), NoDesign),
            (Req(1, 2), Design((1, 1))),
            (Req(1, 2), Design((1, 1), (2, 2)))
        };

        foreach (var (req, designs) in shapes)
        {
            var result = Derive(status, req, designs);
            Assert.True(result.NextStep is null ^ result.Reason is null);
        }
    }

    [Fact]
    public void NoOutcome_DependsOnTheMessageText()
    {
        // Reason messages name the status so a reader sees why; codes carry the meaning.
        var rejected = Derive(ChangeRequestStatus.Rejected, NoReq);
        Assert.Contains("Rejected", rejected.Reason!.Message);
    }
}
