using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Workflow;

namespace AgenticChangeOps.Api.Endpoints;

/// <summary>Read-only operator endpoints.</summary>
public static class OperatorEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/operator/automatic-advancement", GetSummary)
            .WithName("GetAutomaticAdvancementSummary")
            .WithSummary("Show whether automatic advancement is on and the latest check summary")
            .Produces<OperatorSummaryResponse>();
    }

    private static IResult GetSummary(AutomaticAdvancementSettings settings, CheckSummaryHolder holder)
    {
        var latest = holder.Latest;
        return Results.Ok(new OperatorSummaryResponse(
            settings.Enabled,
            latest is null ? null : new LastCheckResponse(latest.CheckedAt, latest.Examined, latest.Ran, latest.Failed)));
    }
}
