using AgenticChangeOps.Agents.Reviewer;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Api.Tests.Fakes;

/// <summary>
/// A real in-memory store whose only failure is reading reviews back, so a change request can be
/// built normally and then fail exactly at retrieval.
/// </summary>
public sealed class ReviewReadFailingStore : IChangeRequestStore
{
    private readonly InMemoryChangeRequestStore _inner = new();
    private readonly Exception _failure;

    public ReviewReadFailingStore(Exception failure) => _failure = failure;

    public object SyncRoot => _inner.SyncRoot;
    public void Save(ChangeRequest changeRequest) => _inner.Save(changeRequest);
    public void Update(ChangeRequest changeRequest) => _inner.Update(changeRequest);
    public ChangeRequest? FindById(Guid id) => _inner.FindById(id);
    public void SaveAuditEntry(ChangeRequestAuditEntry entry) => _inner.SaveAuditEntry(entry);
    public IReadOnlyList<ChangeRequestAuditEntry> GetAuditEntries(Guid changeRequestId) => _inner.GetAuditEntries(changeRequestId);
    public void SaveWithAudit(ChangeRequest changeRequest, ChangeRequestAuditEntry auditEntry) => _inner.SaveWithAudit(changeRequest, auditEntry);
    public void UpdateWithAudit(ChangeRequest changeRequest, ChangeRequestAuditEntry auditEntry) => _inner.UpdateWithAudit(changeRequest, auditEntry);
    public void SaveAgentOutput<T>(T output, ChangeRequestAuditEntry auditEntry) where T : IAgentOutput => _inner.SaveAgentOutput(output, auditEntry);

    public IReadOnlyList<T> GetAgentOutputs<T>(Guid changeRequestId) where T : IAgentOutput =>
        typeof(T) == typeof(Review) ? throw _failure : _inner.GetAgentOutputs<T>(changeRequestId);

    public IReadOnlyList<Guid> ListChangeRequestIds() => _inner.ListChangeRequestIds();
    public AutomaticAttempt? GetAutomaticAttempt(Guid changeRequestId) => _inner.GetAutomaticAttempt(changeRequestId);
    public void SaveAutomaticAttempt(AutomaticAttempt attempt) => _inner.SaveAutomaticAttempt(attempt);
    public void ClearAutomaticAttempt(Guid changeRequestId) => _inner.ClearAutomaticAttempt(changeRequestId);
}
