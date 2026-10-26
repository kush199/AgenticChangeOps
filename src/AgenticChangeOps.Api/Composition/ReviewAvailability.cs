namespace AgenticChangeOps.Api.Composition;

/// <summary>Records once whether reviews can run. Holds no setting value.</summary>
public sealed class ReviewAvailability
{
    public ReviewAvailability(bool isConfigured)
    {
        IsConfigured = isConfigured;
    }

    public bool IsConfigured { get; }
}
