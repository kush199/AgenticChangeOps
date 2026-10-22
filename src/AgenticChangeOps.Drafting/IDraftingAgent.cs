using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Drafting;

/// <summary>
/// The parts of a drafting service that belong to one agent: its eligibility rules and basis, its
/// generator, its review of generated content, its check that the basis is still current, and
/// how it reads its own stored records. The engine decides when each is called, never what it does.
/// </summary>
public interface IDraftingAgent<TRecord, TBasis, TGenerated, TContent, TRejection>
    where TRecord : class
    where TBasis : class
    where TGenerated : class
    where TContent : class
    where TRejection : struct, Enum
{
    /// <summary>
    /// True only when a cancellation raised by the generator, while the caller's own token is
    /// cancelled, must be rethrown. Otherwise every generator exception becomes a failure result.
    /// </summary>
    bool PropagatesCancellation { get; }

    /// <summary>Called before generation and again inside the locked step.</summary>
    (TBasis? Basis, ValidationError? Error) CheckEligibility(Guid changeRequestId, ChangeRequest? request);

    Task<TGenerated?> GenerateAsync(ChangeRequest request, TBasis basis, CancellationToken cancellationToken);

    ReviewOutcome<TContent, TRejection> Review(TGenerated? generated);

    /// <summary>Compares the basis used for generation with the one found inside the locked step (the id is for the agent's own message).</summary>
    ValidationError? CheckStillCurrent(Guid changeRequestId, TBasis usedForGeneration, TBasis current);

    int CountExisting(Guid changeRequestId);

    IReadOnlyList<TRecord> ReadAll(Guid changeRequestId);
}
