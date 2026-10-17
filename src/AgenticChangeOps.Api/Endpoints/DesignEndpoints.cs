using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Errors;
using AgenticChangeOps.Intake.Services;

namespace AgenticChangeOps.Api.Endpoints;

public static class DesignEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/change-requests/{id}/designs", DesignAsync)
            .WithName("RequestDesign")
            .WithSummary("Request a design draft for an approved change request")
            .Produces<DesignDraft>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        routes.MapGet("/change-requests/{id}/designs/latest", GetLatest)
            .WithName("GetLatestDesign")
            .WithSummary("Retrieve the latest design draft")
            .Produces<DesignDraft>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        routes.MapGet("/change-requests/{id}/designs", GetHistory)
            .WithName("GetDesignHistory")
            .WithSummary("List every design draft version in ascending order")
            .Produces<IReadOnlyList<DesignDraft>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> DesignAsync(
        string id,
        HttpContext context,
        IDesignDraftingService service,
        IChangeRequestService requests,
        DesignAvailability availability)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        // An unknown identifier is reported whether or not design drafting is configured.
        if (requests.GetById(changeRequestId) is null)
            return RequestParsing.Error(ErrorMapper.NotFound());

        if (!availability.IsConfigured)
            return RequestParsing.Error(ErrorMapper.DesignNotConfigured());

        var result = await service.DesignAsync(changeRequestId, context.RequestAborted);
        return result.IsSuccess
            ? Results.Created($"/change-requests/{changeRequestId}/designs/latest", result.Draft)
            : RequestParsing.Error(ErrorMapper.ForDesigning(result));
    }

    private static IResult GetLatest(string id, IDesignDraftingService service)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var result = service.GetLatestDesign(changeRequestId);
        if (!result.IsSuccess)
            return RequestParsing.Error(ErrorMapper.ForQuery(result.Errors));

        return result.Value is null
            ? RequestParsing.Error(ErrorMapper.NoDesignDraft())
            : Results.Ok(result.Value);
    }

    private static IResult GetHistory(string id, IDesignDraftingService service)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var result = service.GetDesignHistory(changeRequestId);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : RequestParsing.Error(ErrorMapper.ForQuery(result.Errors));
    }
}
