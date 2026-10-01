using System.Buffers;
using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace Jint.HtmlParser;

// XML 1.0 (fifth edition) §2–§4 and Namespaces in XML 1.0 (third edition) §2–§4.
// The source cursor remains in original UTF-16 offsets. Each call owns all parse state.
internal sealed partial class XmlTreeParser
{
    private readonly string _originalSource;
    private string _source;
    private readonly ParseLimits _limits;
    private readonly CancellationToken _cancellationToken;
    private readonly Action? _onCancellationPoll;
    private readonly Document _document;
    private readonly bool _requireSvgRoot;
    private readonly DocumentFragment? _fragment;
    private readonly Element? _context;
    private readonly Stack<ElementFrame> _frames = new();
    private readonly Dictionary<string, string?> _bindings = new(StringComparer.Ordinal)
    {
        ["xml"] = Namespaces.Xml
    };
    private readonly Stack<InputFrame> _inputFrames = new();
    private readonly HashSet<string> _activeGeneralEntities = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeParameterEntities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, XmlEntityDeclaration> _generalEntities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, XmlEntityDeclaration> _parameterEntities = new(StringComparer.Ordinal);
    private List<XmlSkippedEntity>? _skippedEntities;
    private List<XmlNotationDeclaration>? _notations;
    private List<XmlDtdProcessingInstruction>? _dtdProcessingInstructions;
    private long _expansionCharacters;
    private bool _hasExternalSubset;
    private bool _catalogActive;
    private bool _standalone;
    private string? _doctypeName;
    private int _doctypeTokenStart = -1;
    private bool _unreadParameterEntity;
    private bool _sawParameterReference;
    private int _position;
    private int _work;
    private bool _seenRoot;
    private CharBuffer? _pendingText;
    private int _pendingTextOffset;

    // Per-start-tag scratch, reused across elements. A collection that grew past
    // ScratchLimit is replaced rather than cleared, so one wide element cannot make
    // every later Clear pay for its capacity.
    private const int ScratchLimit = 64;
    private List<RawAttribute> _rawAttributes = [];
    private HashSet<string> _rawNames = new(StringComparer.Ordinal);
    private Dictionary<string, string?> _localBindings = new(StringComparer.Ordinal);
    private HashSet<(string?, string)> _expandedNames = [];
    private List<ParserAttribute> _parsedAttributes = [];

    // Parse-local, bounded name reuse: repeated element and attribute names share one
    // string (and one prefix/local split) without interning input process-wide.
    private const int NameSlots = 256;
    private const int MaxCachedNameLength = 64;
    private QualifiedName?[]? _names;

    private XmlTreeParser(string source, ParseLimits limits, Element? context, Document? document,
        bool requireSvgRoot, Action? onCancellationPoll, CancellationToken cancellationToken)
    {
        _originalSource = source;
        _source = source;
        _limits = limits;
        _cancellationToken = cancellationToken;
        _onCancellationPoll = onCancellationPoll;
        cancellationToken.ThrowIfCancellationRequested();
        if (limits.MaxInputCharacters != 0 && source.Length > limits.MaxInputCharacters)
        {
            throw new ParseLimitException(ParseLimitKind.InputCharacters, limits.MaxInputCharacters, source.Length);
        }
        _context = context;
        _document = document ?? context?.OwnerDocument ?? Document.CreateXml();
        _requireSvgRoot = requireSvgRoot;
        _fragment = context is null ? null : _document.CreateDocumentFragment();
        for (var ancestor = context; ancestor is not null; ancestor = ancestor.ParentNode as Element)
        {
            WorkUnit();
            if (ancestor.NamespaceUri is not null)
            {
                _bindings.TryAdd(ancestor.Prefix ?? string.Empty, ancestor.NamespaceUri);
            }
            foreach (var attribute in ancestor.Attributes)
            {
                WorkUnit();
                if (attribute.NamespaceUri != Namespaces.Xmlns) continue;
                if (attribute.Name == "xmlns") _bindings.TryAdd(string.Empty, EmptyToNull(attribute.Value));
                else if (attribute.Prefix == "xmlns") _bindings.TryAdd(attribute.LocalName, EmptyToNull(attribute.Value));
            }
        }
    }

    internal static Document ParseDocument(string source, ParseLimits limits, CancellationToken cancellationToken)
        => ParseDocument(source, limits, null, cancellationToken);

    // Internal seam makes mid-token cancellation deterministic in tests.
    internal static Document ParseDocument(string source, ParseLimits limits, Action? onCancellationPoll, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(limits);
        var parser = new XmlTreeParser(source, limits, null, null, false, onCancellationPoll, cancellationToken);
        parser.Parse();
        return parser._document;
    }

    internal static DocumentFragment ParseFragment(string source, Element context, ParseLimits limits, CancellationToken cancellationToken)
        => ParseFragment(source, context, limits, null, cancellationToken);

