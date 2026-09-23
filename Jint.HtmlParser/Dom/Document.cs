namespace Jint.HtmlParser;

/// <summary>The parsing mode of a document.</summary>
public enum DocumentKind
{
    Html,
    Xml
}

/// <summary>The HTML parser's persisted document mode.</summary>
internal enum DocumentMode
{
    NoQuirks,
    Quirks,
    LimitedQuirks
}

/// <summary>The owner and root of a native document tree.</summary>
public sealed class Document : Node
{
    private static readonly IReadOnlyList<XmlSkippedEntity> EmptySkippedXmlEntities =
        Array.AsReadOnly(Array.Empty<XmlSkippedEntity>());

    private IReadOnlyList<XmlSkippedEntity>? _skippedXmlEntities;
    private Document? _templateContentsOwnerDocument;
    private readonly bool _isTemplateContentsOwnerDocument;
    private ulong _mutationStamp;
    private bool _mayHaveMutationRegistrations;

    public Document(DocumentKind kind) : this(kind, kind == DocumentKind.Html ? "text/html" : "application/xml") { }

    internal Document(DocumentKind kind, string contentType) : this(kind, contentType, false) { }

    private Document(DocumentKind kind, string contentType, bool isTemplateContentsOwnerDocument) : base(null)
    {
        Kind = kind;
        ContentType = contentType;
        _isTemplateContentsOwnerDocument = isTemplateContentsOwnerDocument;
    }

    internal Document GetTemplateContentsOwnerDocument()
        => _isTemplateContentsOwnerDocument
            ? this
            : _templateContentsOwnerDocument ??= new Document(Kind, "application/xml", true);

    public static Document CreateHtml() => new(DocumentKind.Html);
    public static Document CreateXml() => new(DocumentKind.Xml);
    public static Document CreateXml(string contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        if (!IsCanonicalXmlContentType(contentType))
        {
            throw new ArgumentException("A canonical XML MIME essence is required.", nameof(contentType));
        }

        return new Document(DocumentKind.Xml, contentType);
    }

    public override NodeType NodeType => NodeType.Document;
    public DocumentKind Kind { get; }
    internal DocumentMode Mode { get; private set; }
    internal void SetParserMode(DocumentMode mode)
    {
        if (Mode != mode)
        {
            Mode = mode;
            MarkMutation();
        }
    }
    internal ulong MutationStamp => _mutationStamp;
    internal bool MayHaveMutationRegistrations => _mayHaveMutationRegistrations;
    internal void MarkMutationRegistrationsPresent() => _mayHaveMutationRegistrations = true;
    internal void MarkMutation()
    {
        if (_mutationStamp != ulong.MaxValue)
        {
            _mutationStamp++;
        }
    }

    /// <summary>Observes native mutations on a target, including one from another document.</summary>
#pragma warning disable CA1822 // The instance method is the document's convenience factory.
    public MutationSubscription ObserveMutations(Node target, MutationObserverOptions options)
    {
        var subscription = new MutationSubscription();
        subscription.Observe(target, options);
        return subscription;
    }
#pragma warning restore CA1822
    public string ContentType { get; }
    public string CharacterSet { get; } = "UTF-8";

    /// <summary>Immutable records of XML entities or external subsets omitted during parsing.</summary>
    public IReadOnlyList<XmlSkippedEntity> SkippedXmlEntities => _skippedXmlEntities ?? EmptySkippedXmlEntities;

    // Parsing owns the mutable builder. Copying into a read-only view leaves no mutable
    // reference in the document and no source or resolver attached to a record.
    internal void PublishSkippedXmlEntities(List<XmlSkippedEntity> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (_skippedXmlEntities is not null)
        {
            throw new InvalidOperationException("Skipped XML entities have already been published.");
        }

        if (records.Count != 0)
        {
            _skippedXmlEntities = Array.AsReadOnly(records.ToArray());
        }
    }

    internal void CopySkippedXmlEntitiesFrom(Document source) => _skippedXmlEntities = source._skippedXmlEntities;

    public Element? DocumentElement
    {
        get
        {
            for (var child = FirstChild; child is not null; child = child.NextSibling)
            {
                if (child is Element element)
                {
                    return element;
                }
            }

            return null;
        }
    }

