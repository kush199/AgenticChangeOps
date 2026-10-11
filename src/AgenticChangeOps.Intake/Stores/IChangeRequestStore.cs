using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Intake.Stores;

public interface IChangeRequestStore
{
    void Save(ChangeRequest changeRequest);
    void Update(ChangeRequest changeRequest);
    ChangeRequest? FindById(Guid id);
    void SaveAuditEntry(ChangeRequestAuditEntry entry);
    IReadOnlyList<ChangeRequestAuditEntry> GetAuditEntries(Guid changeRequestId);

    /// <summary>Stores a draft and its audit entry together, or neither.</summary>
    void SaveDraft(RequirementsDraft draft, ChangeRequestAuditEntry auditEntry);

    /// <summary>Returns the drafts of a change request in ascending version order.</summary>
    IReadOnlyList<RequirementsDraft> GetDrafts(Guid changeRequestId);
}
