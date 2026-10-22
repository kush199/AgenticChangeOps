using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Errors;
using AgenticChangeOps.Intake.Services;

namespace AgenticChangeOps.Api.Endpoints;

public static class ProposalEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/change-requests/{id}/proposals", ProposeAsync)
            .WithName("RequestProposal")
            .WithSummary("Request a text-only change proposal for an approved change request")
            .Produces<ChangeProposal>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        routes.MapGet("/change-requests/{id}/proposals/latest", GetLatest)
            .WithName("GetLatestProposal")
            .WithSummary("Retrieve the latest change proposal")
            .Produces<ChangeProposal>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        routes.MapGet("/change-requests/{id}/proposals", GetHistory)
            .WithName("GetProposalHistory")
            .WithSummary("List every change proposal version in ascending order")
            .Produces<IReadOnlyList<ChangeProposal>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ProposeAsync(
        string id,
        HttpContext context,
        IProposalService service,
        IChangeRequestService requests,
        ProposalAvailability availability,
        ILoggerFactory loggers)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        // An unknown identifier is reported whether or not proposals are available.
        if (requests.GetById(changeRequestId) is null)
            return RequestParsing.Error(ErrorMapper.NotFound());

        switch (availability.State)
        {
            case ProposalAvailabilityState.ToolsUnavailable:
                return RequestParsing.Error(ErrorMapper.ProposalToolsUnavailable());
            case ProposalAvailabilityState.NotConfigured:
                return RequestParsing.Error(ErrorMapper.ProposalNotConfigured());
        }

        var result = await service.ProposeAsync(changeRequestId, context.RequestAborted);
        if (result.IsSuccess)
            return Results.Created($"/change-requests/{changeRequestId}/proposals/latest", result.Proposal);

        LogNothingStored(loggers, result);
        return RequestParsing.Error(ErrorMapper.ForProposing(result));
    }

    // One line per request that stores nothing: the rejection reason or failure category, or the
    // names of the rules that refused it. Never a path, a quote, model text or a setting value.
    private static void LogNothingStored(ILoggerFactory loggers, ProposalResult result)
    {
        var outcome = result.Rejection is { } rejection ? $"rejected ({rejection})"
            : result.FailureCategory is { } category ? $"generation failed ({category})"
            : $"refused ({string.Join(", ", result.Errors.Select(e => e.FieldName).Distinct())})";

        loggers.CreateLogger("AgenticChangeOps.Api.Proposing")
            .LogWarning("Proposal request stored nothing: {Outcome}.", outcome);
    }

    private static IResult GetLatest(string id, IProposalService service)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var result = service.GetLatestProposal(changeRequestId);
        if (!result.IsSuccess)
            return RequestParsing.Error(ErrorMapper.ForQuery(result.Errors));

        return result.Value is null
            ? RequestParsing.Error(ErrorMapper.NoProposal())
            : Results.Ok(result.Value);
    }

    private static IResult GetHistory(string id, IProposalService service)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        var result = service.GetProposalHistory(changeRequestId);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : RequestParsing.Error(ErrorMapper.ForQuery(result.Errors));
    }
}
