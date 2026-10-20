#nullable enable
using Jint.Runtime.RegExp.GregsEx.Contracts;
using Jint.Runtime.RegExp.GregsEx.Contracts.EcmaScript;
using Jint.Runtime.RegExp.GregsEx.IR;

namespace Jint.Runtime.RegExp.GregsEx.Flavors.EcmaScript.Parsing;

internal sealed class EcmaCompiler : IEcmaCompiler
{
    public bool TryCompile(ReadOnlySpan<char> pattern, EcmaScriptProfile profile, IEcmaUnicodeData unicode,
        ref CompileBudget budget, out EcmaProgram? program, out EcmaScriptDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(unicode);
        program = null;
        diagnostic = default;
        var source = new SourceRange(0, pattern.Length, RegexOffsetUnit.Utf16CodeUnits);
        if (unicode.DataId != profile.DataId)
        {
            throw new ArgumentException("Unicode data identity must match the profile.", nameof(unicode));
        }

        if (pattern.Length > budget.Limits.MaxPatternUnits)
        {
            diagnostic = new(new(RegexDiagnosticCode.PatternLimitExceeded, 0, pattern.Length, source.Unit),
                EcmaScriptDiagnosticLocation.Pattern, "ES-020");
            return false;
        }
        var initial = budget.ReservedBytes;
        // Includes fixed parser/storage objects and one bounded failure exception.
        if (!budget.TryReserveBytes(2048, source, out var failure))
        { diagnostic = new(failure, EcmaScriptDiagnosticLocation.Pattern, "ES-020"); return false; }
        var storage = new ParserStorage(budget) { Source = source };
        try
        {
            var parser = new EcmaParser(pattern, profile, unicode, storage);
            program = parser.Parse();
            return true;
        }
        catch (ParseFailure error)
        {
            diagnostic = new(error.Diagnostic, EcmaScriptDiagnosticLocation.Pattern, error.Feature);
            return false;
        }
        finally
        {
            storage.ReleaseAll();
            budget = storage.Budget;
            budget.ReleaseBytes(2048);
            if (program is null)
            {
                budget.ReleaseBytes(budget.ReservedBytes - initial);
            }
        }
    }
}

internal sealed class GroupFrame
{
    internal int Start, BodyStart, Capture, Name = -1;
    internal EcmaNodeKind Kind;
    internal EcmaScriptFlags Flags, OuterFlags;
    internal readonly Buffer<int> Terms, Branches;
    internal GroupFrame(ParserStorage storage)
    {
        storage.Reserve(96);
        Terms = storage.Buffer<int>();
        Branches = storage.Buffer<int>();
    }
    internal void Dispose(ParserStorage storage) { Terms.Dispose(); Branches.Dispose(); storage.Release(96); }
}
internal sealed class NameEntry(string name, Buffer<int> aliases)
{
    internal readonly string Name = name;
    internal readonly Buffer<int> Aliases = aliases;
}

