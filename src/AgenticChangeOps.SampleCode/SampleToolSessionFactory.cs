using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.SampleCode;

/// <summary>Opens a <see cref="SampleToolSession"/> per request, or reports the tools unavailable.</summary>
public sealed class SampleToolSessionFactory : IToolSessionFactory
{
    public SampleToolSessionFactory(SampleToolSettings settings, string sampleRoot, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(sampleRoot);
        Settings = settings;
        SampleRoot = sampleRoot;
        Time = time;
    }

    public SampleToolSettings Settings { get; }
    public string SampleRoot { get; }
    public TimeProvider? Time { get; }

    public async Task<IToolSession> OpenAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(SampleRoot) || !Directory.EnumerateFiles(SampleRoot, "*", SearchOption.AllDirectories).Any())
                throw new DraftGenerationException(GenerationFailureCategory.ToolsUnavailable);

            return await SampleToolSession.OpenAsync(Settings, SampleRoot, Time, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DraftGenerationException)
        {
            throw;
        }
        catch (Exception)
        {
            // Whatever went wrong is dropped on purpose: its message could carry a real path.
            throw new DraftGenerationException(GenerationFailureCategory.ToolsUnavailable);
        }
    }
}
