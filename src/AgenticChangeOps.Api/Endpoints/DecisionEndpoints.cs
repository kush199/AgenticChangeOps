using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Errors;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;

namespace AgenticChangeOps.Api.Endpoints;

public static class DecisionEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/change-requests/{id}/decisions", DecideAsync)
            .WithName("RecordDecision")
            .WithSummary("Record an approval decision (Approve, Reject or Defer)")
            .Accepts<DecisionBody>("application/json")
            .Produces<ChangeRequest>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        routes.MapGet("/change-requests/{id}/clearance", GetClearance)
            .WithName("GetClearance")
            .WithSummary("Ask whether a change request is cleared to proceed")
            .Produces<ClearanceResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> DecideAsync(string id, HttpContext context, IApprovalService service)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var body = await RequestParsing.ReadBodyAsync<DecisionBody>(context);
        if (body is null)
            return RequestParsing.Error(ErrorMapper.InvalidBody());

        // The identifier comes from the route only; any identifier in the body is ignored.
        var request = new ApprovalDecisionRequest(
            changeRequestId,
            body.ApproverName ?? string.Empty,
            RequestParsing.ParseEnum<ApprovalDecision>(body.Decision),
            body.Reason);

        var result = service.RecordDecision(request);
        return result.IsSuccess
            ? Results.Ok(result.ChangeRequest)
            : RequestParsing.Error(ErrorMapper.ForDecision(result.Errors));
    }

    private static IResult GetClearance(string id, IChangeRequestService requests, IApprovalService approvals)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        // The gate answers false for an unknown id; existence is checked first so the caller
        // can tell "not cleared" from "no such request".
        if (requests.GetById(changeRequestId) is null)
            return RequestParsing.Error(ErrorMapper.NotFound());

        return Results.Ok(new ClearanceResponse(changeRequestId, approvals.IsClearedToProceed(changeRequestId)));
    }
}
