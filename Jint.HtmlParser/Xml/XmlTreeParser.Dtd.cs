using System.Text;
using Jint.HtmlParser.Html;

namespace Jint.HtmlParser;

// XML 1.0 (fifth edition) §2.8, §3.3, §4.2–§4.5, §5.1.
internal sealed partial class XmlTreeParser
{
    // HTML Standard §14.2. Only these identifiers activate the pinned local
    // character-entity catalog shared with the HTML tokenizer.
    private static readonly HashSet<string> s_knownCatalogPublicIds = new(StringComparer.Ordinal)
    {
        "-//W3C//DTD XHTML 1.0 Transitional//EN",
        "-//W3C//DTD XHTML 1.1//EN",
        "-//W3C//DTD XHTML 1.0 Strict//EN",
        "-//W3C//DTD XHTML 1.0 Frameset//EN",
        "-//W3C//DTD XHTML Basic 1.0//EN",
        "-//W3C//DTD XHTML 1.1 plus MathML 2.0//EN",
        "-//W3C//DTD XHTML 1.1 plus MathML 2.0 plus SVG 1.1//EN",
        "-//W3C//DTD MathML 2.0//EN",
        "-//WAPFORUM//DTD XHTML Mobile 1.0//EN",
        "-//WAPFORUM//DTD XHTML Mobile 1.1//EN",
        "-//WAPFORUM//DTD XHTML Mobile 1.2//EN"
    };
    private readonly Dictionary<string, List<XmlAttributeDeclaration>> _attributeDeclarations = new(StringComparer.Ordinal);

    private bool ResumeInput()
    {
        if (_inputFrames.Count == 0) return false;
        var frame = _inputFrames.Pop();
        if (!frame.Parameter && _frames.Count != frame.ElementDepth)
            Error("xml/invalid-markup", frame.OriginalOffset);
        if (frame.Parameter) _activeParameterEntities.Remove(frame.EntityName);
        else _activeGeneralEntities.Remove(frame.EntityName);
        _source = frame.Source;
        _position = frame.Position;
        WorkUnit();
        return true;
    }

    private void PushEntityInput(string value, string name, int offset, bool parameter)
    {
        var originalOffset = _inputFrames.Count == 0 ? offset : _inputFrames.Peek().OriginalOffset;
        _inputFrames.Push(new InputFrame(_source, _position, name, originalOffset, _frames.Count, parameter));
        _source = value;
        _position = 0;
        WorkUnit();
    }

    private string? ResolveGeneralEntity(string name, int offset, bool inAttribute)
    {
        if (!_generalEntities.TryGetValue(name, out var declaration))
        {
            if (_catalogActive && HtmlEntities.Values.TryGetValue(name + ";", out var catalogValue))
            {
                var result = new StringBuilder(catalogValue.Length);
                foreach (var c in catalogValue)
                {
                    ChargeExpansionCharacter();
                    result.Append(inAttribute && IsWhitespace(c) ? ' ' : c);
                }
                return result.ToString();
            }
            if ((!_hasExternalSubset || _catalogActive) && !_unreadParameterEntity || _standalone || inAttribute)
                Error("xml/undeclared-entity", offset);
            AddSkip(XmlSkippedEntityKind.General, name, null, null, offset);
            return string.Empty;
        }
        if (declaration.Unparsed) Error("xml/invalid-markup", offset);
        if (declaration.Value is null)
        {
            if (inAttribute) Error("xml/invalid-markup", offset);
            AddSkip(XmlSkippedEntityKind.General, name, declaration.PublicId, declaration.SystemId, offset);
            return string.Empty;
        }
        if (!_activeGeneralEntities.Add(name)) Error("xml/recursive-entity", offset);
        if (inAttribute) return ExpandAttributeEntity(name, declaration.Value, offset);
        PushEntityInput(declaration.Value, name, offset, parameter: false);
        return null;
    }

