using System.Text.Json.Serialization;

namespace AgenticChangeOps.Api.Contracts;

/// <summary>
/// The body of every non-success response. Never carries prompt text, model output, endpoint,
/// deployment name, identity detail, exception text, stack traces, or paths.
/// </summary>
public sealed record ErrorResponse(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<ErrorField>? Errors = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Category = null);

public sealed record ErrorField(string Field, string Message);
