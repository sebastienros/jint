#nullable enable
using Jint.Runtime.RegExp.GregsEx.Contracts.EcmaScript;

namespace Jint.Runtime.RegExp.GregsEx.Flavors.EcmaScript.Parsing;

internal sealed class ResolvedSet
{
    private readonly ParserStorage _storage;
    internal readonly Buffer<EcmaInterval> Ranges;
    internal readonly Buffer<int[]> Strings;
    // Static MayContainStrings, independent of the evaluated set's cardinality.
    internal bool MayStrings;
    internal ResolvedSet(ParserStorage storage)
    {
        _storage = storage;
        storage.Reserve(64);
        Ranges = storage.Buffer<EcmaInterval>();
        Strings = storage.Buffer<int[]>();
    }
    internal void AddRange(int first, int last)
    {
        _storage.Work();
        // The first range that touches or follows `first`. Property data, complements and folds add in ascending
        // order, so most adds land past the last range; a scan from the start made loading \p{L}'s ~700 ranges
        // quadratic, hundreds of microseconds per compile.
        var count = Ranges.Count;
        var i = count;
        if (count != 0 && (long)Ranges[count - 1].Last + 1 >= first)
        {
            int lo = 0, hi = count - 1;
            while (lo < hi)
            {
                _storage.Work();
                var mid = lo + (hi - lo) / 2;
                if ((long)Ranges[mid].Last + 1 < first)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }
            i = lo;
        }
        var end = i;
        while (end < Ranges.Count && Ranges[end].First <= (long)last + 1)
        {
            _storage.Work();
            first = Math.Min(first, Ranges[end].First);
            last = Math.Max(last, Ranges[end].Last);
            end++;
        }
        if (end > i)
        {
            Ranges[i] = new(first, last);
            if (end > i + 1)
            {
                Ranges.Remove(i + 1, end - i - 1);
            }
        }
        else
        {
            Ranges.Add(default);
            _storage.Work(Ranges.Count - i);
            Ranges.Items.Slice(i, Ranges.Count - i - 1).CopyTo(Ranges.Items[(i + 1)..]);
            Ranges[i] = new(first, last);
        }
        // MaxClassRanges bounds the final retained plan. Intermediate union or
        // algebra fragments may later merge/disappear; temporary storage/work
        // is admitted by the memory/work ceilings before growth and copying.
    }
    internal bool Contains(int point)
    {
        int lo = 0, hi = Ranges.Count - 1;
        while (lo <= hi)
        {
            _storage.Work();
            var mid = lo + (hi - lo) / 2;
            var r = Ranges[mid];
            if (point < r.First)
            {
                hi = mid - 1;
            }
            else if (point > r.Last)
            {
                lo = mid + 1;
            }
            else
            {
                return true;
            }
        }
        return false;
    }
    private int Compare(ReadOnlySpan<int> a, ReadOnlySpan<int> b)
    {
        _storage.Work();
        var order = b.Length.CompareTo(a.Length);
        for (var i = 0; order == 0 && i < a.Length; i++)
        { _storage.Work(); order = a[i].CompareTo(b[i]); }
        return order;
    }
    internal bool ContainsString(ReadOnlySpan<int> value)
    {
        return FindString(value, out _);
    }
    // Strings remain distinct and ordered longest-first, then lexicographically.
    // Property data is already sorted; scanning every preceding string while loading
    // or folding a large property needlessly exhausted ordinary finite work ceilings.
    // Arbitrary insertion still charges the reference shift before copying it.
    private bool FindString(ReadOnlySpan<int> value, out int insertion)
    {
        int lo = 0, hi = Strings.Count - 1;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            var order = Compare(value, Strings[mid]);
            if (order < 0)
            {
                hi = mid - 1;
            }
            else if (order > 0)
            {
                lo = mid + 1;
            }
            else
            { insertion = mid; return true; }
        }
        insertion = lo;
        return false;
    }
    internal void AddString(ReadOnlySpan<int> points)
    {
        if (points.Length == 1)
        { AddRange(points[0], points[0]); return; }
        if (FindString(points, out var i))
        {
            return;
        }

        _storage.Work(points.Length);
        _storage.Reserve(32L + 4L * points.Length);
        var copy = points.ToArray();
        Strings.Add(copy);
        _storage.Work(Strings.Count - i);
        Strings.Items.Slice(i, Strings.Count - i - 1).CopyTo(Strings.Items[(i + 1)..]);
        Strings[i] = copy;
    }
    internal void Union(ResolvedSet other)
    {
        foreach (var range in other.Ranges.Items)
        { _storage.Work(); AddRange(range.First, range.Last); }
        foreach (var value in other.Strings.Items)
        { _storage.Work(); AddString(value); }
        MayStrings |= other.MayStrings;
    }
    internal ResolvedSet Algebra(ResolvedSet other, bool subtract)
    {
        var result = new ResolvedSet(_storage) { MayStrings = subtract ? MayStrings : MayStrings && other.MayStrings };
        var j = 0;
        foreach (var a in Ranges.Items)
        {
            _storage.Work();
            var first = a.First;
            while (j < other.Ranges.Count && other.Ranges[j].Last < first)
            { _storage.Work(); j++; }
            var k = j;
            while (k < other.Ranges.Count && other.Ranges[k].First <= a.Last)
            {
                _storage.Work();
                var b = other.Ranges[k++];
                if (subtract)
                {
                    if (first < b.First)
                    {
                        result.AddRange(first, Math.Min(a.Last, b.First - 1));
                    }

                    first = Math.Max(first, b.Last + 1);
                    if (first > a.Last)
                    {
                        break;
                    }
                }
                else
                {
                    result.AddRange(Math.Max(first, b.First), Math.Min(a.Last, b.Last));
                }
            }
            if (subtract && first <= a.Last)
            {
                result.AddRange(first, a.Last);
            }
        }
        foreach (var str in Strings.Items)
        {
            _storage.Work();
            if (other.ContainsString(str) != subtract)
            {
                result.AddString(str);
            }
        }
        return result;
    }
    internal ResolvedSet Complement(int maximum)
    {
        var result = new ResolvedSet(_storage);
        var first = 0;
        foreach (var range in Ranges.Items)
        {
            _storage.Work();
            if (first < range.First)
            {
                result.AddRange(first, range.First - 1);
            }

            first = range.Last + 1;
        }
        if (first <= maximum)
        {
            result.AddRange(first, maximum);
        }

        return result;
    }
    internal ResolvedSet Fold(ReadOnlySpan<EcmaCaseMapping> mappings)
    {
        var result = new ResolvedSet(_storage) { MayStrings = MayStrings };
        var m = 0;
        foreach (var range in Ranges.Items)
        {
            _storage.Work();
            var first = range.First;
            while (m < mappings.Length && mappings[m].Source < first)
            { _storage.Work(); m++; }
            while (m < mappings.Length && mappings[m].Source <= range.Last)
            {
                _storage.Work();
                var mapping = mappings[m++];
                if (first < mapping.Source)
                {
                    result.AddRange(first, mapping.Source - 1);
                }

                result.AddRange(mapping.Target, mapping.Target);
                first = mapping.Source + 1;
            }
            if (first <= range.Last)
            {
                result.AddRange(first, range.Last);
            }
        }
        foreach (var str in Strings.Items)
        {
            var temporary = _storage.Buffer<int>();
            foreach (var point in str)
            { _storage.Work(); temporary.Add(Canonicalize(point, mappings, _storage)); }
            result.AddString(temporary.Items);
            temporary.Dispose();
        }
        return result;
    }
    internal static int Canonicalize(int point, ReadOnlySpan<EcmaCaseMapping> mappings, ParserStorage storage)
    {
        int lo = 0, hi = mappings.Length - 1;
        while (lo <= hi)
        {
            storage.Work();
            var mid = lo + (hi - lo) / 2;
            var m = mappings[mid];
            if (point < m.Source)
            {
                hi = mid - 1;
            }
            else if (point > m.Source)
            {
                lo = mid + 1;
            }
            else
            {
                return m.Target;
            }
        }
        return point;
    }
    internal void Dispose()
    {
        foreach (var str in Strings.Items)
        {
            _storage.Release(32L + 4L * str.Length);
        }

        Ranges.Dispose();
        Strings.Dispose();
        _storage.Release(64);
    }
}
