using AgenticChangeOps.Intake.Infrastructure;

namespace AgenticChangeOps.Intake.Tests.Fakes;

public sealed class FakeIdGenerator : IIdGenerator
{
    private readonly Queue<Guid> _ids;

    public FakeIdGenerator(params Guid[] ids)
    {
        _ids = new Queue<Guid>(ids);
    }

    public Guid NewId()
    {
        if (_ids.Count == 0)
            throw new InvalidOperationException("FakeIdGenerator queue is exhausted.");
        return _ids.Dequeue();
    }
}
