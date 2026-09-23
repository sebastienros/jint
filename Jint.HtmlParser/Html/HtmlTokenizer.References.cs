using System;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTokenizer
{
    private bool StepReference(char c)
    {
        switch (_state)
        {
            case State.CharacterReference:
                _reference.Clear();
                _bestEntityLength = 0;
                _bestEntityValue = null;
                if (c == '#')
                {
                    Take(); _reference.Append('#');
                    _numericBase = 10; _numericValue = 0; _numericOverflow = false; _numericDigits = false;
                    _state = State.NumericReference;
                }
                else if (AsciiAlpha(c) || AsciiDigit(c)) _state = State.NamedReference;
                else FinishLiteralReference();
                return false;

            case State.NamedReference:
                var candidate = _reference.ToString() + c;
                if (HtmlEntities.Prefixes.Contains(candidate))
                {
                    Take(); _reference.Append(c);
                    if (HtmlEntities.Values.TryGetValue(candidate, out var value))
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
                    if (_returnState == State.Data) Text(character, offset);
                    else _value.Append(character);
                    return false;
                }
                if (c == ';') Error("unknown-named-character-reference", _input.Offset);
                _referenceStart = -1;
                _state = _returnState;
                return false;

            case State.NumericReference:
                if (c is 'x' or 'X')
                {
                    Take(); _reference.Append(c); _numericBase = 16; _state = State.HexStart;
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
        var spelling = _reference.ToString();
        if (_bestEntityValue is null)
        {
            AppendReferenceResult("&" + spelling);
            _reference.Clear();
            _state = State.AmbiguousAmpersand;
            return;
        }
        var matched = spelling.AsSpan(0, _bestEntityLength);
        var hasSemicolon = matched[^1] == ';';
        var next = _bestEntityLength < spelling.Length ? spelling[_bestEntityLength] : following;
        if (!hasSemicolon && _returnState != State.Data && (AsciiAlpha(next) || AsciiDigit(next) || next == '='))
        {
            FinishLiteralReference();
            return;
        }
        if (!hasSemicolon) Error("missing-semicolon-after-character-reference", _referenceStart);
        AppendReferenceResult(_bestEntityValue);
        if (_bestEntityLength < spelling.Length) AppendReferenceResult(spelling[_bestEntityLength..]);
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
            case State.AmbiguousAmpersand: _referenceStart = -1; break;
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
        if (_returnState == State.Data) Text(value, _referenceStart);
        else _value.Append(value);
    }
}
