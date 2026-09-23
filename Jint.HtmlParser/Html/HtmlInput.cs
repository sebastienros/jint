using System;
using System.Collections.Generic;

namespace Jint.HtmlParser.Html;

// The cursor owns the immutable caller strings. A consumed segment is removed as soon as
// possible; lookahead is bounded by the longest named reference or a markup keyword.
internal sealed class HtmlInput
{
    private readonly List<string> _segments = new();
    private int _head;
    private int _offset;
    private long _appended;

    internal long Offset { get; private set; }
    internal bool IsFinal { get; private set; }
    internal long Appended => _appended;

    internal void Append(string chunk, bool isFinal)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (IsFinal) throw new InvalidOperationException("The input is closed.");
        if (chunk.Length > 0) _segments.Add(chunk);
        _appended = _appended > long.MaxValue - chunk.Length ? long.MaxValue : _appended + chunk.Length;
        if (isFinal) IsFinal = true;
    }

    internal bool Peek(int distance, out char value)
    {
        for (var i = _head; i < _segments.Count; i++)
        {
            var start = i == _head ? _offset : 0;
            var available = _segments[i].Length - start;
            if (distance < available)
            {
                value = _segments[i][start + distance];
                return true;
            }
            distance -= available;
        }
        value = default;
        return false;
    }

    internal char Consume()
    {
        var segment = _segments[_head];
        var value = segment[_offset++];
        Offset++;
        if (_offset == segment.Length)
        {
            _segments[_head] = string.Empty;
            _head++;
            _offset = 0;
            if (_head >= 32 && _head * 2 >= _segments.Count)
            {
                _segments.RemoveRange(0, _head);
                _head = 0;
            }
        }
        return value;
    }
}
