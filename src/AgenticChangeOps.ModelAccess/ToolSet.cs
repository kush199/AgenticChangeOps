using System.Text.Json;

namespace AgenticChangeOps.ModelAccess;

/// <summary>
/// One tool as the model sees it: a name, a short description and the JSON schema of its arguments.
/// </summary>
public sealed record ToolDefinition(string Name, string Description, JsonElement InputSchema);

/// <summary>
/// The tools offered to the model for one request, fixed when the session is opened: their
/// definitions and one way to run a call by name. Free of any framework type so that the project
/// serving the tools needs none; the tool client turns it into whatever the framework expects.
/// Typed on purpose: nothing crosses a project boundary as an untyped object.
/// </summary>
public sealed class ToolSet
{
    private readonly Func<string, string, CancellationToken, Task<string>> _invoke;

    public ToolSet(
        IReadOnlyList<ToolDefinition> definitions,
        Func<string, string, CancellationToken, Task<string>> invoke)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(invoke);

        Definitions = definitions.ToArray();
        Names = Definitions.Select(d => d.Name).ToArray();
        _invoke = invoke;
    }

    public IReadOnlyList<ToolDefinition> Definitions { get; }

    /// <summary>The tool names, in the order they were offered.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>
    /// Runs one call. The arguments are a JSON object as text and the result is the text for the
    /// model. A name that is not in <see cref="Names"/> is never passed on.
    /// </summary>
    public Task<string> InvokeAsync(string name, string argumentsJson, CancellationToken cancellationToken)
    {
        if (!Names.Contains(name, StringComparer.Ordinal))
            throw new InvalidOperationException("Unknown tool.");

        return _invoke(name, argumentsJson, cancellationToken);
    }
}
