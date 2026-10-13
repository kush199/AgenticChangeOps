namespace AgenticChangeOps.Intake.Models;

/// <summary>
/// An agent's output record for a change request. Intake stores and returns outputs through
/// this contract without knowing which agent produced them.
/// </summary>
public interface IAgentOutput
{
    Guid ChangeRequestId { get; }

    /// <summary>1-based; unique per change request and output type.</summary>
    int Version { get; }
}
