namespace AgenticChangeOps.ModelAccess;

/// <summary>Opens a <see cref="IToolSession"/> for one request.</summary>
public interface IToolSessionFactory
{
    /// <summary>
    /// Throws <see cref="DraftGenerationException"/> with <see cref="GenerationFailureCategory.ToolsUnavailable"/>
    /// when the tools cannot be made ready, and throws nothing else.
    /// </summary>
    Task<IToolSession> OpenAsync(CancellationToken cancellationToken);
}
