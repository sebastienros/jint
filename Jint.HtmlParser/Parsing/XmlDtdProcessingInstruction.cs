namespace Jint.HtmlParser;

/// <summary>A processing instruction read in an XML DTD (XML 1.0 fifth edition §2.6).</summary>
public readonly struct XmlDtdProcessingInstruction
{
    private readonly string? _target;
    private readonly string? _data;

    internal XmlDtdProcessingInstruction(string target, string data, long offset)
    {
        _target = target;
        _data = data;
        Offset = offset;
    }

    /// <summary>The case-preserved target without delimiters.</summary>
    public string Target => _target ?? string.Empty;

    /// <summary>The instruction data, with source line endings normalized but references unexpanded.</summary>
    public string Data => _data ?? string.Empty;

    /// <summary>The original-input UTF-16 position of the instruction or outermost parameter-entity invocation.</summary>
    public long Offset { get; }
}
