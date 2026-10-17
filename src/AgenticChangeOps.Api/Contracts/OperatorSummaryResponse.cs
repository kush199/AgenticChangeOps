namespace AgenticChangeOps.Api.Contracts;

/// <summary>Counts and times only; nothing here can carry model text or configuration values.</summary>
public sealed record OperatorSummaryResponse(bool Enabled, LastCheckResponse? LastCheck);

public sealed record LastCheckResponse(DateTime CheckedAt, int Examined, int Ran, int Failed);
