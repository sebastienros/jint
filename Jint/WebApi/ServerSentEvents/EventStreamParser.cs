#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using System.Text;
using Jint.WebApi.Fetch;
using SystemEncoding = System.Text.Encoding;

namespace Jint.WebApi.ServerSentEvents;

/// <summary>
/// One event a stream produced, ready for the engine thread to turn into a <c>MessageEvent</c>.
/// </summary>
/// <param name="Type">
/// The event type: the event type buffer, or <c>message</c> when that buffer was empty — step 6 of
/// https://html.spec.whatwg.org/multipage/server-sent-events.html#dispatchMessage.
/// </param>
/// <param name="Data">The data buffer, its trailing U+000A already removed.</param>
/// <param name="LastEventId">
/// The last event ID buffer as it stood when this event was dispatched. The buffer is deliberately not reset
/// between events, so an event without an <c>id</c> field carries whatever the last one set.
/// </param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct EventStreamMessage(string Type, string Data, string LastEventId);

/// <summary>
/// The event stream parser, https://html.spec.whatwg.org/multipage/server-sent-events.html#event-stream-interpretation —
/// UTF-8 decode, line splitting, field processing and the dispatch steps.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately engine-free</b>, like <c>HeaderList</c> and the URL parser beside it: it mentions no
/// <see cref="Engine"/>, <c>Realm</c> or <c>JsValue</c>, because it runs on a thread pool thread while the
/// engine goes on running script. It produces plain CLR records; the engine thread turns them into events.
/// </para>
/// <para>
/// Feeding is incremental in every dimension, because a network read boundary can fall anywhere: inside a
/// UTF-8 sequence (the <see cref="Decoder"/> holds the partial bytes), between the CR and the LF of a CRLF
/// (<see cref="_afterCarriageReturn"/> holds that), or in the middle of a line (the line buffer holds that).
/// A chunk therefore produces zero or more messages, and "the end of the file" discards whatever is left —
/// "if the file ends in the middle of an event, before the final empty line, the incomplete event is not
/// dispatched", which falls out of never dispatching without a blank line.
/// </para>
/// </remarks>
internal sealed class EventStreamParser
{
    private const string DataField = "data";
    private const string EventField = "event";
    private const string IdField = "id";
    private const string RetryField = "retry";

    /// <summary>The type an event whose event type buffer is empty is dispatched with.</summary>
    internal const string DefaultEventType = "message";

    /// <summary>U+FEFF, spelled as a code point so that no source file has to carry one.</summary>
    private const char ByteOrderMark = (char) 0xFEFF;

    /// <summary>
    /// https://encoding.spec.whatwg.org/#utf-8-decode. The UTF-8 decoder replaces a malformed sequence with
    /// U+FFFD rather than throwing, which is what that algorithm requires.
    /// </summary>
    private readonly Decoder _decoder = SystemEncoding.UTF8.GetDecoder();

    /// <summary>The data buffer, https://html.spec.whatwg.org/multipage/server-sent-events.html#data-buffer.</summary>
    private readonly StringBuilder _data = new();

    /// <summary>The line being read, which a chunk boundary may fall in the middle of.</summary>
    private readonly StringBuilder _line = new();

    private readonly long _maxEventLength;

    /// <summary>The event type buffer.</summary>
    private string _eventType = string.Empty;

    /// <summary>The last event ID buffer, which is never reset between events.</summary>
    private string _lastEventId;

    /// <summary>
    /// Whether the previous character was a CR, so that an LF opening the next chunk is recognized as the
    /// second half of a CRLF rather than as a line ending of its own.
    /// </summary>
    private bool _afterCarriageReturn;

    /// <summary>Whether no character has been consumed yet, which is the only place a BOM may be stripped.</summary>
    private bool _atStreamStart = true;

    private long? _reconnectionTime;