    internal static DocumentFragment ParseFragment(string source, Element context, ParseLimits limits,
        Action? onCancellationPoll, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(limits);
        var parser = new XmlTreeParser(source, limits, context, null, false, onCancellationPoll, cancellationToken);
        parser.Parse();
        return parser._fragment!;
    }

    internal static Document ParseIntoDocument(string source, Document document, ParseLimits limits,
        bool requireSvgRoot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(limits);
        if (document.Kind != DocumentKind.Xml || document.ChildCount != 0)
            throw new ArgumentException("A fresh empty XML document is required.", nameof(document));
        var parser = new XmlTreeParser(source, limits, null, document, requireSvgRoot, null, cancellationToken);
        parser.Parse();
        return document;
    }

    private void Parse()
    {
        // A Unicode BOM is a transport signature, rather than document content.
        if (_context is null && Current == '\uFEFF') Consume();
        if (_context is null && StartsWith("<?xml") && (IsWhitespace(Peek(5)) || Peek(5) == '?')) ParseDeclaration();

        while (true)
        {
            if (End)
            {
                if (!ResumeInput()) break;
                continue;
            }
            if (Current == '<')
            {
                FlushText();
                // The markup openers are mutually exclusive on the units after '<', so one switch replaces
                // a chain of literal probes that every start tag (the common case) used to fail in turn.
                switch (Peek(1))
                {
                    case '/':
                        ParseEndTag();
                        break;
                    case '?':
                        ParseProcessingInstruction();
                        break;
                    case '!':
                        switch (Peek(2))
                        {
                            case '-' when StartsWith("<!--"):
                                ParseComment();
                                break;
                            case '[' when StartsWith("<![CDATA["):
                                ParseCData();
                                break;
                            case 'D' when StartsWith("<!DOCTYPE"):
                                ParseDoctype();
                                break;
                            default:
                                Error("xml/invalid-markup", _position);
                                break;
                        }
                        break;
                    default:
                        ParseStartTag();
                        break;
                }
            }
            else ParseText();
        }

        FlushText();
        if (_frames.Count != 0) Error("xml/unexpected-eof", _position);
        if (_context is null && !_seenRoot) Error("xml/invalid-document", _position);
        if (_skippedEntities is not null) _document.PublishSkippedXmlEntities(_skippedEntities);
        if (_notations is not null) _document.PublishXmlNotations(_notations, _cancellationToken);
        if (_dtdProcessingInstructions is not null)
            _document.PublishXmlDtdProcessingInstructions(_dtdProcessingInstructions, _cancellationToken);
        _cancellationToken.ThrowIfCancellationRequested();
    }

    private void ParseDeclaration()
    {
        var start = _position;
        ConsumeLiteral("<?xml", start);
        if (!SkipWhitespace(start)) Error("xml/invalid-declaration", _position);
        if (!StartsWith("version")) Error("xml/invalid-declaration", _position);
        ConsumeLiteral("version", start);
        SkipWhitespace(start);
        Expect('=', "xml/invalid-declaration", start);
        SkipWhitespace(start);
        var version = ReadQuoted("xml/invalid-declaration", start);
        // XML 1.0 Fifth Edition §2.8: every ASCII 1.[0-9]+ version is
        // processed under XML 1.0 rules, without interpreting its suffix.
        if (version.Length < 3 || version[0] != '1' || version[1] != '.')
            Error("xml/invalid-declaration", start);
        for (var i = 2; i < version.Length; i++)
        {
            WorkUnit();
            if (version[i] is < '0' or > '9') Error("xml/invalid-declaration", start);
        }
        var lastOrder = 0;
        while (true)
        {
            var hadSpace = SkipWhitespace(start);
            if (StartsWith("?>"))
            {
                ConsumeLiteral("?>", start);
                return;
            }

            if (!hadSpace) Error("xml/invalid-declaration", _position);
            var name = ReadName(start);
            var order = XmlDeclarationOrderLookup.Match(name);
            if (order == 0 || order <= lastOrder) Error("xml/invalid-declaration", _position);
            lastOrder = order;
            SkipWhitespace(start);
            Expect('=', "xml/invalid-declaration", start);
            SkipWhitespace(start);
            var value = ReadQuoted("xml/invalid-declaration", start);
            if (order == 1 && !IsEncodingName(value) || order == 2 && !XmlStandaloneValueLookup.Match(value))
                Error("xml/invalid-declaration", start);
            if (order == 2) _standalone = value == "yes";
        }
    }

    private void ParseComment()
    {
        var start = _position;
        ConsumeLiteral("<!--", start);
        var contentStart = _position;
        while (true)
        {
            if (End) Error("xml/unexpected-eof", _position);
            if (StartsWith("-->"))
            {
                var data = NormalizeLines(_source.AsSpan(contentStart, _position - contentStart));
                ConsumeLiteral("-->", start);
                Parent.AppendParsedChild(CurrentDocument.CreateComment(data));
                return;
            }

            if (StartsWith("--")) Error("xml/invalid-markup", _position);
            ConsumeScalar();
            CheckToken(start);
        }
    }

