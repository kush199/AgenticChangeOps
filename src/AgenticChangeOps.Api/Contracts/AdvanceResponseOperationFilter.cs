using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AgenticChangeOps.Api.Contracts;

/// <summary>
/// Describes the advance operation's 201 body as one of the two typed responses. Two
/// <c>Produces</c> declarations for the same status code collapse to the last one, which would
/// leave the requirements form out of the API description.
/// </summary>
public sealed class AdvanceResponseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.OperationId != "AdvanceWorkflow")
            return;

        if (!operation.Responses.TryGetValue("201", out var response))
            return;

        var requirements = context.SchemaGenerator.GenerateSchema(
            typeof(RequirementsAdvanceResponse), context.SchemaRepository);
        var design = context.SchemaGenerator.GenerateSchema(
            typeof(DesignAdvanceResponse), context.SchemaRepository);

        foreach (var media in response.Content.Values)
            media.Schema = new OpenApiSchema { OneOf = new List<OpenApiSchema> { requirements, design } };
    }
}
