using System;

namespace Jint.HtmlParser.Html;

// HTML Standard §13.2.3: the input stream and its insertion point. Slices retain
// immutable caller strings; splitting a cursor and splicing a write never copy a tail.
// Offset counts consumed units of the expanded stream, not original document offsets.
internal sealed class HtmlInput
{
    internal sealed class CrossingBatch { internal bool Passed; }

    internal sealed class SourceUnit(HtmlSourceKind kind, long id)
    {
        private long _offset;
        private long _line = 1;
        private long _column;
        private bool _cr;

        internal HtmlSourceLocation Location => new(kind, id, _offset, _line, _column);

        internal void Consume(char value)
        {
            _offset++;
            if (value == '\r') { _line++; _column = 0; }
            else if (value == '\n') { if (!_cr) _line++; _column = 0; }
            else _column++;
            _cr = value == '\r';
        }

        internal void ConsumeOrdinaryRun(int length)
        {
            _offset += length;
            _column += length;
            _cr = false;
        }
    }

    internal sealed class Node
    {
        internal Node(string? source = null, int start = 0, int end = 0, SourceUnit? unit = null)
        {
            Source = source;
            Unit = unit;
            Start = start;
            End = end;
        }

        internal readonly string? Source;
        internal readonly SourceUnit? Unit;
        internal readonly int Start;
        internal readonly int End;
        internal Node Previous = null!;
        internal Node Next = null!;
        internal bool Passed;
        internal CrossingBatch? Crossings;
    }

    private struct Probe
    {
        internal Node? Node;
        internal int Distance;
        internal int Position;
        internal bool Complete;
        internal bool Found;
    }

    // All declarations probe fewer than sixteen units. Suspended probes retain
    // traversal progress; completed probes also locate an atomic matched keyword.
    private readonly Probe[] _probes = new Probe[16];
    private CrossingBatch? _crossings;
    private int _probeCount;
    private readonly Node _head = new();
    private readonly Node _tail = new();
    private readonly Func<bool> _chargeTraversal;
    private Node? _boundary;
    private int _offset;
    private long _appended;
    private readonly SourceUnit _primary = new(HtmlSourceKind.Primary, 0);
    private SourceUnit? _lastUnit;
    private long _nextUnitId;

    internal HtmlSourceLocation SourceLocation => (_lastUnit ?? _primary).Location;
    internal long SourceChanges { get; private set; }

    internal StringSlice GetCurrentSource(int length)
    {
        var node = _head.Next;
        return new StringSlice(node.Source!, node.Start + _offset, length);
    }

    private void ConsumeSource(Node node, char value)
    {
        var unit = node.Unit!;
        if (_lastUnit is not null && _lastUnit != unit) SourceChanges++;
        _lastUnit = unit;
        unit.Consume(value);
    }

    internal HtmlInput(Func<bool> chargeTraversal)
    {
        _chargeTraversal = chargeTraversal;
        _head.Next = _tail;
        _tail.Previous = _head;
    }

    internal long Offset { get; private set; }
    internal bool IsClosed { get; private set; }
    internal bool IsFinal => IsClosed && _boundary is null && !WorkExhausted;
    internal bool BoundaryReached { get; private set; }
    internal bool WorkExhausted { get; private set; }
    internal long Appended => _appended;

    internal void BeginRead(Node? boundary)
    {
        if (_boundary != boundary) InvalidateProbes();
        _boundary = boundary;
        BoundaryReached = false;
        WorkExhausted = false;
    }

