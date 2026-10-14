using System.Text.Json.Serialization;

namespace AgenticChangeOps.Workflow;

/// <summary>One finished agent step, as recorded by the agent that produced it.</summary>
/// <param name="RequirementsVersion">
/// For a design step, the requirements version it was based on. Null, and omitted from JSON,
/// for a requirements step.
/// </param>
public sealed record CompletedStep(
    WorkflowStep Step,
    int Version,
    DateTime GeneratedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? RequirementsVersion);