    private string ExpandAttributeEntity(string name, string value, int offset)
    {
        var result = new StringBuilder();
        var pending = new Stack<AttributeEntityFrame>();
        pending.Push(new AttributeEntityFrame(name, value));
        try
        {
            while (pending.Count != 0)
            {
                var frame = pending.Peek();
                if (frame.Position == frame.Value.Length)
                {
                    pending.Pop();
                    _activeGeneralEntities.Remove(frame.Name);
                    WorkUnit();
                    continue;
                }
                var c = frame.Value[frame.Position++];
                ChargeExpansionCharacter();
                if (c != '&')
                {
                    if (c == '<') Error("xml/invalid-markup", offset);
                    if (char.IsHighSurrogate(c))
                    {
                        if (frame.Position == frame.Value.Length || !char.IsLowSurrogate(frame.Value[frame.Position]))
                            Error("xml/invalid-character", offset);
                        var low = frame.Value[frame.Position++];
                        ChargeExpansionCharacter();
                        result.Append(c).Append(low);
                        continue;
                    }
                    if (!IsXmlChar(c)) Error("xml/invalid-character", offset);
                    if (c == '\r')
                    {
                        if (frame.Position < frame.Value.Length && frame.Value[frame.Position] == '\n')
                        {
                            frame.Position++;
                            ChargeExpansionCharacter();
                        }
                        result.Append(' ');
                    }
                    else result.Append(IsWhitespace(c) ? ' ' : c);
                    continue;
                }

                var referenceStart = frame.Position;
                while (frame.Position < frame.Value.Length && frame.Value[frame.Position] != ';')
                {
                    frame.Position++;
                    ChargeExpansionCharacter();
                }
                if (frame.Position == frame.Value.Length) Error("xml/invalid-markup", offset);
                var reference = frame.Value.AsSpan(referenceStart, frame.Position - referenceStart);
                frame.Position++;
                ChargeExpansionCharacter();
                if (reference.Length == 0) Error("xml/invalid-markup", offset);
                if (reference[0] == '#')
                {
                    result.Append(DecodeCharacterReference(reference, offset));
                    continue;
                }
                var referenceName = reference.ToString();
                var predefined = referenceName switch
                {
                    "amp" => "&",
                    "lt" => "<",
                    "gt" => ">",
                    "apos" => "'",
                    "quot" => "\"",
                    _ => null
                };
                if (predefined is not null)
                {
                    result.Append(predefined);
                    continue;
                }
                if (!_generalEntities.TryGetValue(referenceName, out var nested))
                    Error("xml/undeclared-entity", offset);
                if (nested.Value is null || nested.Unparsed) Error("xml/invalid-markup", offset);
                if (!_activeGeneralEntities.Add(referenceName)) Error("xml/recursive-entity", offset);
                pending.Push(new AttributeEntityFrame(referenceName, nested.Value!));
                WorkUnit();
            }
        }
        finally
        {
            while (pending.TryPop(out var frame)) _activeGeneralEntities.Remove(frame.Name);
        }
        return result.ToString();
    }

    private string DecodeCharacterReference(ReadOnlySpan<char> reference, int offset)
    {
        var hex = reference.Length >= 2 && reference[1] == 'x';
        var digits = reference[(hex ? 2 : 1)..];
        if (digits.IsEmpty) Error("xml/invalid-character", offset);
        uint codePoint = 0;
        foreach (var c in digits)
        {
            WorkUnit();
            var digit = hex ? HexValue(c) : c is >= '0' and <= '9' ? c - '0' : -1;
            if (digit < 0 || codePoint > (0x10FFFFu - (uint) digit) / (hex ? 16u : 10u))
                Error("xml/invalid-character", offset);
            codePoint = codePoint * (hex ? 16u : 10u) + (uint) digit;
        }
        if (!IsXmlChar(codePoint)) Error("xml/invalid-character", offset);
        return char.ConvertFromUtf32((int) codePoint);
    }

    private void ChargeExpansionCharacter()
    {
        if (_limits.MaxEntityExpansionCharacters != 0 && _expansionCharacters >= _limits.MaxEntityExpansionCharacters)
            throw new ParseLimitException(ParseLimitKind.EntityExpansionCharacters,
                _limits.MaxEntityExpansionCharacters, _limits.MaxEntityExpansionCharacters + 1);
        _expansionCharacters++;
        WorkUnit();
    }

    private void AddSkip(XmlSkippedEntityKind kind, string name, string? publicId, string? systemId, int offset)
    {
        _skippedEntities ??= [];
        _skippedEntities.Add(new XmlSkippedEntity(kind, name, publicId, systemId,
            _inputFrames.Count == 0 ? offset : _inputFrames.Peek().OriginalOffset));
        WorkUnit();
    }

