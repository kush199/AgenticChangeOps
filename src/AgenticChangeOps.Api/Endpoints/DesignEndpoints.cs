using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Errors;
using AgenticChangeOps.Intake.Services;
using System.Globalization;

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

        routes.MapPost("/change-requests/{id}/designs/{version}/decisions", DecideAsync)
            .WithName("RecordDesignDecision")
            .WithSummary("Record an Accept or Reject decision on a design draft")
            .Accepts<DesignDecisionBody>("application/json")
            .Produces<DesignDecision>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        routes.MapGet("/change-requests/{id}/designs/latest", GetLatest)
            .WithName("GetLatestDesign")
            .WithSummary("Retrieve the latest design draft")
            .Produces<DesignView>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        routes.MapGet("/change-requests/{id}/designs", GetHistory)
            .WithName("GetDesignHistory")
            .WithSummary("List every design draft version in ascending order")
            .Produces<IReadOnlyList<DesignView>>()
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

    private static async Task<IResult> DecideAsync(
        string id, string version, HttpContext context, IDesignDecisionService service)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        if (!int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var designVersion)
            || designVersion < 1)
            return RequestParsing.Error(ErrorMapper.InvalidDesignVersion());

        var body = await RequestParsing.ReadBodyAsync<DesignDecisionBody>(context);
        if (body is null)
            return RequestParsing.Error(ErrorMapper.InvalidBody());

        // The identifier and version come from the route only; any in the body are ignored.
        var result = service.Decide(
            changeRequestId,
            designVersion,
            body.ApproverName ?? string.Empty,
            RequestParsing.ParseEnum<DesignVerdict>(body.Decision),
            body.Reason);

        return result.IsSuccess
            ? Results.Created($"/change-requests/{changeRequestId}/designs/latest", result.Decision)
            : RequestParsing.Error(ErrorMapper.ForDesignDecision(result));
    }

    private static IResult GetLatest(string id, IDesignDraftingService service, IDesignDecisionService decisions)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var result = service.GetLatestDesign(changeRequestId);
        if (!result.IsSuccess)
            return RequestParsing.Error(ErrorMapper.ForQuery(result.Errors));

        if (result.Value is null)
            return RequestParsing.Error(ErrorMapper.NoDesignDraft());

        var decision = decisions.GetDecisions(changeRequestId).FirstOrDefault(d => d.Version == result.Value.Version);
        return Results.Ok(DesignView.From(result.Value, decision));
    }

    private static IResult GetHistory(string id, IDesignDraftingService service, IDesignDecisionService decisions)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var result = service.GetDesignHistory(changeRequestId);
        if (!result.IsSuccess)
            return RequestParsing.Error(ErrorMapper.ForQuery(result.Errors));

        var byVersion = decisions.GetDecisions(changeRequestId).ToDictionary(d => d.Version);
        return Results.Ok(result.Value!
            .Select(d => DesignView.From(d, byVersion.GetValueOrDefault(d.Version)))
            .ToList());
    }
}
