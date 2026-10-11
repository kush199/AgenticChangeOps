namespace AgenticChangeOps.Intake.Tests.Live;

/// <summary>
/// Marks a test that calls the real model. It is skipped unless a person explicitly opts in by
/// setting <see cref="OptInVariable"/> to 1, so the routine suite, CI, and a bare
/// <c>dotnet test</c> never reach a real model.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LiveModelFactAttribute : FactAttribute
{
    public const string OptInVariable = "AGENTICCHANGEOPS_LIVE_MODEL_CHECK";

    public LiveModelFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(OptInVariable) != "1")
            Skip = $"Live model check not enabled. Set {OptInVariable}=1 (and the model settings) to run it by hand.";
    }
}