    internal void Append(string chunk, bool isFinal)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (IsClosed) throw new InvalidOperationException("The input is closed.");
        if (chunk.Length > 0) LinkBefore(_tail, new Node(chunk, 0, chunk.Length, _primary));
        Account(chunk.Length);
        if (isFinal) IsClosed = true;
        InvalidateProbes();
    }

    internal Node CreateMarker()
    {
        if (_offset != 0)
        {
            var current = _head.Next;
            var suffix = new Node(current.Source, current.Start + _offset, current.End, current.Unit);
            LinkBefore(current, suffix);
            Unlink(current);
            _offset = 0;
        }
        var marker = new Node();
        LinkBefore(_head.Next, marker);
        InvalidateProbes();
        return marker;
    }

    internal void Insert(Node marker, string text)
    {
        if (text.Length > 0) LinkBefore(marker, new Node(text, 0, text.Length, new SourceUnit(HtmlSourceKind.Inserted, ++_nextUnitId)));
        Account(text.Length);
        InvalidateProbes();
    }

    internal void Release(Node marker)
    {
        if (!HasPassed(marker)) Unlink(marker);
        if (_boundary == marker) _boundary = null;
        InvalidateProbes();
    }

    // Crossing one marker uses one scanner iteration. A bounded read leaves its
    // marker linked, including when a partial token is waiting before that marker.
    internal bool SkipMarker()
    {
        var node = _head.Next;
        if (node == _tail || node.Source is not null || node == _boundary) return false;
        Unlink(node);
        node.Passed = true;
        InvalidateProbes();
        return true;
    }

    internal bool Peek(int distance, out char value)
    {
        if ((uint) distance >= 16u) throw new ArgumentOutOfRangeException(nameof(distance));
        value = default;
        if (WorkExhausted) return false;
        if (distance >= _probeCount) _probeCount = distance + 1;
        ref var probe = ref _probes[distance];
        if (probe.Node is null)
        {
            probe.Node = _head.Next;
            probe.Distance = distance;
        }
        while (!probe.Complete)
        {
            var node = probe.Node;
            if (node == _boundary || node == _tail)
            {
                probe.Complete = true;
                break;
            }
            if (node.Source is null)
            {
                if (!_chargeTraversal()) { WorkExhausted = true; return false; }
                // Speculative traversal does not pass a marker. The matched
                // keyword later commits this batch at once, without a cleanup
                // loop proportional to the number of crossed markers.
                _crossings ??= new CrossingBatch();
                node.Crossings = _crossings;
            }
            else
            {
                var start = node.Start + (node == _head.Next ? _offset : 0);
                var available = node.End - start;
                if (probe.Distance < available)
                {
                    probe.Position = start + probe.Distance;
                    probe.Found = probe.Complete = true;
                    break;
                }
                probe.Distance -= available;
            }
            probe.Node = node.Next;
        }
        if (!probe.Found && probe.Node == _boundary) BoundaryReached = true;
        if (probe.Found) value = probe.Node!.Source![probe.Position];
        return probe.Found;
    }

    internal bool PeekCurrent(out char value)
    {
        var node = _head.Next;
        if (node.Source is { } source && !WorkExhausted)
        {
            value = source[node.Start + _offset];
            return true;
        }
        return Peek(0, out value);
    }

    internal ReadOnlySpan<char> CurrentSpan => _head.Next.Source is { } source && !WorkExhausted
        ? source.AsSpan(_head.Next.Start + _offset, _head.Next.End - _head.Next.Start - _offset)
        : default;

    internal static bool HasPassed(Node marker) => marker.Passed || marker.Crossings?.Passed == true;

    // Prefix has already located each character. Commit its bounded character
    // count while jumping across marker chains in constant time. Marker points
    // hold weak node references, so detaching a chain releases its source slices.
    internal void BeginKeywordConsumption()
    {
        if (_crossings is not null) _crossings.Passed = true;
    }

    internal char ConsumeKeywordCharacter(int distance)
    {
        var probe = _probes[distance];
        var node = probe.Node!;
        var value = node.Source![probe.Position];
        var previous = node.Previous;
        if (previous != _head) previous.Next = null!;
        _head.Next = node;
        node.Previous = _head;
        _offset = probe.Position - node.Start + 1;
        ConsumeSource(node, value);
        Offset++;
        if (probe.Position + 1 == node.End)
        {
            Unlink(node);
            _offset = 0;
        }
        return value;
    }

    internal void EndKeywordConsumption() => InvalidateProbes();

    internal char Consume()
    {
        var node = _head.Next;
        var value = node.Source![node.Start + _offset++];
        ConsumeSource(node, value);
        Offset++;
        if (node.Start + _offset == node.End)
        {
            Unlink(node);
            _offset = 0;
        }
        InvalidateProbes();
        return value;
    }

    // The tokenizer stops before CR/LF and never crosses a source slice or marker.
    internal void ConsumeOrdinaryRun(int length)
    {
        var node = _head.Next;
        var unit = node.Unit!;
        if (_lastUnit is not null && _lastUnit != unit) SourceChanges++;
        _lastUnit = unit;
        unit.ConsumeOrdinaryRun(length);
        Offset += length;
        _offset += length;
        if (node.Start + _offset == node.End)
        {
            Unlink(node);
            _offset = 0;
        }
        InvalidateProbes();
    }

    private void InvalidateProbes()
    {
        if (_probeCount == 0) return;
        // Release only populated lookahead slots and their saved source references.
        Array.Clear(_probes, 0, _probeCount);
        _probeCount = 0;
        _crossings = null;
    }

    private void Account(int length) =>
        _appended = _appended > long.MaxValue - length ? long.MaxValue : _appended + length;

    private static void LinkBefore(Node before, Node node)
    {
        node.Previous = before.Previous;
        node.Next = before;
        before.Previous.Next = node;
        before.Previous = node;
    }

    private static void Unlink(Node node)
    {
        node.Previous.Next = node.Next;
        node.Next.Previous = node.Previous;
        // Released/consumed nodes do not retain the unread source chain.
        node.Previous = node.Next = null!;
    }
}
