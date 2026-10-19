using System;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTokenizer
{
    private static readonly int s_ampState = HtmlEntities.Lookup.StateOf("amp;");
    private static readonly int s_nbspState = HtmlEntities.Lookup.StateOf("nbsp;");
    private static readonly int s_quotState = HtmlEntities.Lookup.StateOf("quot;");
    private static readonly int s_ltState = HtmlEntities.Lookup.StateOf("lt;");
    private static readonly int s_gtState = HtmlEntities.Lookup.StateOf("gt;");

    private bool StepReference(char c)
    {
        switch (_state)
        {
            case State.CharacterReference:
                _reference.Clear();
                _bestEntityLength = 0;
                _bestEntityValue = null;
                _entityState = 0;
                if (c == '#')
                {
                    Take(); Append(_reference, '#');
                    _numericBase = 10; _numericValue = 0; _numericOverflow = false; _numericDigits = false;
                    _state = State.NumericReference;
                }
                else if (AsciiAlpha(c) || AsciiDigit(c))
                {
                    // Reconsume in the named character reference state without another scanner turn.
                    _state = State.NamedReference;
                    goto case State.NamedReference;
                }
                else FinishLiteralReference();
                return false;

            case State.NamedReference:
                if (TryConsumeNamedReferenceRun()) return false;
                if (HtmlEntities.Lookup.TryAdvance(_entityState, c, out var nextState, out var value))
                {
                    _entityState = nextState;
                    Take(); Append(_reference, c);
                    if (value is not null)
                    {
                        _bestEntityLength = _reference.Length;
                        _bestEntityValue = value;
                    }
                    return false;
                }
                FinishNamedReference(c);
                return false;

            case State.AmbiguousAmpersand:
                if (AsciiAlpha(c) || AsciiDigit(c))
                {
                    var offset = _input.Offset;
                    var character = Take();
                    if (_returnState is State.Data or State.RcData) Text(character, offset);
                    else Append(_value, character);
                    return false;
                }
                if (c == ';') Error("unknown-named-character-reference", _input.Offset);
                _referenceStart = -1;
                _state = _returnState;
                return false;

            case State.NumericReference:
                if (c is 'x' or 'X')
                {
                    Take(); Append(_reference, c); _numericBase = 16; _state = State.HexStart;
                }
                else _state = State.DecimalStart;
                return false;
            case State.HexStart:
                if (!HexDigit(c, out _)) { Error("absence-of-digits-in-numeric-character-reference", _referenceStart); FinishLiteralReference(); return false; }
                _state = State.HexReference; return false;
            case State.DecimalStart:
                if (!AsciiDigit(c)) { Error("absence-of-digits-in-numeric-character-reference", _referenceStart); FinishLiteralReference(); return false; }
                _state = State.DecimalReference; return false;
            case State.HexReference:
            case State.DecimalReference:
                var valid = _numericBase == 16 ? HexDigit(c, out var digit) : DecimalDigit(c, out digit);
                if (valid)
                {
                    Take(); _numericDigits = true;
                    if (!_numericOverflow)
                    {
                        var next = (ulong) _numericValue * (uint) _numericBase + (uint) digit;
                        if (next > 0x10FFFF) _numericOverflow = true;
                        else _numericValue = (uint) next;
                    }
                    return false;
                }
                if (c == ';') Take();
                else Error("missing-semicolon-after-character-reference", _input.Offset);
                FinishNumericReference();
                return false;
        }
        throw new InvalidOperationException("Invalid character-reference state.");
    }

    // HTML Standard §13.2.5.73: the trie walk is the state's per-character step, batched over the
    // contiguous source the same way TryConsumeRun batches ordinary appends. Trie edges are ASCII
    // alphanumerics and ';', so no consumed unit needs input preprocessing or line tracking.
    private bool TryConsumeNamedReferenceRun()
    {
        var source = _input.CurrentSpan;
        var limit = (int) Math.Min(_remainingWork + 1, source.Length);
        if (_maxToken > 0)
        {
            var start = _tokenStart >= 0 ? _tokenStart : _referenceStart;
            if (start >= 0) limit = (int) Math.Min(limit, _maxToken - (_input.Offset - start));
        }
        if (limit < 2) return false;

        var state = _entityState;
        var length = 0;
        var bestLength = -1;
        string? bestValue = null;
        while (length < limit && HtmlEntities.Lookup.TryAdvance(state, source[length], out var next, out var value))
        {
            state = next;
            length++;
            if (value is not null)
            {
                bestLength = length;
                bestValue = value;
            }
        }
        if (length < 2) return false;

        Poll();
        var existing = _reference.Length;
        EnsureAppendCapacity(_reference, length);
        _reference.Append(source[..length]);
        if (bestValue is not null)
        {
            _bestEntityLength = existing + bestLength;
            _bestEntityValue = bestValue;
        }
        _entityState = state;
        _input.ConsumeOrdinaryRun(length);
        // ReadCore already charged the first unit of this scanner iteration.
        ChargeCopy(length - 1);
        Poll();
        // The unit that stopped the walk is already in view: finish without another scanner turn.
        if (length < limit) FinishNamedReference(source[length]);
        return true;
    }

    // HTML Standard §13.2.5.72–73: a named reference whose name ends in ';' and is a complete
    // entity takes the error-free path through the named character reference state, whatever
    // follows it. The scan reads from the '&' at amp; length counts the units after it, ';'
    // included, and state is the trie state the ';' reaches.
    private static bool TryScanTerminatedReference(ReadOnlySpan<char> source, int amp, out int length, out int state)
    {
        var rest = source[(amp + 1)..];
        // The references nearly every document is made of, matched before the trie walk; each state
        // is the one the walk reaches.
        if (rest.StartsWith("amp;")) { length = 4; state = s_ampState; return true; }
        if (rest.StartsWith("nbsp;")) { length = 5; state = s_nbspState; return true; }
        if (rest.StartsWith("quot;")) { length = 5; state = s_quotState; return true; }
        if (rest.StartsWith("lt;")) { length = 3; state = s_ltState; return true; }
        if (rest.StartsWith("gt;")) { length = 3; state = s_gtState; return true; }
        state = 0;
        length = 0;
        var lookup = HtmlEntities.Lookup;
        for (var i = amp + 1; i < source.Length; i++)
        {
            var c = source[i];
            if (c == ';')
            {
                if (i == amp + 1 || !lookup.TryAdvance(state, ';', out state, out var value) || value is null) return false;
                length = i - amp;
                return true;
            }
            if (!AsciiAlpha(c) && !AsciiDigit(c)) return false;
            if (!lookup.TryAdvance(state, c, out state, out _)) return false;
        }
        return false;
    }

    // The Data or RCDATA state's '&' step and the named character reference state's run, in one
    // scanner iteration: one unit for the '&' (already charged by ReadCore) plus one per unit of
    // the name, the same text append, and the same offsets. The scalar states charge exactly that
    // when the run that walks the name also sees the unit after it, so the reference ends inside
    // the current slice and inside the quota; otherwise they are left to it.
    private bool TryConsumeTextReference()
    {
        var source = _input.CurrentSpan;
        if (!TryScanTerminatedReference(source, 0, out var length, out var state)) return false;
        if (length + 1 >= source.Length || length >= _remainingWork) return false;
        if (_maxToken > 0 && length + 1 > _maxToken) return false;
        Text(HtmlEntities.Lookup.ValueAt(state)!, _input.Offset);
        _input.ConsumeOrdinaryRun(length + 1);
        ChargeCopy(length);
        return true;
    }

    private static bool DecimalDigit(char c, out int digit)
    {
        digit = c - '0';
        return (uint) digit <= 9;
    }

    private static bool HexDigit(char c, out int digit)
    {
        if (DecimalDigit(c, out digit)) return true;
        digit = Lower(c) - 'a' + 10;
        return (uint) (digit - 10) <= 5;
    }

    private void FinishLiteralReference()
    {
        AppendReferenceResult("&" + _reference);
        _reference.Clear();
        _referenceStart = -1;
        _state = _returnState;
    }

    private void FinishNamedReference(char following)
    {
        if (_bestEntityValue is null)
        {
            AppendReferenceResult("&" + Materialize(_reference));
            _reference.Clear();
            _state = State.AmbiguousAmpersand;
            return;
        }
        var hasSemicolon = _reference[_bestEntityLength - 1] == ';';
        var next = _bestEntityLength < _reference.Length ? _reference[_bestEntityLength] : following;
        if (!hasSemicolon && _returnState is not (State.Data or State.RcData) && (AsciiAlpha(next) || AsciiDigit(next) || next == '='))
        {
            FinishLiteralReference();
            return;
        }
        if (!hasSemicolon) Error("missing-semicolon-after-character-reference", _referenceStart);
        AppendReferenceResult(_bestEntityValue);
        if (_bestEntityLength < _reference.Length)
        {
            Poll();
            var length = _reference.Length - _bestEntityLength;
            var suffix = _reference.ToString(_bestEntityLength, length);
            ChargeCopy(length);
            Poll();
            AppendReferenceResult(suffix);
        }
        _reference.Clear();
        _referenceStart = -1;
        _state = _returnState;
    }

    private void FinishNumericReference()
    {
        uint code = _numericValue;
        if (_numericOverflow || code > 0x10FFFF) { Error("character-reference-outside-unicode-range", _referenceStart); code = 0xFFFD; }
        else if (code == 0) { Error("null-character-reference", _referenceStart); code = 0xFFFD; }
        else if (code is >= 0xD800 and <= 0xDFFF) { Error("surrogate-character-reference", _referenceStart); code = 0xFFFD; }
        else
        {
            var replacement = code switch
            {
                0x80 => 0x20ACu,
                0x82 => 0x201Au,
                0x83 => 0x0192u,
                0x84 => 0x201Eu,
                0x85 => 0x2026u,
                0x86 => 0x2020u,
                0x87 => 0x2021u,
                0x88 => 0x02C6u,
                0x89 => 0x2030u,
                0x8A => 0x0160u,
                0x8B => 0x2039u,
                0x8C => 0x0152u,
                0x8E => 0x017Du,
                0x91 => 0x2018u,
                0x92 => 0x2019u,
                0x93 => 0x201Cu,
                0x94 => 0x201Du,
                0x95 => 0x2022u,
                0x96 => 0x2013u,
                0x97 => 0x2014u,
                0x98 => 0x02DCu,
                0x99 => 0x2122u,
                0x9A => 0x0161u,
                0x9B => 0x203Au,
                0x9C => 0x0153u,
                0x9E => 0x017Eu,
                0x9F => 0x0178u,
                _ => code
            };
            if (replacement != code) { Error("control-character-reference", _referenceStart); code = replacement; }
            else if (code == 0x0D || code is <= 0x1F and not (0x09 or 0x0A or 0x0C) || code is >= 0x7F and <= 0x9F)
                Error("control-character-reference", _referenceStart);
            else if (code is >= 0xFDD0 and <= 0xFDEF || (code & 0xFFFE) == 0xFFFE)
                Error("noncharacter-character-reference", _referenceStart);
        }
        AppendReferenceResult(char.ConvertFromUtf32((int) code));
        _reference.Clear();
        _referenceStart = -1;
        _state = _returnState;
    }

    private void FinishReferenceAtEof()
    {
        switch (_state)
        {
            case State.CharacterReference: FinishLiteralReference(); break;
            case State.NamedReference: FinishNamedReference('\0'); break;
            case State.AmbiguousAmpersand: _referenceStart = -1; _state = _returnState; break;
            case State.NumericReference:
            case State.HexStart:
            case State.DecimalStart:
                Error("absence-of-digits-in-numeric-character-reference", _referenceStart);
                FinishLiteralReference(); break;
            case State.HexReference:
            case State.DecimalReference:
                if (_numericDigits) { Error("missing-semicolon-after-character-reference", _input.Offset); FinishNumericReference(); }
                else FinishLiteralReference();
                break;
        }
    }

    private void AppendReferenceResult(string value)
    {
        if (_returnState is State.Data or State.RcData) Text(value, _referenceStart);
        else Append(_value, value);
    }
}
