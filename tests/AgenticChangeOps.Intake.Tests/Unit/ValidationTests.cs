using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Intake.Tests.Fakes;
using Xunit;

namespace AgenticChangeOps.Intake.Tests.Unit;

public sealed class ValidationTests
{
    private static readonly Guid CrId    = new("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid AuditId = new("bbbbbbbb-0000-0000-0000-000000000002");

    private static ChangeRequestService BuildSut()
    {
        var store = new InMemoryChangeRequestStore();
        var clock = new FakeSystemClock();
        var idGen = new FakeIdGenerator(CrId, AuditId);
        return new ChangeRequestService(store, clock, idGen, new ChangeRequestValidator());
    }

    private static SubmissionRequest ValidBase() => new(
        "Automate deployment pipeline",
        "Reduce manual steps in the release process.",
        "Jordan Smith",
        "DeploymentOrchestrator",
        Priority.Medium,
        ChangeType.Feature);

    // ── Required field checks ─────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Submit_BlankTitle_ReturnsErrorForTitle(string? title)
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { Title = title! });
        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors.Where(e => e.FieldName == "Title"));
        Assert.Contains("required", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Submit_BlankDescription_ReturnsErrorForDescription(string description)
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { Description = description });
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.FieldName == "Description");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Submit_BlankRequester_ReturnsErrorForRequester(string requester)
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { Requester = requester });
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.FieldName == "Requester");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Submit_BlankAffectedSystem_ReturnsErrorForAffectedSystem(string system)
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { AffectedSystem = system });
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.FieldName == "AffectedSystem");
    }

    // ── Max-length checks ─────────────────────────────────────────────────────

    [Fact]
    public void Submit_TitleExactly200Chars_NoTitleError()
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { Title = new string('A', 200) });
        Assert.DoesNotContain(result.Errors, e => e.FieldName == "Title");
    }

    [Fact]
    public void Submit_Title201Chars_ReturnsLengthError()
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { Title = new string('A', 201) });
        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors.Where(e => e.FieldName == "Title"));
        Assert.Contains("200", error.Message);
    }

    [Fact]
    public void Submit_Description2001Chars_ReturnsLengthError()
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { Description = new string('B', 2001) });
        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors.Where(e => e.FieldName == "Description"));
        Assert.Contains("2000", error.Message);
    }

    [Fact]
    public void Submit_Requester101Chars_ReturnsLengthError()
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { Requester = new string('C', 101) });
        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors.Where(e => e.FieldName == "Requester"));
        Assert.Contains("100", error.Message);
    }

    [Fact]
    public void Submit_AffectedSystem201Chars_ReturnsLengthError()
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { AffectedSystem = new string('D', 201) });
        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors.Where(e => e.FieldName == "AffectedSystem"));
        Assert.Contains("200", error.Message);
    }

    // ── Enum validation ───────────────────────────────────────────────────────

    [Fact]
    public void Submit_InvalidPriority_ReturnsErrorListingAcceptedValues()
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { Priority = (Priority)99 });
        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors.Where(e => e.FieldName == "Priority"));
        Assert.Contains("Low", error.Message);
        Assert.Contains("Medium", error.Message);
        Assert.Contains("High", error.Message);
        Assert.Contains("Critical", error.Message);
    }

    [Fact]
    public void Submit_InvalidChangeType_ReturnsErrorListingAcceptedValues()
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { ChangeType = (ChangeType)99 });
        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors.Where(e => e.FieldName == "ChangeType"));
        Assert.Contains("Feature", error.Message);
        Assert.Contains("Bugfix", error.Message);
        Assert.Contains("ConfigChange", error.Message);
    }

    // ── Collect-all semantics ─────────────────────────────────────────────────

    [Fact]
    public void Submit_TitleAndDescriptionBothBlank_ReturnsTwoErrors()
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { Title = "", Description = "" });
        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.FieldName == "Title");
        Assert.Contains(result.Errors, e => e.FieldName == "Description");
    }

    [Fact]
    public void Submit_AllFieldsInvalid_ReturnsErrorsForAllSixFields()
    {
        var svc = BuildSut();
        var result = svc.Submit(new SubmissionRequest("", "", "", "", (Priority)99, (ChangeType)99));
        Assert.False(result.IsSuccess);
        Assert.Equal(6, result.Errors.Count);
    }

    // ── Nothing persisted on failure ──────────────────────────────────────────

    [Fact]
    public void Submit_InvalidRequest_NothingPersistedInStore()
    {
        var store = new InMemoryChangeRequestStore();
        var svc = new ChangeRequestService(
            store,
            new FakeSystemClock(),
            new FakeIdGenerator(CrId, AuditId),
            new ChangeRequestValidator());

        svc.Submit(ValidBase() with { Title = "" });

        Assert.Null(store.FindById(CrId));
        Assert.Empty(store.GetAuditEntries(CrId));
    }

    [Fact]
    public void Submit_InvalidRequest_ChangeRequestIsNull()
    {
        var svc = BuildSut();
        var result = svc.Submit(ValidBase() with { Title = "" });
        Assert.Null(result.ChangeRequest);
    }
}
