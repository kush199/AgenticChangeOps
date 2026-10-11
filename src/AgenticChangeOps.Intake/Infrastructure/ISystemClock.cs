namespace AgenticChangeOps.Intake.Infrastructure;

public interface ISystemClock
{
    DateTime UtcNow { get; }
}
