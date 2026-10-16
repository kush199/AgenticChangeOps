using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Persistence.Tests.Support;

namespace AgenticChangeOps.Persistence.Tests.Contract;

public sealed class SqliteStoreContractTests : ChangeRequestStoreContract
{
    private readonly TempDatabase _database = new();

    protected override IChangeRequestStore CreateStore() => new SqliteChangeRequestStore(_database.Options);

    public override void Dispose()
    {
        base.Dispose();
        _database.Dispose();
    }
}
