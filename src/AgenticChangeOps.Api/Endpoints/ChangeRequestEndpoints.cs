using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Errors;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;

namespace AgenticChangeOps.Api.Endpoints;

public static class ChangeRequestEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/change-requests", SubmitAsync)
            .WithName("SubmitChangeRequest")
            .WithSummary("Submit a change request")
            .Accepts<SubmitChangeRequestBody>("application/json")
            .Produces<ChangeRequest>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status413PayloadTooLarge);

        routes.MapGet("/change-requests/{id}", Get)
            .WithName("GetChangeRequest")
            .WithSummary("Retrieve a change request by ChangeRequestId")
            .Produces<ChangeRequest>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        routes.MapGet("/change-requests/{id}/audit", GetAudit)
            .WithName("GetAuditHistory")
            .WithSummary("List a change request's audit history in recorded order")
            .Produces<IReadOnlyList<ChangeRequestAuditEntry>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> SubmitAsync(HttpContext context, IChangeRequestService service)
    {
        var body = await RequestParsing.ReadBodyAsync<SubmitChangeRequestBody>(context);
        if (body is null)
            return RequestParsing.Error(ErrorMapper.InvalidBody());

        var request = new SubmissionRequest(
            body.Title ?? string.Empty,
            body.Description ?? string.Empty,
            body.Requester ?? string.Empty,
            body.AffectedSystem ?? string.Empty,
            RequestParsing.ParseEnum<Priority>(body.Priority),
            RequestParsing.ParseEnum<ChangeType>(body.ChangeType));

        var result = service.Submit(request);
        if (!result.IsSuccess)
            return RequestParsing.Error(ErrorMapper.ForSubmission(result.Errors));

        var created = result.ChangeRequest!;
        return Results.Created($"/change-requests/{created.ChangeRequestId}", created);
    }

    private static IResult Get(string id, IChangeRequestService service)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var found = service.GetById(changeRequestId);
        return found is null
            ? RequestParsing.Error(ErrorMapper.NotFound())
            : Results.Ok(found);
    }

    private static IResult GetAudit(string id, IChangeRequestService service)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var history = service.GetAuditHistory(changeRequestId);
        return history is null
            ? RequestParsing.Error(ErrorMapper.NotFound())
            : Results.Ok(history);
    }
}
