namespace Jint.HtmlParser;

/// <summary>One complete XML notation declaration encountered during parsing (XML 1.0 fifth edition §4.7).</summary>
public readonly struct XmlNotationDeclaration
{
    private readonly string? _name;

    internal XmlNotationDeclaration(string name, string? publicId, string? systemId, long offset)
    {
        _name = name;
        PublicId = publicId;
        SystemId = systemId;
        Offset = offset;
    }

    /// <summary>The case-preserved notation name without delimiters.</summary>
    public string Name => _name ?? string.Empty;

    /// <summary>The normalized public identifier, or null when absent.</summary>
    public string? PublicId { get; }

    /// <summary>The system identifier, or null when absent.</summary>
    public string? SystemId { get; }

    /// <summary>The original-input UTF-16 position of the declaration or outermost parameter-entity invocation.</summary>
    public long Offset { get; }
}
