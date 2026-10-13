namespace AgenticChangeOps.Api.Composition;

/// <summary>
/// Records once, at startup, whether design drafting can run. Holds no setting value.
/// </summary>
public sealed class DesignAvailability
{
    public DesignAvailability(bool isConfigured)
    {
        IsConfigured = isConfigured;
    }

    public bool IsConfigured { get; }
}
