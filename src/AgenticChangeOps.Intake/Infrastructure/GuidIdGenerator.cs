namespace AgenticChangeOps.Intake.Infrastructure;

public sealed class GuidIdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.NewGuid();
}
