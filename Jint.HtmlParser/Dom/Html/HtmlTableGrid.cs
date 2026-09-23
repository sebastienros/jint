namespace Jint.HtmlParser;

// HTML Standard §4.9.12.1, forming a table. This is only the column projection:
// no slot matrix, header associations, or presentation-derived columns are kept.
internal sealed partial class HtmlTableGrid
{
    private readonly Dictionary<Element, ColumnRange> _cellColumns;
    private readonly Dictionary<Element, ColumnRange> _columnColumns;
    private readonly ColumnEntry[] _columns;
    private readonly CellEntry[] _cells;
    // Derived index is published only when complete. It does not alter any
    // model interval and ordinary nth-column lookups never pay to build it.
    private CellIndex? _cellIndex;

    private HtmlTableGrid(Element table, long columnCount, ColumnEntry[] columns, CellEntry[] cells, Work work)
    {
        Table = table;
        ColumnCount = columnCount;
        _columns = columns;
        _cells = cells;
        _columnColumns = new Dictionary<Element, ColumnRange>(columns.Length);
        _cellColumns = new Dictionary<Element, ColumnRange>(cells.Length);
        foreach (var column in columns)
        {
            work.Step();
            _columnColumns.Add(column.Element, column.Range);
        }

        foreach (var cell in cells)
        {
            work.Step();
            _cellColumns.Add(cell.Element, cell.Range);
        }
    }

    internal Element Table { get; }
    internal long ColumnCount { get; }

    internal static HtmlTableGrid Build(Element table, CancellationToken cancellationToken)
        => Build(table, null, cancellationToken);

    // The callback is an invocation-local deterministic work checkpoint for tests.
    internal static HtmlTableGrid Build(Element table, Action? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!IsHtml(table, "table"))
        {
            throw new ArgumentException("An HTML table element is required.", nameof(table));
        }

