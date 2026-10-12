using AgenticChangeOps.Api.Contracts;
using Microsoft.AspNetCore.Http.Features;

namespace AgenticChangeOps.Api.Errors;

/// <summary>
/// Guarantees JSON error bodies: host and size refusals, routing 404 and 405, and a generic
/// body for unexpected failures. Nothing about an exception except its type name is logged.
/// </summary>
public sealed class JsonErrorMiddleware
{
    public const long MaxBodyBytes = 64 * 1024;
    public const string AllowedHostsKey = "ChangeOps:AllowedHosts";
    private const string DefaultAllowedHosts = "localhost;127.0.0.1;[::1]";

    private readonly RequestDelegate _next;
    private readonly ILogger<JsonErrorMiddleware> _logger;
    private readonly string[] _allowedHosts;

    public JsonErrorMiddleware(RequestDelegate next, ILogger<JsonErrorMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        var configured = configuration[AllowedHostsKey];
        _allowedHosts = (string.IsNullOrWhiteSpace(configured) ? DefaultAllowedHosts : configured)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await InvokeCoreAsync(context);
        LogRefusal(context);
    }

    // One line per refused or failed request: matched route template, status and outcome code.
    // Never a raw path, query, header, body, exception message or configuration value.
    private void LogRefusal(HttpContext context)
    {
        var status = context.Response.StatusCode;
        if (status < 400 || !context.Items.TryGetValue(ErrorResult.OutcomeCodeKey, out var code))
            return;

        var template = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
        _logger.LogWarning(
            "Request refused: {Method} {Route} -> {Status} {Code}.",
            context.Request.Method, template, status, code);
    }

    private async Task InvokeCoreAsync(HttpContext context)
    {
        try
        {
            if (!IsAllowedHost(context.Request.Host.Host))
            {
                await WriteAsync(context, ErrorMapper.InvalidHost());
                return;
            }

            if (context.Request.ContentLength is > MaxBodyBytes)
            {
                await WriteAsync(context, ErrorMapper.BodyTooLarge());
                return;
            }

            var sizeLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (sizeLimit is { IsReadOnly: false })
                sizeLimit.MaxRequestBodySize = MaxBodyBytes;

            await _next(context);

            // Routing answers unknown routes and methods with an empty body; give them JSON.
            var response = context.Response;
            if (!response.HasStarted && response.ContentLength is null && response.ContentType is null)
            {
                if (response.StatusCode == StatusCodes.Status404NotFound)
                    await WriteAsync(context, ErrorMapper.RouteNotFound());
                else if (response.StatusCode == StatusCodes.Status415UnsupportedMediaType)
                    await WriteAsync(context, ErrorMapper.UnsupportedMediaType());
                else if (response.StatusCode == StatusCodes.Status405MethodNotAllowed)
                    await WriteAsync(context, ErrorMapper.MethodNotAllowed());
            }
        }
        catch (BadHttpRequestException ex) when (!context.Response.HasStarted)
        {
            await WriteAsync(
                context,
                ex.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? ErrorMapper.BodyTooLarge()
                    : ErrorMapper.InvalidBody());
        }
        catch (Exception ex)
        {
            // Only the type name is logged: messages from real clients can carry endpoints or
            // request details, and the exception object itself would print them.
            _logger.LogError("Unhandled failure of type {ExceptionType}.", ex.GetType().Name);

            if (context.Response.HasStarted)
                return;

            context.Response.Clear();
            await WriteAsync(context, ErrorMapper.Unexpected());
        }
    }

    private bool IsAllowedHost(string host)
    {
        foreach (var allowed in _allowedHosts)
        {
            if (allowed == "*" || string.Equals(allowed, host, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static Task WriteAsync(HttpContext context, (int StatusCode, ErrorResponse Body) error) =>
        new ErrorResult(error.StatusCode, error.Body).ExecuteAsync(context);
}
