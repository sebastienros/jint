using Jint.HtmlParser.Html;

namespace Jint.HtmlParser;

// XML 1.0 (fifth edition) §2.8, §3.3, §4.2–§4.5, §5.1.
internal sealed partial class XmlTreeParser
{
    // HTML Standard §14.2. Only these identifiers activate the pinned local
    // character-entity catalog shared with the HTML tokenizer.
    private readonly Dictionary<string, Dictionary<string, XmlAttributeDeclaration>> _attributeDeclarations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<XmlAttributeDeclaration>> _defaultAttributeDeclarations = new(StringComparer.Ordinal);

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
        var referenceInsideParameterEntity = _inputFrames.Count != 0 && _inputFrames.Peek().Parameter;
        if (!_generalEntities.TryGetValue(name, out var declaration))
        {
            if (_catalogActive && (!_standalone || referenceInsideParameterEntity) &&
                HtmlEntities.Lookup.FindTerminatedName(name) is { } catalogValue)
                return IncludeCatalogEntity(catalogValue, inAttribute);
            if ((!_hasExternalSubset && !_sawParameterReference || _standalone) && !referenceInsideParameterEntity)
                Error("xml/undeclared-entity", offset);
            AddSkip(XmlSkippedEntityKind.General, name, null, null, offset);
            return string.Empty;
        }
        if (_standalone && declaration.FromParameterEntity && !referenceInsideParameterEntity)
            Error("xml/undeclared-entity", offset);
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
        var result = new ValueStringBuilder(stackalloc char[128]);
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
                        result.Append(c);
                        result.Append(low);
                        continue;
                    }
                    if (!IsXmlChar(c)) Error("xml/invalid-character", offset);
                    if (c == '\r')
                    {
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
                ValidateEntityReferenceName(reference, offset);
                var referenceName = reference.ToString();
                var predefined = XmlPredefinedEntityLookup.Match(referenceName);
                if (predefined is not null)
                {
                    result.Append(predefined);
                    continue;
                }
                if (!_generalEntities.TryGetValue(referenceName, out var nested))
                {
                    var referenceInsideParameterEntity = _inputFrames.Count != 0 && _inputFrames.Peek().Parameter;
                    if (_catalogActive && (!_standalone || referenceInsideParameterEntity) &&
                        HtmlEntities.Lookup.FindTerminatedName(referenceName) is { } catalogValue)
                    {
                        var included = IncludeCatalogEntity(catalogValue, inAttribute: true);
                        AppendCopy(ref result, included);
                        continue;
                    }
                    if ((!_hasExternalSubset && !_sawParameterReference || _standalone) &&
                        !referenceInsideParameterEntity)
                        Error("xml/undeclared-entity", offset);
                    AddSkip(XmlSkippedEntityKind.General, referenceName, null, null, offset);
                    continue;
                }
                if (_standalone && nested.FromParameterEntity &&
                    !(_inputFrames.Count != 0 && _inputFrames.Peek().Parameter))
                    Error("xml/undeclared-entity", offset);
                if (nested.Value is null || nested.Unparsed) Error("xml/invalid-markup", offset);
                if (!_activeGeneralEntities.Add(referenceName)) Error("xml/recursive-entity", offset);
                pending.Push(new AttributeEntityFrame(referenceName, nested.Value!));
                WorkUnit();
            }
            return Materialize(ref result);
        }
        finally
        {
            while (pending.TryPop(out var frame)) _activeGeneralEntities.Remove(frame.Name);
            result.Dispose();
        }
    }

    private string IncludeCatalogEntity(string value, bool inAttribute)
    {
        var normalized = inAttribute ? new CharBuffer(value.Length) : null;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var scalar = char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])
                ? char.ConvertToUtf32(c, value[i + 1]) : c;
            // The local XHTML DTD catalog escapes < and & with six-unit character
            // references. Other decoded characters are literal replacement units.
            var replacementUnits = scalar is '<' or '&' ? 6 : scalar > 0xFFFF ? 2 : 1;
            for (var work = replacementUnits; work > 0; work--) ChargeExpansionCharacter();
            if (normalized is not null)
            {
                normalized.Append(IsWhitespace(c) ? ' ' : c);
                WorkUnit();
            }
            if (scalar > 0xFFFF)
            {
                i++;
                if (normalized is not null)
                {
                    normalized.Append(value[i]);
                    WorkUnit();
                }
            }
        }
        if (normalized is null) return value;
        return Materialize(normalized);
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
            ValidateQName(name, start);
            _doctypeName = name;
            string? publicId = null;
            var systemId = string.Empty;
            SkipWhitespace(_position);
            if (StartsWith("SYSTEM") || StartsWith("PUBLIC"))
            {
                (publicId, systemId) = ReadExternalId();
                _hasExternalSubset = true;
                _catalogActive = publicId is not null && XmlCatalogIdentifierLookup.Match(publicId);
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
            if (Current == '<')
            {
                // Declarations are mutually exclusive on the units after "<!", so each is verified once
                // instead of probing every literal in turn.
                switch (Peek(1))
                {
                    case '?':
                        ParseDtdPi();
                        continue;
                    case '!':
                        switch (Peek(2))
                        {
                            case '-' when StartsWith("<!--"):
                                SkipDtdComment();
                                continue;
                            case 'E' when StartsWith("<!ENTITY"):
                                ParseEntityDeclaration();
                                continue;
                            case 'E' when StartsWith("<!ELEMENT"):
                                ParseElementDeclaration();
                                continue;
                            case 'A' when StartsWith("<!ATTLIST"):
                                ParseAttlistDeclaration();
                                continue;
                            case 'N' when StartsWith("<!NOTATION"):
                                ParseNotationDeclaration();
                                continue;
                        }
                        break;
                }
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
        ValidateUnprefixedDtdName(name, start);
        RequireDtdSpace();
        var fromParameterEntity = _inputFrames.Count != 0 && _inputFrames.Peek().Parameter;
        XmlEntityDeclaration declaration;
        if (Current is '\'' or '"')
        {
            var replacementSource = _inputFrames.Count != 0;
            var value = ConstructEntityValue(ReadQuoted("xml/invalid-declaration", start), start,
                replacementSource);
            declaration = new XmlEntityDeclaration(value, null, null, false, fromParameterEntity);
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
                ValidateUnprefixedDtdName(ReadName(_position), start);
                unparsed = true;
            }
            declaration = new XmlEntityDeclaration(null, publicId, systemId, unparsed, fromParameterEntity);
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
        ValidateQName(elementName, start);
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
            ValidateQName(attributeName, _position);
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
                var replacementSource = _inputFrames.Count != 0;
                defaultValue = NormalizeDtdDefault(ReadQuoted("xml/invalid-declaration", start), start,
                    replacementSource);
                if (type != "CDATA") defaultValue = CollapseSpaces(defaultValue);
            }
            declarations.Add(new XmlAttributeDeclaration(attributeName, type == "CDATA", type == "ID",
                defaultValue, fixedValue));
        }
        if (!_unreadParameterEntity || _standalone)
        {
            if (!_attributeDeclarations.TryGetValue(elementName, out var existing))
            {
                _attributeDeclarations.Add(elementName, existing = new Dictionary<string, XmlAttributeDeclaration>(StringComparer.Ordinal));
            }
            foreach (var declaration in declarations)
            {
                // XML §3.3: merge ATTLISTs once, preserving the first declaration of each attribute.
                if (existing.TryAdd(declaration.Name, declaration) && declaration.DefaultValue is not null)
                {
                    if (!_defaultAttributeDeclarations.TryGetValue(elementName, out var defaults))
                        _defaultAttributeDeclarations.Add(elementName, defaults = new List<XmlAttributeDeclaration>());
                    defaults.Add(declaration);
                }
                WorkUnit();
            }
        }
    }

    private string ConstructEntityValue(string source, int offset, bool replacementSource)
    {
        var result = new ValueStringBuilder(stackalloc char[128]);
        result.EnsureCapacity(source.Length);
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
                    if (c == '%' && frame.Name.Length == 0)
                        Error("xml/invalid-declaration", offset);
                    if (c == '%') _sawParameterReference = true;
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
                            result.Append('&');
                            result.Append(reference);
                            result.Append(';');
                        }
                        continue;
                    }
                    ValidateEntityReferenceName(reference, offset);
                    var name = reference.ToString();
                    if (!_parameterEntities.TryGetValue(name, out var declaration))
                    {
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
                    result.Append(c);
                    result.Append(low);
                    continue;
                }
                if (!IsXmlChar(c)) Error("xml/invalid-character", offset);
                if (c == '\r' && frame.Name.Length == 0 && !replacementSource)
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
            return Materialize(ref result);
        }
        finally
        {
            while (frames.TryPop(out var frame))
            {
                if (frame.Name.Length != 0) _activeParameterEntities.Remove(frame.Name);
            }
            result.Dispose();
        }
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
            if (scalar == ':') Error("xml/namespace-error", offset);
            if (first ? !IsNameStart(scalar) : !IsNameChar(scalar))
                Error("xml/invalid-name", offset);
        }
    }

    private void ValidateUnprefixedDtdName(string name, int offset)
    {
        if (name.Contains(':')) Error("xml/namespace-error", offset);
    }

    private void ApplyDtdAttributes(string elementName, List<RawAttribute> attributes,
        Dictionary<string, string?> localBindings)
    {
        // Only defaults need per-element work; #IMPLIED and #REQUIRED declarations do not.
        if (!_defaultAttributeDeclarations.TryGetValue(elementName, out var declarations)) return;
        foreach (var declaration in declarations)
        {
            WorkUnit();
            if (!AddRawName(attributes, declaration.Name)) continue;
            var value = declaration.DefaultValue!;
            attributes.Add(new RawAttribute(declaration.Name, value, _position));
            if (declaration.Name == "xmlns" || declaration.Name.StartsWith("xmlns:", StringComparison.Ordinal))
            {
                var prefix = declaration.Name == "xmlns" ? string.Empty : declaration.Name[6..];
                ValidateBinding(prefix, value, _position);
                localBindings.Add(prefix, EmptyToNull(value));
            }
        }
    }

    private Dictionary<string, XmlAttributeDeclaration>? GetDeclaredAttributeTypes(string elementName)
        => _attributeDeclarations.GetValueOrDefault(elementName);

    private string NormalizeDtdDefault(string value, int offset, bool replacementSource)
    {
        var result = new ValueStringBuilder(stackalloc char[128]);
        try
        {
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
                    ValidateEntityReferenceName(reference, offset);
                    var name = reference.ToString();
                    var predefined = XmlPredefinedEntityLookup.Match(name);
                    if (predefined is not null) result.Append(predefined);
                    else AppendCopy(ref result, ResolveGeneralEntity(name, offset, inAttribute: true));
                    continue;
                }
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 == value.Length || !char.IsLowSurrogate(value[i + 1]))
                        Error("xml/invalid-character", offset);
                    result.Append(c);
                    result.Append(value[++i]);
                    WorkUnit();
                    continue;
                }
                if (!IsXmlChar(c)) Error("xml/invalid-character", offset);
                if (c == '\r' && !replacementSource && i + 1 < value.Length && value[i + 1] == '\n') i++;
                result.Append(IsWhitespace(c) ? ' ' : c);
            }
            return Materialize(ref result);
        }
        finally { result.Dispose(); }
    }

    private string CollapseSpaces(string value)
    {
        var result = new ValueStringBuilder(stackalloc char[128]);
        try
        {
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
            return Materialize(ref result);
        }
        finally { result.Dispose(); }
    }

    private string ReadAttributeType()
    {
        if (Current == '(') return ReadDtdEnumeration(namesOnly: false);
        var type = ReadName(_position);
        if (type == "NOTATION")
        {
            RequireDtdSpace();
            ReadDtdEnumeration(namesOnly: true);
            return type;
        }
        if (!XmlAttributeTypeLookup.Match(type))
            Error("xml/invalid-declaration", _position);
        return type;
    }

    private string ReadDtdEnumeration(bool namesOnly)
    {
        var start = _position;
        Expect('(', "xml/invalid-declaration", start);
        var any = false;
        while (true)
        {
            SkipWhitespace(_position);
            if (namesOnly ? !IsNameStart(PeekScalar()) : !IsNameChar(PeekScalar()))
                Error("xml/invalid-declaration", _position);
            var valueStart = _position;
            do ConsumeScalar();
            while (IsNameChar(PeekScalar()));
            if (namesOnly && _source.AsSpan(valueStart, _position - valueStart).IndexOf(':') >= 0)
                Error("xml/namespace-error", valueStart);
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
        _sawParameterReference = true;
        Consume();
        var name = ReadName(_position);
        ValidateUnprefixedDtdName(name, offset);
        Expect(';', "xml/invalid-declaration", offset);
        if (!_parameterEntities.TryGetValue(name, out var declaration))
        {
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
        if (_limits.MaxEntityExpansionCharacters != 0 &&
            (long) declaration.Value.Length + 2 > _limits.MaxEntityExpansionCharacters - _expansionCharacters)
            throw new ParseLimitException(ParseLimitKind.EntityExpansionCharacters,
                _limits.MaxEntityExpansionCharacters, _limits.MaxEntityExpansionCharacters + 1);
        WorkUnits(declaration.Value.Length);
        WorkUnit();
        WorkUnit();
        _cancellationToken.ThrowIfCancellationRequested();
        var padded = string.Concat(" ", declaration.Value, " ");
        _cancellationToken.ThrowIfCancellationRequested();
        PushEntityInput(padded, name, offset, parameter: true);
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

    private void ParseDtdPi()
    {
        var start = _position;
        ConsumeLiteral("<?", start);
        var name = ReadName(_position);
        if (name.Equals("xml", StringComparison.OrdinalIgnoreCase) || name.Contains(':'))
            Error("xml/invalid-declaration", start);
        if (!StartsWith("?>") && !SkipWhitespace(_position)) Error("xml/invalid-declaration", _position);
        var contentStart = _position;
        while (!StartsWith("?>"))
        {
            if (End) Error("xml/unexpected-eof", _position);
            ConsumeScalar();
        }
        var content = _source.AsSpan(contentStart, _position - contentStart);
        var data = NormalizeLines(content);
        ConsumeLiteral("?>", start);
        (_dtdProcessingInstructions ??= []).Add(new XmlDtdProcessingInstruction(name, data,
            _inputFrames.Count == 0 ? start : _inputFrames.Peek().OriginalOffset));
        _onCancellationPoll?.Invoke();
        _cancellationToken.ThrowIfCancellationRequested();
    }

    private void ParseElementDeclaration()
    {
        var start = _position;
        ConsumeLiteral("<!ELEMENT", start);
        RequireDtdSpace();
        ValidateQName(ReadName(_position), start);
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
                ValidateQName(ReadName(_position), _position);
                names++;
                SkipWhitespace(_position);
            }
            Expect(')', "xml/invalid-declaration", start);
            if (names != 0) Expect('*', "xml/invalid-declaration", start);
            else if (Current == '*') Consume();
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
                ValidateQName(ReadName(_position), _position);
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
        var name = ReadName(_position);
        ValidateUnprefixedDtdName(name, start);
        RequireDtdSpace();
        string? publicId = null;
        string? systemId = null;
        if (StartsWith("SYSTEM"))
        {
            ConsumeLiteral("SYSTEM", start);
            RequireDtdSpace();
            systemId = NormalizeLines(ReadQuoted("xml/invalid-declaration", start).AsSpan());
        }
        else if (StartsWith("PUBLIC"))
        {
            ConsumeLiteral("PUBLIC", start);
            RequireDtdSpace();
            publicId = NormalizeNotationPublicId(ReadQuoted("xml/invalid-declaration", start), start);
            if (SkipWhitespace(_position) && Current is '\'' or '"')
                systemId = NormalizeLines(ReadQuoted("xml/invalid-declaration", start).AsSpan());
        }
        else Error("xml/invalid-declaration", _position);
        SkipWhitespace(_position);
        Expect('>', "xml/invalid-declaration", start);
        _onCancellationPoll?.Invoke();
        _cancellationToken.ThrowIfCancellationRequested();
        (_notations ??= new List<XmlNotationDeclaration>()).Add(new XmlNotationDeclaration(name,
            publicId, systemId, _inputFrames.Count == 0 ? start : _inputFrames.Peek().OriginalOffset));
        WorkUnit();
        _cancellationToken.ThrowIfCancellationRequested();
    }

    private string NormalizeNotationPublicId(string value, int offset)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        var normalized = new CharBuffer(value.Length);
        _cancellationToken.ThrowIfCancellationRequested();
        var pendingSpace = false;
        foreach (var c in value)
        {
            WorkUnit();
            if (!IsPubidChar(c)) Error("xml/invalid-declaration", offset);
            if (c is ' ' or '\r' or '\n')
            {
                pendingSpace = normalized.Length != 0;
                continue;
            }
            if (pendingSpace) normalized.Append(' ');
            normalized.Append(c);
            pendingSpace = false;
        }
        return Materialize(normalized);
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

    private readonly record struct XmlAttributeDeclaration(string Name, bool CData, bool IsId, string? DefaultValue,
        bool Fixed);
    private sealed class AttributeEntityFrame(string name, string value)
    {
        internal string Name { get; } = name;
        internal string Value { get; } = value;
        internal int Position { get; set; }
    }
}