    private void ParseDoctype()
    {
        var start = _position;
        if (_context is not null || _seenRoot || _doctypeName is not null || _inputFrames.Count != 0)
            Error("xml/invalid-declaration", start);
        _doctypeTokenStart = start;
        try
        {
            ConsumeLiteral("<!DOCTYPE", start);
            RequireDtdSpace();
            var name = ReadName(_position);
            _doctypeName = name;
            string? publicId = null;
            var systemId = string.Empty;
            SkipWhitespace(_position);
            if (StartsWith("SYSTEM") || StartsWith("PUBLIC"))
            {
                (publicId, systemId) = ReadExternalId();
                _hasExternalSubset = true;
                _catalogActive = publicId is not null && s_knownCatalogPublicIds.Contains(publicId);
                if (!_catalogActive)
                    AddSkip(XmlSkippedEntityKind.ExternalSubset, string.Empty, publicId, systemId, start);
                SkipWhitespace(_position);
            }

            if (Current == '[')
            {
                Consume();
                ParseInternalSubset();
                Expect(']', "xml/invalid-declaration", start);
                SkipWhitespace(_position);
            }
            Expect('>', "xml/invalid-declaration", start);
            _document.AppendParsedChild(_document.CreateDocumentType(name, publicId ?? string.Empty, systemId));
        }
        finally
        {
            _doctypeTokenStart = -1;
        }
    }

    private void ParseInternalSubset()
    {
        while (true)
        {
            if (End)
            {
                if (_inputFrames.Count != 0 && _inputFrames.Peek().Parameter && ResumeInput()) continue;
                Error("xml/unexpected-eof", _position);
            }
            if (Current == ']' && _inputFrames.Count == 0) return;
            if (IsWhitespace(Current))
            {
                Consume();
                continue;
            }
            if (Current == '%')
            {
                ReadParameterReference();
                continue;
            }
            if (StartsWith("<!--"))
            {
                SkipDtdComment();
                continue;
            }
            if (StartsWith("<?"))
            {
                SkipDtdPi();
                continue;
            }
            if (StartsWith("<!ENTITY"))
            {
                ParseEntityDeclaration();
                continue;
            }
            if (StartsWith("<!ATTLIST"))
            {
                ParseAttlistDeclaration();
                continue;
            }
            if (StartsWith("<!ELEMENT"))
            {
                ParseElementDeclaration();
                continue;
            }
            if (StartsWith("<!NOTATION"))
            {
                ParseNotationDeclaration();
                continue;
            }
            Error("xml/invalid-declaration", _position);
        }
    }

