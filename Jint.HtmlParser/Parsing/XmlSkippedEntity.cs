namespace Jint.HtmlParser;

/// <summary>The kind of XML entity reference or external subset left unread.</summary>
public enum XmlSkippedEntityKind
{
    General,
    Parameter,
    ExternalSubset
}

/// <summary>Immutable provenance for one skipped XML entity occurrence.</summary>
public readonly struct XmlSkippedEntity
{
    private readonly string? _name;

    internal XmlSkippedEntity(XmlSkippedEntityKind kind, string name, string? publicId, string? systemId, long offset)
    {
        Kind = kind;
        _name = name;
        PublicId = publicId;
        SystemId = systemId;
        Offset = offset;
    }

    /// <summary>The skipped reference or external subset kind.</summary>
    public XmlSkippedEntityKind Kind { get; }

    /// <summary>The entity name without reference delimiters, or empty for an external subset.</summary>
    public string Name => _name ?? string.Empty;

    /// <summary>The declared public identifier, or null when unavailable.</summary>
    public string? PublicId { get; }

    /// <summary>The declared system identifier, or null when unavailable.</summary>
    public string? SystemId { get; }

    /// <summary>The original-input UTF-16 position of the reference or doctype opener.</summary>
    public long Offset { get; }
}
