using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Workflow;

namespace AgenticChangeOps.Api.Contracts;

/// <summary>
/// The body of a successful advance that ran the requirements step: the draft it stored
/// (serialised exactly as the requirements route returns it) and the status afterwards.
/// </summary>
public sealed record RequirementsAdvanceResponse(
    WorkflowStep Step, RequirementsDraft Draft, WorkflowStatus Status);

/// <summary>
/// The body of a successful advance that ran the design step: the draft it stored
/// (serialised exactly as the design route returns it) and the status afterwards.
/// </summary>
public sealed record DesignAdvanceResponse(
    WorkflowStep Step, DesignDraft Draft, WorkflowStatus Status);