    /// <param name="maxEventLength">
    /// The most characters one event may accumulate — the data buffer plus the line being read — before the
    /// connection is failed. <see cref="long.MaxValue"/> means unbounded.
    /// </param>
    /// <param name="lastEventId">
    /// What the last event ID buffer starts as, which is the event source's last event ID string.
    /// </param>
    /// <remarks>
    /// Seeding that buffer is a <b>deliberate divergence from a literal reading</b> of "when a stream is
    /// parsed, a data buffer, an event type buffer, and a last event ID buffer must be associated with it …
    /// initialized to the empty string". Taken literally, the first event of a reconnected stream that
    /// carries no <c>id</c> field would set the event source's last event ID string back to the empty string
    /// — and the <i>next</i> reconnection would then resume from the beginning, silently, which is the one
    /// thing the whole mechanism exists to prevent. Browsers seed it, and so does this.
    /// </remarks>
    internal EventStreamParser(long maxEventLength, string lastEventId)
    {
        _maxEventLength = maxEventLength;
        _lastEventId = lastEventId;
    }

    /// <summary>
    /// Decodes a chunk of the response body and appends whatever events it completed to
    /// <paramref name="messages"/>.
    /// </summary>
    /// <exception cref="FetchFailureException">
    /// One event grew past the cap this parser was built with. There is no such failure in the standard — a
    /// browser's event stream is bounded by the tab's memory — but a stream is unbounded by nature and an
    /// engine embedded in a server cannot buffer whatever a server chooses to send.
    /// </exception>
    internal void Feed(byte[] buffer, int count, List<EventStreamMessage> messages)
    {
        Span<char> chars = stackalloc char[512];
        var bytes = new ReadOnlySpan<byte>(buffer, 0, count);

        while (!bytes.IsEmpty)
        {
            _decoder.Convert(bytes, chars, flush: false, out var bytesUsed, out var charsUsed, out _);
            Consume(chars.Slice(0, charsUsed), messages);

            if (bytesUsed == 0 && charsUsed == 0)
            {
                // Everything left is an incomplete UTF-8 sequence the decoder is holding on to; it completes
                // when the next chunk arrives.
                break;
            }

            bytes = bytes.Slice(bytesUsed);
        }
    }

    /// <summary>
    /// The reconnection time a <c>retry</c> field last set, taken once. It is applied on the engine thread,
    /// which is where the event source's own state lives.
    /// </summary>
    internal long? TakeReconnectionTime()
    {
        var value = _reconnectionTime;
        _reconnectionTime = null;
        return value;
    }

    /// <summary>
    /// The line splitting: "a CRLF character pair, a single LF character not preceded by a CR character, and a
    /// single CR character not followed by an LF character being the ways in which a line can end".
    /// </summary>
    private void Consume(ReadOnlySpan<char> chars, List<EventStreamMessage> messages)
    {
        foreach (var c in chars)
        {
            if (_atStreamStart)
            {
                _atStreamStart = false;

                // "The UTF-8 decode algorithm strips one leading UTF-8 Byte Order Mark (BOM), if any."
                if (c == ByteOrderMark)
                {
                    continue;
                }
            }

            if (_afterCarriageReturn)
            {
                _afterCarriageReturn = false;

                // The CR already ended the line; the LF completing a CRLF adds nothing.
                if (c == '\n')
                {
                    continue;
                }
            }

            switch (c)
            {
                case '\r':
                    _afterCarriageReturn = true;
                    EndLine(messages);
                    break;
                case '\n':
                    EndLine(messages);
                    break;
                default:
                    EnsureRoom(1);
                    _line.Append(c);
                    break;
            }
        }
    }

    /// <summary>
    /// "Lines must be processed, in the order they are received, as follows" — the four cases of
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#event-stream-interpretation.
    /// </summary>
    private void EndLine(List<EventStreamMessage> messages)
    {
        var line = _line.ToString();
        _line.Clear();

        if (line.Length == 0)
        {
            // "If the line is empty (a blank line): dispatch the event."
            Dispatch(messages);
            return;
        }

        if (line[0] == ':')
        {
            // "If the line starts with a U+003A COLON character (:): ignore the line." This is what a
            // server's keep-alive comment is, and why one keeps a connection alive without producing an event.
            return;
        }

        var colon = line.IndexOf(':');
        if (colon < 0)
        {
            // "the string is not empty but does not contain a U+003A COLON character": the whole line is the
            // field name and the value is the empty string.
            ProcessField(line, string.Empty);
            return;
        }

        var value = line.Substring(colon + 1);

        // "If value starts with a U+0020 SPACE character, remove it from value." One space, not a trim.
        if (value.Length > 0 && value[0] == ' ')
        {
            value = value.Substring(1);
        }

        ProcessField(line.Substring(0, colon), value);
    }

