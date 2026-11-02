using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Agents.Architect.Tests.Fakes;

/// <summary>
/// Wraps a real in-memory store. Counts every call that could change a change request, and can
/// fail the one step that stores a design decision.
/// </summary>
public sealed class DecisionTestStore : IChangeRequestStore
{
    private readonly IChangeRequestStore _inner;

    public DecisionTestStore(IChangeRequestStore inner) => _inner = inner;

    public Exception? FailDecisionSave { get; set; }
    public int UpdateCalls { get; private set; }

    public object SyncRoot => _inner.SyncRoot;
    public void Save(ChangeRequest changeRequest) => _inner.Save(changeRequest);

    public void Update(ChangeRequest changeRequest)
    {
        UpdateCalls++;
        _inner.Update(changeRequest);
    }

    public ChangeRequest? FindById(Guid id) => _inner.FindById(id);
    public void SaveAuditEntry(ChangeRequestAuditEntry entry) => _inner.SaveAuditEntry(entry);
    public IReadOnlyList<ChangeRequestAuditEntry> GetAuditEntries(Guid changeRequestId) => _inner.GetAuditEntries(changeRequestId);
    public void SaveWithAudit(ChangeRequest changeRequest, ChangeRequestAuditEntry auditEntry) => _inner.SaveWithAudit(changeRequest, auditEntry);

    public void UpdateWithAudit(ChangeRequest changeRequest, ChangeRequestAuditEntry auditEntry)
    {
        UpdateCalls++;
        _inner.UpdateWithAudit(changeRequest, auditEntry);
    }

    public void SaveAgentOutput<T>(T output, ChangeRequestAuditEntry auditEntry) where T : IAgentOutput
    {
        if (typeof(T) == typeof(DesignDecision) && FailDecisionSave is not null)
            throw FailDecisionSave;
        _inner.SaveAgentOutput(output, auditEntry);
    }

    public IReadOnlyList<T> GetAgentOutputs<T>(Guid changeRequestId) where T : IAgentOutput => _inner.GetAgentOutputs<T>(changeRequestId);
    public IReadOnlyList<Guid> ListChangeRequestIds() => _inner.ListChangeRequestIds();
    public AutomaticAttempt? GetAutomaticAttempt(Guid changeRequestId) => _inner.GetAutomaticAttempt(changeRequestId);
    public void SaveAutomaticAttempt(AutomaticAttempt attempt) => _inner.SaveAutomaticAttempt(attempt);
    public void ClearAutomaticAttempt(Guid changeRequestId) => _inner.ClearAutomaticAttempt(changeRequestId);
}