    private void ParseCData()
    {
        var start = _position;
        if (_frames.Count == 0 && _context is null) Error("xml/invalid-document", start);
        ConsumeLiteral("<![CDATA[", start);
        var contentStart = _position;
        while (true)
        {
            if (End) Error("xml/unexpected-eof", _position);
            if (StartsWith("]]>"))
            {
                var data = NormalizeLines(_source.AsSpan(contentStart, _position - contentStart));
                ConsumeLiteral("]]>", start);
                Parent.AppendParsedChild(CurrentDocument.CreateParsedCDataSection(data));
                return;
            }

            ConsumeScalar();
            CheckToken(start);
        }
    }

    private void ParseProcessingInstruction()
    {
        var start = _position;
        ConsumeLiteral("<?", start);
        var target = ReadName(start);
        if (target.Contains(':')) Error("xml/namespace-error", start);
        if (target.Equals("xml", StringComparison.OrdinalIgnoreCase)) Error("xml/invalid-declaration", start);
        string data;
        if (StartsWith("?>")) data = string.Empty;
        else
        {
            if (!SkipWhitespace(start)) Error("xml/invalid-markup", _position);
            var contentStart = _position;
            while (!StartsWith("?>"))
            {
                if (End) Error("xml/unexpected-eof", _position);
                ConsumeScalar();
                CheckToken(start);
            }
            data = NormalizeLines(_source.AsSpan(contentStart, _position - contentStart));
        }

        ConsumeLiteral("?>", start);
        Parent.AppendParsedChild(CurrentDocument.CreateParsedProcessingInstruction(target, data));
    }

    private void ParseStartTag()
    {
        var start = _position;
        Expect('<', "xml/invalid-markup", start);
        var name = ReadName(start);
        ValidateQName(name, start);
        if (_frames.Count == 0)
        {
            if (_context is null && _seenRoot) Error("xml/invalid-document", start);
            _seenRoot = true;
        }

        var depth = _frames.Count + 1;
        if (_limits.MaxNestingDepth != 0 && depth > _limits.MaxNestingDepth)
            throw new ParseLimitException(ParseLimitKind.NestingDepth, _limits.MaxNestingDepth, depth);

        var attributes = Reuse(ref _rawAttributes);
        var declaredAttributes = GetDeclaredAttributeTypes(name);
        Reuse(ref _rawNames);
        var localBindings = Reuse(ref _localBindings);
        bool empty;
        while (true)
        {
            var whitespace = SkipWhitespace(start);
            if (StartsWith("/>"))
            {
                ConsumeLiteral("/>", start);
                empty = true;
                break;
            }
            if (Current == '>')
            {
                Consume();
                CheckToken(start);
                empty = false;
                break;
            }
            if (!whitespace) Error("xml/invalid-markup", _position);
            var attributeOffset = _position;
            var attributeName = ReadName(start);
            ValidateQName(attributeName, attributeOffset);
            SkipWhitespace(start);
            Expect('=', "xml/invalid-markup", start);
            SkipWhitespace(start);
            var value = ReadAttributeValue(start);
            if (declaredAttributes is not null &&
                declaredAttributes.TryGetValue(attributeName, out var declaration) && !declaration.CData)
                value = CollapseSpaces(value);
            CheckToken(start);
            if (!AddRawName(attributes, attributeName)) Error("xml/duplicate-attribute", attributeOffset);
            attributes.Add(new RawAttribute(attributeName, value, attributeOffset));

            if (attributeName == "xmlns" || attributeName.StartsWith("xmlns:", StringComparison.Ordinal))
            {
                var prefix = attributeName == "xmlns" ? string.Empty : attributeName[6..];
                ValidateBinding(prefix, value, attributeOffset);
                localBindings.Add(prefix, value.Length == 0 ? null : value);
            }
        }

        ApplyDtdAttributes(name, attributes, localBindings);
        var split = SplitName(name);
        var namespaceUri = split.Prefix is null ? Resolve(string.Empty, localBindings) : ResolveRequired(split.Prefix, localBindings, start);
        if (namespaceUri == Namespaces.Xmlns || split.Prefix == "xmlns") Error("xml/namespace-error", start);
        if (_requireSvgRoot && _frames.Count == 0 && (split.LocalName != "svg" || namespaceUri != Namespaces.Svg))
            Error("xml/svg-root-required", start);
        Reuse(ref _expandedNames);
        var parsedAttributes = Reuse(ref _parsedAttributes);
        string? isValue = null;
        foreach (var attribute in attributes)
        {
            var attrSplit = SplitName(attribute.Name);
            var attrNamespace = attribute.Name == "xmlns" || attrSplit.Prefix == "xmlns"
                ? Namespaces.Xmlns
                : attrSplit.Prefix is null ? null : ResolveRequired(attrSplit.Prefix, localBindings, attribute.Offset);
            if (!AddExpandedName(parsedAttributes, attrNamespace, attrSplit.LocalName)) Error("xml/duplicate-attribute", attribute.Offset);
            if (attrNamespace is null && attrSplit.LocalName == "is") isValue = attribute.Value;
            var isDtdId = declaredAttributes is not null &&
                          declaredAttributes.TryGetValue(attribute.Name, out var declaration) && declaration.IsId;
            parsedAttributes.Add(new ParserAttribute(attrNamespace, attrSplit.LocalName, attrSplit.Prefix,
                attribute.Value, isDtdId));
            WorkUnit();
        }

        var element = CurrentDocument.CreateParsedElement(namespaceUri, split.LocalName, split.Prefix, isValue);
        element.InitializeParsedAttributes(CollectionsMarshal.AsSpan(parsedAttributes), _cancellationToken);
        Parent.AppendParsedChild(element);
        if (!empty)
        {
            Dictionary<string, BindingUndo>? previousBindings = null;
            if (localBindings.Count != 0) previousBindings = new(localBindings.Count, StringComparer.Ordinal);
            foreach (var (prefix, uri) in localBindings)
            {
                WorkUnit();
                previousBindings!.Add(prefix, _bindings.TryGetValue(prefix, out var previous)
                    ? new BindingUndo(true, previous) : new BindingUndo(false, null));
                _bindings[prefix] = uri;
            }
            _frames.Push(new ElementFrame(element, name, previousBindings));
        }
    }

