using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Intake.Stores;

/// <summary>
/// In-memory store, safe for use from several threads. Every access takes <see cref="SyncRoot"/>;
/// the monitor is re-entrant, so a service can hold it across a check-then-write sequence.
/// </summary>
public sealed class InMemoryChangeRequestStore : IChangeRequestStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, ChangeRequest> _changeRequests = new();
    private readonly Dictionary<Guid, List<ChangeRequestAuditEntry>> _auditEntries = new();
    private readonly Dictionary<Guid, List<RequirementsDraft>> _drafts = new();

    public object SyncRoot => _syncRoot;

    public void Save(ChangeRequest changeRequest)
    {
        lock (_syncRoot)
        {
            if (_changeRequests.ContainsKey(changeRequest.ChangeRequestId))
                throw new InvalidOperationException(
                    $"A change request with id {changeRequest.ChangeRequestId} already exists.");
            _changeRequests[changeRequest.ChangeRequestId] = changeRequest;
        }
    }

    public void Update(ChangeRequest changeRequest)
    {
        lock (_syncRoot)
        {
            if (!_changeRequests.ContainsKey(changeRequest.ChangeRequestId))
                throw new InvalidOperationException(
                    $"No change request with id {changeRequest.ChangeRequestId} exists to update.");
            _changeRequests[changeRequest.ChangeRequestId] = changeRequest;
        }
    }

    public ChangeRequest? FindById(Guid id)
    {
        lock (_syncRoot)
            return _changeRequests.TryGetValue(id, out var cr) ? cr : null;
    }

    public void SaveAuditEntry(ChangeRequestAuditEntry entry)
    {
        lock (_syncRoot)
        {
            if (!_auditEntries.TryGetValue(entry.ChangeRequestId, out var list))
            {
                list = new List<ChangeRequestAuditEntry>();
                _auditEntries[entry.ChangeRequestId] = list;
            }
            list.Add(entry);
        }
    }

    public void SaveDraft(RequirementsDraft draft, ChangeRequestAuditEntry auditEntry)
    {
        // All checks happen before any mutation so the call stores both items or neither.
        if (draft.ChangeRequestId != auditEntry.ChangeRequestId)
            throw new ArgumentException(
                "The draft and its audit entry must belong to the same change request.",
                nameof(auditEntry));

        lock (_syncRoot)
        {
            if (_drafts.TryGetValue(draft.ChangeRequestId, out var existing) &&
                existing.Any(d => d.Version == draft.Version))
                throw new InvalidOperationException(
                    $"Draft version {draft.Version} already exists for change request {draft.ChangeRequestId}.");

            if (existing is null)
            {
                existing = new List<RequirementsDraft>();
                _drafts[draft.ChangeRequestId] = existing;
            }
            existing.Add(draft);
            SaveAuditEntry(auditEntry);
        }
    }

    public IReadOnlyList<RequirementsDraft> GetDrafts(Guid changeRequestId)
    {
        lock (_syncRoot)
        {
            if (!_drafts.TryGetValue(changeRequestId, out var list))
                return Array.Empty<RequirementsDraft>();
            return list.OrderBy(d => d.Version).ToList().AsReadOnly();
        }
    }

    public IReadOnlyList<ChangeRequestAuditEntry> GetAuditEntries(Guid changeRequestId)
    {
        lock (_syncRoot)
        {
            if (!_auditEntries.TryGetValue(changeRequestId, out var list))
                return Array.Empty<ChangeRequestAuditEntry>();
            return list.OrderBy(e => e.OccurredAt).ToList().AsReadOnly();
        }
    }
}