    private (string? PublicId, string SystemId) ReadExternalId()
    {
        var start = _position;
        if (StartsWith("SYSTEM"))
        {
            ConsumeLiteral("SYSTEM", start);
            RequireDtdSpace();
            return (null, ReadQuoted("xml/invalid-declaration", start));
        }
        ConsumeLiteral("PUBLIC", start);
        RequireDtdSpace();
        var publicId = ReadQuoted("xml/invalid-declaration", start);
        foreach (var c in publicId)
        {
            WorkUnit();
            if (!IsPubidChar(c)) Error("xml/invalid-declaration", start);
        }
        RequireDtdSpace();
        var systemId = ReadQuoted("xml/invalid-declaration", start);
        return (CollapseSpaces(publicId.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ')), systemId);
    }

    private void ParseEntityDeclaration()
    {
        var start = _position;
        ConsumeLiteral("<!ENTITY", start);
        RequireDtdSpace();
        var parameter = false;
        if (Current == '%')
        {
            parameter = true;
            Consume();
            RequireDtdSpace();
        }
        var name = ReadName(_position);
        RequireDtdSpace();
        XmlEntityDeclaration declaration;
        if (Current is '\'' or '"')
        {
            var value = ConstructEntityValue(ReadQuoted("xml/invalid-declaration", start), start);
            declaration = new XmlEntityDeclaration(value, null, null, false);
        }
        else
        {
            var (publicId, systemId) = ReadExternalId();
            var unparsed = false;
            if (SkipWhitespace(_position) && StartsWith("NDATA"))
            {
                if (parameter) Error("xml/invalid-declaration", start);
                ConsumeLiteral("NDATA", start);
                RequireDtdSpace();
                ReadName(_position);
                unparsed = true;
            }
            declaration = new XmlEntityDeclaration(null, publicId, systemId, unparsed);
        }
        SkipWhitespace(_position);
        Expect('>', "xml/invalid-declaration", start);
        if (!_unreadParameterEntity || _standalone)
        {
            var table = parameter ? _parameterEntities : _generalEntities;
            table.TryAdd(name, declaration); // XML §4.2: the first declaration binds.
        }
    }

    private void ParseAttlistDeclaration()
    {
        var start = _position;
        ConsumeLiteral("<!ATTLIST", start);
        RequireDtdSpace();
        var elementName = ReadName(_position);
        var declarations = new List<XmlAttributeDeclaration>();
        while (true)
        {
            var space = SkipWhitespace(_position);
            if (Current == '>')
            {
                Consume();
                break;
            }
            if (!space) Error("xml/invalid-declaration", _position);
            var attributeName = ReadName(_position);
            RequireDtdSpace();
            var type = ReadAttributeType();
            RequireDtdSpace();
            string? defaultValue = null;
            var fixedValue = false;
            if (StartsWith("#REQUIRED")) ConsumeLiteral("#REQUIRED", start);
            else if (StartsWith("#IMPLIED")) ConsumeLiteral("#IMPLIED", start);
            else
            {
                if (StartsWith("#FIXED"))
                {
                    fixedValue = true;
                    ConsumeLiteral("#FIXED", start);
                    RequireDtdSpace();
                }
                defaultValue = ReadQuoted("xml/invalid-declaration", start);
            }
            declarations.Add(new XmlAttributeDeclaration(attributeName, type == "CDATA", defaultValue, fixedValue));
        }
        if (!_unreadParameterEntity || _standalone)
        {
            if (!_attributeDeclarations.TryGetValue(elementName, out var existing))
                _attributeDeclarations.Add(elementName, existing = new List<XmlAttributeDeclaration>());
            foreach (var declaration in declarations)
            {
                if (!existing.Exists(item => item.Name == declaration.Name)) existing.Add(declaration);
            }
        }
    }

    private string ConstructEntityValue(string source, int offset)
    {
        var result = new StringBuilder(source.Length);
        var frames = new Stack<AttributeEntityFrame>();
        frames.Push(new AttributeEntityFrame(string.Empty, source));
        try
        {
            while (frames.Count != 0)
            {
                var frame = frames.Peek();
                if (frame.Position == frame.Value.Length)
                {
                    frames.Pop();
                    if (frame.Name.Length != 0) _activeParameterEntities.Remove(frame.Name);
                    WorkUnit();
                    continue;
                }
                var c = frame.Value[frame.Position++];
                if (frame.Name.Length == 0) WorkUnit();
                else ChargeExpansionCharacter();
                if (c is '&' or '%')
                {
                    var start = frame.Position;
                    while (frame.Position < frame.Value.Length && frame.Value[frame.Position] != ';')
                    {
                        frame.Position++;
                        if (frame.Name.Length == 0) WorkUnit();
                        else ChargeExpansionCharacter();
                    }
                    if (frame.Position == frame.Value.Length) Error("xml/invalid-declaration", offset);
                    var reference = frame.Value.AsSpan(start, frame.Position - start);
                    frame.Position++;
                    if (frame.Name.Length == 0) WorkUnit();
                    else ChargeExpansionCharacter();
                    if (reference.IsEmpty) Error("xml/invalid-declaration", offset);
                    if (c == '&')
                    {
                        if (reference[0] == '#') result.Append(DecodeCharacterReference(reference, offset));
                        else
                        {
                            ValidateEntityReferenceName(reference, offset);
                            result.Append('&').Append(reference).Append(';');
                        }
                        continue;
                    }
                    ValidateEntityReferenceName(reference, offset);
                    var name = reference.ToString();
                    if (!_parameterEntities.TryGetValue(name, out var declaration))
                    {
                        if (!_hasExternalSubset || _catalogActive || _standalone) Error("xml/undeclared-entity", offset);
                        AddSkip(XmlSkippedEntityKind.Parameter, name, null, null, offset);
                        _unreadParameterEntity = true;
                        continue;
                    }
                    if (declaration.Value is null)
                    {
                        AddSkip(XmlSkippedEntityKind.Parameter, name, declaration.PublicId, declaration.SystemId, offset);
                        _unreadParameterEntity = true;
                        continue;
                    }
                    if (!_activeParameterEntities.Add(name)) Error("xml/recursive-entity", offset);
                    frames.Push(new AttributeEntityFrame(name, declaration.Value));
                    WorkUnit();
                    continue;
                }
                if (char.IsHighSurrogate(c))
                {
                    if (frame.Position == frame.Value.Length || !char.IsLowSurrogate(frame.Value[frame.Position]))
                        Error("xml/invalid-character", offset);
                    var low = frame.Value[frame.Position++];
                    if (frame.Name.Length == 0) WorkUnit();
                    else ChargeExpansionCharacter();
                    result.Append(c).Append(low);
                    continue;
                }
                if (!IsXmlChar(c)) Error("xml/invalid-character", offset);
                if (c == '\r')
                {
                    if (frame.Position < frame.Value.Length && frame.Value[frame.Position] == '\n')
                    {
                        frame.Position++;
                        if (frame.Name.Length == 0) WorkUnit();
                        else ChargeExpansionCharacter();
                    }
                    result.Append('\n');
                }
                else result.Append(c);
            }
        }
        finally
        {
            while (frames.TryPop(out var frame))
            {
                if (frame.Name.Length != 0) _activeParameterEntities.Remove(frame.Name);
            }
        }
        return result.ToString();
    }

    private void ValidateEntityReferenceName(ReadOnlySpan<char> name, int offset)
    {
        if (name.IsEmpty) Error("xml/invalid-name", offset);
        for (var i = 0; i < name.Length; i++)
        {
            var first = i == 0;
            int scalar = name[i];
            WorkUnit();
            if (char.IsHighSurrogate(name[i]) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
            {
                scalar = char.ConvertToUtf32(name[i], name[++i]);
                WorkUnit();
            }
            if (first ? !IsNameStart(scalar) : !IsNameChar(scalar))
                Error("xml/invalid-name", offset);
        }
    }

    private void ApplyDtdAttributes(string elementName, List<RawAttribute> attributes,
        HashSet<string> rawNames, Dictionary<string, string?> localBindings)
    {
        if (!_attributeDeclarations.TryGetValue(elementName, out var declarations)) return;
        var byName = new Dictionary<string, XmlAttributeDeclaration>(declarations.Count, StringComparer.Ordinal);
        foreach (var declaration in declarations)
        {
            byName.Add(declaration.Name, declaration);
            WorkUnit();
        }
        for (var i = 0; i < attributes.Count; i++)
        {
            var attribute = attributes[i];
            if (byName.TryGetValue(attribute.Name, out var declaration) && !declaration.CData)
                attributes[i] = attribute with { Value = CollapseSpaces(attribute.Value) };
            WorkUnit();
        }
        foreach (var declaration in declarations)
        {
            WorkUnit();
            if (declaration.DefaultValue is null || !rawNames.Add(declaration.Name)) continue;
            ValidateQName(declaration.Name, _position);
            var value = NormalizeDtdDefault(declaration.DefaultValue, _position);
            if (!declaration.CData) value = CollapseSpaces(value);
            attributes.Add(new RawAttribute(declaration.Name, value, _position));
            if (declaration.Name == "xmlns" || declaration.Name.StartsWith("xmlns:", StringComparison.Ordinal))
            {
                var prefix = declaration.Name == "xmlns" ? string.Empty : declaration.Name[6..];
                ValidateBinding(prefix, value, _position);
                localBindings.Add(prefix, EmptyToNull(value));
            }
        }
    }

    private string NormalizeDtdDefault(string value, int offset)
    {
        var result = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            WorkUnit();
            if (c == '<') Error("xml/invalid-markup", offset);
            if (c == '&')
            {
                var start = ++i;
                while (i < value.Length && value[i] != ';')
                {
                    WorkUnit();
                    i++;
                }
                if (i == value.Length) Error("xml/invalid-markup", offset);
                var reference = value.AsSpan(start, i - start);
                if (reference.IsEmpty) Error("xml/invalid-markup", offset);
                if (reference[0] == '#')
                {
                    result.Append(DecodeCharacterReference(reference, offset));
                    continue;
                }
                var name = reference.ToString();
                var predefined = name switch
                {
                    "amp" => "&",
                    "lt" => "<",
                    "gt" => ">",
                    "apos" => "'",
                    "quot" => "\"",
                    _ => null
                };
                if (predefined is not null) result.Append(predefined);
                else result.Append(ResolveGeneralEntity(name, offset, inAttribute: true));
                continue;
            }
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 == value.Length || !char.IsLowSurrogate(value[i + 1]))
                    Error("xml/invalid-character", offset);
                result.Append(c).Append(value[++i]);
                WorkUnit();
                continue;
            }
            if (!IsXmlChar(c)) Error("xml/invalid-character", offset);
            if (c == '\r' && i + 1 < value.Length && value[i + 1] == '\n') i++;
            result.Append(IsWhitespace(c) ? ' ' : c);
        }
        return result.ToString();
    }

