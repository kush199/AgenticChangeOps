namespace AgenticChangeOps.ModelAccess;

/// <summary>
/// Reported by a content generator when it cannot produce content. Carries the failure
/// category only: the message is fixed per category and never contains prompt text, model
/// output, endpoint, deployment name, or identity detail.
/// </summary>
public sealed class DraftGenerationException : Exception
{
    public DraftGenerationException(GenerationFailureCategory category)
        : base(MessageFor(category))
    {
        Category = category;
    }

    public GenerationFailureCategory Category { get; }

    private static string MessageFor(GenerationFailureCategory category) => category switch
    {
        GenerationFailureCategory.Unreachable => "The model service could not be reached.",
        GenerationFailureCategory.TimedOut => "The model did not answer within the time limit.",
        GenerationFailureCategory.ResponseTooLarge => "The model answer exceeded the size limit.",
        GenerationFailureCategory.MalformedOutput => "The model answer could not be used.",
        _ => "The content generator failed."
    };
}
