using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Workflow;

/// <summary>Whether automatic advancement is off, on, or has left the request for a person.</summary>
public enum AutomaticMode
{
    Off,
    On,
    LeftForPerson
}

/// <summary>
/// Shown inside <see cref="WorkflowStatus"/>. The category is set only for
/// <see cref="AutomaticMode.LeftForPerson"/> and is the only failure detail ever carried.
/// </summary>
public sealed record AutomaticAdvancementStatus(
    AutomaticMode Mode,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    GenerationFailureCategory? FailureCategory = null);