    private string CollapseSpaces(string value)
    {
        var result = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var c in value)
        {
            WorkUnit();
            if (c == ' ')
            {
                pendingSpace = result.Length != 0;
                continue;
            }
            if (pendingSpace) result.Append(' ');
            result.Append(c);
            pendingSpace = false;
        }
        return result.ToString();
    }

    private string ReadAttributeType()
    {
        if (Current == '(') return ReadDtdEnumeration();
        var type = ReadName(_position);
        if (type == "NOTATION")
        {
            RequireDtdSpace();
            ReadDtdEnumeration();
            return type;
        }
        if (type is not ("CDATA" or "ID" or "IDREF" or "IDREFS" or "ENTITY" or "ENTITIES" or "NMTOKEN" or "NMTOKENS"))
            Error("xml/invalid-declaration", _position);
        return type;
    }

    private string ReadDtdEnumeration()
    {
        var start = _position;
        Expect('(', "xml/invalid-declaration", start);
        var any = false;
        while (true)
        {
            SkipWhitespace(_position);
            if (!IsNameChar(PeekScalar())) Error("xml/invalid-declaration", _position);
            do ConsumeScalar();
            while (IsNameChar(PeekScalar()));
            any = true;
            SkipWhitespace(_position);
            if (Current == ')')
            {
                Consume();
                break;
            }
            Expect('|', "xml/invalid-declaration", start);
        }
        if (!any) Error("xml/invalid-declaration", start);
        return "ENUMERATION";
    }

    private void ReadParameterReference()
    {
        var offset = _position;
        Consume();
        var name = ReadName(_position);
        Expect(';', "xml/invalid-declaration", offset);
        if (!_parameterEntities.TryGetValue(name, out var declaration))
        {
            if ((!_hasExternalSubset || _catalogActive) && !_unreadParameterEntity || _standalone)
                Error("xml/undeclared-entity", offset);
            AddSkip(XmlSkippedEntityKind.Parameter, name, null, null, offset);
            _unreadParameterEntity = true;
            return;
        }
        if (declaration.Value is null)
        {
            AddSkip(XmlSkippedEntityKind.Parameter, name, declaration.PublicId, declaration.SystemId, offset);
            _unreadParameterEntity = true;
            return;
        }
        if (!_activeParameterEntities.Add(name)) Error("xml/recursive-entity", offset);
        PushEntityInput(" " + declaration.Value + " ", name, offset, parameter: true);
    }

    private void SkipDtdComment()
    {
        var start = _position;
        ConsumeLiteral("<!--", start);
        while (!StartsWith("-->"))
        {
            if (End) Error("xml/unexpected-eof", _position);
            if (StartsWith("--")) Error("xml/invalid-declaration", _position);
            ConsumeScalar();
        }
        ConsumeLiteral("-->", start);
    }

    private void SkipDtdPi()
    {
        var start = _position;
        ConsumeLiteral("<?", start);
        var name = ReadName(_position);
        if (name.Equals("xml", StringComparison.OrdinalIgnoreCase) || name.Contains(':'))
            Error("xml/invalid-declaration", start);
        if (!StartsWith("?>") && !SkipWhitespace(_position)) Error("xml/invalid-declaration", _position);
        while (!StartsWith("?>"))
        {
            if (End) Error("xml/unexpected-eof", _position);
            ConsumeScalar();
        }
        ConsumeLiteral("?>", start);
    }

    private void ParseElementDeclaration()
    {
        var start = _position;
        ConsumeLiteral("<!ELEMENT", start);
        RequireDtdSpace();
        ReadName(_position);
        RequireDtdSpace();
        if (StartsWith("EMPTY")) ConsumeLiteral("EMPTY", start);
        else if (StartsWith("ANY")) ConsumeLiteral("ANY", start);
        else if (Current == '(') ParseContentModel();
        else Error("xml/invalid-declaration", _position);
        SkipWhitespace(_position);
        Expect('>', "xml/invalid-declaration", start);
    }

    private void ParseContentModel()
    {
        var start = _position;
        Expect('(', "xml/invalid-declaration", start);
        SkipWhitespace(_position);
        if (Current == '#')
        {
            ConsumeLiteral("#PCDATA", start);
            SkipWhitespace(_position);
            var names = 0;
            while (Current == '|')
            {
                Consume();
                SkipWhitespace(_position);
                ReadName(_position);
                names++;
                SkipWhitespace(_position);
            }
            Expect(')', "xml/invalid-declaration", start);
            if (names != 0) Expect('*', "xml/invalid-declaration", start);
            return;
        }

        var groups = new Stack<ContentGroup>();
        groups.Push(new ContentGroup());
        while (groups.Count != 0)
        {
            SkipWhitespace(_position);
            var group = groups.Peek();
            if (group.ExpectTerm)
            {
                if (Current == '(')
                {
                    Consume();
                    groups.Push(new ContentGroup());
                    continue;
                }
                ReadName(_position);
                group.ExpectTerm = false;
                if (Current is '?' or '*' or '+') Consume();
                continue;
            }

            if (Current == ')')
            {
                Consume();
                groups.Pop();
                if (Current is '?' or '*' or '+') Consume();
                if (groups.Count != 0) groups.Peek().ExpectTerm = false;
                continue;
            }
            if (Current is not (',' or '|')) Error("xml/invalid-declaration", _position);
            var separator = Current;
            if (group.Separator != '\0' && group.Separator != separator)
                Error("xml/invalid-declaration", _position);
            group.Separator = separator;
            Consume();
            group.ExpectTerm = true;
        }
    }

    private void ParseNotationDeclaration()
    {
        var start = _position;
        ConsumeLiteral("<!NOTATION", start);
        RequireDtdSpace();
        ReadName(_position);
        RequireDtdSpace();
        if (StartsWith("SYSTEM"))
        {
            ConsumeLiteral("SYSTEM", start);
            RequireDtdSpace();
            ReadQuoted("xml/invalid-declaration", start);
        }
        else if (StartsWith("PUBLIC"))
        {
            ConsumeLiteral("PUBLIC", start);
            RequireDtdSpace();
            var publicId = ReadQuoted("xml/invalid-declaration", start);
            foreach (var c in publicId)
            {
                WorkUnit();
                if (!IsPubidChar(c)) Error("xml/invalid-declaration", start);
            }
            if (SkipWhitespace(_position) && Current is '\'' or '"')
                ReadQuoted("xml/invalid-declaration", start);
        }
        else Error("xml/invalid-declaration", _position);
        SkipWhitespace(_position);
        Expect('>', "xml/invalid-declaration", start);
    }

    private sealed class ContentGroup
    {
        internal bool ExpectTerm { get; set; } = true;
        internal char Separator { get; set; }
    }

    private void RequireDtdSpace()
    {
        if (!SkipWhitespace(_position)) Error("xml/invalid-declaration", _position);
    }

    private static bool IsPubidChar(char c)
        => c is ' ' or '\r' or '\n' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'
            or '-' or '\'' or '(' or ')' or '+' or ',' or '.' or '/' or ':' or '=' or '?' or ';' or '!'
            or '*' or '#' or '@' or '$' or '_' or '%';

    private readonly record struct XmlAttributeDeclaration(string Name, bool CData, string? DefaultValue, bool Fixed);
    private sealed class AttributeEntityFrame(string name, string value)
    {
        internal string Name { get; } = name;
        internal string Value { get; } = value;
        internal int Position { get; set; }
    }
}
