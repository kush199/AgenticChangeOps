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
    private readonly Dictionary<(Guid ChangeRequestId, Type OutputType), List<IAgentOutput>> _outputs = new();

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

    public void SaveAgentOutput<T>(T output, ChangeRequestAuditEntry auditEntry) where T : IAgentOutput
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(auditEntry);

        // All checks happen before any mutation so the call stores both items or neither.
        if (output.ChangeRequestId != auditEntry.ChangeRequestId)
            throw new ArgumentException(
                "The output and its audit entry must belong to the same change request.",
                nameof(auditEntry));

        var key = (output.ChangeRequestId, typeof(T));
        lock (_syncRoot)
        {
            if (_outputs.TryGetValue(key, out var existing) &&
                existing.Any(o => o.Version == output.Version))
                throw new InvalidOperationException(
                    $"Output version {output.Version} already exists for change request {output.ChangeRequestId}.");

            if (existing is null)
            {
                existing = new List<IAgentOutput>();
                _outputs[key] = existing;
            }
            existing.Add(output);
            SaveAuditEntry(auditEntry);
        }
    }

    public IReadOnlyList<T> GetAgentOutputs<T>(Guid changeRequestId) where T : IAgentOutput
    {
        lock (_syncRoot)
        {
            if (!_outputs.TryGetValue((changeRequestId, typeof(T)), out var list))
                return Array.Empty<T>();
            return list.Cast<T>().OrderBy(o => o.Version).ToList().AsReadOnly();
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
