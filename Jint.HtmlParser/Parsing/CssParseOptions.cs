namespace Jint.HtmlParser;

/// <summary>Resource bounds and an optional diagnostic sink for one CSS parse.</summary>
public sealed class CssParseOptions
{
    private ParseLimits _limits = ParseLimits.Default;

    /// <summary>Inclusive resource limits.</summary>
    public ParseLimits Limits
    {
        get => _limits;
        init => _limits = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>An optional bounded diagnostic collector.</summary>
    public ParseDiagnosticCollector? Diagnostics { get; init; }
}

/// <summary>A CSS syntax entry point could not produce the requested single construct.</summary>
public sealed class CssParseException : Exception
{
    internal CssParseException(string code, long offset)
        : base($"CSS parse error {code} at UTF-16 offset {offset}.")
    {
        Code = code;
        Offset = offset;
    }

    /// <summary>A stable CSS parse error identifier.</summary>
    public string Code { get; }

    /// <summary>The original UTF-16 input offset.</summary>
    public long Offset { get; }
}