    /// <summary>
    /// "The steps to process the field given a field name and a field value" — the names are compared
    /// literally, with no case folding, and an unknown field is ignored.
    /// </summary>
    private void ProcessField(string field, string value)
    {
        switch (field)
        {
            case EventField:
                _eventType = value;
                break;

            case DataField:
                EnsureRoom(value.Length + 1);
                _data.Append(value).Append('\n');
                break;

            case IdField:
                // "If the field value does not contain U+0000 NULL, then set the last event ID buffer to the
                // field value. Otherwise, ignore the field." An empty value is not ignored: it resets the
                // buffer, so no Last-Event-ID header is sent on the next reconnect.
                if (!value.Contains('\0', StringComparison.Ordinal))
                {
                    _lastEventId = value;
                }

                break;

            case RetryField:
                if (TryParseReconnectionTime(value, out var milliseconds))
                {
                    _reconnectionTime = milliseconds;
                }

                break;
        }
    }

    /// <summary>
    /// "If the field value consists of only ASCII digits, then interpret the field value as an integer in base
    /// ten … Otherwise, ignore the field."
    /// </summary>
    /// <remarks>
    /// The integer is unbounded in the standard and clamped to <see cref="int.MaxValue"/> milliseconds here —
    /// about 24.8 days — which is the same ceiling <c>setTimeout</c> has, and for the same reason: the delay
    /// rides the engine's timer queue. Nothing can observe the difference.
    /// </remarks>
    private static bool TryParseReconnectionTime(string value, out long milliseconds)
    {
        milliseconds = 0;

        if (value.Length == 0)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }

            milliseconds = milliseconds * 10 + (c - '0');
            if (milliseconds > int.MaxValue)
            {
                milliseconds = int.MaxValue;
            }
        }

        return true;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#dispatchMessage, steps 1 to 7. Step 8 —
    /// "queue a task which, if the readyState attribute is set to a value other than CLOSED, dispatches the
    /// newly created event" — is the caller's, because only the engine thread may create or dispatch one.
    /// </summary>
    private void Dispatch(List<EventStreamMessage> messages)
    {
        // Step 2: "If the data buffer is an empty string, set the data buffer and the event type buffer to the
        // empty string and return." Step 1 has already happened, in that the last event ID buffer was set by
        // the id field itself and is never reset here.
        if (_data.Length == 0)
        {
            _eventType = string.Empty;
            return;
        }

        // Step 3: "If the data buffer's last character is a U+000A LINE FEED (LF) character, then remove the
        // last character from the data buffer." It always is — every data field appends one — so this undoes
        // the separator after the last field rather than trimming the data.
        if (_data[_data.Length - 1] == '\n')
        {
            _data.Length--;
        }

        // Steps 4 to 6.
        var type = _eventType.Length == 0 ? DefaultEventType : _eventType;
        messages.Add(new EventStreamMessage(type, _data.ToString(), _lastEventId));

        // Step 7.
        _data.Clear();
        _eventType = string.Empty;
    }

    /// <summary>
    /// The per-event cap. Measured in decoded characters rather than bytes, which for UTF-8 can only be
    /// stricter — a character never costs less than one byte — and which is what actually bounds the memory
    /// this parser holds.
    /// </summary>
    private void EnsureRoom(int additional)
    {
        if (_maxEventLength == long.MaxValue)
        {
            return;
        }

        if (_data.Length + (long) _line.Length + additional > _maxEventLength)
        {
            throw new FetchFailureException(
                FetchFailureKind.ResponseTooLarge,
                $"A single event exceeded the {_maxEventLength} character limit set by Options.WebApi.Fetch.MaxResponseBytes.");
        }
    }
}
#endif
