using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Errors;
using AgenticChangeOps.Workflow;

namespace AgenticChangeOps.Api.Endpoints;

public static class WorkflowEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/change-requests/{id}/workflow", GetStatus)
            .WithName("GetWorkflowStatus")
            .WithSummary("Retrieve the workflow status of a change request")
            .Produces<WorkflowStatus>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        routes.MapPost("/change-requests/{id}/workflow/advance", AdvanceAsync)
            .WithName("AdvanceWorkflow")
            .WithSummary("Run the next allowed agent step for a change request")
            .Produces<RequirementsAdvanceResponse>(StatusCodes.Status201Created)
            .Produces<DesignAdvanceResponse>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);
    }

    private static IResult GetStatus(string id, IWorkflowService service)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var result = service.GetStatus(changeRequestId);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : RequestParsing.Error(ErrorMapper.ForQuery(result.Errors));
    }

    private static async Task<IResult> AdvanceAsync(string id, HttpContext context, IWorkflowService service)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var result = await service.AdvanceAsync(changeRequestId, context.RequestAborted);

        switch (result.Outcome)
        {
            case AdvanceOutcome.Advanced when result.Requirements is { Draft: { } draft }:
                return Results.Created(
                    $"/change-requests/{changeRequestId}/drafts/latest",
                    new RequirementsAdvanceResponse(WorkflowStep.RequirementsDraft, draft, result.Status!));

            case AdvanceOutcome.Advanced when result.Design is { Draft: { } design }:
                return Results.Created(
                    $"/change-requests/{changeRequestId}/designs/latest",
                    new DesignAdvanceResponse(WorkflowStep.DesignDraft, design, result.Status!));

            case AdvanceOutcome.NotFound:
                return RequestParsing.Error(ErrorMapper.ForQuery(result.Errors));

            case AdvanceOutcome.NothingDue:
                return RequestParsing.Error(ErrorMapper.NoStepDue(result.Status!.Reason!));

            case AdvanceOutcome.NotConfigured:
                return RequestParsing.Error(result.Step == WorkflowStep.RequirementsDraft
                    ? ErrorMapper.DraftingNotConfigured()
                    : ErrorMapper.DesignNotConfigured());

            case AdvanceOutcome.StepFailed when result.Requirements is not null:
                return RequestParsing.Error(ErrorMapper.ForDrafting(result.Requirements));

            case AdvanceOutcome.StepFailed when result.Design is not null:
                return RequestParsing.Error(ErrorMapper.ForDesigning(result.Design));

            default:
                return RequestParsing.Error(ErrorMapper.Unexpected());
        }
    }
}
