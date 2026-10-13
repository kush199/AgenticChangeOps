using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Errors;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Agents.Requirement;

namespace AgenticChangeOps.Api.Endpoints;

public static class DraftEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/change-requests/{id}/drafts", DraftAsync)
            .WithName("RequestDraft")
            .WithSummary("Request a requirements draft for a change request")
            .Produces<RequirementsDraft>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        routes.MapGet("/change-requests/{id}/drafts/latest", GetLatest)
            .WithName("GetLatestDraft")
            .WithSummary("Retrieve the latest requirements draft")
            .Produces<RequirementsDraft>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        routes.MapGet("/change-requests/{id}/drafts", GetHistory)
            .WithName("GetDraftHistory")
            .WithSummary("List every requirements draft version in ascending order")
            .Produces<IReadOnlyList<RequirementsDraft>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> DraftAsync(
        string id,
        HttpContext context,
        IRequirementsDraftingService service,
        DraftingAvailability availability)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        if (!availability.IsConfigured)
            return RequestParsing.Error(ErrorMapper.DraftingNotConfigured());

        var result = await service.DraftAsync(changeRequestId, context.RequestAborted);
        return result.IsSuccess
            ? Results.Created($"/change-requests/{changeRequestId}/drafts/latest", result.Draft)
            : RequestParsing.Error(ErrorMapper.ForDrafting(result));
    }

    private static IResult GetLatest(string id, IRequirementsDraftingService service)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var result = service.GetLatestDraft(changeRequestId);
        if (!result.IsSuccess)
            return RequestParsing.Error(ErrorMapper.ForQuery(result.Errors));

        return result.Value is null
            ? RequestParsing.Error(ErrorMapper.NoDraft())
            : Results.Ok(result.Value);
    }

    private static IResult GetHistory(string id, IRequirementsDraftingService service)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var result = service.GetDraftHistory(changeRequestId);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : RequestParsing.Error(ErrorMapper.ForQuery(result.Errors));
    }
}
