using AgenticChangeOps.Intake.Infrastructure;

namespace AgenticChangeOps.Intake.Tests.Fakes;

public sealed class FakeSystemClock : ISystemClock
{
    public DateTime Now { get; set; } = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow => Now;
}
