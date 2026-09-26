namespace Jint.HtmlParser;

/// <summary>Resource bounds for one XML document or fragment parse.</summary>
public sealed class XmlParseOptions
{
    private ParseLimits _limits = ParseLimits.Unbounded;

    /// <summary>Inclusive resource limits.</summary>
    public ParseLimits Limits
    {
        get => _limits;
        init => _limits = value ?? throw new ArgumentNullException(nameof(value));
    }
}

/// <summary>An XML or SVG syntax error at an original-input UTF-16 offset.</summary>
public sealed class MarkupParseException : Exception
{
    internal MarkupParseException(string code, long offset)
        : base($"Markup parse error {code} at UTF-16 offset {offset}.")
    {
        Code = code;
        Offset = offset;
    }

    /// <summary>A stable XML-domain error identifier.</summary>
    public string Code { get; }

    /// <summary>The original UTF-16 input offset, or input length for EOF.</summary>
    public long Offset { get; }
}
