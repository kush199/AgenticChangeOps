using AgenticChangeOps.Api.Composition;
using AgenticChangeOps.Api.Endpoints;
using AgenticChangeOps.Api.Errors;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Load the shipped settings from beside the binaries so the working directory does not matter.
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false);

// Loopback only unless the operator supplies an address (ASPNETCORE_URLS or --urls).
builder.WebHost.UseUrls(ListenAddress.Resolve(
    name => builder.Configuration["urls"] ?? Environment.GetEnvironmentVariable(name)));
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = JsonErrorMiddleware.MaxBodyBytes);

builder.Services.AddChangeOpsApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AgenticChangeOps change request API",
        Version = "v1",
        Description =
            "Local-only JSON API over the intake, approval, requirements drafting and design drafting capabilities. " +
            "No authentication. Error bodies carry a code: " +
            "invalid-body (400), validation-failed (400), invalid-identifier (400), invalid-host (400), " +
            "not-found (404), no-draft (404), no-design-draft (404), route-not-found (404), method-not-allowed (405), " +
            "conflict (409), body-too-large (413), unsupported-media-type (415), generation-failed (502, with only a category), " +
            "drafting-not-configured (503), design-not-configured (503), unexpected-error (500)."
    });
});

var app = builder.Build();
app.UseMiddleware<JsonErrorMiddleware>();
app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "v1");
    options.RoutePrefix = "swagger";
});

ChangeRequestEndpoints.Map(app);
DecisionEndpoints.Map(app);
DraftEndpoints.Map(app);
DesignEndpoints.Map(app);
app.Run();

public partial class Program
{
}
