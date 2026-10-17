using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

/// <summary>
/// An unrecognised priority, change type or decision is reported by naming the field and listing
/// the accepted values. The rejected value (which the API reports as an out-of-range number) is
/// never part of the message.
/// </summary>
public sealed class EnumMessageTests
{
    private static ChangeRequestService BuildSut() => new(
        new InMemoryChangeRequestStore(),
        new FakeSystemClock(),
        new FakeIdGenerator(
            new Guid("aaaaaaaa-0000-0000-0000-000000000003"),
            new Guid("bbbbbbbb-0000-0000-0000-000000000003")),
        new ChangeRequestValidator());

    private static SubmissionRequest ValidBase() => new(
        "Automate deployment pipeline",
        "Reduce manual steps in the release process.",
        "Jordan Smith",
        "DeploymentOrchestrator",
        Priority.Medium,
        ChangeType.Feature);

    private static string Expected<TEnum>(string field) where TEnum : struct, Enum =>
        $"{field} is not a recognised value. Accepted values: {string.Join(", ", Enum.GetNames<TEnum>())}.";

    private static void AssertNoPlaceholder(string message)
    {
        Assert.DoesNotContain("'", message);
        Assert.DoesNotContain("-1", message);
        Assert.DoesNotContain("99", message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void Priority_Unrecognised_NamesTheFieldAndListsTheAcceptedValuesOnly(int value)
    {
        var result = BuildSut().Submit(ValidBase() with { Priority = (Priority)value });

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Priority", error.FieldName);
        Assert.Equal(Expected<Priority>("Priority"), error.Message);
        Assert.Equal("Priority is not a recognised value. Accepted values: Low, Medium, High, Critical.", error.Message);
        AssertNoPlaceholder(error.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void ChangeType_Unrecognised_NamesTheFieldAndListsTheAcceptedValuesOnly(int value)
    {
        var result = BuildSut().Submit(ValidBase() with { ChangeType = (ChangeType)value });

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("ChangeType", error.FieldName);
        Assert.Equal(Expected<ChangeType>("ChangeType"), error.Message);
        Assert.Equal("ChangeType is not a recognised value. Accepted values: Feature, Bugfix, ConfigChange.", error.Message);
        AssertNoPlaceholder(error.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void Decision_Unrecognised_NamesTheFieldAndListsTheAcceptedValuesOnly_AndRecordsNothing(int value)
    {
        var fx = new ApprovalTestFixture();
        var id = fx.SubmitSynthetic();
        var requestBefore = fx.Intake.GetById(id);
        var historyBefore = fx.Intake.GetAuditHistory(id);

        var result = fx.Approval.RecordDecision(ApprovalTestFixture.Decide(id, (ApprovalDecision)value));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Decision", error.FieldName);
        Assert.Equal(Expected<ApprovalDecision>("Decision"), error.Message);
        Assert.Equal("Decision is not a recognised value. Accepted values: Approve, Reject, Defer.", error.Message);
        AssertNoPlaceholder(error.Message);
        Assert.Equal(requestBefore, fx.Intake.GetById(id));
        Assert.Equal(historyBefore, fx.Intake.GetAuditHistory(id));
    }
}
