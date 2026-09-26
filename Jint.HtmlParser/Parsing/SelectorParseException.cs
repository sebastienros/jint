namespace Jint.HtmlParser;

/// <summary>A selector syntax or support failure at an original-input UTF-16 offset.</summary>
internal sealed class SelectorParseException : Exception
{
    internal SelectorParseException(string code, long offset)
        : base($"Selector parse error {code} at UTF-16 offset {offset}.")
    {
        Code = code;
        Offset = offset;
    }

    /// <summary>A stable selector-domain error identifier.</summary>
    public string Code { get; }

    /// <summary>The original UTF-16 input offset, or input length for EOF.</summary>
    public long Offset { get; }
}