    public DocumentType? Doctype
    {
        get
        {
            for (var child = FirstChild; child is not null; child = child.NextSibling)
            {
                if (child is DocumentType doctype)
                {
                    return doctype;
                }
            }

            return null;
        }
    }

    public Element CreateElement(string localName)
    {
        ArgumentNullException.ThrowIfNull(localName);
        var normalized = Kind == DocumentKind.Html ? QualifiedName.AsciiLower(localName) : localName;
        QualifiedName.ValidateElementLocalName(normalized);
        return new Element(this, Kind == DocumentKind.Html || ContentType == "application/xhtml+xml" ? Namespaces.Html : null, normalized, null);
    }

    public Element CreateElementNS(string? namespaceUri, string qualifiedName)
    {
        var name = QualifiedName.Parse(namespaceUri, qualifiedName, attribute: false);
        return new Element(this, name.NamespaceUri, name.LocalName, name.Prefix);
    }

    // The parser has already validated and resolved all three name components.
    // In particular, legal XML <xmlns/> must not pass through CreateElementNS.
    internal Element CreateParsedElement(string? namespaceUri, string localName, string? prefix)
        => new(this, namespaceUri, localName, prefix);

    public Attr CreateAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var normalized = Kind == DocumentKind.Html ? QualifiedName.AsciiLower(name) : name;
        QualifiedName.ValidateAttributeLocalName(normalized);
        return new Attr(this, null, normalized, null, "");
    }

    public Attr CreateAttributeNS(string? namespaceUri, string qualifiedName)
    {
        var name = QualifiedName.Parse(namespaceUri, qualifiedName, attribute: true);
        return new Attr(this, name.NamespaceUri, name.LocalName, name.Prefix, "");
    }

    public Text CreateTextNode(string data) => new(this, data);
    public Comment CreateComment(string data) => new(this, data);
    public CDataSection CreateCDataSection(string data)
    {
        if (Kind == DocumentKind.Html)
        {
            throw DomException.NotSupported();
        }

        return new CDataSection(this, data);
    }
    internal CDataSection CreateParsedCDataSection(string data) => new(this, data);
    public ProcessingInstruction CreateProcessingInstruction(string target, string data) => new(this, target, data);
    public DocumentType CreateDocumentType(string name, string publicId = "", string systemId = "") => new(this, name, publicId, systemId);
    public DocumentFragment CreateDocumentFragment() => new(this);

    /// <summary>Creates a detached copy of a node owned by this document.</summary>
    public Node ImportNode(Node source, bool deep = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source is Document)
        {
            throw DomException.NotSupported();
        }

        return NodeCloner.Clone(source, this, deep);
    }

    /// <summary>Removes a node from its parent and gives this document its identity and subtree.</summary>
    public Node AdoptNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node is Document)
        {
            throw DomException.NotSupported();
        }

        node.AdoptInto(this);
        return node;
    }

    /// <summary>Creates a detached copy of an attribute owned by this document.</summary>
    public Attr ImportAttribute(Attr source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return NodeCloner.CloneAttribute(source, this);
    }

    // RFC 9110 token grammar, limited to a canonical lowercase MIME essence.
    private static bool IsCanonicalXmlContentType(string contentType)
    {
        var slash = contentType.IndexOf('/');
        if (slash <= 0 || slash == contentType.Length - 1 || contentType.IndexOf('/', slash + 1) >= 0)
        {
            return false;
        }

        for (var i = 0; i < contentType.Length; i++)
        {
            var ch = contentType[i];
            if (ch == '/')
            {
                continue;
            }

            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~')
            {
                continue;
            }

            return false;
        }

        return contentType is "text/xml" or "application/xml" ||
            contentType.AsSpan(slash + 1).EndsWith("+xml", StringComparison.Ordinal);
    }
}

/// <summary>Common namespace names used by markup documents.</summary>
public static class Namespaces
{
    public const string Html = "http://www.w3.org/1999/xhtml";
    public const string Svg = "http://www.w3.org/2000/svg";
    public const string MathMl = "http://www.w3.org/1998/Math/MathML";
    public const string Xml = "http://www.w3.org/XML/1998/namespace";
    public const string Xmlns = "http://www.w3.org/2000/xmlns/";
}
