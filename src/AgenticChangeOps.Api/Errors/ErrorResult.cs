using AgenticChangeOps.Api.Contracts;

namespace AgenticChangeOps.Api.Errors;

/// <summary>
/// Writes an error body and records its outcome code on the request, so the middleware can log
/// the route template, status and code without ever reading a body.
/// </summary>
internal sealed class ErrorResult : IResult
{
    public const string OutcomeCodeKey = "AgenticChangeOps.OutcomeCode";

    private readonly int _statusCode;
    private readonly ErrorResponse _body;

    public ErrorResult(int statusCode, ErrorResponse body)
    {
        _statusCode = statusCode;
        _body = body;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Items[OutcomeCodeKey] = _body.Code;
        httpContext.Response.StatusCode = _statusCode;
        return httpContext.Response.WriteAsJsonAsync(_body);
    }
}
