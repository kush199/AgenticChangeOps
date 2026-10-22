using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Drafting;

/// <summary>
/// An agent's own save, run inside the engine's locked step. Builds the agent's record and audit
/// entries and calls the agent's own atomic store save, so the record and its audit entry are
/// stored together or not at all. Anything specific to one call (for example a marker the agent
/// accepts) is carried by the delegate, never by the engine.
/// </summary>
public delegate TRecord SaveStep<out TRecord, in TBasis, in TContent>(
    ChangeRequest current, TBasis basis, TContent content, int version, DateTime generatedAt);
