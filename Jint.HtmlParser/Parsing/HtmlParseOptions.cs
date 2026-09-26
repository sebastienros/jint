namespace Jint.HtmlParser;

/// <summary>Internal grammar context, limits and diagnostics for an HTML parse session.</summary>
internal sealed class HtmlParseOptions
{
    private ParseLimits _limits = ParseLimits.Unbounded;

    internal bool ScriptingEnabled { get; init; }

    internal ParseLimits Limits
    {
        get => _limits;
        init => _limits = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal ParseDiagnosticCollector? Diagnostics { get; init; }
}