        var work = new Work(cancellationToken, checkpoint);
        work.Check();
        var grid = new Builder(table, work).Build();
        work.Check();
        return grid;
    }

    internal bool TryGetCellColumns(Element cell, out long start, out long endExclusive)
        => TryGet(_cellColumns, cell, out start, out endExclusive);

    internal bool TryGetColumnColumns(Element column, out long start, out long endExclusive)
        => TryGet(_columnColumns, column, out start, out endExclusive);

    private static bool TryGet(Dictionary<Element, ColumnRange> ranges, Element element,
        out long start, out long endExclusive)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (ranges.TryGetValue(element, out var range))
        {
            start = range.Start;
            endExclusive = range.End;
            return true;
        }

        start = endExclusive = 0;
        return false;
    }

    internal IEnumerable<Element> ColumnsOverlapping(long start, long endExclusive,
        CancellationToken cancellationToken)
        => ColumnsOverlapping(start, endExclusive, null, cancellationToken);

    internal IEnumerable<Element> ColumnsOverlapping(long start, long endExclusive,
        Action? checkpoint, CancellationToken cancellationToken)
    {
        ValidateRange(start, endExclusive);
        var work = new Work(cancellationToken, checkpoint);
        work.Check();
        if (start == endExclusive)
        {
            yield break;
        }

        // Explicit columns are disjoint and ordered. A binary search finds the
        // first possible predecessor, then only the actual overlapping run is read.
        var lo = 0;
        var hi = _columns.Length;
        while (lo < hi)
        {
            work.Step();
            var mid = lo + (hi - lo) / 2;
            if (_columns[mid].Range.End <= start) lo = mid + 1;
            else hi = mid;
        }

        for (var i = lo; i < _columns.Length && _columns[i].Range.Start < endExclusive; i++)
        {
            work.Step();
            work.Check();
            yield return _columns[i].Element;
        }

        work.Check();
    }

    internal IEnumerable<Element> CellsOverlapping(long start, long endExclusive,
        CancellationToken cancellationToken)
        => CellsOverlapping(start, endExclusive, null, cancellationToken);

    internal IEnumerable<Element> CellsOverlapping(long start, long endExclusive,
        Action? checkpoint, CancellationToken cancellationToken)
    {
        ValidateRange(start, endExclusive);
        var work = new Work(cancellationToken, checkpoint);
        work.Check();
        if (start == endExclusive || _cells.Length == 0)
        {
            yield break;
        }

        var index = Volatile.Read(ref _cellIndex);
        if (index is null)
        {
            var built = BuildCellIndex(work);
            index = Interlocked.CompareExchange(ref _cellIndex, built, null) ?? built;
        }
        // The augmented start-order tree reports intersecting intervals without
        // scanning all cells for every column. Sort the reported identities by
        // their model ordinal to preserve the seam's processing-order contract.
        var matches = new ChunkedList<CellEntry>();
        var stack = new Stack<int>();
        stack.Push(1);
        while (stack.Count != 0)
        {
            work.Step();
            var node = stack.Pop();
            if (index.MaxEnds[node] <= start)
            {
                continue;
            }

            if (node >= index.LeafCount)
            {
                var entry = node - index.LeafCount;
                if (entry < index.Sorted.Length && index.Sorted[entry].Range.Start < endExclusive)
                {
                    matches.Add(index.Sorted[entry]);
                }

                continue;
            }

            var firstIndex = FirstLeafIndex(node, index.LeafCount);
            if (firstIndex >= index.Sorted.Length || index.Sorted[firstIndex].Range.Start >= endExclusive)
            {
                continue;
            }

            stack.Push(node * 2 + 1);
            stack.Push(node * 2);
        }

        foreach (var cell in SortByOrdinal(matches, work))
        {
            work.Step();
            work.Check();
            yield return cell.Element;
        }

        work.Check();
    }

    private static int FirstLeafIndex(int node, int leafCount)
    {
        while (node < leafCount) node *= 2;
        return node - leafCount;
    }

    private void ValidateRange(long start, long endExclusive)
    {
        if (start < 0 || start > ColumnCount) throw new ArgumentOutOfRangeException(nameof(start));
        if (endExclusive < start || endExclusive > ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(endExclusive));
    }

    private static bool IsHtml(Element? element, string localName)
        => element is not null && element.NamespaceUri == Namespaces.Html && element.LocalName == localName;

    private readonly record struct ColumnRange(long Start, long End);
    private readonly record struct ColumnEntry(Element Element, ColumnRange Range);
    private readonly record struct CellEntry(Element Element, ColumnRange Range, int Ordinal);
    private sealed record CellIndex(CellEntry[] Sorted, long[] MaxEnds, int LeafCount);

    // Appends never copy an accumulated list. Each allocation and subsequent
    // freeze copy is bounded by one 256-element segment between work polls.
    private sealed class ChunkedList<T> : IEnumerable<T>
    {
        private Segment? _first;
        private Segment? _last;
        internal int Count { get; private set; }

        internal void Add(T value)
        {
            if (_last is null || _last.Used == _last.Items.Length)
            {
                var next = new Segment();
                if (_last is null) _first = next;
                else _last.Next = next;
                _last = next;
            }

            _last.Items[_last.Used++] = value;
            Count = checked(Count + 1);
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var segment = _first; segment is not null; segment = segment.Next)
                for (var i = 0; i < segment.Used; i++)
                    yield return segment.Items[i];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Segment
        {
            internal readonly T[] Items = new T[256];
            internal int Used;
            internal Segment? Next;
        }
    }

    private sealed class Work(CancellationToken token, Action? checkpoint = null)
    {
        private int _steps;

        internal void Step()
        {
            checkpoint?.Invoke();
            if (++_steps != 256) return;
            _steps = 0;
            token.ThrowIfCancellationRequested();
        }

        internal void Check() => token.ThrowIfCancellationRequested();
    }

    private static CellEntry[] SortByOrdinal(ChunkedList<CellEntry> entries, Work work)
    {
        var source = new CellEntry[entries.Count];
        var offset = 0;
        foreach (var entry in entries) { work.Step(); source[offset++] = entry; }
        if (source.Length < 2) return source;
        var target = new CellEntry[source.Length];
        for (long width = 1; width < source.Length; width *= 2)
        {
            for (long left = 0; left < source.Length; left += 2 * width)
            {
                var middle = (int) Math.Min(left + width, source.Length);
                var right = (int) Math.Min(left + 2 * width, source.Length);
                var a = (int) left;
                var b = middle;
                for (var i = (int) left; i < right; i++)
                {
                    work.Step();
                    target[i] = b == right || a < middle && source[a].Ordinal <= source[b].Ordinal
                        ? source[a++] : source[b++];
                }
            }

            (source, target) = (target, source);
        }

        return source;
    }

    private CellIndex BuildCellIndex(Work work)
    {
        var sorted = new CellEntry[_cells.Length];
        for (var i = 0; i < sorted.Length; i++) { work.Step(); sorted[i] = _cells[i]; }
        SortByStart(sorted, work);

        var leafCount = 1;
        while (leafCount < sorted.Length) { work.Step(); leafCount = checked(leafCount * 2); }
        var maxEnds = new long[checked(leafCount * 2)];
        for (var i = 0; i < sorted.Length; i++)
        {
            work.Step();
            maxEnds[leafCount + i] = sorted[i].Range.End;
        }

        for (var i = leafCount - 1; i > 0; i--)
        {
            work.Step();
            maxEnds[i] = Math.Max(maxEnds[i * 2], maxEnds[i * 2 + 1]);
        }

        work.Check();
        return new CellIndex(sorted, maxEnds, leafCount);
    }

    private static void SortByStart(CellEntry[] entries, Work work)
    {
        if (entries.Length < 2) return;
        var source = entries;
        var target = new CellEntry[entries.Length];
        for (long width = 1; width < source.Length; width *= 2)
        {
            for (long left = 0; left < source.Length; left += 2 * width)
            {
                var middle = (int) Math.Min(left + width, source.Length);
                var right = (int) Math.Min(left + 2 * width, source.Length);
                var a = (int) left;
                var b = middle;
                for (var i = (int) left; i < right; i++)
                {
                    work.Step();
                    target[i] = b == right || a < middle && Compare(source[a], source[b]) <= 0
                        ? source[a++] : source[b++];
                }
            }

            (source, target) = (target, source);
        }

        if (ReferenceEquals(source, entries)) return;
        for (var i = 0; i < entries.Length; i++) { work.Step(); entries[i] = source[i]; }
    }

    private static int Compare(CellEntry left, CellEntry right)
    {
        var result = left.Range.Start.CompareTo(right.Range.Start);
        return result != 0 ? result : left.Ordinal.CompareTo(right.Ordinal);
    }
}
