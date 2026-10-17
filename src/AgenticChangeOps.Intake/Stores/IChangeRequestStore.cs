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
    /// Stores a new change request and its audit entry together, or neither. Rejects an entry
    /// whose change request differs, and an id that already exists.
    /// </summary>
    void SaveWithAudit(ChangeRequest changeRequest, ChangeRequestAuditEntry auditEntry);

    /// <summary>
    /// Replaces a stored change request and stores its audit entry together, or neither. Rejects
    /// an entry whose change request differs, and an id that does not exist.
    /// </summary>
    void UpdateWithAudit(ChangeRequest changeRequest, ChangeRequestAuditEntry auditEntry);

    /// <summary>
    /// Stores an agent output and its audit entry together, or neither. Rejects an output whose
    /// change request differs from the entry's, and a version already stored for the same change
    /// request and output type.
    /// </summary>
    void SaveAgentOutput<T>(T output, ChangeRequestAuditEntry auditEntry) where T : IAgentOutput;

    /// <summary>Returns the outputs of one type for a change request in ascending version order.</summary>
    IReadOnlyList<T> GetAgentOutputs<T>(Guid changeRequestId) where T : IAgentOutput;

    /// <summary>The ids of every stored change request, in the order they were saved.</summary>
    IReadOnlyList<Guid> ListChangeRequestIds();

    /// <summary>The automatic-advancement attempt record for a change request, or null.</summary>
    AutomaticAttempt? GetAutomaticAttempt(Guid changeRequestId);

    /// <summary>Stores the attempt record, replacing any existing one for the same change request.</summary>
    void SaveAutomaticAttempt(AutomaticAttempt attempt);

    /// <summary>Removes the attempt record for a change request. No error when there is none.</summary>
    void ClearAutomaticAttempt(Guid changeRequestId);
}
