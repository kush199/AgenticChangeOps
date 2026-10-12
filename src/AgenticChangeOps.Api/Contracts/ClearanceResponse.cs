namespace AgenticChangeOps.Api.Contracts;

public sealed record ClearanceResponse(Guid ChangeRequestId, bool IsCleared);
