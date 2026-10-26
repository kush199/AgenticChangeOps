using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Api.Errors;
using AgenticChangeOps.Intake.Services;

namespace AgenticChangeOps.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/change-requests/{id}/reviews", ReviewAsync)
            .WithName("RequestReview")
            .WithSummary("Request an advisory review of the latest change proposal for an approved change request")
            .Produces<Review>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> ReviewAsync(
        string id,
        HttpContext context,
        IReviewService service,
        IChangeRequestService requests,
        ReviewAvailability availability,
        ILoggerFactory loggers)
    {
        if (!RequestParsing.TryParseId(id, out var changeRequestId))
            return RequestParsing.Error(ErrorMapper.InvalidIdentifier());

        // An unknown identifier is reported whether or not reviews are configured.
        if (requests.GetById(changeRequestId) is null)
            return RequestParsing.Error(ErrorMapper.NotFound());

        if (!availability.IsConfigured)
            return RequestParsing.Error(ErrorMapper.ReviewNotConfigured());

        var result = await service.ReviewAsync(changeRequestId, context.RequestAborted);
        if (result.IsSuccess)
            return Results.Json(result.Review, statusCode: StatusCodes.Status201Created);

        LogNothingStored(loggers, result);
        return RequestParsing.Error(ErrorMapper.ForReviewing(result));
    }

    // One line per request that stores nothing: the rejection reason or failure category, or the
    // names of the rules that refused it. Never a finding, a reference, a path or model text.
    private static void LogNothingStored(ILoggerFactory loggers, ReviewResult result)
    {
        var outcome = result.Rejection is { } rejection ? $"rejected ({rejection})"
            : result.FailureCategory is { } category ? $"generation failed ({category})"
            : $"refused ({string.Join(", ", result.Errors.Select(e => e.FieldName).Distinct())})";

        loggers.CreateLogger("AgenticChangeOps.Api.Reviewing")
            .LogWarning("Review request stored nothing: {Outcome}.", outcome);
    }
}