    private void ParseEndTag()
    {
        var start = _position;
        ConsumeLiteral("</", start);
        var name = ReadName(start);
        ValidateQName(name, start);
        SkipWhitespace(start);
        Expect('>', "xml/invalid-markup", start);
        CheckToken(start);
        if (_frames.Count == 0 || _frames.Peek().QualifiedName != name)
            Error("xml/mismatched-end-tag", start);
        if (_inputFrames.Count != 0 && !_inputFrames.Peek().Parameter &&
            _frames.Count <= _inputFrames.Peek().ElementDepth)
            Error("xml/invalid-markup", start);
        var frame = _frames.Pop();
        if (frame.PreviousBindings is null) return;
        foreach (var (prefix, previous) in frame.PreviousBindings)
        {
            WorkUnit();
            if (previous.Exists) _bindings[prefix] = previous.Value;
            else _bindings.Remove(prefix);
        }
    }

    private void ParseText()
    {
        _pendingText ??= new CharBuffer();
        if (_pendingText.Length == 0)
        {
            _pendingTextOffset = _position;
        }
        while (!End && Current != '<')
        {
            if (CanBatch)
            {
                // Text has no token limit; ordinary characters cost one unit each.
                var run = OrdinaryRun(TextStops, tokenStart: -1);
                if (run > 0)
                {
                    _pendingText.Append(_source.AsSpan(_position, run));
                    _position += run;
                    WorkUnits(run);
                    continue;
                }
            }
            if (StartsWith("]]>")) Error("xml/invalid-markup", _position);
            if (Current == '&')
            {
                if (_frames.Count == 0 && _context is null) Error("xml/invalid-document", _position);
                var value = ReadReference(-1, inAttribute: false);
                if (value is null) return;
                WorkUnits(value.Length);
                _pendingText.Append(value);
                continue;
            }
            AppendNormalizedScalar(_pendingText);
        }
    }

    private void FlushText()
    {
        if (_pendingText is null || _pendingText.Length == 0) return;
        WorkUnits(_pendingText.Length);
        _cancellationToken.ThrowIfCancellationRequested();
        var value = _pendingText.ToString();
        _cancellationToken.ThrowIfCancellationRequested();
        _pendingText.Clear();
        if (_frames.Count == 0 && _context is null)
        {
            foreach (var character in value)
            {
                WorkUnit();
                if (!IsWhitespace(character)) Error("xml/invalid-document", _pendingTextOffset);
            }
            return;
        }

        Parent.AppendParsedChild(CurrentDocument.CreateTextNode(value));
    }

