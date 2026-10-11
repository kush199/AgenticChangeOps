using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Intake.Stores;

public sealed class InMemoryChangeRequestStore : IChangeRequestStore
{
    private readonly Dictionary<Guid, ChangeRequest> _changeRequests = new();
    private readonly Dictionary<Guid, List<ChangeRequestAuditEntry>> _auditEntries = new();

    public void Save(ChangeRequest changeRequest)
    {
        if (_changeRequests.ContainsKey(changeRequest.ChangeRequestId))
            throw new InvalidOperationException(
                $"A change request with id {changeRequest.ChangeRequestId} already exists.");
        _changeRequests[changeRequest.ChangeRequestId] = changeRequest;
    }

    public void Update(ChangeRequest changeRequest)
    {
        if (!_changeRequests.ContainsKey(changeRequest.ChangeRequestId))
            throw new InvalidOperationException(
                $"No change request with id {changeRequest.ChangeRequestId} exists to update.");
        _changeRequests[changeRequest.ChangeRequestId] = changeRequest;
    }

    public ChangeRequest? FindById(Guid id) =>
        _changeRequests.TryGetValue(id, out var cr) ? cr : null;

    public void SaveAuditEntry(ChangeRequestAuditEntry entry)
    {
        if (!_auditEntries.TryGetValue(entry.ChangeRequestId, out var list))
        {
            list = new List<ChangeRequestAuditEntry>();
            _auditEntries[entry.ChangeRequestId] = list;
        }
        list.Add(entry);
    }

    public IReadOnlyList<ChangeRequestAuditEntry> GetAuditEntries(Guid changeRequestId)
    {
        if (!_auditEntries.TryGetValue(changeRequestId, out var list))
            return Array.Empty<ChangeRequestAuditEntry>();
        return list.OrderBy(e => e.OccurredAt).ToList().AsReadOnly();
    }
}
