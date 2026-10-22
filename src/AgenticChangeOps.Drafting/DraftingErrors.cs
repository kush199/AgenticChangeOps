using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Drafting;

/// <summary>Errors that read the same for every agent.</summary>
public static class DraftingErrors
{
    public static ValidationError NotFound(Guid changeRequestId) =>
        new("ChangeRequestId", $"No change request with id {changeRequestId} exists.");
}
