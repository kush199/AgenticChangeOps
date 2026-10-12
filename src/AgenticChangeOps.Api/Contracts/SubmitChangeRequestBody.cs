namespace AgenticChangeOps.Api.Contracts;

/// <summary>
/// Transport shape for a submission. Enum fields are strings so an unparseable value can be
/// reported by the existing validation rather than failing the whole body.
/// </summary>
public sealed record SubmitChangeRequestBody(
    string? Title,
    string? Description,
    string? Requester,
    string? AffectedSystem,
    string? Priority,
    string? ChangeType);
