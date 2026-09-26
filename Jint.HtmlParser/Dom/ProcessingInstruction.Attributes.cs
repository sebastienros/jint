using System.Text;

namespace Jint.HtmlParser;

internal readonly record struct ProcessingInstructionAttributeWork(Action<int>? Checkpoint, CancellationToken CancellationToken)
{
    internal void Charge(int units)
    {
        CancellationToken.ThrowIfCancellationRequested();
        Checkpoint?.Invoke(units);
        CancellationToken.ThrowIfCancellationRequested();
    }
}

public sealed partial class ProcessingInstruction
{
    // DOM §4.13: https://dom.spec.whatwg.org/#interface-processinginstruction.
    // Lazy parsing is unobservable, and lets the HTML parser charge/resume the
    // same native algorithm when inspecting a host-created marker.
    private Dictionary<string, string>? _attributeMap;
    private List<string>? _attributeNames;
    private AttributeParser? _attributeParser;
    internal bool HasAttributeState => _attributeMap is not null || _attributeParser is not null;

    /// <summary>Returns whether this processing instruction has pseudo-attributes.</summary>
    public bool HasAttributes() => HasAttributes(default);
    internal bool HasAttributes(ProcessingInstructionAttributeWork work) { EnsureAttributes(work); return _attributeMap!.Count != 0; }

    /// <summary>Returns pseudo-attribute names in insertion order.</summary>
    public string[] GetAttributeNames() => GetAttributeNames(default);
    internal string[] GetAttributeNames(ProcessingInstructionAttributeWork work)
    {
        EnsureAttributes(work);
        var names = new string[_attributeNames!.Count];
        for (var i = 0; i < names.Length; i++) { work.Charge(1); names[i] = _attributeNames[i]; }
        return names;
    }

    /// <summary>Returns the case-sensitive pseudo-attribute value, or null when absent.</summary>
    public string? GetAttribute(string name) => GetAttribute(name, default);
    internal string? GetAttribute(string name, ProcessingInstructionAttributeWork work)
    {
        ArgumentNullException.ThrowIfNull(name);
        EnsureAttributes(work);
        ChargeString(name, work);
        return _attributeMap!.GetValueOrDefault(name);
    }

    /// <summary>Returns whether a case-sensitive pseudo-attribute exists.</summary>
    public bool HasAttribute(string name) => HasAttribute(name, default);
    internal bool HasAttribute(string name, ProcessingInstructionAttributeWork work)
    {
        ArgumentNullException.ThrowIfNull(name);
        EnsureAttributes(work);
        ChargeString(name, work);
        return _attributeMap!.ContainsKey(name);
    }

    /// <summary>Sets a pseudo-attribute and replaces data with the serialized attribute map.</summary>
    public void SetAttribute(string name, string value) => SetAttribute(name, value, default);
    internal void SetAttribute(string name, string value, ProcessingInstructionAttributeWork work)
    {
        ValidateAttributeName(name, work);
        ArgumentNullException.ThrowIfNull(value);
        EnsureAttributes(work);
        var map = CopyMap(work, out var names);
        if (!map.ContainsKey(name)) names.Add(name);
        map[name] = value;
        UpdateDataFromAttributes(map, names, work);
    }

    /// <summary>Removes a pseudo-attribute and replaces data with the serialized attribute map.</summary>
    public void RemoveAttribute(string name) => RemoveAttribute(name, default);
    internal void RemoveAttribute(string name, ProcessingInstructionAttributeWork work)
    {
        ArgumentNullException.ThrowIfNull(name);
        EnsureAttributes(work);
        ChargeString(name, work);
        var map = CopyMap(work, out var names);
        if (map.Remove(name)) names.Remove(name);
        UpdateDataFromAttributes(map, names, work);
    }

