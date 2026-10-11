using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Intake.Stores;

public interface IChangeRequestStore
{
    void Save(ChangeRequest changeRequest);
    void Update(ChangeRequest changeRequest);
    ChangeRequest? FindById(Guid id);
    void SaveAuditEntry(ChangeRequestAuditEntry entry);
    IReadOnlyList<ChangeRequestAuditEntry> GetAuditEntries(Guid changeRequestId);
}
