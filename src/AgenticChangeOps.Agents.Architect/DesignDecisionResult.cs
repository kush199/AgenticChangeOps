using AgenticChangeOps.Intake.Models;

namespace AgenticChangeOps.Agents.Architect;

public enum DesignDecisionRefusal
{
    NotFound,
    NotApproved,
    VersionNotFound,
    Outdated,
    AlreadyDecided,
    Invalid
}

public sealed class DesignDecisionResult
{
    private DesignDecisionResult(
        DesignDecision? decision, DesignDecisionRefusal? refusal, IReadOnlyList<ValidationError> errors)
    {
        Decision = decision;
        Refusal = refusal;
        Errors = errors;
    }

    public bool IsSuccess => Decision is not null;
    public DesignDecision? Decision { get; }
    public DesignDecisionRefusal? Refusal { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    public static DesignDecisionResult Success(DesignDecision decision) =>
        new(decision, null, Array.Empty<ValidationError>());

    public static DesignDecisionResult Failure(DesignDecisionRefusal refusal, IReadOnlyList<ValidationError> errors) =>
        new(null, refusal, errors);
}
