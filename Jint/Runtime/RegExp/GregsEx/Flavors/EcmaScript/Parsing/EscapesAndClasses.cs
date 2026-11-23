#nullable enable
using Jint.Runtime.RegExp.GregsEx.Contracts.EcmaScript;

namespace Jint.Runtime.RegExp.GregsEx.Flavors.EcmaScript.Parsing;

internal readonly record struct ClassValue(int Point, ResolvedSet? Set = null);
internal sealed class SetFrame
{
    internal int Start, Count;
    internal bool Negated, ExpectOperand, LastRange;
    internal char Mode;
    internal ResolvedSet? Value;
}

internal ref partial struct EcmaParser
{
    private ClassValue ReadEscape(int start, bool inClass, bool stringCharacter)
    {
        At(start, "ES-008");
        if (_position == _pattern.Length)
        {
            _s.Fail();
        }

        var letterPosition = _position;
        var ch = Peek();
        _position++;
        if (!stringCharacter && ch is 'd' or 'D' or 'w' or 'W' or 's' or 'S')
        {
            return new(-1, Predefined(ch));
        }

        if (!stringCharacter && Unicode && ch is 'p' or 'P')
        {
            return new(-1, Property(ch == 'P', start));
        }

        if (ch == 'b' && inClass)
        {
            return new(8);
        }

        if (ch is 'n' or 'r' or 't' or 'f' or 'v')
        {
            return new(ch switch { 'n' => 10, 'r' => 13, 't' => 9, 'f' => 12, _ => 11 });
        }

        if (ch == 'c')
        {
            var next = Peek();
            if (_position < _pattern.Length && (next is >= 'a' and <= 'z' or >= 'A' and <= 'Z' ||
                !Unicode && inClass && next is >= '0' and <= '9' or '_'))
            { _position++; return new(next % 32); }
            if (Unicode)
            {
                _s.Fail();
            }
            // Annex B ClassAtomNoDash: a backslash followed by malformed c is
            // the backslash itself. c is then an ordinary character in/outside a class.
            _position = letterPosition;
            return new('\\');
        }
        if (ch == 'x')
        {
            if (TryHex4(2, out var point))
            {
                return new(point);
            }

            if (Unicode)
            {
                _s.Fail();
            }

            return new('x');
        }
        if (ch == 'u')
        {
            var saved = _position;
            if (Unicode)
            {
                return new(ReadUnicode(true));
            }

            if (TryHex4(4, out var point))
            {
                return new(point);
            }

            _position = saved;
            return new('u');
        }
        if (ch is >= '0' and <= '9')
        {
            if (Unicode)
            {
                if (ch != '0' || Peek() is >= '0' and <= '9')
                {
                    _s.Fail();
                }

                return new(0);
            }
            if (ch is '8' or '9')
            {
                return new(ch);
            }

            int point = ch - '0', digits = ch <= '3' ? 3 : 2;
            for (var i = 1; i < digits && _position < _pattern.Length && Peek() is >= '0' and <= '7'; i++)
            {
                point = point * 8 + _pattern[_position++] - '0';
            }

            return new(point);
        }
        var syntax = "^$\\.*+?()[]{}|/".AsSpan().Contains(ch);
        var extra = inClass && ch == '-' || Sets && inClass && "&-!#%,:;<=>@`~".AsSpan().Contains(ch);
        if (Unicode && !syntax && !extra || !Unicode && _hasNames && ch == 'k')
        {
            _s.Fail();
        }
        // IdentityEscape is a SourceCharacter (a code point in Unicode mode).
        _position = letterPosition;
        return new(ReadRaw(Unicode));
    }
    private static int Hex(char ch) => ch is >= '0' and <= '9' ? ch - '0' : ch is >= 'a' and <= 'f' ? ch - 'a' + 10 : ch is >= 'A' and <= 'F' ? ch - 'A' + 10 : -1;
    private bool TryHex4(int digits, out int point)
    {
        point = 0;
        if (digits > _pattern.Length - _position)
        {
            return false;
        }

        for (var i = 0; i < digits; i++)
        { _s.Work(); var hex = Hex(_pattern[_position + i]); if (hex < 0) { return false; } point = point * 16 + hex; }
        _position += digits;
        return true;
    }
    private int ReadUnicode(bool combine)
    {
        if (Eat('{'))
        {
            int start = _position, point = 0;
            while (_position < _pattern.Length && Hex(Peek()) is int hex && hex >= 0)
            { point = Math.Min(0x110000, point * 16 + hex); _position++; }
            if (_position == start || !Eat('}') || point > 0x10FFFF)
            {
                _s.Fail();
            }

            return point;
        }
        if (!TryHex4(4, out var value))
        {
            _s.Fail();
        }

        if (combine && value is >= 0xD800 and <= 0xDBFF && _position + 6 <= _pattern.Length && Peek() == '\\' && Peek(1) == 'u')
        {
            var saved = _position;
            _position += 2;
            if (TryHex4(4, out var low) && low is >= 0xDC00 and <= 0xDFFF)
            {
                return 0x10000 + ((value - 0xD800) << 10) + low - 0xDC00;
            }

            _position = saved;
        }
        return value;
    }
    private readonly ResolvedSet Fold(ResolvedSet set)
    {
        if (!IgnoreCase)
        {
            return set;
        }

        var folded = set.Fold(Mappings);
        set.Dispose();
        return folded;
    }
    private readonly ResolvedSet InvertCanonical(ResolvedSet set)
    {
        // Invert after CharacterSetMatcher's canonicalization. Complements have
        // no noncanonical members, including legacy default-uppercase sources.
        var domain = new ResolvedSet(_s);
        domain.AddRange(0, Maximum);
        domain = Fold(domain);
        var result = domain.Algebra(set, true);
        domain.Dispose();
        set.Dispose();
        return result;
    }
    private readonly ResolvedSet Predefined(char letter)
    {
        _s.Feature = "ES-009";
        var set = new ResolvedSet(_s);
        if (letter is 'd' or 'D')
        {
            set.AddRange('0', '9');
        }
        else if (letter is 'w' or 'W')
        {
            set.AddRange('0', '9');
            set.AddRange('A', 'Z');
            set.AddRange('_', '_');
            set.AddRange('a', 'z');
            if (Unicode && IgnoreCase)
            {
                foreach (var mapping in Mappings)
                {
                    _s.Work();
                    if (mapping.Target is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
                    {
                        set.AddRange(mapping.Source, mapping.Source);
                    }
                }
            }
        }
        else
        {
            // ECMAScript WhiteSpace + LineTerminator, frozen explicit grammar
            // values. U+180E and U+0085 are deliberately absent.
            set.AddRange(9, 13);
            set.AddRange(0x20, 0x20);
            set.AddRange(0xA0, 0xA0);
            set.AddRange(0x1680, 0x1680);
            set.AddRange(0x2000, 0x200A);
            set.AddRange(0x2028, 0x2029);
            set.AddRange(0x202F, 0x202F);
            set.AddRange(0x205F, 0x205F);
            set.AddRange(0x3000, 0x3000);
            set.AddRange(0xFEFF, 0xFEFF);
        }
        if (Sets)
        {
            set = Fold(set);
        }

        if (letter is 'D' or 'W' or 'S')
        {
            if (Sets)
            {
                set = InvertCanonical(set);
            }
            else
            { var inverted = set.Complement(Maximum); set.Dispose(); set = inverted; }
        }
        return set;
    }
    private ResolvedSet Property(bool negate, int start)
    {
        At(start, "ES-010");
        if (!Eat('{'))
        {
            _s.Fail();
        }

        var nameStart = _position;
        while (_position < _pattern.Length && IsPropertyChar(Peek()))
        {
            _position++;
        }

        int nameEnd = _position, valueStart = _position, valueEnd = _position;
        var equals = Eat('=');
        if (equals)
        {
            valueStart = _position;
            while (_position < _pattern.Length && (IsPropertyChar(Peek()) || Peek() is >= '0' and <= '9'))
            {
                _position++;
            }

            valueEnd = _position;
        }
        else
        {
            // Lone values may contain digits, e.g. assigned binary aliases.
            while (_position < _pattern.Length && (IsPropertyChar(Peek()) || Peek() is >= '0' and <= '9'))
            {
                _position++;
            }

            nameEnd = _position;
        }
        if (nameStart == nameEnd || equals && valueStart == valueEnd || !Eat('}'))
        {
            _s.Fail();
        }

        var name = _pattern[nameStart..nameEnd];
        var value = equals ? _pattern[valueStart..valueEnd] : default;
        _s.Work(1L + name.Length + value.Length);
        if (!_unicode.TryResolveProperty(name, value, Sets, out var property))
        {
            _s.Fail();
        }

        if (property.Kind == EcmaPropertyKind.String && (!Sets || negate))
        {
            _s.Fail();
        }

        var set = new ResolvedSet(_s) { MayStrings = property.Kind == EcmaPropertyKind.String };
        foreach (var range in _unicode.GetIntervals(property))
        { _s.Work(); set.AddRange(range.First, range.Last); }
        var points = _unicode.GetStringCodePoints(property);
        foreach (var str in _unicode.GetStrings(property))
        { _s.Work(); set.AddString(points.Slice(str.Offset, str.Length)); }
        if (Sets)
        {
            set = Fold(set);
        }

        if (negate)
        {
            if (Sets)
            {
                set = InvertCanonical(set);
            }
            else
            { var inverted = set.Complement(Maximum); set.Dispose(); set = inverted; }
        }
        return set;
    }
    private static bool IsPropertyChar(char ch) => ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_';
    private ClassValue ClassAtom()
    {
        var start = _position;
        if (_position == _pattern.Length)
        { At(start, "ES-009"); _s.Fail(); }
        if (Eat('\\'))
        {
            return ReadEscape(start, true, false);
        }

        return new(ReadRaw(Unicode));
    }
    private ResolvedSet ReadClass()
    {
        var start = _position;
        At(start, "ES-009");
        if (_groups.Count > _s.Budget.Limits.MaxNestingDepth)
        {
            _s.Fail(RegexDiagnosticCode.NestingLimitExceeded);
        }

        _position++;
        var negate = Eat('^');
        var set = new ResolvedSet(_s);
        while (_position < _pattern.Length && Peek() != ']')
        {
            var left = ClassAtom();
            if (Peek() == '-' && _position + 1 < _pattern.Length && Peek(1) != ']')
            {
                _position++;
                var right = ClassAtom();
                if (left.Set is null && right.Set is null)
                {
                    if (left.Point > right.Point)
                    { At(start, "ES-009"); _s.Fail(); }
                    set.AddRange(left.Point, right.Point);
                }
                else
                {
                    if (Unicode)
                    { At(start, "ES-009"); _s.Fail(); }
                    AddAtom(set, left);
                    set.AddRange('-', '-');
                    AddAtom(set, right);
                }
            }
            else
            {
                AddAtom(set, left);
            }
        }
        if (!Eat(']'))
        { At(start, "ES-009"); _s.Fail(); }
        set = Fold(set);
        if (negate)
        {
            set = InvertCanonical(set);
        }

        return set;
    }
    private static void AddAtom(ResolvedSet set, ClassValue atom)
    {
        if (atom.Set is null)
        {
            set.AddRange(atom.Point, atom.Point);
        }
        else
        { set.Union(atom.Set); atom.Set.Dispose(); }
    }
    private readonly int SetNode(ResolvedSet set, int start)
    {
        int intervals = _intervals.Count, strings = _strings.Count;
        if ((long)intervals + set.Ranges.Count > _s.Budget.Limits.MaxClassRanges)
        {
            _s.Fail(RegexDiagnosticCode.ClassLimitExceeded);
        }

        foreach (var range in set.Ranges.Items)
        {
            _intervals.Add(range);
        }

        foreach (var str in set.Strings.Items)
        {
            _s.Work();
            _strings.Add(new(_points.Count, str.Length));
            foreach (var point in str)
            {
                _points.Add(point);
            }
        }
        _sets.Add(new(intervals, set.Ranges.Count, strings, set.Strings.Count));
        set.Dispose();
        return Node(EcmaNodeKind.Set, Range(start), value: _sets.Count - 1);
    }
    private SetFrame NewSetFrame()
    {
        var start = _position;
        _position++;
        _s.Reserve(64);
        return new() { Start = start, Negated = Eat('^') };
    }
    private ResolvedSet ReadVClass()
    {
        var frames = _s.Buffer<SetFrame>();
        frames.Add(NewSetFrame());
        while (true)
        {
            var frame = frames[frames.Count - 1];
            At(frame.Start, "ES-011");
            if (_groups.Count - 1 + frames.Count > _s.Budget.Limits.MaxNestingDepth)
            {
                _s.Fail(RegexDiagnosticCode.NestingLimitExceeded);
            }

            if (_position == _pattern.Length)
            {
                _s.Fail();
            }

            if (Eat(']'))
            {
                if (frame.ExpectOperand)
                {
                    _s.Fail();
                }

                var value = frame.Value ?? new ResolvedSet(_s);
                if (frame.Negated)
                { if (value.MayStrings) { _s.Fail(); } value = InvertCanonical(value); }
                frames.Pop();
                _s.Release(64);
                if (frames.Count == 0)
                { frames.Dispose(); return value; }
                Feed(frames[frames.Count - 1], value, false);
                continue;
            }
            bool intersection = Peek() == '&' && Peek(1) == '&', subtraction = Peek() == '-' && Peek(1) == '-';
            if (intersection || subtraction)
            {
                var mode = intersection ? '&' : '-';
                if (frame.Count == 0 || frame.ExpectOperand || frame.LastRange || frame.Mode != '\0' && frame.Mode != mode)
                {
                    _s.Fail();
                }

                frame.Mode = mode;
                frame.ExpectOperand = true;
                _position += 2;
                if (intersection && Peek() == '&')
                {
                    _s.Fail();
                }

                continue;
            }
            if (Peek() == '[')
            { frames.Add(NewSetFrame()); continue; }
            ClassValue atom;
            if (Peek() == '\\' && Peek(1) == 'q')
            {
                atom = new(-1, ReadQString());
            }
            else if (Peek() == '\\' && "dDsSwWpP".AsSpan().Contains(Peek(1)))
            { var start = _position++; atom = ReadEscape(start, true, false); }
            else
            {
                atom = new(ReadSetCharacter());
            }

            var range = atom.Set is null && Peek() == '-' && Peek(1) != '-';
            ResolvedSet operand;
            if (range)
            {
                _position++;
                var last = ReadSetCharacter();
                if (atom.Point > last)
                {
                    _s.Fail();
                }

                operand = new(_s);
                operand.AddRange(atom.Point, last);
                operand = Fold(operand);
                if (frame.Mode is '&' or '-')
                {
                    _s.Fail();
                }
            }
            else if (atom.Set is { } resolved)
            {
                operand = resolved;
            }
            else
            { operand = new(_s); operand.AddRange(atom.Point, atom.Point); operand = Fold(operand); }
            Feed(frame, operand, range);
        }
    }
    private readonly void Feed(SetFrame frame, ResolvedSet operand, bool range)
    {
        if (frame.Count == 0)
        {
            frame.Value = operand;
        }
        else if (frame.ExpectOperand)
        {
            var value = frame.Value!.Algebra(operand, frame.Mode == '-');
            frame.Value.Dispose();
            operand.Dispose();
            frame.Value = value;
        }
        else
        {
            if (frame.Mode is '&' or '-')
            {
                _s.Fail();
            }

            frame.Mode = 'u';
            frame.Value!.Union(operand);
            operand.Dispose();
        }
        frame.Count++;
        frame.ExpectOperand = false;
        frame.LastRange = range;
    }
    private int ReadSetCharacter()
    {
        var start = _position;
        At(start, "ES-011");
        if (_position == _pattern.Length)
        {
            _s.Fail();
        }

        if (Eat('\\'))
        {
            return ReadEscape(start, true, true).Point;
        }

        var ch = Peek();
        if ("()[]{}/-\\|".AsSpan().Contains(ch) || Peek(1) == ch && "&!#$%*+,.:;<=>?@^`~".AsSpan().Contains(ch))
        {
            _s.Fail();
        }

        return ReadRaw(true);
    }
    private ResolvedSet ReadQString()
    {
        var start = _position;
        At(start, "ES-012");
        _position += 2;
        if (!Eat('{'))
        {
            _s.Fail();
        }

        var set = new ResolvedSet(_s);
        var points = _s.Buffer<int>();
        while (true)
        {
            if (_position == _pattern.Length)
            {
                _s.Fail();
            }

            if (Peek() is '|' or '}')
            {
                set.MayStrings |= points.Count != 1;
                set.AddString(points.Items);
                points.Count = 0;
                if (Eat('}'))
                {
                    break;
                }

                _position++;
                continue;
            }
            points.Add(ReadSetCharacter());
        }
        points.Dispose();
        return Fold(set);
    }
}
