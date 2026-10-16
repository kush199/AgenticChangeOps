namespace AgenticChangeOps.Intake.Stores;

public enum StorageFailureCategory
{
    Unavailable,
    UnsupportedLayout
}

/// <summary>
/// The storage cannot be opened or written, or holds a layout this version does not understand.
/// The message is fixed and there is never an inner exception, so no path, connection detail or
/// driver text can travel with it.
/// </summary>
public sealed class StorageUnavailableException : Exception
{
    public StorageUnavailableException(StorageFailureCategory category)
        : base("The change request storage is unavailable.")
    {
        Category = category;
    }

    public StorageFailureCategory Category { get; }
}

/// <summary>An overlapping request took the version number first.</summary>
public sealed class StorageVersionConflictException : Exception
{
    public StorageVersionConflictException()
        : base("The request conflicts with a concurrent change; try again.")
    {
    }
}
