namespace AgenticChangeOps.Drafting;

/// <summary>
/// The basis of an agent that is generated from the change request alone. Lets such an agent use
/// the engine without inventing a basis type.
/// </summary>
public sealed class NoBasis
{
    public static NoBasis Instance { get; } = new();

    private NoBasis()
    {
    }
}
