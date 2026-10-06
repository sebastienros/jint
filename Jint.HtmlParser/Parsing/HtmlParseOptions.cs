namespace Jint.HtmlParser;

/// <summary>Grammar context, resource limits and diagnostics for an inert HTML parse.</summary>
public sealed class HtmlParseOptions
{
    private ParseLimits _limits = ParseLimits.Default;

    /// <summary>Enables scripting-dependent grammar without executing scripts.</summary>
    public bool ScriptingEnabled { get; init; }

    /// <summary>Input, token and nesting bounds. Zero values disable individual bounds.</summary>
    public ParseLimits Limits
    {
        get => _limits;
        init => _limits = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Optional collector for recoverable HTML parse errors.</summary>
    public ParseDiagnosticCollector? Diagnostics { get; init; }

}
