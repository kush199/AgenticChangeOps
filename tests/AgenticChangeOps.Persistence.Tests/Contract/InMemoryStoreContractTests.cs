using AgenticChangeOps.Intake.Stores;

namespace AgenticChangeOps.Persistence.Tests.Contract;

public sealed class InMemoryStoreContractTests : ChangeRequestStoreContract
{
    protected override IChangeRequestStore CreateStore() => new InMemoryChangeRequestStore();
}
