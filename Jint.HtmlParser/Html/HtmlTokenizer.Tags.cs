using System;
using System.Runtime.CompilerServices;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTokenizer
{
    private const int MaxWholeTagAttributes = 16;
    // Per attribute: name start, name length, name has upper alphas, value start, value length
    // (-1: none), first value segment, value segment count (0: the value is one source slice).
    private const int WholeTagAttributeFields = 7;
    // Segments of values holding character references, over the whole tag: a literal is
    // (start, length), a reference is (start, ~state) with the trie state its ';' reaches.
    private const int MaxWholeTagSegments = 32;

    private enum WholeTagState { AfterName, BeforeAttributeName, AfterAttributeName, AfterValue }

    // Scratch for one scan, owned by the tokenizer rather than zeroed on the stack per tag.
    private int[]? _wholeTagFound;
    private int[]? _wholeTagSegments;

    private enum WholeTagResult { None, Token, Resumed }

    // HTML Standard §13.2.5.6–13.2.5.40, tag open through self-closing start tag, with §13.2.5.72–73
    // for a terminated named character reference in an attribute value. A whole tag in the current
    // source slice whose every unit takes an error-free transition produces the token the scalar
    // states would, in one scanner iteration, and charges exactly their work: one unit per state
    // step, including the steps that reconsume, plus the same name, attribute, value and copy
    // charges. Work therefore stays independent of the quota that decides whether this path runs.
    //
    // A value holding only terminated named references is assembled with the appends the scalar
    // states make, in their order, so the value buffer grows and charges as it would. Any other
    // reference ends the scan at the first '&' of the tag: the prefix is committed as the exact
    // state the scalar states reach before it, and they continue from it. Any other shape (a parse
    // error, CR, NUL, a duplicate name, a name that would grow its buffer, a tag crossing a slice or
    // marker, the work quota or the token limit) is left untouched for the scalar states.
    private WholeTagResult TryConsumeTag(out HtmlToken token)
    {
        token = default;
        var source = _input.CurrentSpan;
        var limit = (int) Math.Min(_remainingWork + 1, source.Length);
        if (_maxToken > 0) limit = Math.Min(limit, _maxToken);
        if (limit < 3) return WholeTagResult.None;
        source = source[..limit];

        var endTag = source[1] == '/';
        var p = endTag ? 2 : 1;
        if (!AsciiAlpha(source[p])) return WholeTagResult.None;
        var nameStart = p;
        var nameUpper = false;
        while (p < source.Length && IsWholeTagNameUnit(source[p], ref nameUpper)) p++;
        var nameLength = p - nameStart;
        if (nameLength > Math.Max(_tagName.Capacity, CharBuffer.DefaultCapacity) || nameLength > MaxCachedNameLength) return WholeTagResult.None;

        // TagOpen or EndTagOpen reconsumes its first name unit in TagName.
        var steps = 1;
        Span<int> found = _wholeTagFound ??= new int[MaxWholeTagAttributes * WholeTagAttributeFields];
        Span<int> segments = _wholeTagSegments ??= new int[MaxWholeTagSegments * 2];
        var segmentCount = 0;
        var count = 0;
        var state = WholeTagState.AfterName;
        var selfClosing = false;
        // The first '&' of the tag, where the scalar states resume if a later unit abandons the scan.
        var resumeState = State.Data;
        int resumeAt = 0, resumeCount = 0, resumeSteps = 0, resumePrefix = 0;
        while (true)
        {
            if (p >= source.Length) goto Abandon;
            var c = source[p];
            if (White(c))
            {
                if (state is WholeTagState.AfterName or WholeTagState.AfterValue) state = WholeTagState.BeforeAttributeName;
                p++;
                continue;
            }
            if (c is '>' or '/')
            {
                // BeforeAttributeName reconsumes these in AfterAttributeName.
                if (state == WholeTagState.BeforeAttributeName) steps++;
                if (c == '/')
                {
                    if (endTag || p + 1 >= source.Length || source[p + 1] != '>') goto Abandon;
                    selfClosing = true;
                    p++;
                }
                p++;
                break;
            }
            if (endTag || state is WholeTagState.AfterName or WholeTagState.AfterValue || count == MaxWholeTagAttributes)
                goto Abandon;

            // BeforeAttributeName or AfterAttributeName reconsumes the first unit in AttributeName.
            steps++;
            var attributeStart = p;
            var attributeUpper = false;
            while (p < source.Length && IsWholeTagNameUnit(source[p], ref attributeUpper)) p++;
            var attributeLength = p - attributeStart;
            if (attributeLength == 0 || p >= source.Length ||
                attributeLength > Math.Max(_name.Capacity, CharBuffer.DefaultCapacity) || attributeLength > MaxCachedNameLength) goto Abandon;
            var attributeName = source.Slice(attributeStart, attributeLength);
            for (var i = 0; i < count; i++)
            {
                var field = i * WholeTagAttributeFields;
                if (attributeName.Equals(source.Slice(found[field], found[field + 1]), StringComparison.OrdinalIgnoreCase))
                    goto Abandon;
            }
            var entry = count++ * WholeTagAttributeFields;
            found[entry] = attributeStart;
            found[entry + 1] = attributeLength;
            found[entry + 2] = attributeUpper ? 1 : 0;
            found[entry + 4] = -1;
            found[entry + 6] = 0;

            c = source[p];
            if (c != '=')
            {
                if (!White(c) && c is not ('>' or '/')) goto Abandon;
                // AttributeName reconsumes its terminator in AfterAttributeName.
                steps++;
                while (White(c))
                {
                    if (++p >= source.Length) goto Abandon;
                    c = source[p];
                }
                state = WholeTagState.AfterAttributeName;
                if (c != '=') continue;
            }

            p++;
            while (p < source.Length && White(source[p])) p++;
            if (p >= source.Length) goto Abandon;
            var quote = source[p];
            var quoted = quote is '"' or '\'';
            State valueState;
            if (quoted)
            {
                p++;
                valueState = quote == '"' ? State.DoubleQuotedValue : State.SingleQuotedValue;
            }
            else
            {
                // BeforeAttributeValue reconsumes the first unit in UnquotedValue.
                steps++;
                valueState = State.UnquotedValue;
            }
            var stops = valueState switch
            {
                State.DoubleQuotedValue => DoubleQuotedStops,
                State.SingleQuotedValue => SingleQuotedStops,
                _ => UnquotedStops,
            };
            var valueStart = p;
            var literalStart = p;
            var firstSegment = segmentCount;
            while (true)
            {
                var distance = source[p..].IndexOfAny(stops);
                if (distance < 0) goto Abandon;
                p += distance;
                var stop = source[p];
                if (stop != '&')
                {
                    if (quoted ? stop != quote : !White(stop) && stop != '>') goto Abandon;
                    // An unquoted value with no unit at all is the missing-attribute-value error.
                    if (!quoted && p == valueStart) goto Abandon;
                    break;
                }
                if (resumeState == State.Data)
                {
                    resumeState = valueState;
                    resumeAt = p;
                    resumeCount = count;
                    resumeSteps = steps;
                    resumePrefix = p - valueStart;
                }
                if (!TryScanTerminatedReference(source, p, out var referenceLength, out var referenceState) ||
                    segmentCount + 2 > MaxWholeTagSegments) goto Abandon;
                if (literalStart < p)
                {
                    segments[segmentCount * 2] = literalStart;
                    segments[segmentCount * 2 + 1] = p - literalStart;
                    segmentCount++;
                }
                segments[segmentCount * 2] = p;
                segments[segmentCount * 2 + 1] = ~referenceState;
                segmentCount++;
                p += referenceLength + 1;
                literalStart = p;
            }
            found[entry + 3] = valueStart;
            found[entry + 4] = p - valueStart;
            if (segmentCount > firstSegment)
            {
                if (literalStart < p)
                {
                    if (segmentCount == MaxWholeTagSegments) goto Abandon;
                    segments[segmentCount * 2] = literalStart;
                    segments[segmentCount * 2 + 1] = p - literalStart;
                    segmentCount++;
                }
                found[entry + 5] = firstSegment;
                found[entry + 6] = segmentCount - firstSegment;
            }
            // A closing quote is consumed; UnquotedValue consumes its whitespace or '>' itself,
            // as AfterQuotedValue does.
            if (quoted) p++;
            state = WholeTagState.AfterValue;
        }
        // Every consumed unit is one step; ReadCore already charged the first.
        var work = (long) p + steps - 1;
        if (work > _remainingWork) goto Abandon;

        BeginWholeTag(endTag, selfClosing);
        for (var i = 0; i < count; i++)
        {
            var field = i * WholeTagAttributeFields;
            var attributeName = FinishWholeTagAttributeName(source, found, field, resumed: false);
            var valueLength = found[field + 4];
            if (found[field + 6] == 0)
            {
                AddAttribute(valueLength <= 0
                    ? new HtmlAttribute(attributeName, "")
                    : new HtmlAttribute(attributeName, _input.GetCurrentSource(found[field + 3], valueLength)));
                continue;
            }
            // The runs and reference appends of the value states, in their order.
            _value.Clear();
            _valueSource = default;
            var first = found[field + 5];
            for (var s = first; s < first + found[field + 6]; s++)
            {
                var length = segments[s * 2 + 1];
                if (length >= 0) AppendSource(_value, _input.GetCurrentSource(segments[s * 2], length));
                else Append(_value, HtmlEntities.Lookup.ValueAt(~length)!);
            }
            AddAttribute(new HtmlAttribute(attributeName, TakeValue(_value)));
        }
        var attributes = CopyAttributes();
        var name = WholeTagName(source.Slice(nameStart, nameLength), nameUpper);
        BeginWholeTagConsumption(source[..p], work);
        EmitTag(name, attributes, out token);
        return WholeTagResult.Token;

Abandon:
        if (resumeState == State.Data) return WholeTagResult.None;
        // The scalar states stop before the first '&' with the tag and attribute names buffered and
        // the value read so far as a source slice; every earlier attribute is already finished.
        p = resumeAt;
        count = resumeCount;
        work = (long) p + resumeSteps - 1;
        if (work > _remainingWork) return WholeTagResult.None;
        BeginWholeTag(endTag, selfClosing: false);
        for (var i = 0; i < count - 1; i++)
        {
            var field = i * WholeTagAttributeFields;
            var attributeName = FinishWholeTagAttributeName(source, found, field, resumed: true);
            var valueLength = found[field + 4];
            AddAttribute(valueLength <= 0
                ? new HtmlAttribute(attributeName, "")
                : new HtmlAttribute(attributeName, _input.GetCurrentSource(found[field + 3], valueLength)));
        }
        var resumeField = (count - 1) * WholeTagAttributeFields;
        _tagName.Clear();
        AppendLowered(_tagName, source.Slice(nameStart, nameLength));
        _name.Clear();
        AppendLowered(_name, source.Slice(found[resumeField], found[resumeField + 1]));
        _value.Clear();
        _valueSource = resumePrefix > 0 ? _input.GetCurrentSource(p - resumePrefix, resumePrefix) : default;
        BeginWholeTagConsumption(source[..p], work);
        _state = resumeState;
        return WholeTagResult.Resumed;
    }

    // BeginTag, in scalar order: clear the previous tag's attribute bookkeeping.
    private void BeginWholeTag(bool endTag, bool selfClosing)
    {
        Poll();
        ChargeCopy((long) _attributes.Count + _attributeNameCount);
        _attributes.Clear();
        _attributeNames.Clear();
        _attributeNameCount = 0;
        _endTag = endTag;
        _selfClosing = selfClosing;
        _endTagHadAttributes = false;
        _endTagHadSelfClosing = false;
        Poll();
    }

    // FinishAttribute's name steps for an attribute the scalar states would already have finished.
    // The scan has checked the names against each other, so a complete tag only counts the name;
    // a tag the scalar states resume enters it, as their duplicate check reads the set.
    private string FinishWholeTagAttributeName(ReadOnlySpan<char> source, ReadOnlySpan<int> found, int field, bool resumed)
    {
        var attributeName = WholeTagName(source.Slice(found[field], found[field + 1]), found[field + 2] != 0);
        EnsureAttributeNameCapacity();
        Poll();
        if (resumed) _attributeNames.Add(attributeName);
        _attributeNameCount++;
        ChargeCopy(attributeName.Length);
        Poll();
        return attributeName;
    }

    // The Data state's '<' step and every step after it, in one consumption.
    private void BeginWholeTagConsumption(ReadOnlySpan<char> consumed, long work)
    {
        _markupOpenerAfterText = false;
        _declarationAllowCData = _readAllowCData;
        _tokenStart = _input.Offset;
        _input.ConsumeRun(consumed);
        ChargeCopy(work);
        _tokenSourceChanges = _input.SourceChanges;
    }

    // Units of a tag or attribute name taking the ordinary append transition, a subset chosen so
    // that every other unit ends the name or abandons the tag; ASCII upper alphas are lowered.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWholeTagNameUnit(char c, ref bool upper)
    {
        if ((uint) (c - 'a') <= 'z' - 'a' || (uint) (c - '0') <= 9 || c is '-' or '_' or ':' or '.') return true;
        if ((uint) (c - 'A') > 'Z' - 'A') return false;
        upper = true;
        return true;
    }

    private string WholeTagName(ReadOnlySpan<char> name, bool upper)
    {
        if (!upper) return MaterializeName(name);
        Span<char> lowered = stackalloc char[MaxCachedNameLength];
        for (var i = 0; i < name.Length; i++) lowered[i] = Lower(name[i]);
        return MaterializeName(lowered[..name.Length]);
    }

    // The caller checked that the name fits the buffer's capacity, or the default one an unused
    // buffer takes on first use, so nothing is charged: a first allocation copies nothing.
    private static void AppendLowered(CharBuffer buffer, ReadOnlySpan<char> name)
    {
        foreach (var c in name) buffer.Append(Lower(c));
    }
}
