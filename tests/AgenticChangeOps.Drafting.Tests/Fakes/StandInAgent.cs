using AgenticChangeOps.Drafting;
using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Drafting.Tests.Fakes;

/// <summary>The stand-in agent's own stored output. Defined here, so the engine knows nothing of it.</summary>
public sealed record StandInRecord(Guid ChangeRequestId, int Version, string Text, int BasisVersion) : IAgentOutput;

/// <summary>The stand-in's basis: a version number the content was generated against.</summary>
public sealed class StandInBasis
{
    public StandInBasis(int version) => Version = version;

    public int Version { get; }
}

public sealed class StandInGenerated
{
    public StandInGenerated(string? text) => Text = text;

    public string? Text { get; }
}

public sealed class StandInContent
{
    public StandInContent(string text) => Text = text;

    public string Text { get; }
}

public enum StandInRejection
{
    Forbidden
}

/// <summary>
/// A throwaway agent with its own types and rules, scripted per test. It is the fourth agent of
/// the skeleton's tests: it uses only the Drafting, Intake and ModelAccess projects.
/// </summary>
public sealed class StandInAgent
    : IDraftingAgent<StandInRecord, StandInBasis, StandInGenerated, StandInContent, StandInRejection>
{
    public const string NotEligibleField = "StandInStatus";
    public const string BasisField = "StandInBasis";

    private readonly IChangeRequestStore _store;

    public StandInAgent(IChangeRequestStore store) => _store = store;

    /// <summary>Everything the engine asked of the agent, in order.</summary>
    public List<string> Calls { get; } = new();

    public int GenerateCalls { get; private set; }

    public bool PropagatesCancellation { get; set; }

    /// <summary>Only requests in this status are eligible.</summary>
    public ChangeRequestStatus EligibleStatus { get; set; } = ChangeRequestStatus.Approved;

    /// <summary>The basis eligibility currently finds. Tests change it to simulate a draft changing.</summary>
    public StandInBasis CurrentBasis { get; set; } = new(1);

    public Func<CancellationToken, Task<StandInGenerated?>> Generate { get; set; } =
        _ => Task.FromResult<StandInGenerated?>(new StandInGenerated("valid"));

    /// <summary>Runs inside the generator call, to change the world while the engine is waiting.</summary>
    public Action? DuringGeneration { get; set; }

    public (StandInBasis? Basis, ValidationError? Error) CheckEligibility(Guid changeRequestId, ChangeRequest? request)
    {
        Calls.Add("eligibility");
        if (request is null)
            return (null, DraftingErrors.NotFound(changeRequestId));
        if (request.Status != EligibleStatus)
            return (null, new ValidationError(NotEligibleField, $"Status {request.Status} is not eligible."));
        return (CurrentBasis, null);
    }

    public async Task<StandInGenerated?> GenerateAsync(
        ChangeRequest request, StandInBasis basis, CancellationToken cancellationToken)
    {
        Calls.Add("generate");
        GenerateCalls++;
        DuringGeneration?.Invoke();
        return await Generate(cancellationToken).ConfigureAwait(false);
    }

    public ReviewOutcome<StandInContent, StandInRejection> Review(StandInGenerated? generated)
    {
        Calls.Add("review");
        if (generated?.Text is null or "invalid")
            return ReviewOutcome<StandInContent, StandInRejection>.Invalid(
                new[] { new ValidationError("Text", "The text is unusable.") });
        if (generated.Text == "forbidden")
            return ReviewOutcome<StandInContent, StandInRejection>.Rejected(StandInRejection.Forbidden);
        return ReviewOutcome<StandInContent, StandInRejection>.Valid(new StandInContent(generated.Text));
    }

    public ValidationError? CheckStillCurrent(Guid changeRequestId, StandInBasis usedForGeneration, StandInBasis current)
    {
        Calls.Add("current");
        return usedForGeneration.Version == current.Version
            ? null
            : new ValidationError(BasisField, "The basis changed while generating.");
    }

    public int CountExisting(Guid changeRequestId) => _store.GetAgentOutputs<StandInRecord>(changeRequestId).Count;

    public IReadOnlyList<StandInRecord> ReadAll(Guid changeRequestId) =>
        _store.GetAgentOutputs<StandInRecord>(changeRequestId);

    /// <summary>The stand-in's save: its own record and its own audit entry, stored together by the store.</summary>
    public StandInRecord Save(
        IIdGenerator ids, ChangeRequest current, StandInBasis basis, StandInContent content, int version, DateTime at)
    {
        Calls.Add("save");
        var record = new StandInRecord(current.ChangeRequestId, version, content.Text, basis.Version);
        var entry = new ChangeRequestAuditEntry(
            AuditEntryId: ids.NewId(),
            ChangeRequestId: current.ChangeRequestId,
            EventType: AuditEventType.DraftGenerated,
            OccurredAt: at,
            Details: null,
            StatusBefore: current.Status,
            StatusAfter: current.Status,
            AgentName: "StandInAgent",
            DraftVersion: version);
        _store.SaveAgentOutput(record, entry);
        return record;
    }
}