    private string ReadAttributeValue(int tokenStart)
    {
        var quote = Current;
        if (quote is not ('\'' or '"')) Error("xml/invalid-markup", _position);
        Consume();
        var valueStart = _position;
        var builder = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            while (Current != quote)
            {
                if (CanBatch)
                {
                    var run = OrdinaryRun(AttributeValueStops, tokenStart);
                    if (run > 0)
                    {
                        if (_position == valueStart && Peek(run) == quote)
                        {
                            // The whole value is one ordinary run: take it straight from the source,
                            // charging the same units the builder copy would have.
                            Advance(run, tokenStart);
                            Consume();
                            WorkUnits(run);
                            _cancellationToken.ThrowIfCancellationRequested();
                            var value = _source.Substring(valueStart, run);
                            _cancellationToken.ThrowIfCancellationRequested();
                            return value;
                        }
                        builder.Append(_source.AsSpan(_position, run));
                        Advance(run, tokenStart);
                        continue;
                    }
                }
                if (End) Error("xml/unexpected-eof", _position);
                if (Current == '<') Error("xml/invalid-markup", _position);
                if (Current == '&')
                {
                    var replacement = ReadReference(tokenStart, inAttribute: true);
                    AppendCopy(ref builder, replacement);
                }
                else
                {
                    if (IsWhitespace(Current))
                    {
                        if (Current == '\r' && _inputFrames.Count == 0 && Peek(1) == '\n') Consume();
                        Consume();
                        builder.Append(' ');
                    }
                    else AppendNormalizedScalar(ref builder);
                }
                CheckToken(tokenStart);
            }
            Consume();
            return Materialize(ref builder);
        }
        finally { builder.Dispose(); }
    }

    private string? ReadReference(int parentTokenStart, bool inAttribute)
    {
        var start = _position;
        Consume();
        if (Current == '#')
        {
            Consume();
            var hex = Current == 'x';
            if (hex) Consume();
            var digits = 0;
            uint value = 0;
            while (!End && Current != ';')
            {
                var digit = hex ? HexValue(Current) : Current is >= '0' and <= '9' ? Current - '0' : -1;
                if (digit < 0) Error("xml/invalid-character", _position);
                if (value > (0x10FFFFu - (uint) digit) / (hex ? 16u : 10u)) Error("xml/invalid-character", _position);
                value = value * (hex ? 16u : 10u) + (uint) digit;
                digits++;
                Consume();
                CheckToken(start);
                if (parentTokenStart >= 0) CheckToken(parentTokenStart);
            }
            if (digits == 0 || !IsXmlChar(value)) Error("xml/invalid-character", start);
            Expect(';', "xml/unexpected-eof", start);
            CheckToken(start);
            if (parentTokenStart >= 0) CheckToken(parentTokenStart);
            return char.ConvertFromUtf32((int) value);
        }

        var name = ReadName(start, parentTokenStart);
        ValidateUnprefixedDtdName(name, start);
        Expect(';', "xml/invalid-markup", start);
        CheckToken(start);
        if (parentTokenStart >= 0) CheckToken(parentTokenStart);
        var predefined = XmlPredefinedEntityLookup.Match(name);
        if (predefined is not null) return predefined;
        return ResolveGeneralEntity(name, start, inAttribute);
    }

    private string ReadName(int tokenStart, int parentTokenStart = -1)
    {
        var start = _position;
        if (!IsNameStart(PeekScalar())) Error("xml/invalid-name", _position);
        var run = CanBatch ? AsciiNameRun(tokenStart, parentTokenStart) : 0;
        if (run > 0) Advance(run, tokenStart, parentTokenStart);
        else
        {
            ConsumeScalar();
            CheckToken(tokenStart);
            if (parentTokenStart >= 0) CheckToken(parentTokenStart);
        }
        while (IsNameChar(PeekScalar()))
        {
            ConsumeScalar();
            CheckToken(tokenStart);
            if (parentTokenStart >= 0) CheckToken(parentTokenStart);
        }
        _cancellationToken.ThrowIfCancellationRequested();
        return CachedName(_source.AsSpan(start, _position - start)).Name;
    }

    private QualifiedName CachedName(ReadOnlySpan<char> name, string? materialized = null)
    {
        if (name.Length > MaxCachedNameLength) return CreateQualifiedName(Materialize(name, materialized));
        var slot = (int) (XxHash3.HashToUInt64(MemoryMarshal.AsBytes(name)) & (NameSlots - 1));
        _names ??= new QualifiedName?[NameSlots];
        if (_names[slot] is { } cached && name.SequenceEqual(cached.Name)) return cached;
        return _names[slot] = CreateQualifiedName(Materialize(name, materialized));

        static string Materialize(ReadOnlySpan<char> name, string? materialized)
            => materialized ?? XmlKnownNameLookup.Match(name) ?? name.ToString();
    }

    private static QualifiedName CreateQualifiedName(string name)
    {
        var colon = name.IndexOf(':');
        return colon < 0 ? new QualifiedName(name, null, name) : new QualifiedName(name, name[..colon], name[(colon + 1)..]);
    }

    private string ReadQuoted(string code, int tokenStart)
    {
        var quote = Current;
        if (quote is not ('\'' or '"')) Error(code, _position);
        Consume();
        var start = _position;
        while (Current != quote)
        {
            if (End) Error("xml/unexpected-eof", _position);
            ConsumeScalar();
            CheckToken(tokenStart);
        }
        var value = _source[start.._position];
        Consume();
        CheckToken(tokenStart);
        return value;
    }

    private void ValidateQName(string name, int offset)
    {
        var colon = name.IndexOf(':');
        if (colon < 0) return;
        if (colon == 0 || colon == name.Length - 1 || name.IndexOf(':', colon + 1) >= 0)
            Error("xml/namespace-error", offset);
        var firstLocal = char.IsSurrogatePair(name, colon + 1)
            ? char.ConvertToUtf32(name, colon + 1) : name[colon + 1];
        if (!IsNameStart(firstLocal))
            Error("xml/namespace-error", offset);
    }

    private (string? Prefix, string LocalName) SplitName(string name)
    {
        var colon = name.IndexOf(':');
        if (colon < 0) return (null, name);
        var cached = CachedName(name, name);
        return (cached.Prefix, cached.LocalName);
    }

    private void ValidateBinding(string prefix, string value, int offset)
    {
        if (prefix == "xmlns" || value == Namespaces.Xmlns || prefix == "xml" && value != Namespaces.Xml ||
            prefix != "xml" && value == Namespaces.Xml || prefix.Length != 0 && value.Length == 0)
            Error("xml/namespace-error", offset);
    }

    private string ResolveRequired(string prefix, Dictionary<string, string?> local, int offset)
    {
        var value = Resolve(prefix, local);
        if (value is null) Error("xml/namespace-error", offset);
        return value!;
    }

    private string? Resolve(string prefix, Dictionary<string, string?> local)
    {
        if (local.TryGetValue(prefix, out var uri)) return uri;
        return _bindings.TryGetValue(prefix, out uri) ? uri : null;
    }

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private Node Parent
    {
        get
        {
            if (_frames.Count == 0) return (Node?) _fragment ?? _document;
            var element = _frames.Peek().Element;
            return (Node?) element.TemplateContent ?? element;
        }
    }

    private Document CurrentDocument
    {
        get
        {
            var parent = Parent;
            return parent as Document ?? parent.OwnerDocument!;
        }
    }
    private bool End => _position >= _source.Length;
    private char Current => End ? '\0' : _source[_position];
    private char Peek(int delta) => _position + delta < _source.Length ? _source[_position + delta] : '\0';
    private bool StartsWith(string value) => _source.AsSpan(_position).StartsWith(value, StringComparison.Ordinal);

    private void Consume()
    {
        if (_inputFrames.Count != 0)
        {
            if (_limits.MaxEntityExpansionCharacters != 0 && _expansionCharacters >= _limits.MaxEntityExpansionCharacters)
                throw new ParseLimitException(ParseLimitKind.EntityExpansionCharacters,
                    _limits.MaxEntityExpansionCharacters, _limits.MaxEntityExpansionCharacters + 1);
            _expansionCharacters++;
        }
        _position++;
        WorkUnit();
        if (_doctypeTokenStart >= 0 && ReferenceEquals(_source, _originalSource)) CheckToken(_doctypeTokenStart);
    }

    private void ConsumeLiteral(string literal, int tokenStart)
    {
        foreach (var character in literal)
        {
            if (Current != character) Error("xml/invalid-markup", _position);
            Consume();
            CheckToken(tokenStart);
        }
    }

    private void Expect(char expected, string code, int tokenStart)
    {
        if (Current != expected) Error(End ? "xml/unexpected-eof" : code, _position);
        Consume();
        CheckToken(tokenStart);
    }

    private bool SkipWhitespace(int tokenStart)
    {
        if (CanBatch)
        {
            var rest = _source.AsSpan(_position);
            var run = rest.IndexOfAnyExcept(XmlWhitespace);
            run = ClampToTokenLimit(run < 0 ? rest.Length : run, tokenStart, -1);
            if (run == 0) return false;
            Advance(run, tokenStart);
            return true;
        }
        var found = false;
        while (!End && IsWhitespace(Current))
        {
            found = true;
            Consume();
            CheckToken(tokenStart);
        }
        return found;
    }

    private void CheckToken(int start)
    {
        var length = _position - start;
        if (_limits.MaxTokenCharacters != 0 && length > _limits.MaxTokenCharacters)
            throw new ParseLimitException(ParseLimitKind.TokenCharacters, _limits.MaxTokenCharacters, length);
    }

    private int PeekScalar()
    {
        if (End) return -1;
        if (char.IsHighSurrogate(Current) && char.IsLowSurrogate(Peek(1)))
            return char.ConvertToUtf32(Current, Peek(1));
        return Current;
    }

    private void ConsumeScalar()
    {
        var scalar = PeekScalar();
        if (!IsXmlChar((uint) scalar)) Error("xml/invalid-character", _position);
        Consume();
        if (scalar > 0xFFFF) Consume();
    }

    private void AppendNormalizedScalar(CharBuffer builder)
    {
        var (first, second) = ReadNormalizedScalar();
        builder.Append(first);
        if (second != '\0') builder.Append(second);
    }

    private void AppendNormalizedScalar(ref ValueStringBuilder builder)
    {
        var (first, second) = ReadNormalizedScalar();
        builder.Append(first);
        if (second != '\0') builder.Append(second);
    }

    private (char First, char Second) ReadNormalizedScalar()
    {
        var scalar = PeekScalar();
        if (!IsXmlChar((uint) scalar)) Error("xml/invalid-character", _position);
        if (scalar == '\r' && _inputFrames.Count == 0)
        {
            Consume();
            if (Current == '\n') Consume();
            return ('\n', '\0');
        }
        var first = Current;
        Consume();
        if (scalar > 0xFFFF)
        {
            var second = Current;
            Consume();
            return (first, second);
        }
        return (first, '\0');
    }

    private string NormalizeLines(ReadOnlySpan<char> value)
    {
        if (_inputFrames.Count != 0)
        {
            WorkUnits(value.Length);
            _cancellationToken.ThrowIfCancellationRequested();
            return value.ToString();
        }
        var first = value.IndexOf('\r');
        if (first < 0)
        {
            WorkUnits(value.Length);
            return value.ToString();
        }
        var builder = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            builder.EnsureCapacity(value.Length);
            builder.Append(value[..first]);
            WorkUnits(first);
            for (var i = first; i < value.Length; i++)
            {
                WorkUnit();
                if (value[i] == '\r')
                {
                    builder.Append('\n');
                    if (i + 1 < value.Length && value[i + 1] == '\n') i++;
                }
                else builder.Append(value[i]);
            }
            return Materialize(ref builder);
        }
        finally { builder.Dispose(); }
    }

    // A secondary copy is work too, even when the BCL performs it in one call. Polls
    // exactly where `count` WorkUnit calls would.
    private void WorkUnits(int count)
    {
        while (count > 0)
        {
            var step = Math.Min(count, WorkPollInterval - _work);
            _work += step;
            count -= step;
            if (_work >= WorkPollInterval) PollWork();
        }
    }

    // Ordinary runs are consumed in one step only from the primary source and outside a DOCTYPE,
    // where Consume has no per-character bookkeeping beyond the work unit.
    private bool CanBatch => _inputFrames.Count == 0 && _doctypeTokenStart < 0;

    // Length of the run of characters at the cursor that are not stops, ended early on the character
    // that would exceed the token limit so the CheckToken after Advance throws exactly where
    // character-by-character consumption would.
    private int OrdinaryRun(SearchValues<char> stops, int tokenStart, int parentTokenStart = -1)
    {
        var rest = _source.AsSpan(_position);
        var run = rest.IndexOfAny(stops);
        return ClampToTokenLimit(run < 0 ? rest.Length : run, tokenStart, parentTokenStart);
    }

    // Ends a run early on the character that would exceed the token limit, so the CheckToken
    // after Advance throws exactly where character-by-character consumption would.
    private int ClampToTokenLimit(int run, int tokenStart, int parentTokenStart)
    {
        var max = _limits.MaxTokenCharacters;
        if (max == 0 || run == 0) return run;
        if (tokenStart >= 0) run = Math.Min(run, Math.Max(1, max + 1 - (_position - tokenStart)));
        if (parentTokenStart >= 0) run = Math.Min(run, Math.Max(1, max + 1 - (_position - parentTokenStart)));
        return run;
    }

    // The run of ASCII name characters at the cursor; a non-ASCII name continues scalar by scalar.
    private int AsciiNameRun(int tokenStart, int parentTokenStart)
    {
        var rest = _source.AsSpan(_position);
        var run = rest.IndexOfAnyExcept(AsciiNameChars);
        return ClampToTokenLimit(run < 0 ? rest.Length : run, tokenStart, parentTokenStart);
    }

    private void Advance(int count, int tokenStart, int parentTokenStart = -1)
    {
        _position += count;
        WorkUnits(count);
        CheckToken(tokenStart);
        if (parentTokenStart >= 0) CheckToken(parentTokenStart);
    }

    private static readonly SearchValues<char> XmlWhitespace = SearchValues.Create(" \t\r\n");

    private static readonly SearchValues<char> AsciiNameChars =
        SearchValues.Create("-.0123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz");

    // XML 1.0 §2.2/§2.3: controls other than TAB/LF, lone surrogates and U+FFFE/U+FFFF are errors,
    // CR is normalized, and the markup delimiters end a run. Everything else is copied as is.
    private static readonly SearchValues<char> TextStops = CreateStops("\r<&]", allowTab: true);
    private static readonly SearchValues<char> AttributeValueStops = CreateStops("\t\n\r\"'<&", allowTab: false);

    private static SearchValues<char> CreateStops(string delimiters, bool allowTab)
    {
        var stops = new List<char>(delimiters);
        for (var c = '\0'; c < ' '; c++)
        {
            if (allowTab && c is '\t' or '\n') continue;
            stops.Add(c);
        }
        for (var c = '\uD800'; c <= '\uDFFF'; c++) stops.Add(c);
        stops.Add('\uFFFE');
        stops.Add('\uFFFF');
        return SearchValues.Create(stops.ToArray());
    }

    // Duplicate attribute detection scans the few attributes a start tag usually has, and
    // switches to a set (seeded with those already added) once a tag is wide enough for
    // the quadratic scan to matter. Callers add the attribute after a successful call.
    private const int LinearDuplicateScanLimit = 8;

    private bool AddRawName(List<RawAttribute> attributes, string name)
    {
        var added = CollectionsMarshal.AsSpan(attributes);
        if (added.Length < LinearDuplicateScanLimit)
        {
            foreach (var attribute in added)
            {
                if (attribute.Name == name) return false;
            }
            return true;
        }
        if (_rawNames.Count == 0)
        {
            foreach (var attribute in added) _rawNames.Add(attribute.Name);
        }
        return _rawNames.Add(name);
    }

    private bool AddExpandedName(List<ParserAttribute> attributes, string? namespaceUri, string localName)
    {
        var added = CollectionsMarshal.AsSpan(attributes);
        if (added.Length < LinearDuplicateScanLimit)
        {
            foreach (var attribute in added)
            {
                if (attribute.LocalName == localName && attribute.NamespaceUri == namespaceUri) return false;
            }
            return true;
        }
        if (_expandedNames.Count == 0)
        {
            foreach (var attribute in added) _expandedNames.Add((attribute.NamespaceUri, attribute.LocalName));
        }
        return _expandedNames.Add((namespaceUri, localName));
    }

    private static List<T> Reuse<T>(ref List<T> list)
    {
        if (list.Count > ScratchLimit) list = [];
        else list.Clear();
        return list;
    }

    private static HashSet<T> Reuse<T>(ref HashSet<T> set)
    {
        if (set.Count > ScratchLimit) set = new HashSet<T>(set.Comparer);
        else set.Clear();
        return set;
    }

    private static Dictionary<TKey, TValue> Reuse<TKey, TValue>(ref Dictionary<TKey, TValue> dictionary) where TKey : notnull
    {
        if (dictionary.Count > ScratchLimit) dictionary = new Dictionary<TKey, TValue>(dictionary.Comparer);
        else dictionary.Clear();
        return dictionary;
    }

    private string Materialize(CharBuffer builder)
    {
        WorkUnits(builder.Length);
        _cancellationToken.ThrowIfCancellationRequested();
        var value = builder.ToString();
        _cancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    private void AppendCopy(ref ValueStringBuilder builder, string? value)
    {
        if (value is null) return;
        WorkUnits(value.Length);
        _cancellationToken.ThrowIfCancellationRequested();
        builder.Append(value);
        _cancellationToken.ThrowIfCancellationRequested();
    }

    private string Materialize(ref ValueStringBuilder builder)
    {
        WorkUnits(builder.Length);
        _cancellationToken.ThrowIfCancellationRequested();
        var value = builder.ToString();
        _cancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    private const int WorkPollInterval = 4096;

    private void WorkUnit()
    {
        if (++_work >= WorkPollInterval) PollWork();
    }

    private void PollWork()
    {
        _work = 0;
        _onCancellationPoll?.Invoke();
        _cancellationToken.ThrowIfCancellationRequested();
    }

    private static bool IsWhitespace(char value) => value is ' ' or '\t' or '\r' or '\n';
    private static bool IsXmlChar(uint value)
        => value is 0x9 or 0xA or 0xD or >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF;
    private static bool IsNameStart(int value)
        => value < 0x80
            ? (uint) ((value | 0x20) - 'a') <= 'z' - 'a' || value is ':' or '_'
            : value is >= 0xC0 and <= 0xD6 or >= 0xD8 and <= 0xF6 or >= 0xF8 and <= 0x2FF or
            >= 0x370 and <= 0x37D or >= 0x37F and <= 0x1FFF or >= 0x200C and <= 0x200D or
            >= 0x2070 and <= 0x218F or >= 0x2C00 and <= 0x2FEF or >= 0x3001 and <= 0xD7FF or
            >= 0xF900 and <= 0xFDCF or >= 0xFDF0 and <= 0xFFFD or >= 0x10000 and <= 0xEFFFF;
    private static bool IsNameChar(int value)
        => IsNameStart(value) || value is '-' or '.' or >= '0' and <= '9' or 0xB7 or
            >= 0x0300 and <= 0x036F or >= 0x203F and <= 0x2040;
    private static int HexValue(char value)
        => value is >= '0' and <= '9' ? value - '0' : value is >= 'a' and <= 'f' ? value - 'a' + 10 : value is >= 'A' and <= 'F' ? value - 'A' + 10 : -1;
    private static bool IsEncodingName(string value)
    {
        if (value.Length == 0 || value[0] is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z')) return false;
        for (var i = 1; i < value.Length; i++)
        {
            if (value[i] is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')) return false;
        }
        return true;
    }

    private void Error(string code, int offset)
        => throw new MarkupParseException(code, _inputFrames.Count == 0 ? offset : _inputFrames.Peek().OriginalOffset);

    private readonly record struct RawAttribute(string Name, string Value, int Offset);
    private readonly record struct ElementFrame(Element Element, string QualifiedName, Dictionary<string, BindingUndo>? PreviousBindings);
    private sealed record QualifiedName(string Name, string? Prefix, string LocalName);
    private readonly record struct BindingUndo(bool Exists, string? Value);
    private readonly record struct InputFrame(string Source, int Position, string EntityName, int OriginalOffset, int ElementDepth, bool Parameter);
    private readonly record struct XmlEntityDeclaration(string? Value, string? PublicId, string? SystemId,
        bool Unparsed, bool FromParameterEntity);
}