internal ref partial struct EcmaParser
{
    private readonly ReadOnlySpan<char> _pattern;
    private readonly EcmaScriptProfile _profile;
    private readonly IEcmaUnicodeData _unicode;
    private readonly ParserStorage _s;
    private readonly Buffer<EcmaNode> _nodes;
    private readonly Buffer<int> _depths;
    private readonly Buffer<GroupFrame> _groups;
    private readonly Buffer<NameEntry> _names;
    private readonly Buffer<EcmaSetSlice> _sets;
    private readonly Buffer<EcmaInterval> _intervals;
    private readonly Buffer<EcmaStringSlice> _strings;
    private readonly Buffer<int> _points;
    private int _position, _captures, _totalCaptures;
    private bool _hasNames;
    private EcmaScriptFlags _flags;
    private readonly bool Unicode => _profile.IsUnicodeAware;
    private readonly bool Sets => (_profile.Flags & EcmaScriptFlags.UnicodeSets) != 0;
    private readonly bool IgnoreCase => (_flags & EcmaScriptFlags.IgnoreCase) != 0;
    private readonly int Maximum => Unicode ? 0x10FFFF : 0xFFFF;
    private readonly ReadOnlySpan<EcmaCaseMapping> Mappings => Unicode ? _unicode.CaseTables.UnicodeFold : _unicode.CaseTables.LegacyUppercase;
    internal EcmaParser(ReadOnlySpan<char> pattern, EcmaScriptProfile profile, IEcmaUnicodeData unicode, ParserStorage storage)
    {
        _pattern = pattern;
        _profile = profile;
        _unicode = unicode;
        _s = storage;
        _nodes = _s.Buffer<EcmaNode>();
        _depths = _s.Buffer<int>();
        _groups = _s.Buffer<GroupFrame>();
        _names = _s.Buffer<NameEntry>();
        _sets = _s.Buffer<EcmaSetSlice>();
        _intervals = _s.Buffer<EcmaInterval>();
        _strings = _s.Buffer<EcmaStringSlice>();
        _points = _s.Buffer<int>();
        _position = _captures = _totalCaptures = 0;
        _hasNames = false;
        _flags = profile.Flags & (EcmaScriptFlags.IgnoreCase | EcmaScriptFlags.Multiline | EcmaScriptFlags.DotAll);
    }
    private readonly void At(int start, string feature, int? end = null)
    {
        _s.Source = new(start, Math.Max(0, Math.Min(_pattern.Length, end ?? Math.Max(start + 1, _position)) - start), RegexOffsetUnit.Utf16CodeUnits);
        _s.Feature = feature;
        _s.Work();
    }
    private readonly char Peek(int ahead = 0) { _s.Work(); return _position + ahead < _pattern.Length ? _pattern[_position + ahead] : '\0'; }
    private bool Eat(char ch) { if (_position < _pattern.Length && Peek() == ch) { _position++; return true; } return false; }
    internal EcmaProgram Parse()
    {
        CountCaptures();
        var frame = new GroupFrame(_s) { Flags = _flags, OuterFlags = _flags, Kind = EcmaNodeKind.Empty };
        _groups.Add(frame);
        while (true)
        {
            At(_position, "ES-002");
            _flags = frame.Flags;
            if (_position == _pattern.Length || Peek() is ')' or '|')
            {
                frame.Branches.Add(Balance(frame.Terms, EcmaNodeKind.Sequence, frame.BodyStart));
                frame.Terms.Count = 0;
                if (Eat('|'))
                { frame.BodyStart = _position; continue; }
                var root = Balance(frame.Branches, EcmaNodeKind.Alternative, frame.Start);
                if (_groups.Count == 1)
                {
                    if (_position != _pattern.Length)
                    {
                        _s.Fail();
                    }

                    ValidateNames();
                    var names = _s.Buffer<EcmaNameSlice>();
                    var aliases = _s.Buffer<int>();
                    foreach (var name in _names.Items)
                    {
                        _s.Work();
                        names.Add(new(name.Name, aliases.Count, name.Aliases.Count));
                        foreach (var id in name.Aliases.Items)
                        {
                            aliases.Add(id);
                        }
                    }
                    if (!EcmaProgram.TryCreate(_profile, root, _captures + 1, _pattern.Length,
                        _nodes.Items, _sets.Items, _intervals.Items, _strings.Items, _points.Items,
                        names.Items, aliases.Items, ref _s.Budget, out var program, out var diagnostic))
                    {
                        throw new ParseFailure(diagnostic, _s.Feature);
                    }

                    return program!;
                }
                if (!Eat(')'))
                { At(frame.Start, "ES-005"); _s.Fail(); }
                _flags = frame.OuterFlags;
                var span = new SourceRange(frame.Start, _position - frame.Start, RegexOffsetUnit.Utf16CodeUnits);
                if (frame.Kind != EcmaNodeKind.Empty)
                {
                    root = Node(frame.Kind, span, root, value: frame.Capture, name: frame.Name);
                }
                else
                {
                    _nodes[root] = _nodes[root] with { Source = span };
                }

                var quantifiable = frame.Kind is EcmaNodeKind.Empty or EcmaNodeKind.Capture ||
                    (!Unicode && frame.Kind is EcmaNodeKind.Lookahead or EcmaNodeKind.NegativeLookahead);
                root = Quantify(root, quantifiable);
                _groups.Pop();
                frame.Dispose(_s);
                frame = _groups[_groups.Count - 1];
                frame.Terms.Add(root);
                continue;
            }
            var start = _position;
            if (Eat('('))
            { frame = OpenGroup(start, frame.Flags); _groups.Add(frame); continue; }
            int atom;
            var canRepeat = true;
            var ch = Peek();
            if (ch is '*' or '+' or '?')
            { At(start, "ES-004"); _s.Fail(); }
            if (ch == '{' && ReadQuantifier(out _, out _, out _))
            { At(start, "ES-004"); _s.Fail(); }
            _position = start;
            if (ch is '^' or '$')
            { _position++; atom = Node(ch == '^' ? EcmaNodeKind.Start : EcmaNodeKind.End, Range(start)); canRepeat = false; }
            else if (ch == '.')
            {
                _position++;
                var set = new ResolvedSet(_s);
                set.AddRange(0, Maximum);
                if ((_flags & EcmaScriptFlags.DotAll) == 0)
                {
                    var lines = new ResolvedSet(_s);
                    lines.AddRange(10, 10);
                    lines.AddRange(13, 13);
                    lines.AddRange(0x2028, 0x2029);
                    var difference = set.Algebra(lines, true);
                    set.Dispose();
                    lines.Dispose();
                    set = difference;
                }
                set = Fold(set);
                atom = SetNode(set, start);
            }
            else if (ch == '[')
            { var set = Sets ? ReadVClass() : ReadClass(); atom = SetNode(set, start); }
            else if (ch == '\\')
            {
                _position++;
                if (Peek() is 'b' or 'B')
                { ch = Peek(); _position++; atom = Node(ch == 'b' ? EcmaNodeKind.WordBoundary : EcmaNodeKind.NotWordBoundary, Range(start)); canRepeat = false; }
                else if (Peek() == 'k' && (Unicode || _hasNames))
                {
                    _position++;
                    At(start, "ES-006");
                    var name = ReadName();
                    var index = Intern(name);
                    atom = Node(EcmaNodeKind.Backreference, Range(start), name: index);
                }
                else if (Peek() is >= '1' and <= '9' && NumericReference(start, out var reference))
                {
                    atom = Node(EcmaNodeKind.Backreference, Range(start), value: reference);
                }
                else
                {
                    var value = ReadEscape(start, false, false);
                    atom = value.Set is { } set ? SetNode(Fold(set), start) : Literal(value.Point, start);
                }
            }
            else
            {
                if (Unicode && ch is '{' or '}' or ']')
                { At(start, "ES-008"); _s.Fail(); }
                var point = ReadRaw(Unicode);
                atom = Literal(point, start);
            }
            frame.Terms.Add(Quantify(atom, canRepeat));
        }
    }
    private readonly SourceRange Range(int start) => new(start, _position - start, RegexOffsetUnit.Utf16CodeUnits);
    private readonly int Literal(int point, int start)
    {
        if (IgnoreCase)
        {
            point = ResolvedSet.Canonicalize(point, Mappings, _s);
        }

        return Node(EcmaNodeKind.Literal, Range(start), value: point);
    }
    private readonly int Node(EcmaNodeKind kind, SourceRange source, int x = -1, int y = -1, int value = 0,
        int name = -1, int min = 0, int max = 0, bool lazy = false)
    {
        _s.Source = source;
        _s.Work();
        if (_nodes.Count >= _s.Budget.Limits.MaxInstructions)
        {
            _s.Fail(RegexDiagnosticCode.ProgramLimitExceeded);
        }

        var depth = 1 + Math.Max(x < 0 ? 0 : _depths[x], y < 0 ? 0 : _depths[y]);
        if (depth > _s.Budget.Limits.MaxNestingDepth)
        {
            _s.Fail(RegexDiagnosticCode.NestingLimitExceeded);
        }

        int first = kind == EcmaNodeKind.Capture ? value : 0, count = kind == EcmaNodeKind.Capture ? 1 : 0;
        if (x >= 0 && _nodes[x].CaptureCount > 0)
        { if (count == 0) { first = _nodes[x].FirstCapture; } count += _nodes[x].CaptureCount; }
        if (y >= 0 && _nodes[y].CaptureCount > 0)
        { if (count == 0) { first = _nodes[y].FirstCapture; } count += _nodes[y].CaptureCount; }
        _nodes.Add(new(kind, x, y, value, name, min, max, lazy, _flags, first, count, source));
        _depths.Add(depth);
        return _nodes.Count - 1;
    }
    private readonly int Balance(Buffer<int> items, EcmaNodeKind kind, int emptyStart)
    {
        if (items.Count == 0)
        {
            return Node(EcmaNodeKind.Empty, new(emptyStart, 0, RegexOffsetUnit.Utf16CodeUnits));
        }

        var count = items.Count;
        while (count > 1)
        {
            var output = 0;
            for (var i = 0; i < count; i += 2)
            {
                _s.Work();
                var x = items[i];
                if (i + 1 == count)
                {
                    items[output++] = x;
                }
                else
                {
                    var y = items[i + 1];
                    var a = _nodes[x].Source;
                    var b = _nodes[y].Source;
                    items[output++] = Node(kind, new(a.Offset, b.Offset + b.Length - a.Offset, a.Unit), x, y);
                }
            }
            count = output;
        }
        return items[0];
    }
    private GroupFrame OpenGroup(int start, EcmaScriptFlags flags)
    {
        At(start, "ES-005");
        // The outer pattern frame is not a syntactic group.
        if (_groups.Count > _s.Budget.Limits.MaxNestingDepth)
        {
            _s.Fail(RegexDiagnosticCode.NestingLimitExceeded);
        }

        var frame = new GroupFrame(_s) { Start = start, Flags = flags, OuterFlags = flags, Kind = EcmaNodeKind.Capture };
        if (Eat('?'))
        {
            if (Eat(':'))
            {
                frame.Kind = EcmaNodeKind.Empty;
            }
            else if (Eat('='))
            {
                frame.Kind = EcmaNodeKind.Lookahead;
            }
            else if (Eat('!'))
            {
                frame.Kind = EcmaNodeKind.NegativeLookahead;
            }
            else if (Eat('<'))
            {
                if (Eat('='))
                {
                    frame.Kind = EcmaNodeKind.Lookbehind;
                }
                else if (Eat('!'))
                {
                    frame.Kind = EcmaNodeKind.NegativeLookbehind;
                }
                else
                { _position--; frame.Name = Intern(ReadName()); }
            }
            else
            {
                frame.Kind = EcmaNodeKind.Empty;
                At(start, "ES-003");
                EcmaScriptFlags added = 0, removed = 0;
                var minus = false;
                while (_position < _pattern.Length && Peek() != ':')
                {
                    if (Eat('-'))
                    { if (minus) { _s.Fail(); } minus = true; continue; }
                    var letter = Peek();
                    var flag = letter switch { 'i' => EcmaScriptFlags.IgnoreCase, 'm' => EcmaScriptFlags.Multiline, 's' => EcmaScriptFlags.DotAll, _ => EcmaScriptFlags.None };
                    if (flag == 0 || ((added | removed) & flag) != 0)
                    {
                        _s.Fail();
                    }

                    _position++;
                    if (minus)
                    {
                        removed |= flag;
                    }
                    else
                    {
                        added |= flag;
                    }
                }
                if ((added | removed) == 0 || !Eat(':'))
                {
                    _s.Fail();
                }

                frame.Flags = (flags | added) & ~removed;
            }
        }
        if (frame.Kind == EcmaNodeKind.Capture)
        {
            frame.Capture = ++_captures;
            if (_captures >= _s.Budget.Limits.MaxCaptureGroups)
            {
                _s.Fail(RegexDiagnosticCode.CaptureLimitExceeded);
            }

            if (frame.Name >= 0)
            {
                _names[frame.Name].Aliases.Add(_captures);
            }
        }
        frame.BodyStart = _position;
        return frame;
    }
    private void CountCaptures()
    {
        // Lexical count enables forward DecimalEscape/legacy NamedCaptureGroups.
        // Class/braced-escape/string bodies are skipped, never interpreted as groups.
        var classes = 0;
        for (var i = 0; i < _pattern.Length; i++)
        {
            At(i, "ES-006", i + 1);
            var ch = _pattern[i];
            if (ch == '\\')
            {
                if (++i == _pattern.Length)
                {
                    break;
                }

                if (Sets && _pattern[i] == 'q' && i + 1 < _pattern.Length && _pattern[i + 1] == '{')
                {
                    i += 2;
                    while (i < _pattern.Length && _pattern[i] != '}')
                    { _s.Work(); if (_pattern[i] == '\\') { i++; } i++; }
                }
                continue;
            }
            if (ch == '[' && (classes == 0 || Sets))
            { classes++; continue; }
            if (ch == ']' && classes > 0)
            { classes--; continue; }
            if (classes != 0 || ch != '(')
            {
                continue;
            }

            var question = i + 1 < _pattern.Length && _pattern[i + 1] == '?';
            var named = question && i + 3 < _pattern.Length && _pattern[i + 2] == '<' && _pattern[i + 3] is not ('=' or '!');
            if (!question || named)
            { _totalCaptures++; _hasNames |= named; }
        }
    }
    private string ReadName()
    {
        At(_position, "ES-005");
        if (!Eat('<'))
        {
            _s.Fail();
        }

        var units = _s.Buffer<char>();
        var first = true;
        while (_position < _pattern.Length && Peek() != '>')
        {
            int point;
            if (Eat('\\'))
            { if (!Eat('u')) { _s.Fail(); } point = ReadUnicode(true); }
            else
            {
                point = ReadRaw(true);
            }

            var valid = first ? point is '$' or '_' || _unicode.IsIdentifierStart(point) :
                point is '$' or '_' or 0x200C or 0x200D || _unicode.IsIdentifierContinue(point);
            _s.Work();
            if (!valid)
            {
                _s.Fail();
            }

            first = false;
            if (point <= 0xFFFF)
            {
                units.Add((char)point);
            }
            else
            { point -= 0x10000; units.Add((char)(0xD800 + (point >> 10))); units.Add((char)(0xDC00 + (point & 1023))); }
        }
        if (first || !Eat('>'))
        {
            _s.Fail();
        }

        var name = _s.Own(units.Items);
        units.Dispose();
        return name;
    }
    private readonly int Intern(string name)
    {
        for (var i = 0; i < _names.Count; i++)
        {
            _s.Work(1L + Math.Min(name.Length, _names[i].Name.Length));
            if (_names[i].Name == name)
            {
                return i;
            }
        }
        _s.Reserve(48);
        _names.Add(new(name, _s.Buffer<int>()));
        return _names.Count - 1;
    }
    private readonly void ValidateNames()
    {
        At(0, "ES-005", _pattern.Length);
        foreach (var name in _names.Items)
        {
            _s.Work();
            if (name.Aliases.Count == 0)
            { _s.Feature = "ES-006"; _s.Fail(); }
            for (var i = 0; i < name.Aliases.Count; i++)
            {
                for (var j = i + 1; j < name.Aliases.Count; j++)
                {
                    var disjoint = false;
                    int a = name.Aliases[i], b = name.Aliases[j];
                    foreach (var node in _nodes.Items)
                    {
                        _s.Work();
                        if (node.Kind != EcmaNodeKind.Alternative)
                        {
                            continue;
                        }

                        var x = _nodes[node.X];
                        var y = _nodes[node.Y];
                        disjoint |= ContainsCapture(x, a) && ContainsCapture(y, b) || ContainsCapture(x, b) && ContainsCapture(y, a);
                    }
                    if (!disjoint)
                    {
                        _s.Fail();
                    }
                }
            }
        }
    }
    private static bool ContainsCapture(EcmaNode node, int id) => id >= node.FirstCapture && id < node.FirstCapture + node.CaptureCount;
    private int ReadRaw(bool unicode)
    {
        _s.Work();
        int point = _pattern[_position++];
        if (unicode && point is >= 0xD800 and <= 0xDBFF && _position < _pattern.Length && _pattern[_position] is >= '\uDC00' and <= '\uDFFF')
        { _s.Work(); point = 0x10000 + ((point - 0xD800) << 10) + (_pattern[_position++] - 0xDC00); }
        return point;
    }
    private bool NumericReference(int start, out int value)
    {
        var saved = _position;
        long number = 0;
        while (_position < _pattern.Length && Peek() is >= '0' and <= '9')
        { number = Math.Min((long)int.MaxValue + 1, number * 10 + _pattern[_position++] - '0'); }
        value = number <= _totalCaptures ? (int)number : 0;
        if (value > 0)
        {
            return true;
        }

        if (Unicode)
        { At(start, "ES-006"); _s.Fail(); }
        _position = saved;
        return false;
    }
    private int Quantify(int atom, bool allowed)
    {
        var start = _position;
        At(start, "ES-004");
        if (!ReadQuantifier(out var min, out var max, out var lazy))
        {
            return atom;
        }

        if (!allowed)
        {
            _s.Fail();
        }

        var source = _nodes[atom].Source;
        return Node(EcmaNodeKind.Repeat, new(source.Offset, _position - source.Offset, source.Unit), atom, min: min, max: max, lazy: lazy);
    }
    private bool ReadQuantifier(out int min, out int max, out bool lazy)
    {
        min = max = 0;
        lazy = false;
        var saved = _position;
        if (Eat('*'))
        {
            max = -1;
        }
        else if (Eat('+'))
        { min = 1; max = -1; }
        else if (Eat('?'))
        {
            max = 1;
        }
        else if (Eat('{'))
        {
            var a = _position;
            var lower = Decimal();
            var aEnd = _position;
            if (a == aEnd)
            { _position = saved; return false; }
            var upper = lower;
            int b = a, bEnd = aEnd;
            if (Eat(','))
            {
                b = _position;
                upper = Decimal();
                bEnd = _position;
                if (b == bEnd)
                {
                    upper = -1;
                }
            }
            if (!Eat('}'))
            { _position = saved; return false; }
            if (upper != -1 && GreaterDecimal(a, aEnd, b, bEnd))
            {
                _s.Fail();
            }

            // ECMAScript admits any DecimalDigits here (test262 built-ins/RegExp/quantifier-integer-limit.js uses
            // 2^53-1). No UTF-16 subject is longer than int.MaxValue,
            // so a larger lower bound is unsatisfiable by any non-empty atom exactly as int.MaxValue is, and a
            // larger upper bound is indistinguishable from an unbounded one.
            min = (int)Math.Min(lower, int.MaxValue);
            max = upper > int.MaxValue ? -1 : (int)upper;
        }
        else
        {
            return false;
        }

        lazy = Eat('?');
        return true;
    }
    private long Decimal()
    {
        long value = 0;
        while (_position < _pattern.Length && Peek() is >= '0' and <= '9')
        {
            value = Math.Min((long)int.MaxValue + 1, value * 10 + _pattern[_position++] - '0');
        }

        return value;
    }
    private readonly bool GreaterDecimal(int a, int aEnd, int b, int bEnd)
    {
        while (a < aEnd && _pattern[a] == '0')
        { _s.Work(); a++; }
        while (b < bEnd && _pattern[b] == '0')
        { _s.Work(); b++; }
        if (aEnd - a != bEnd - b)
        {
            return aEnd - a > bEnd - b;
        }

        while (a < aEnd)
        {
            _s.Work();
            var order = _pattern[a++].CompareTo(_pattern[b++]);
            if (order != 0)
            {
                return order > 0;
            }
        }
        return false;
    }
}
