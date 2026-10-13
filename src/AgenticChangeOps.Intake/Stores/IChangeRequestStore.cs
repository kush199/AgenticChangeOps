using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Intake.Stores;

public interface IChangeRequestStore
{
    /// <summary>
    /// The lock every store operation takes. A service that must check and then write as one
    /// uninterrupted step holds it for the whole sequence (the monitor is re-entrant).
    /// </summary>
    object SyncRoot { get; }

    void Save(ChangeRequest changeRequest);
    void Update(ChangeRequest changeRequest);
    ChangeRequest? FindById(Guid id);
    void SaveAuditEntry(ChangeRequestAuditEntry entry);
    IReadOnlyList<ChangeRequestAuditEntry> GetAuditEntries(Guid changeRequestId);

    /// <summary>
    /// Stores an agent output and its audit entry together, or neither. Rejects an output whose
    /// change request differs from the entry's, and a version already stored for the same change
    /// request and output type.
    /// </summary>
    void SaveAgentOutput<T>(T output, ChangeRequestAuditEntry auditEntry) where T : IAgentOutput;

    /// <summary>Returns the outputs of one type for a change request in ascending version order.</summary>
    IReadOnlyList<T> GetAgentOutputs<T>(Guid changeRequestId) where T : IAgentOutput;
}