    /// <summary>Toggles a pseudo-attribute, optionally forcing its presence, and returns its resulting presence.</summary>
    public bool ToggleAttribute(string name, bool? force = null) => ToggleAttribute(name, force, default);
    internal bool ToggleAttribute(string name, bool? force, ProcessingInstructionAttributeWork work)
    {
        ValidateAttributeName(name, work);
        EnsureAttributes(work);
        var present = _attributeMap!.ContainsKey(name);
        if (!present && force != false) { SetAttribute(name, string.Empty, work); return true; }
        if (present && force != true) { RemoveAttribute(name, work); return false; }
        return present;
    }

    private static void ValidateAttributeName(string name, ProcessingInstructionAttributeWork work)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0) throw DomException.InvalidCharacter();
        foreach (var c in name)
        {
            work.Charge(1);
            if (c is '\0' or '\t' or '\n' or '\r' or '\f' or ' ' or '/' or '=' or '>')
                throw DomException.InvalidCharacter();
        }
    }

    private static void ChargeString(string value, ProcessingInstructionAttributeWork work)
    {
        for (var i = 0; i < value.Length; i += Math.Min(256, value.Length - i)) work.Charge(Math.Min(256, value.Length - i));
        work.Charge(0);
    }

    private Dictionary<string, string> CopyMap(ProcessingInstructionAttributeWork work, out List<string> names)
    {
        foreach (var name in _attributeNames!) ChargeString(name, work);
        var map = new Dictionary<string, string>(_attributeMap!, StringComparer.Ordinal);
        names = new List<string>(_attributeNames);
        work.Charge(0);
        return map;
    }

    private void UpdateDataFromAttributes(Dictionary<string, string> map, List<string> names, ProcessingInstructionAttributeWork work)
    {
        var data = new StringBuilder();
        foreach (var name in names)
        {
            ChargeString(name, work);
            if (data.Length != 0) data.Append(' ');
            data.Append(name).Append("=\"");
            foreach (var c in map[name])
            {
                work.Charge(1);
                switch (c)
                {
                    case '&': data.Append("&amp;"); break;
                    case '<': data.Append("&lt;"); break;
                    case '>': data.Append("&gt;"); break;
                    case '"': data.Append("&quot;"); break;
                    default: data.Append(c); break;
                }
            }
            data.Append('"');
        }
        var replacement = data.ToString();
        work.Charge(0);
        ReplaceDataCore(replacement, 0, (uint) _data.Length, (uint) replacement.Length, preserveAttributes: true);
        _attributeMap = map;
        _attributeNames = names;
    }

    private void InvalidateAttributes()
    {
        _attributeMap = null;
        _attributeNames = null;
        _attributeParser = null;
    }

    private void EnsureAttributes(ProcessingInstructionAttributeWork work)
    {
        bool done;
        do
        {
            done = PrepareAttributes(4096, work.CancellationToken, out var used);
            work.Charge(used);
        } while (!done);
    }

    // A caller must charge workUsed even when this returns false. Invalid data
    // publishes an empty map, never the successfully parsed prefix.
    internal bool PrepareAttributes(int workQuota, CancellationToken cancellationToken, out int workUsed)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workQuota);
        cancellationToken.ThrowIfCancellationRequested();
        workUsed = 0;
        if (_attributeMap is not null) return true;
        _attributeParser ??= new AttributeParser(_data);
        if (!_attributeParser.Advance(workQuota, cancellationToken, out workUsed)) return false;
        _attributeMap = _attributeParser.Map;
        _attributeNames = _attributeParser.Names;
        _attributeParser = null;
        return true;
    }

    // https://www.w3.org/TR/xml-stylesheet/#NT-PseudoAtts. Each transition
    // consumes at most one scalar; no restarted scans, regexes or XML wrapper.
    private sealed class AttributeParser(string source)
    {
        private enum State { Between, Name, Equals, Quote, Value, Reference, Separator, Done }
        private State _state;
        private int _offset;
        private int _nameStart;
        private string _name = string.Empty;
        private char _quote;
        private readonly StringBuilder _value = new();
        private readonly StringBuilder _entity = new();
        private int _referenceKind;
        private int _scalar;
        private bool _hasDigit;
        internal Dictionary<string, string> Map { get; private set; } = new(StringComparer.Ordinal);
        internal List<string> Names { get; private set; } = [];

        internal bool Advance(int quota, CancellationToken cancellationToken, out int used)
        {
            used = 0;
            while (_state != State.Done && used < quota)
            {
                if ((used & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (_offset == source.Length)
                {
                    if (_state is not (State.Between or State.Separator)) Fail();
                    _state = State.Done;
                    break;
                }
                var before = _offset;
                Step();
                used += Math.Max(1, _offset - before);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return _state == State.Done;
        }

        private void Step()
        {
            var c = source[_offset];
            switch (_state)
            {
                case State.Between:
                    if (IsSpace(c)) { _offset++; break; }
                    _nameStart = _offset;
                    if (!TryReadScalar(source, ref _offset, out var first) || !IsNameStart(first)) { Fail(); break; }
                    _state = State.Name;
                    break;
                case State.Name:
                    var end = _offset;
                    if (TryReadScalar(source, ref end, out var scalar) && (IsNameStart(scalar) ||
                        scalar is '-' or '.' or >= '0' and <= '9' or 0xB7 or >= 0x300 and <= 0x36F or >= 0x203F and <= 0x2040))
                    { _offset = end; break; }
                    _name = source[_nameStart.._offset];
                    _state = State.Equals;
                    break;
                case State.Equals:
                    if (IsSpace(c)) { _offset++; break; }
                    if (c != '=') { Fail(); break; }
                    _offset++; _state = State.Quote;
                    break;
                case State.Quote:
                    if (IsSpace(c)) { _offset++; break; }
                    if (c is not ('\'' or '"')) { Fail(); break; }
                    _quote = c; _offset++; _value.Clear(); _state = State.Value;
                    break;
                case State.Value:
                    _offset++;
                    if (c == _quote)
                    {
                        if (!Map.TryAdd(_name, _value.ToString())) { Fail(); break; }
                        Names.Add(_name); _state = State.Separator;
                    }
                    else if (c == '<') Fail();
                    else if (c == '&')
                    { _entity.Clear(); _referenceKind = 0; _scalar = 0; _hasDigit = false; _state = State.Reference; }
                    else _value.Append(c);
                    break;
                case State.Reference:
                    _offset++;
                    if (_referenceKind == 0)
                    {
                        if (c == '#') _referenceKind = 2;
                        else { _referenceKind = 1; _entity.Append(c); }
                    }
                    else if (c == ';') FinishReference();
                    else if (_referenceKind == 1)
                    {
                        if (_entity.Length == 4) { Fail(); break; }
                        _entity.Append(c);
                    }
                    else
                    {
                        if (_referenceKind == 2 && c == 'x') { _referenceKind = 4; break; }
                        if (_referenceKind == 2) _referenceKind = 3;
                        var digit = c is >= '0' and <= '9' ? c - '0' :
                            _referenceKind == 4 && c is >= 'a' and <= 'f' ? c - 'a' + 10 :
                            _referenceKind == 4 && c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
                        if (digit < 0) { Fail(); break; }
                        _hasDigit = true;
                        _scalar = _scalar * (_referenceKind == 4 ? 16 : 10) + digit;
                        if (_scalar > 0x10FFFF) Fail();
                    }
                    break;
                case State.Separator:
                    if (!IsSpace(c)) { Fail(); break; }
                    _offset++; _state = State.Between;
                    break;
            }
        }

        private void FinishReference()
        {
            if (_referenceKind == 1)
            {
                var value = _entity.ToString() switch { "amp" => '&', "lt" => '<', "gt" => '>', "quot" => '"', "apos" => '\'', _ => '\0' };
                if (value == '\0') { Fail(); return; }
                _value.Append(value);
            }
            else
            {
                if (!_hasDigit || !(_scalar is 9 or 10 or 13 or >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF))
                { Fail(); return; }
                _value.Append(char.ConvertFromUtf32(_scalar));
            }
            _state = State.Value;
        }

        private void Fail() { Map = new(StringComparer.Ordinal); Names = []; _state = State.Done; }
        private static bool IsSpace(char c) => c is ' ' or '\t' or '\n' or '\r';
    }
}
