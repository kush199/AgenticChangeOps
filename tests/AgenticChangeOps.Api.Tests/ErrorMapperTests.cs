using System.Text.Json;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Errors;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Api.Tests;

public class ErrorMapperTests
{
    [Fact]
    public void Submission_ValidationErrors_MapTo400ListingEveryError()
    {
        var errors = new[]
        {
            new ValidationError("Title", "Title is required."),
            new ValidationError("Priority", "Priority value is not valid.")
        };

        var (status, body) = ErrorMapper.ForSubmission(errors);

        Assert.Equal(400, status);
        Assert.Equal("validation-failed", body.Code);
        Assert.Equal(new[] { "Title", "Priority" }, body.Errors!.Select(e => e.Field).ToArray());
    }

    [Fact]
    public void Query_ChangeRequestIdError_MapsTo404NotFound()
    {
        var errors = new[] { new ValidationError("ChangeRequestId", "No change request with that id exists.") };

        var (status, body) = ErrorMapper.ForQuery(errors);

        Assert.Equal(404, status);
        Assert.Equal("not-found", body.Code);
    }

    [Fact]
    public void Decision_OnlyStatusErrors_MapTo409ConflictCarryingTheMessage()
    {
        var errors = new[] { new ValidationError("Status", "Change request is already Approved and in a final state.") };

        var (status, body) = ErrorMapper.ForDecision(errors);

        Assert.Equal(409, status);
        Assert.Equal("conflict", body.Code);
        var field = Assert.Single(body.Errors!);
        Assert.Equal("Status", field.Field);
        Assert.Equal("Change request is already Approved and in a final state.", field.Message);
    }

    [Fact]
    public void Decision_FieldErrorAlongsideStatus_MapsTo400ListingAll()
    {
        var errors = new[]
        {
            new ValidationError("Reason", "Reason is required when the decision is Reject."),
            new ValidationError("Status", "Change request is already Approved and in a final state.")
        };

        var (status, body) = ErrorMapper.ForDecision(errors);

        Assert.Equal(400, status);
        Assert.Equal("validation-failed", body.Code);
        Assert.Equal(new[] { "Reason", "Status" }, body.Errors!.Select(e => e.Field).ToArray());
    }

    [Fact]
    public void Decision_ChangeRequestIdError_MapsTo404AndTakesPrecedence()
    {
        var errors = new[]
        {
            new ValidationError("ApproverName", "ApproverName is required."),
            new ValidationError("ChangeRequestId", "Change request was not found.")
        };

        var (status, body) = ErrorMapper.ForDecision(errors);

        Assert.Equal(404, status);
        Assert.Equal("not-found", body.Code);
    }

    [Theory]
    [InlineData(GenerationFailureCategory.Unreachable)]
    [InlineData(GenerationFailureCategory.TimedOut)]
    [InlineData(GenerationFailureCategory.ResponseTooLarge)]
    [InlineData(GenerationFailureCategory.MalformedOutput)]
    public void Drafting_ResultWithCategory_MapsTo502CarryingOnlyThatCategory(GenerationFailureCategory category)
    {
        var errors = new[] { new ValidationError("Generator", $"The content generator failed ({category}).") };
        var result = DraftingResult.Failure(errors, category);

        var (status, body) = ErrorMapper.ForDrafting(result);

        Assert.Equal(502, status);
        Assert.Equal("generation-failed", body.Code);
        Assert.Equal(category.ToString(), body.Category);
        Assert.Null(body.Errors);
    }

    [Fact]
    public void Drafting_SectionErrorsWithoutCategory_MapTo502MalformedOutput()
    {
        var result = DraftingResult.Failure(new[] { new ValidationError("Summary", "Summary is required and must not be blank.") });

        var (status, body) = ErrorMapper.ForDrafting(result);

        Assert.Equal(502, status);
        Assert.Equal("generation-failed", body.Code);
        Assert.Equal("MalformedOutput", body.Category);
        Assert.Null(body.Errors);
    }

    [Fact]
    public void Drafting_StatusError_MapsTo409Conflict()
    {
        var result = DraftingResult.Failure(new[] { new ValidationError("Status", "Cannot be drafted in status Approved.") });

        var (status, body) = ErrorMapper.ForDrafting(result);

        Assert.Equal(409, status);
        Assert.Equal("conflict", body.Code);
        Assert.Equal("Cannot be drafted in status Approved.", Assert.Single(body.Errors!).Message);
    }

    [Fact]
    public void Drafting_ChangeRequestIdError_MapsTo404()
    {
        var result = DraftingResult.Failure(new[] { new ValidationError("ChangeRequestId", "No change request exists.") });

        var (status, body) = ErrorMapper.ForDrafting(result);

        Assert.Equal(404, status);
        Assert.Equal("not-found", body.Code);
    }

    [Fact]
    public void Drafting_GeneratorErrorWithoutCategory_MapsTo500Unexpected()
    {
        var result = DraftingResult.Failure(new[] { new ValidationError("Generator", "The content generator failed (SomeException).") });

        var (status, body) = ErrorMapper.ForDrafting(result);

        Assert.Equal(500, status);
        Assert.Equal("unexpected-error", body.Code);
        Assert.DoesNotContain("SomeException", body.Message);
        Assert.Null(body.Errors);
    }

    [Fact]
    public void ErrorResponse_OmitsNullMembersWhenSerialised()
    {
        var json = JsonSerializer.Serialize(
            new ErrorResponse("not-found", "No such change request."),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("errors", json);
        Assert.DoesNotContain("category", json);
    }
}
