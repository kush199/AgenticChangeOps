namespace AgenticChangeOps.Api.Composition;

/// <summary>
/// Records once, at startup, whether drafting can run. Holds no setting value.
/// </summary>
public sealed class DraftingAvailability
{
    public DraftingAvailability(bool isConfigured)
    {
        IsConfigured = isConfigured;
    }

    public bool IsConfigured { get; }
}
