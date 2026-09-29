using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTokenizer
{
    private static readonly SearchValues<char> DataStops = SearchValues.Create("&<\0\r\n");
    private static readonly SearchValues<char> RawTextStops = SearchValues.Create("<\0\r\n");
    private static readonly SearchValues<char> PlainTextStops = SearchValues.Create("\0\r\n");
    private static readonly SearchValues<char> TagNameStops = SearchValues.Create(" \t\n\f\r/>\0ABCDEFGHIJKLMNOPQRSTUVWXYZ");
    private static readonly SearchValues<char> AttributeNameStops = SearchValues.Create(" \t\n\f\r/=>\0\"'<ABCDEFGHIJKLMNOPQRSTUVWXYZ");
    private static readonly SearchValues<char> DoubleQuotedStops = SearchValues.Create("\"&\0\r\n");
    private static readonly SearchValues<char> SingleQuotedStops = SearchValues.Create("'&\0\r\n");
    private static readonly SearchValues<char> UnquotedStops = SearchValues.Create(" \t\n\f\r>&\0\"'<=`");
    private static readonly SearchValues<char> CommentStops = SearchValues.Create("<-\0\r\n");

    private static ReadOnlySpan<bool> RunStates =>
    [
        true, false, false, true, false, true, false, false, true, true, true, false, false, false, // Data..MarkupDeclaration
        false, false, false, false, false, false, false, false, true, // PiOpen..Comment
    ];

    // The states TryConsumeRun batches; every other state is scalar.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsRunState(State state) =>
        (uint) state < (uint) RunStates.Length ? RunStates[(int) state]
        : state is State.RcData or State.RawText or State.ScriptData or State.PlainText;

    // HTML Standard §13.2.5: batch only characters whose state transition is an
    // ordinary append. Delimiters, errors and input preprocessing stay scalar.
    private bool TryConsumeRun(char current, out bool stopAhead)
    {
        stopAhead = false;
        CharBuffer buffer;
        SearchValues<char> stops;
        switch (_state)
        {
            case State.Data:
            case State.RcData: buffer = _text; stops = DataStops; break;
            case State.RawText:
            case State.ScriptData: buffer = _text; stops = RawTextStops; break;
            case State.PlainText: buffer = _text; stops = PlainTextStops; break;
            case State.TagName: buffer = _tagName; stops = TagNameStops; break;
            case State.AttributeName: buffer = _name; stops = AttributeNameStops; break;
            case State.DoubleQuotedValue: buffer = _value; stops = DoubleQuotedStops; break;
            case State.SingleQuotedValue: buffer = _value; stops = SingleQuotedStops; break;
            case State.UnquotedValue: buffer = _value; stops = UnquotedStops; break;
            case State.Comment: buffer = _comment; stops = CommentStops; break;
            default: return false;
        }
        if (stops.Contains(current)) return false;
        var sliced = buffer == _text || buffer == _value;

        var source = _input.CurrentSpan;
        var limit = (int) Math.Min(Math.Min(_remainingWork + 1, source.Length), 4096);
        if (buffer == _text) limit = Math.Min(limit, 4096 - TextLength);
        if (_maxToken > 0)
        {
            var start = _tokenStart >= 0 ? _tokenStart : _referenceStart;
            if (start >= 0) limit = (int) Math.Min(limit, _maxToken - (_input.Offset - start));
        }
        if (limit < (sliced ? 1 : 2)) return false;

        source = source[..limit];
        var length = source.IndexOfAny(stops);
        if (length < 0) length = source.Length;
        if (length < (sliced ? 1 : 2)) return false;
        // A stop inside the slice is the next unit, and a run leaves the state unchanged.
        stopAhead = length < source.Length;

        Poll();
        if (buffer == _text && TextLength == 0) _textStart = _input.Offset;
        if (sliced) AppendSource(buffer, _input.GetCurrentSource(length));
        else
        {
            EnsureAppendCapacity(buffer, length);
            buffer.Append(source[..length]);
        }
        _input.ConsumeOrdinaryRun(length);
        // ReadCore already charged the first unit of this scanner iteration.
        ChargeCopy(length - 1);
        Poll();
        return true;
    }
}
