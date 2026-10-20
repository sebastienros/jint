#nullable enable
using System.Threading;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using Jint.Runtime.RegExp.GregsEx.Contracts;
using Jint.Runtime.RegExp.GregsEx.Contracts.EcmaScript;
using Jint.Runtime.RegExp.GregsEx.Text;
using ExecutionState = Jint.Runtime.RegExp.GregsEx.Contracts.RunState;

namespace Jint.Runtime.RegExp.GregsEx.Execution.Backtracking;

internal sealed class EcmaFastSet
{
    internal readonly EcmaSetSlice Slice;
    internal readonly ulong AsciiLow;
    internal readonly ulong AsciiHigh;
    internal readonly ulong[]? BmpBits;
    internal readonly bool BmpBitsIsExact;
    internal readonly SearchValues<char>? SearchValues;
    internal readonly bool IsNegatedSearchValues;
    // For a Unicode-aware class holding every surrogate and astral code point (/./u, /[^"]/u), the BMP code
    // units that end a run of it. Such a class matches a pair or a lone surrogate alike, so a run is a span of code
    // units up to the first of these, and is measured vectorized where the inexact bitmap scanned unit by unit.
    internal readonly SearchValues<char>? UnicodeRunStops;
    // Whether the units that end a run (the negated SearchValues, or UnicodeRunStops) are exactly the line
    // terminators, as for /./ and /./u: see IndexOfLineTerminator.
    internal readonly bool StopsAtLineTerminatorsOnly;
    internal readonly bool IgnoreCase;
    private readonly EcmaInterval[] _intervals;

    internal EcmaFastSet(EcmaProgram program, EcmaSetSlice slice, EcmaScriptFlags effectiveFlags, EcmaCaseTables caseTables)
    {
        Slice = slice;
        var ignoreCase = IgnoreCase = (effectiveFlags & EcmaScriptFlags.IgnoreCase) != 0;
        var unicodeAware = program.Profile.IsUnicodeAware;
        var intervals = program.Intervals.Slice(slice.IntervalOffset, slice.IntervalCount);
        _intervals = intervals.ToArray();

        // Under legacy i (no u/v) every UTF-16 unit is matched alone and Canonicalize maps BMP to BMP, so the folded
        // membership of each unit is exact too: the intervals, corrected at the few units that have a case mapping.
        // Without a bitmap an i-flag class took the slow path per character and lost every accelerated plan.
        var foldedBmp = ignoreCase && !unicodeAware;
        var hasBmp = (!ignoreCase || foldedBmp) && slice.StringCount == 0;
        ulong low = 0, high = 0;
        // A bitmap's first two words are the ASCII membership, folded or not; only u/v with i folds here.
        if (slice.StringCount == 0 && !hasBmp)
        {
            for (var c = 0; c < 128; c++)
            {
                var folded = ignoreCase ? Fold(c, unicodeAware, ignoreCase, caseTables) : c;
                if (ContainsInterval(intervals, folded))
                {
                    if (c < 64)
                    {
                        low |= 1UL << c;
                    }
                    else
                    {
                        high |= 1UL << (c - 64);
                    }
                }
            }
        }
        AsciiLow = low;
        AsciiHigh = high;

        if (hasBmp && IsAsciiOnly(intervals))
        {
            // Every member is ASCII (legacy folding never maps a non-ASCII unit to an ASCII one, so i adds none): the
            // bitmap is two words, and identical classes share one bitmap and one SearchValues instead of allocating
            // 8 KiB and a SearchValues per Set node on every compile.
            for (var c = 0; c < 128; c++)
            {
                if (ContainsInterval(intervals, ignoreCase ? Fold(c, unicodeAware, ignoreCase, caseTables) : c))
                {
                    if (c < 64)
                    {
                        low |= 1UL << c;
                    }
                    else
                    {
                        high |= 1UL << (c - 64);
                    }
                }
            }

            var shared = SharedAsciiSet(low, high);
            BmpBits = shared.Bits;
            AsciiLow = low;
            AsciiHigh = high;
            BmpBitsIsExact = true;
            SearchValues = shared.Values;
            return;
        }

        if (hasBmp && SharedBmpSets.TryGetValue(new(_intervals, unicodeAware, foldedBmp ? caseTables : null), out var sharedBmp))
        {
            BmpBits = sharedBmp.Bits;
            AsciiLow = sharedBmp.Bits[0];
            AsciiHigh = sharedBmp.Bits[1];
            BmpBitsIsExact = sharedBmp.Exact;
            SearchValues = sharedBmp.Values;
            IsNegatedSearchValues = sharedBmp.Negated;
            UnicodeRunStops = sharedBmp.RunStops;
            StopsAtLineTerminatorsOnly = sharedBmp.StopsAtLineTerminatorsOnly;
            return;
        }

        if (hasBmp)
        {
            var bmp = new ulong[1024];
            var hasSurrogateOrAstral = false;
            var totalBmp = 0;
            foreach (var iv in intervals)
            {
                if (iv.Last >= 0xD800)
                {
                    hasSurrogateOrAstral = true;
                }

                var first = iv.First;
                if (first > 0xFFFF)
                {
                    continue;
                }

                var last = Math.Min(iv.Last, 0xFFFF);
                totalBmp += last - first + 1;
                SetBitRange(bmp, first, last);
            }
            if (foldedBmp)
            {
                // A unit with a mapping is a member exactly when its target is. Only the mappings whose source or
                // target lies in the class can change a bit: clear the sources inside it, then set the sources whose
                // target is inside it. Testing each of the ~1,400 mappings cost more than the rest of the set.
                var bySource = caseTables.LegacyUppercase;
                var byTarget = GetLegacyByTarget(caseTables);
                foreach (var iv in intervals)
                {
                    for (var m = FirstAtOrAfter(bySource, iv.First, byTarget: false); m < bySource.Length && bySource[m].Source <= iv.Last; m++)
                    {
                        var source = bySource[m].Source;
                        if (source <= 0xFFFF)
                        {
                            bmp[source >> 6] &= ~(1UL << (source & 63));
                        }
                    }
                }

                foreach (var iv in intervals)
                {
                    for (var m = FirstAtOrAfter(byTarget, iv.First, byTarget: true); m < byTarget.Length && byTarget[m].Target <= iv.Last; m++)
                    {
                        var source = byTarget[m].Source;
                        if (source <= 0xFFFF)
                        {
                            bmp[source >> 6] |= 1UL << (source & 63);
                        }
                    }
                }

                totalBmp = 0;
                foreach (var word in bmp)
                {
                    totalBmp += BitOperations.PopCount(word);
                }
            }

            if (unicodeAware)
            {
                // Clear surrogate range 0xD800..0xDFFF (words 864..895) so a 1-bit in BmpBits
                // guarantees a non-surrogate BMP scalar of UTF-16 width 1.
                Array.Clear(bmp, 0xD800 >> 6, (0xE000 - 0xD800) >> 6);
            }
            BmpBits = bmp;
            AsciiLow = bmp[0];
            AsciiHigh = bmp[1];
            BmpBitsIsExact = !unicodeAware || !hasSurrogateOrAstral;

            if (unicodeAware && hasSurrogateOrAstral)
            {
                var coversAstral = false;
                foreach (var iv in intervals)
                {
                    if (iv.First <= 0xD800 && iv.Last >= 0x10FFFF)
                    {
                        coversAstral = true;
                    }
                }

                var stops = new List<char>();
                for (var w = 0; coversAstral && w < 0xD800 >> 6 && stops.Count <= 512; w++)
                {
                    for (var missing = ~bmp[w]; missing != 0 && stops.Count <= 512; missing &= missing - 1)
                    {
                        stops.Add((char)((w << 6) + BitOperations.TrailingZeroCount(missing)));
                    }
                }

                if (coversAstral && stops.Count is >= 1 and <= 512)
                {
                    StopsAtLineTerminatorsOnly = AreLineTerminators(stops.ToArray());
                    UnicodeRunStops = System.Buffers.SearchValues.Create(stops.ToArray());
                }
            }

            if (BmpBitsIsExact)
            {
                if (foldedBmp)
                {
                    // The intervals are not the membership here; take the members from the bitmap.
                    if (totalBmp is >= 1 and <= 512 || (65536 - totalBmp) is >= 1 and <= 512)
                    {
                        var negated = totalBmp > 512;
                        var chars = new char[negated ? 65536 - totalBmp : totalBmp];
                        // Walk the set bits of the 1,024 words: testing all 65,536 units one by one was most of the
                        // cost of compiling an i-flag pattern.
                        var idx = 0;
                        for (var w = 0; w < bmp.Length; w++)
                        {
                            for (var bits = negated ? ~bmp[w] : bmp[w]; bits != 0; bits &= bits - 1)
                            {
                                chars[idx++] = (char)((w << 6) + BitOperations.TrailingZeroCount(bits));
                            }
                        }

                        SearchValues = System.Buffers.SearchValues.Create(chars);
                        IsNegatedSearchValues = negated;
                    }
                }
                else if (totalBmp is >= 1 and <= 512)
                {
                    var chars = new char[totalBmp];
                    var idx = 0;
                    foreach (var iv in intervals)
                    {
                        if (iv.First > 0xFFFF)
                        {
                            continue;
                        }

                        var last = Math.Min(iv.Last, 0xFFFF);
                        for (var c = iv.First; c <= last; c++)
                        {
                            chars[idx++] = (char)c;
                        }
                    }
                    SearchValues = System.Buffers.SearchValues.Create(chars);
                    IsNegatedSearchValues = false;
                }
                else if ((65536 - totalBmp) is >= 1 and <= 512)
                {
                    var excludedCount = 65536 - totalBmp;
                    var excluded = new char[excludedCount];
                    var idx = 0;
                    var prev = 0;
                    foreach (var iv in intervals)
                    {
                        var first = Math.Min(iv.First, 0x10000);
                        for (var c = prev; c < first; c++)
                        {
                            excluded[idx++] = (char)c;
                        }

                        if (iv.Last >= 0xFFFF)
                        { prev = 0x10000; break; }
                        prev = iv.Last + 1;
                    }
                    for (var c = prev; c <= 0xFFFF; c++)
                    {
                        excluded[idx++] = (char)c;
                    }

                    StopsAtLineTerminatorsOnly = AreLineTerminators(excluded);
                    SearchValues = System.Buffers.SearchValues.Create(excluded);
                    IsNegatedSearchValues = true;
                }
            }

            if (SharedBmpSets.Count < MaxSharedBmpSets)
            {
                SharedBmpSets.TryAdd(new(_intervals, unicodeAware, foldedBmp ? caseTables : null),
                    new(bmp, BmpBitsIsExact, SearchValues, IsNegatedSearchValues, UnicodeRunStops, StopsAtLineTerminatorsOnly));
            }
        }
    }

    // Classes with non-ASCII members ([^"\\], ., \s) recur across patterns, and each built an 8 KiB bitmap and often a
    // SearchValues per compile. Keyed by the exact intervals, the Unicode mode (which clears the surrogates) and, for a
    // class folded under legacy i, the case tables it was folded with; bounded like SharedAsciiSets, and never written
    // after construction.
    private const int MaxSharedBmpSets = 1024;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<IntervalsKey, BmpSet> SharedBmpSets = new();

    private sealed record BmpSet(ulong[] Bits, bool Exact, SearchValues<char>? Values, bool Negated,
        SearchValues<char>? RunStops, bool StopsAtLineTerminatorsOnly);

    private readonly struct IntervalsKey(EcmaInterval[] intervals, bool unicodeAware, EcmaCaseTables? foldedWith) : IEquatable<IntervalsKey>
    {
        private readonly EcmaInterval[] _intervals = intervals;
        private readonly bool _unicodeAware = unicodeAware;
        private readonly EcmaCaseTables? _foldedWith = foldedWith;

        public bool Equals(IntervalsKey other)
            => _unicodeAware == other._unicodeAware && ReferenceEquals(_foldedWith, other._foldedWith) &&
                _intervals.AsSpan().SequenceEqual(other._intervals);

        public override bool Equals(object? obj) => obj is IntervalsKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = _unicodeAware ? 1 : 0;
            foreach (var iv in _intervals)
            {
                hash = unchecked(hash * 31 + iv.First) * 31 + iv.Last;
            }

            return hash;
        }
    }

    private static bool IsAsciiOnly(ReadOnlySpan<EcmaInterval> intervals)
    {
        foreach (var iv in intervals)
        {
            if (iv.Last >= 128)
            {
                return false;
            }
        }

        return true;
    }

    private sealed record AsciiSet(ulong[] Bits, SearchValues<char>? Values);

    // Bounded, so a script compiling ever-new ASCII classes cannot grow it without limit; a class past the bound gets
    // its own instance. Shared instances are never written after construction.
    private const int MaxSharedAsciiSets = 4096;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(ulong, ulong), AsciiSet> SharedAsciiSets = new();

    private static AsciiSet SharedAsciiSet(ulong low, ulong high)
    {
        if (SharedAsciiSets.TryGetValue((low, high), out var shared))
        {
            return shared;
        }

        var bits = new ulong[1024];
        bits[0] = low;
        bits[1] = high;
        var count = BitOperations.PopCount(low) + BitOperations.PopCount(high);
        SearchValues<char>? values = null;
        if (count > 0)
        {
            var chars = new char[count];
            var idx = 0;
            for (var w = 0; w < 2; w++)
            {
                for (var word = w == 0 ? low : high; word != 0; word &= word - 1)
                {
                    chars[idx++] = (char) ((w << 6) + BitOperations.TrailingZeroCount(word));
                }
            }

            values = System.Buffers.SearchValues.Create(chars);
        }

        shared = new AsciiSet(bits, values);
        return SharedAsciiSets.Count < MaxSharedAsciiSets ? SharedAsciiSets.GetOrAdd((low, high), shared) : shared;
    }

    private static bool AreLineTerminators(ReadOnlySpan<char> chars)
        => chars.Length == 4 && chars.Contains('\n') && chars.Contains('\r') && chars.Contains('\u2028') && chars.Contains('\u2029');

    // The first line terminator. Every one of them is outside 0x0E..0x2027, and an IndexOfAnyExceptInRange for that
    // range runs as fast as IndexOf(char) - twice the speed of a four-value IndexOfAny - so it finds the candidates
    // and each is checked; a span with many units outside the range (tabs, CJK) falls back to the exact search.
    internal static int IndexOfLineTerminator(ReadOnlySpan<char> span, SearchValues<char> lineTerminators)
    {
#if NET8_0_OR_GREATER
        var offset = 0;
        for (var falseHits = 0; falseHits < 8; falseHits++)
        {
            var rel = span.Slice(offset).IndexOfAnyExceptInRange('\u000E', '\u2027');
            if (rel < 0)
            {
                return -1;
            }

            var index = offset + rel;
            if (span[index] is '\n' or '\r' or '\u2028' or '\u2029')
            {
                return index;
            }

            offset = index + 1;
        }

        var tail = span.Slice(offset).IndexOfAny(lineTerminators);
        return tail < 0 ? -1 : offset + tail;
#else
        return span.IndexOfAny(lineTerminators);
#endif
    }

    private static void SetBitRange(ulong[] bits, int first, int last)
    {
        var startWord = first >> 6;
        var endWord = last >> 6;
        var startMask = ulong.MaxValue << (first & 63);
        var endMask = ulong.MaxValue >> (63 - (last & 63));
        if (startWord == endWord)
        {
            bits[startWord] |= startMask & endMask;
            return;
        }
        bits[startWord] |= startMask;
        for (var w = startWord + 1; w < endWord; w++)
        {
            bits[w] = ulong.MaxValue;
        }

        bits[endWord] |= endMask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool MatchesSingleChar(char c)
    {
        if (c < 128)
        {
            return ((c < 64 ? AsciiLow : AsciiHigh) & (1UL << (c & 63))) != 0;
        }

        var bmp = BmpBits;
        return bmp is not null && (bmp[c >> 6] & (1UL << (c & 63))) != 0;
    }

    internal bool IsSubsetOf(EcmaFastSet other)
    {
        if (!BmpBitsIsExact || !other.BmpBitsIsExact || BmpBits is null || other.BmpBits is null)
        {
            return false;
        }

        var a = BmpBits;
        var b = other.BmpBits;
        for (var i = 0; i < 1024; i++)
        {
            if ((a[i] & ~b[i]) != 0)
            {
                return false;
            }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int FindFirst(ReadOnlySpan<char> span, EcmaProgram program)
    {
        if (SearchValues is { } sv)
        {
            return IsNegatedSearchValues ? span.IndexOfAnyExcept(sv) : span.IndexOfAny(sv);
        }

        var bmp = BmpBits;
        if (bmp is not null)
        {
            if (BmpBitsIsExact)
            {
                for (var i = 0; i < span.Length; i++)
                {
                    var c = span[i];
                    if ((bmp[c >> 6] & (1UL << (c & 63))) != 0)
                    {
                        return i;
                    }
                }
                return -1;
            }
            for (var i = 0; i < span.Length; i++)
            {
                var c = span[i];
                if ((bmp[c >> 6] & (1UL << (c & 63))) != 0)
                {
                    return i;
                }

                if (c >= 0xD800 && c <= 0xDFFF && TryMatchSurrogateAt(span, ref i))
                {
                    return i;
                }
            }
            return -1;
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryMatchSurrogateAt(ReadOnlySpan<char> span, ref int i)
    {
        var c = span[i];
        int cp = c;
        var next = i + 1;
        if (char.IsHighSurrogate(c) && next < span.Length && char.IsLowSurrogate(span[next]))
        {
            cp = char.ConvertToUtf32(c, span[next]);
            if (ContainsInterval(_intervals, cp))
            {
                return true;
            }

            i = next;
            return false;
        }
        return ContainsInterval(_intervals, cp);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int CountRun(ReadOnlySpan<char> span, int maxCount, EcmaProgram program, out int unitLength)
    {
        if (SearchValues is { } sv)
        {
            var limit = maxCount >= 0 && maxCount < span.Length ? maxCount : span.Length;
            var slice = span.Slice(0, limit);
            var stop = !IsNegatedSearchValues ? slice.IndexOfAnyExcept(sv) :
                StopsAtLineTerminatorsOnly ? IndexOfLineTerminator(slice, sv) : slice.IndexOfAny(sv);
            unitLength = stop < 0 ? limit : stop;
            return unitLength;
        }
        var bmp = BmpBits!;
        if (BmpBitsIsExact)
        {
            var limit = maxCount >= 0 && maxCount < span.Length ? maxCount : span.Length;
            var i = 0;
            for (; i < limit; i++)
            {
                var c = span[i];
                if ((bmp[c >> 6] & (1UL << (c & 63))) == 0)
                {
                    break;
                }
            }
            unitLength = i;
            return i;
        }
        else if (maxCount < 0)
        {
            var i = 0;
            var count = 0;
            while (i < span.Length)
            {
                var c = span[i];
                if ((bmp[c >> 6] & (1UL << (c & 63))) != 0)
                {
                    i++;
                    count++;
                    continue;
                }
                if (c < 0xD800 || c > 0xDFFF || !TryStepSurrogateRun(span, ref i))
                {
                    break;
                }

                count++;
            }
            unitLength = i;
            return count;
        }
        else
        {
            var i = 0;
            var count = 0;
            while (i < span.Length && count < maxCount)
            {
                var c = span[i];
                if ((bmp[c >> 6] & (1UL << (c & 63))) != 0)
                {
                    i++;
                    count++;
                    continue;
                }
                if (c < 0xD800 || c > 0xDFFF || !TryStepSurrogateRun(span, ref i))
                {
                    break;
                }

                count++;
            }
            unitLength = i;
            return count;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryStepSurrogateRun(ReadOnlySpan<char> span, ref int i)
    {
        var c = span[i];
        int cp = c;
        var step = 1;
        if (char.IsHighSurrogate(c) && i + 1 < span.Length && char.IsLowSurrogate(span[i + 1]))
        {
            cp = char.ConvertToUtf32(c, span[i + 1]);
            step = 2;
        }
        if (!ContainsInterval(_intervals, cp))
        {
            return false;
        }

        i += step;
        return true;
    }

    internal static bool ContainsInterval(ReadOnlySpan<EcmaInterval> intervals, int value)
    {
        int lo = 0, hi = intervals.Length - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            var cur = intervals[mid];
            if (value < cur.First)
            {
                hi = mid - 1;
            }
            else if (value > cur.Last)
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

    // The legacy mappings ordered by target, per case-table instance (the tables are per Unicode version).
    private static readonly ConditionalWeakTable<EcmaCaseTables, EcmaCaseMapping[]> LegacyByTarget = new();

    private static EcmaCaseMapping[] GetLegacyByTarget(EcmaCaseTables caseTables)
        => LegacyByTarget.GetValue(caseTables, static tables =>
        {
            var sorted = tables.LegacyUppercase.ToArray();
            Array.Sort(sorted, static (a, b) => a.Target != b.Target ? a.Target.CompareTo(b.Target) : a.Source.CompareTo(b.Source));
            return sorted;
        });

    // The first mapping whose source (or target) is at least `point`, in mappings sorted by that key.
    private static int FirstAtOrAfter(ReadOnlySpan<EcmaCaseMapping> mappings, int point, bool byTarget)
    {
        int lo = 0, hi = mappings.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >> 1;
            if ((byTarget ? mappings[mid].Target : mappings[mid].Source) < point)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo;
    }

    internal static int Fold(int cp, bool unicodeAware, bool ignoreCase, EcmaCaseTables caseTables)
    {
        if (!ignoreCase)
        {
            return cp;
        }

        var mappings = unicodeAware ? caseTables.UnicodeFold : caseTables.LegacyUppercase;
        int lo = 0, hi = mappings.Length - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            var item = mappings[mid];
            if (cp < item.Source)
            {
                hi = mid - 1;
            }
            else if (cp > item.Source)
            {
                lo = mid + 1;
            }
            else
            {
                return item.Target;
            }
        }
        return cp;
    }
}

internal sealed class EcmaAccelerator
{
    private enum PlanKind
    {
        None,
        Literal,
        LiteralAlternation,
        SingleSetRepeat,
        SetRepeatSuffixLiteral,
        AtomicSequence
    }

    private readonly record struct CaptureSlice(int Group, int RelStart, int RelLength);

    private enum AtomicStepKind { Literal, LiteralAlt, SetRepeat }

    private readonly record struct AtomicStep(
        AtomicStepKind Kind,
        string? Literal,
        string[]? Branches,
        EcmaFastSet? Set,
        EcmaFastSet? HeadSet,
        int Min,
        int Max,
        int CaptureGroup);

    private readonly EcmaProgram _program;
    internal readonly EcmaFastSet?[] FastSets;
    internal readonly bool HasBackreferences;
    internal readonly bool[] NonNullable;

    // Direct whole-pattern plan fields
    private readonly PlanKind _planKind;
    internal bool HasDirectPlan => _planKind != PlanKind.None;
    private readonly bool _hasStartAnchor;
    private readonly bool _startMultiline;
    private readonly bool _hasEndAnchor;
    private readonly bool _endMultiline;
    private readonly string _literal = string.Empty;
    private readonly bool _hasRareLiteralChar;
    private readonly char _literalRareChar;
    private readonly int _literalRareOffset;
    private readonly CaptureSlice[] _literalCaptures = [];
    private readonly bool _literalLeadBoundary;
    private readonly bool _literalTrailBoundary;
    private readonly string[] _altBranches = [];
#if NET9_0_OR_GREATER
    private readonly SearchValues<string>? _altSearchValues;
#else
    // Jint: SearchValues<string> is .NET 9+; downlevel the plan keeps its branch strings and scans them.
    private readonly string[]? _altSearchValues;
#endif
    private readonly SearchValues<char>? _altFirstCharValues;
    private readonly EcmaFastSet? _repeatSet;
    private readonly int _repeatMin;
    private readonly int _repeatMax;
    private readonly int _repeatCaptureGroup;
    private readonly bool _repeatLeadBoundary;
    private readonly AtomicStep[] _atomicSteps = [];
    private readonly int _atomicAnchorStepIndex = -1;
    private readonly int _atomicFixedAnchorOffset = -1;
    private readonly int _atomicMinBeforeAnchor;
    private readonly int _atomicMinAfterAnchor;
    private readonly int _atomicMinTotalLength;

    // The position matcher replacing the VM for a pattern no plan takes (see EcmaPositionMatcher). Built on the first search
    // rather than here: a pattern compiled and never searched, or refused after the build, costs its compile nothing.
    private readonly EcmaCaseTables _caseTables;
    private readonly bool _positionEligible;
    private EcmaPositionMatcher? _position;
    private int _positionState;

    // General VM prefilters
    internal readonly bool HasEndTerminalChar;
    internal readonly char EndTerminalChar;
    internal readonly string? RequiredLiteral;
    internal readonly int RequiredLiteralMinOffset;
    internal readonly string? PrefixLiteral;
    internal readonly EcmaFastSet? FirstSet;
    internal readonly bool StartAnchoredNonMultiline;
    // A superset of the characters that can start a match whose first atom is case-insensitive, or that begins with
    // nullable atoms, which neither PrefixLiteral nor FirstSet covers: without it /ZZQ/i over 64 KiB started the VM
    // at every position.
    internal readonly SearchValues<char>? FirstChars;
    // FirstChars leaves out what a non-multiline ^ branch could start with, so the input's start is always a candidate.
    internal bool FirstCharsTryStart { get; private set; }
#if NET9_0_OR_GREATER
    private SearchValues<string>? _literalSearchValues;

    // The leading fixed strings of every branch (literals and small classes expanded), a superset prefilter for
    // an alternation or a case-insensitive literal run, which FirstChars leaves at nearly every position over a small
    // alphabet: /agggtaaa|tttaccct/i over DNA, /aaaaaaaaaa/i over text.
    internal readonly SearchValues<string>? FirstStrings;
#endif

    // An i-flag literal that no other character canonicalizes with (`@`, `-`, digits) matches exactly what the
    // case-sensitive literal matches, so the planners see it without the flag: they refuse i-flag literals, and an i
    // pattern like /[a-z]+@/i otherwise ran the VM from every position. Only the planners read these nodes.
    private static ReadOnlySpan<EcmaNode> WithoutCaselessIgnoreCase(ReadOnlySpan<EcmaNode> nodes, bool unicodeAware, EcmaCaseTables caseTables)
    {
        EcmaNode[]? copy = null;
        HashSet<int>? cased = null;
        for (var i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n.Kind != EcmaNodeKind.Literal || (n.EffectiveFlags & EcmaScriptFlags.IgnoreCase) == 0)
            {
                continue;
            }

            cased ??= GetCasedPoints(caseTables, unicodeAware);
            if (cased.Contains(n.Value))
            {
                continue;
            }

            copy ??= nodes.ToArray();
            copy[i] = n with { EffectiveFlags = n.EffectiveFlags & ~EcmaScriptFlags.IgnoreCase };
        }

        return copy is null ? nodes : copy;
    }

    // Every source and target of a case mapping, per case-table instance like InverseFolds: rebuilt per compile, the
    // ~2,500-point set cost more than compiling the rest of an i-flag pattern.
    private static readonly ConditionalWeakTable<EcmaCaseTables, HashSet<int>?[]> CasedPoints = new();

    internal static HashSet<int> GetCasedPoints(EcmaCaseTables caseTables, bool unicodeAware)
    {
        var slots = CasedPoints.GetValue(caseTables, static _ => new HashSet<int>?[2]);
        var slot = unicodeAware ? 1 : 0;
        if (Volatile.Read(ref slots[slot]) is { } cached)
        {
            return cached;
        }

        var cased = new HashSet<int>();
        foreach (var mapping in unicodeAware ? caseTables.UnicodeFold : caseTables.LegacyUppercase)
        {
            cased.Add(mapping.Source);
            cased.Add(mapping.Target);
        }

        Interlocked.CompareExchange(ref slots[slot], cased, null);
        return slots[slot]!;
    }

    // Set nodes with the same members and case mode share one EcmaFastSet: each builds an 8 KiB bitmap and possibly a
    // SearchValues, and a pattern such as /[a-z]+@[a-z]+\.[a-z]+/ repeats its classes.
    private static EcmaFastSet GetOrCreateFastSet(EcmaProgram program, EcmaSetSlice slice, EcmaScriptFlags effectiveFlags,
        EcmaCaseTables caseTables, ref List<EcmaFastSet>? built)
    {
        var ignoreCase = (effectiveFlags & EcmaScriptFlags.IgnoreCase) != 0;
        var intervals = program.Intervals.Slice(slice.IntervalOffset, slice.IntervalCount);
        if (slice.StringCount == 0 && built is not null)
        {
            foreach (var candidate in built)
            {
                if (candidate.IgnoreCase == ignoreCase &&
                    program.Intervals.Slice(candidate.Slice.IntervalOffset, candidate.Slice.IntervalCount).SequenceEqual(intervals))
                {
                    return candidate;
                }
            }
        }

        var fastSet = new EcmaFastSet(program, slice, effectiveFlags, caseTables);
        if (slice.StringCount == 0)
        {
            (built ??= []).Add(fastSet);
        }

        return fastSet;
    }

    internal EcmaAccelerator(EcmaProgram program, EcmaCaseTables caseTables)
    {
        _program = program;
        _caseTables = caseTables;
        var nodes = WithoutCaselessIgnoreCase(program.Nodes, program.Profile.IsUnicodeAware, caseTables);
        FastSets = new EcmaFastSet?[nodes.Length];
        NonNullable = new bool[nodes.Length];
        var minLengths = new int[nodes.Length];
        var hasAmbiguousRepeat = new bool[nodes.Length];

        var hasBackref = false;
        // A lone surrogate literal in a Unicode-aware pattern matches a lone surrogate code point only,
        // but the plans below assemble literals into UTF-16 strings, where an escaped lead followed by a raw
        // trail (/\uD83D\uDC38/u spelled with one escape) reads as a valid pair and matches U+1F438.
        var loneSurrogateLiteral = false;
        var unicodeAwareProgram = program.Profile.IsUnicodeAware;
        List<EcmaFastSet>? builtSets = null;
        for (var i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n.Kind == EcmaNodeKind.Backreference)
            {
                hasBackref = true;
            }

            if (unicodeAwareProgram && n.Kind == EcmaNodeKind.Literal && n.Value is >= 0xD800 and <= 0xDFFF)
            {
                loneSurrogateLiteral = true;
            }

            if (n.Kind == EcmaNodeKind.Set)
            {
                FastSets[i] = GetOrCreateFastSet(program, program.Sets[n.Value], n.EffectiveFlags, caseTables, ref builtSets);
            }

            var minLen = n.Kind switch
            {
                EcmaNodeKind.Literal => n.Value > 0xFFFF ? 2 : 1,
                EcmaNodeKind.Set => MinSetLength(program, program.Sets[n.Value]),
                EcmaNodeKind.Sequence => SaturatingAdd(minLengths[n.X], minLengths[n.Y]),
                EcmaNodeKind.Alternative => Math.Min(minLengths[n.X], minLengths[n.Y]),
                EcmaNodeKind.Repeat => SaturatingMul(n.Min, minLengths[n.X]),
                EcmaNodeKind.Capture => minLengths[n.X],
                _ => 0
            };
            minLengths[i] = minLen;
            NonNullable[i] = minLen > 0;

            var ambig = n.Kind switch
            {
                EcmaNodeKind.Sequence or EcmaNodeKind.Alternative => hasAmbiguousRepeat[n.X] || hasAmbiguousRepeat[n.Y],
                EcmaNodeKind.Capture or EcmaNodeKind.Lookahead or EcmaNodeKind.NegativeLookahead or
                    EcmaNodeKind.Lookbehind or EcmaNodeKind.NegativeLookbehind => hasAmbiguousRepeat[n.X],
                EcmaNodeKind.Repeat => n.CaptureCount > 0 || ContainsBranchOrBackref(nodes, n.X),
                EcmaNodeKind.Backreference => true,
                _ => false
            };
            hasAmbiguousRepeat[i] = ambig;
        }
        HasBackreferences = hasBackref;
        if (loneSurrogateLiteral)
        {
            return;
        }

        // Try building direct whole-pattern plans
        if (TryBuildLiteralPlan(nodes, out _hasStartAnchor, out _startMultiline, out _hasEndAnchor, out _endMultiline,
            out _literal, out _hasRareLiteralChar, out _literalRareChar, out _literalRareOffset, out _literalCaptures,
            out _literalLeadBoundary, out _literalTrailBoundary))
        {
            _planKind = PlanKind.Literal;
            return;
        }

        if (TryBuildLiteralAlternationPlan(nodes, out _altBranches, out _altSearchValues, out _altFirstCharValues))
        {
            _planKind = PlanKind.LiteralAlternation;
            return;
        }

        if (TryBuildSingleSetRepeatPlan(nodes, out _hasStartAnchor, out _startMultiline, out _hasEndAnchor, out _endMultiline, out _repeatSet, out _repeatMin, out _repeatMax, out _repeatCaptureGroup, out _repeatLeadBoundary))
        {
            _planKind = PlanKind.SingleSetRepeat;
            return;
        }

        if (TryBuildSetRepeatSuffixLiteralPlan(nodes, out _repeatSet, out _repeatMin, out _literal))
        {
            _planKind = PlanKind.SetRepeatSuffixLiteral;
            return;
        }

        if (TryBuildAtomicSequencePlan(nodes, out _hasStartAnchor, out _startMultiline, out _hasEndAnchor, out _endMultiline,
            out _atomicSteps, out _atomicAnchorStepIndex, out _atomicFixedAnchorOffset, out _atomicMinBeforeAnchor, out _atomicMinAfterAnchor, out _atomicMinTotalLength))
        {
            _planKind = PlanKind.AtomicSequence;
            return;
        }

        // General VM prefilters
        var root = program.Root;
        if (nodes[root].Kind == EcmaNodeKind.Sequence)
        {
            var right = nodes[nodes[root].Y];
            if (right.Kind == EcmaNodeKind.End && (right.EffectiveFlags & EcmaScriptFlags.Multiline) == 0 &&
                TryGetTrailingChar(nodes, nodes[root].X, out var termChar))
            {
                HasEndTerminalChar = true;
                EndTerminalChar = termChar;
            }
        }

        if (TryExtractPrefixInfo(nodes, root, out var startNonMl, out var prefixLit, out var firstSet))
        {
            StartAnchoredNonMultiline = startNonMl;
            PrefixLiteral = prefixLit;
            FirstSet = firstSet;
        }

        if (!StartAnchoredNonMultiline && PrefixLiteral is null && FirstSet is null)
        {
#if NET9_0_OR_GREATER
            FirstStrings = TryBuildFirstStrings(nodes, root);
#endif
            FirstChars = TryBuildFirstChars(nodes, root, caseTables);
        }

        if (TryExtractRequiredLiteral(nodes, root, minLengths, hasAmbiguousRepeat, out var reqLit, out var reqMinOffset))
        {
            RequiredLiteral = reqLit;
            RequiredLiteralMinOffset = reqMinOffset;
        }

        _positionEligible = EcmaPositionMatcher.MayApply(program, nodes);
    }

    internal EcmaPositionMatcher? PositionMatcher
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (!_positionEligible)
            {
                return null;
            }

            var built = Volatile.Read(ref _positionState);
            return built != 0 ? _position : BuildPositionMatcher();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private EcmaPositionMatcher? BuildPositionMatcher()
    {
        // Deterministic, so threads racing here build equal matchers and either may win.
        var nodes = WithoutCaselessIgnoreCase(_program.Nodes, _program.Profile.IsUnicodeAware, _caseTables);
        _position = EcmaPositionMatcher.TryCreate(_program, nodes, FastSets, _caseTables);
        Volatile.Write(ref _positionState, 1);
        return _position;
    }

    private static int MinSetLength(EcmaProgram program, EcmaSetSlice slice)
    {
        var min = int.MaxValue;
        var intervals = program.Intervals.Slice(slice.IntervalOffset, slice.IntervalCount);
        if (!intervals.IsEmpty)
        {
            min = intervals[0].First > 0xFFFF ? 2 : 1;
        }

        var strings = program.Strings.Slice(slice.StringOffset, slice.StringCount);
        var points = program.StringCodePoints;
        foreach (var s in strings)
        {
            var len = 0;
            for (var k = 0; k < s.Length; k++)
            {
                len += points[s.Offset + k] > 0xFFFF ? 2 : 1;
            }

            if (len < min)
            {
                min = len;
            }
        }
        return min == int.MaxValue ? 1 : min;
    }

    private static int SaturatingAdd(int a, int b) => (long)a + b > int.MaxValue ? int.MaxValue : a + b;
    private static int SaturatingMul(int a, int b) => (long)a * b > int.MaxValue ? int.MaxValue : a * b;

    private static bool ContainsBranchOrBackref(ReadOnlySpan<EcmaNode> nodes, int root)
    {
        for (var i = 0; i <= root; i++)
        {
            var k = nodes[i].Kind;
            if (k is EcmaNodeKind.Alternative or EcmaNodeKind.Backreference or EcmaNodeKind.Repeat or
                EcmaNodeKind.Lookahead or EcmaNodeKind.NegativeLookahead or EcmaNodeKind.Lookbehind or EcmaNodeKind.NegativeLookbehind)
            {
                if (IsDescendant(nodes, root, i))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsDescendant(ReadOnlySpan<EcmaNode> nodes, int root, int target)
    {
        if (root == target)
        {
            return true;
        }

        if (root < target)
        {
            return false;
        }

        var n = nodes[root];
        return (n.X >= target && IsDescendant(nodes, n.X, target)) ||
               (n.Y >= target && IsDescendant(nodes, n.Y, target));
    }

    private bool IsUnicodeSafeLiteral(string s)
    {
        if (!_program.Profile.IsUnicodeAware || s.Length == 0)
        {
            return true;
        }

        if (char.IsLowSurrogate(s[0]) || char.IsHighSurrogate(s[^1]))
        {
            return false;
        }

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1]))
                {
                    return false;
                }

                i++;
            }
            else if (char.IsLowSurrogate(c))
            {
                return false;
            }
        }
        return true;
    }

    private static int GetCharRarityScore(char c)
    {
        if (c >= 0x80)
        {
            return 100;
        }

        if (c is 'Z' or 'Q' or 'X' or 'J' or 'K' or 'V')
        {
            return 95;
        }

        if (c is >= 'A' and <= 'Y')
        {
            return 85;
        }

        if (c is 'z' or 'q' or 'x' or 'j' or 'k' or 'v')
        {
            return 80;
        }

        if (c is >= '0' and <= '9')
        {
            return 75;
        }

        if (c is '=' or '@' or '#' or '$' or '%' or '^' or '&' or '*' or '+' or '?' or '~' or '|' or '\\' or '/')
        {
            return 70;
        }

        if (c is '_' or ':' or ';' or '<' or '>' or '[' or ']' or '{' or '}' or '(' or ')')
        {
            return 40;
        }

        if (c is '-' or '.' or ',' or '\'' or '"')
        {
            return 20;
        }

        if (c is ' ' or '\n' or '\r' or '\t')
        {
            return 0;
        }

        return 10; // ordinary lowercase letters
    }

    private static int ScoreAnchorLiteral(string lit)
    {
        var bestCharScore = 0;
        for (var i = 0; i < lit.Length; i++)
        {
            var s = GetCharRarityScore(lit[i]);
            if (s > bestCharScore)
            {
                bestCharScore = s;
            }
        }
        return bestCharScore + lit.Length * 15;
    }

    private bool TryBuildLiteralPlan(
        ReadOnlySpan<EcmaNode> nodes,
        out bool hasStart, out bool startMl,
        out bool hasEnd, out bool endMl,
        out string literal,
        out bool hasRareChar, out char rareChar, out int rareOffset,
        out CaptureSlice[] captures,
        out bool leadBoundary, out bool trailBoundary)
    {
        leadBoundary = false;
        trailBoundary = false;
        hasStart = false;
        startMl = false;
        hasEnd = false;
        endMl = false;
        literal = string.Empty;
        hasRareChar = false;
        rareChar = '\0';
        rareOffset = 0;
        captures = [];

        var sb = new ValueStringBuilder();
        string text;
        var caps = new List<CaptureSlice>();
        var seenChar = false;
        try
        {
            // \b on either side of the literal (/\bkeyword\b/, the packer's replace) is checked at each hit instead of
            // starting the VM there. Outside u+i a word character is [A-Za-z0-9_], which the check reads directly.
            var items = new List<int>();
            FlattenSequence(nodes, _program.Root, items);
            var first = 0;
            var last = items.Count - 1;
            if (items.Count > 1 && IsPlainWordBoundary(nodes[items[0]]))
            {
                leadBoundary = true;
                first++;
            }

            if (last > first && IsPlainWordBoundary(nodes[items[last]]))
            {
                trailBoundary = true;
                last--;
            }

            for (var i = first; i <= last; i++)
            {
                if (!WalkLiteralNode(nodes, items[i], ref sb, caps, ref hasStart, ref startMl, ref hasEnd, ref endMl, ref seenChar))
                {
                    return false;
                }
            }

            if ((leadBoundary || trailBoundary) && (hasStart || hasEnd || sb.Length == 0))
            {
                return false;
            }

            text = sb.ToString();
        }
        finally
        {
            sb.Dispose();
        }

        if (!IsUnicodeSafeLiteral(text))
        {
            return false;
        }

        literal = text;
        captures = caps.ToArray();

        if (text.Length >= 4)
        {
            var bestScore = 0;
            var bestIdx = -1;
            for (var i = 0; i < text.Length; i++)
            {
                var score = GetCharRarityScore(text[i]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }
            if (bestScore >= 70 && bestIdx >= 0)
            {
                hasRareChar = true;
                rareChar = text[bestIdx];
                rareOffset = bestIdx;
            }
        }
        return true;
    }

    private bool IsPlainWordBoundary(EcmaNode n)
        => n.Kind == EcmaNodeKind.WordBoundary &&
           !(_program.Profile.IsUnicodeAware && (n.EffectiveFlags & EcmaScriptFlags.IgnoreCase) != 0);

    private static bool IsAsciiWordChar(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    private static bool WalkLiteralNode(
        ReadOnlySpan<EcmaNode> nodes, int id,
        ref ValueStringBuilder sb, List<CaptureSlice> caps,
        ref bool hasStart, ref bool startMl,
        ref bool hasEnd, ref bool endMl,
        ref bool seenChar)
    {
        var n = nodes[id];
        switch (n.Kind)
        {
            case EcmaNodeKind.Empty:
                return true;
            case EcmaNodeKind.Start:
                if (seenChar || hasEnd || hasStart)
                {
                    return false;
                }

                hasStart = true;
                startMl = (n.EffectiveFlags & EcmaScriptFlags.Multiline) != 0;
                return true;
            case EcmaNodeKind.End:
                if (hasEnd)
                {
                    return false;
                }

                hasEnd = true;
                endMl = (n.EffectiveFlags & EcmaScriptFlags.Multiline) != 0;
                return true;
            case EcmaNodeKind.Literal:
                if (hasEnd || (n.EffectiveFlags & EcmaScriptFlags.IgnoreCase) != 0)
                {
                    return false;
                }

                seenChar = true;
                sb.AppendScalar(n.Value);
                return true;
            case EcmaNodeKind.Capture:
                if (hasEnd)
                {
                    return false;
                }

                var capStart = sb.Length;
                if (!WalkLiteralNode(nodes, n.X, ref sb, caps, ref hasStart, ref startMl, ref hasEnd, ref endMl, ref seenChar))
                {
                    return false;
                }

                if (hasEnd)
                {
                    return false;
                }

                caps.Add(new(n.Value, capStart, sb.Length - capStart));
                return true;
            case EcmaNodeKind.Sequence:
                return WalkLiteralNode(nodes, n.X, ref sb, caps, ref hasStart, ref startMl, ref hasEnd, ref endMl, ref seenChar) &&
                       WalkLiteralNode(nodes, n.Y, ref sb, caps, ref hasStart, ref startMl, ref hasEnd, ref endMl, ref seenChar);
            default:
                return false;
        }
    }

    private bool TryBuildLiteralAlternationPlan(
        ReadOnlySpan<EcmaNode> nodes,
        out string[] branches,
#if NET9_0_OR_GREATER
        out SearchValues<string>? searchValues,
#else
        out string[]? searchValues,
#endif
        out SearchValues<char>? firstCharValues)
    {
        branches = [];
        searchValues = null;
        firstCharValues = null;
        if (_program.GroupCount != 1)
        {
            return false;
        }

        if (nodes[_program.Root].Kind != EcmaNodeKind.Alternative)
        {
            return false;
        }

        var list = new List<string>();
        if (!CollectAltBranches(nodes, _program.Root, list) || list.Count < 2)
        {
            return false;
        }

        var allRareFirst = true;
        Span<char> distinctFirst = stackalloc char[5];
        var distinctCount = 0;

        foreach (var b in list)
        {
            if (b.Length == 0 || !IsUnicodeSafeLiteral(b))
            {
                return false;
            }

            if (allRareFirst)
            {
                var c0 = b[0];
                if (b.Length < 2 || GetCharRarityScore(c0) < 70)
                {
                    allRareFirst = false;
                }
                else
                {
                    var found = false;
                    for (var k = 0; k < distinctCount; k++)
                    {
                        if (distinctFirst[k] == c0)
                        { found = true; break; }
                    }
                    if (!found)
                    {
                        if (distinctCount < 5)
                        {
                            distinctFirst[distinctCount++] = c0;
                        }
                        else
                        {
                            allRareFirst = false;
                        }
                    }
                }
            }
        }
        branches = list.ToArray();
#if NET9_0_OR_GREATER
        searchValues = SearchValues.Create(branches, StringComparison.Ordinal);
#else
        searchValues = branches;
#endif
        if (allRareFirst && distinctCount is >= 1 and <= 5)
        {
            firstCharValues = SearchValues.Create(distinctFirst.Slice(0, distinctCount));
        }

        return true;
    }

#if !NET9_0_OR_GREATER
    private static int IndexOfAnyBranch(ReadOnlySpan<char> span, string[] branches)
    {
        var best = -1;
        foreach (var branch in branches)
        {
            var limit = best < 0 ? span : span.Slice(0, Math.Min(span.Length, best + branch.Length));
            var index = limit.IndexOf(branch.AsSpan(), StringComparison.Ordinal);
            if (index >= 0 && (best < 0 || index < best))
            {
                best = index;
            }
        }

        return best;
    }

#endif
    private static bool CollectAltBranches(ReadOnlySpan<EcmaNode> nodes, int id, List<string> branches)
    {
        var n = nodes[id];
        if (n.Kind == EcmaNodeKind.Alternative)
        {
            return CollectAltBranches(nodes, n.X, branches) && CollectAltBranches(nodes, n.Y, branches);
        }

        var sb = new ValueStringBuilder();
        try
        {
            if (!CollectPureLiteral(nodes, id, ref sb) || sb.Length == 0)
            {
                return false;
            }

            branches.Add(sb.ToString());
            return true;
        }
        finally
        {
            sb.Dispose();
        }
    }

    private static bool CollectPureLiteral(ReadOnlySpan<EcmaNode> nodes, int id, ref ValueStringBuilder sb)
    {
        var n = nodes[id];
        switch (n.Kind)
        {
            case EcmaNodeKind.Literal:
                if ((n.EffectiveFlags & EcmaScriptFlags.IgnoreCase) != 0)
                {
                    return false;
                }

                sb.AppendScalar(n.Value);
                return true;
            case EcmaNodeKind.Sequence:
                return CollectPureLiteral(nodes, n.X, ref sb) && CollectPureLiteral(nodes, n.Y, ref sb);
            default:
                return false;
        }
    }

    private bool TryBuildSingleSetRepeatPlan(
        ReadOnlySpan<EcmaNode> nodes,
        out bool hasStart, out bool startMl,
        out bool hasEnd, out bool endMl,
        out EcmaFastSet? fastSet, out int min, out int max, out int capGroup, out bool leadBoundary)
    {
        hasStart = false;
        startMl = false;
        hasEnd = false;
        endMl = false;
        fastSet = null;
        min = 0;
        max = 0;
        capGroup = 0;
        leadBoundary = false;

        var items = new List<int>();
        FlattenSequence(nodes, _program.Root, items);
        // \b\w+\b (the packer's word replace): an unbounded run of exactly the word class ends at a boundary by
        // itself, and starts at one unless the search starts inside a run, which the leading \b alone has to skip.
        var trailBoundary = items.Count > 1 && IsPlainWordBoundary(nodes[items[^1]]);
        if (trailBoundary)
        {
            items.RemoveAt(items.Count - 1);
        }

        if (items.Count > 1 && IsPlainWordBoundary(nodes[items[0]]))
        {
            leadBoundary = true;
            items.RemoveAt(0);
        }

        var idx = 0;
        if (idx < items.Count && nodes[items[idx]].Kind == EcmaNodeKind.Start)
        {
            var sNode = nodes[items[idx++]];
            if ((sNode.EffectiveFlags & EcmaScriptFlags.Multiline) != 0)
            {
                return false;
            }

            hasStart = true;
        }
        if (idx >= items.Count)
        {
            return false;
        }

        var coreId = items[idx++];
        if (nodes[coreId].Kind == EcmaNodeKind.Capture)
        {
            capGroup = nodes[coreId].Value;
            coreId = nodes[coreId].X;
        }
        var rep = nodes[coreId];
        if (rep.Kind != EcmaNodeKind.Repeat || rep.Lazy || rep.Min < 1)
        {
            return false;
        }

        var setNode = nodes[rep.X];
        if (setNode.Kind != EcmaNodeKind.Set)
        {
            return false;
        }

        var fs = FastSets[rep.X];
        if (fs is null || fs.BmpBits is null || fs.Slice.StringCount != 0)
        {
            return false;
        }

        if (idx < items.Count && nodes[items[idx]].Kind == EcmaNodeKind.End)
        {
            var eNode = nodes[items[idx++]];
            if ((eNode.EffectiveFlags & EcmaScriptFlags.Multiline) != 0)
            {
                return false;
            }

            hasEnd = true;
        }
        if (idx != items.Count)
        {
            return false;
        }

        if (hasStart && !hasEnd)
        {
            return false; // ^Set+ without $ is handled by AtomicSequencePlan
        }

        if (!hasStart && hasEnd)
        {
            return false;
        }

        if ((leadBoundary || trailBoundary) && (hasStart || rep.Max != -1 || !IsExactlyAsciiWordClass(fs)))
        {
            return false;
        }

        fastSet = fs;
        min = rep.Min;
        max = rep.Max;
        return true;
    }

    private static bool IsExactlyAsciiWordClass(EcmaFastSet fs)
    {
        const ulong WordLow = 0x03FF000000000000UL;      // 0-9
        const ulong WordHigh = 0x07FFFFFE87FFFFFEUL;     // A-Z, _, a-z
        if (!fs.BmpBitsIsExact || fs.BmpBits is not { } bmp || fs.AsciiLow != WordLow || fs.AsciiHigh != WordHigh)
        {
            return false;
        }

        for (var i = 2; i < bmp.Length; i++)
        {
            if (bmp[i] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryBuildSetRepeatSuffixLiteralPlan(
        ReadOnlySpan<EcmaNode> nodes,
        out EcmaFastSet? fastSet, out int min, out string suffixLiteral)
    {
        fastSet = null;
        min = 0;
        suffixLiteral = string.Empty;
        if (_program.GroupCount != 1)
        {
            return false;
        }

        var items = new List<int>();
        FlattenSequence(nodes, _program.Root, items);
        if (items.Count < 2)
        {
            return false;
        }

        var rep = nodes[items[0]];
        if (rep.Kind != EcmaNodeKind.Repeat || rep.Lazy || rep.Min < 1 || rep.Max != -1)
        {
            return false;
        }

        if (nodes[rep.X].Kind != EcmaNodeKind.Set)
        {
            return false;
        }

        var fs = FastSets[rep.X];
        if (fs is null || !fs.BmpBitsIsExact)
        {
            return false;
        }

        var sb = new ValueStringBuilder();
        string lit;
        try
        {
            for (var i = 1; i < items.Count; i++)
            {
                if (!CollectPureLiteral(nodes, items[i], ref sb))
                {
                    return false;
                }
            }
            if (sb.Length == 0)
            {
                return false;
            }

            lit = sb.ToString();
        }
        finally
        {
            sb.Dispose();
        }

        if (!IsUnicodeSafeLiteral(lit))
        {
            return false;
        }

        // If the first char of lit is NOT in fs, AtomicSequencePlan handles it even more generally.
        // SetRepeatSuffixLiteralPlan handles the overlapping case where all chars of lit ARE in fs (e.g. [a-zA-Z]+ing).
        for (var i = 0; i < lit.Length; i++)
        {
            if (!fs.MatchesSingleChar(lit[i]))
            {
                return false;
            }
        }

        fastSet = fs;
        min = rep.Min;
        suffixLiteral = lit;
        return true;
    }

    private bool TryBuildAtomicSequencePlan(
        ReadOnlySpan<EcmaNode> nodes,
        out bool hasStart, out bool startMl,
        out bool hasEnd, out bool endMl,
        out AtomicStep[] steps,
        out int anchorStepIndex,
        out int fixedAnchorOffset,
        out int minBeforeAnchor,
        out int minAfterAnchor,
        out int minTotalLength)
    {
        hasStart = false;
        startMl = false;
        hasEnd = false;
        endMl = false;
        steps = [];
        anchorStepIndex = -1;
        fixedAnchorOffset = -1;
        minBeforeAnchor = 0;
        minAfterAnchor = 0;
        minTotalLength = 0;

        var items = new List<int>();
        FlattenSequence(nodes, _program.Root, items);
        if (items.Count == 0)
        {
            return false;
        }

        var idx = 0;
        if (nodes[items[idx]].Kind == EcmaNodeKind.Start)
        {
            var sNode = nodes[items[idx++]];
            if ((sNode.EffectiveFlags & EcmaScriptFlags.Multiline) != 0)
            {
                return false;
            }

            hasStart = true;
        }
        if (idx < items.Count && nodes[items[^1]].Kind == EcmaNodeKind.End)
        {
            var eNode = nodes[items[^1]];
            if ((eNode.EffectiveFlags & EcmaScriptFlags.Multiline) != 0)
            {
                return false;
            }

            hasEnd = true;
            items.RemoveAt(items.Count - 1);
        }
        if (idx >= items.Count)
        {
            return false;
        }

        var parsed = new List<AtomicStep>();
        var litBuf = new ValueStringBuilder();
        try
        {
            for (; idx < items.Count; idx++)
            {
                var itemId = items[idx];
                var n = nodes[itemId];
                if (n.Kind == EcmaNodeKind.Literal)
                {
                    if ((n.EffectiveFlags & EcmaScriptFlags.IgnoreCase) != 0)
                    {
                        return false;
                    }

                    litBuf.AppendScalar(n.Value);
                    continue;
                }

                if (litBuf.Length > 0)
                {
                    var s = litBuf.ToStringAndClear();
                    parsed.Add(new(AtomicStepKind.Literal, s, null, null, null, s.Length, s.Length, 0));
                }

                var capGroup = 0;
                if (n.Kind == EcmaNodeKind.Capture)
                {
                    capGroup = n.Value;
                    itemId = n.X;
                    n = nodes[itemId];
                    // Captured literal, or HeadSet + TailSet* sequence (e.g. ([A-Za-z_][A-Za-z0-9_]*))
                    if (n.Kind is EcmaNodeKind.Literal or EcmaNodeKind.Sequence)
                    {
                        var subSb = new ValueStringBuilder();
                        try
                        {
                            if (CollectPureLiteral(nodes, itemId, ref subSb) && subSb.Length > 0)
                            {
                                var s = subSb.ToString();
                                if (!IsUnicodeSafeLiteral(s))
                                {
                                    return false;
                                }

                                parsed.Add(new(AtomicStepKind.Literal, s, null, null, null, s.Length, s.Length, capGroup));
                                continue;
                            }
                        }
                        finally
                        {
                            subSb.Dispose();
                        }
                        if (n.Kind == EcmaNodeKind.Sequence && nodes[n.X].Kind == EcmaNodeKind.Set && nodes[n.Y].Kind == EcmaNodeKind.Repeat)
                        {
                            var repY = nodes[n.Y];
                            if (!repY.Lazy && repY.Min == 0 && repY.Max == -1 && repY.CaptureCount == 0 &&
                                nodes[repY.X].Kind == EcmaNodeKind.Set)
                            {
                                var fsHead = FastSets[n.X];
                                var fsTail = FastSets[repY.X];
                                if (fsHead is not null && fsTail is not null && fsHead.IsSubsetOf(fsTail))
                                {
                                    parsed.Add(new(AtomicStepKind.SetRepeat, null, null, fsTail, fsHead, 1, -1, capGroup));
                                    continue;
                                }
                            }
                        }
                        return false;
                    }
                    if (n.Kind == EcmaNodeKind.Alternative)
                    {
                        var altList = new List<string>();
                        if (!CollectAltBranches(nodes, itemId, altList) || altList.Count < 2)
                        {
                            return false;
                        }

                        int minB = int.MaxValue, maxB = 0;
                        foreach (var b in altList)
                        {
                            if (!IsUnicodeSafeLiteral(b))
                            {
                                return false;
                            }

                            if (b.Length < minB)
                            {
                                minB = b.Length;
                            }

                            if (b.Length > maxB)
                            {
                                maxB = b.Length;
                            }
                        }
                        parsed.Add(new(AtomicStepKind.LiteralAlt, null, altList.ToArray(), null, null, minB, maxB, capGroup));
                        continue;
                    }
                }

                if (n.Kind == EcmaNodeKind.Set)
                {
                    var fs = FastSets[itemId];
                    if (fs is null || !fs.BmpBitsIsExact)
                    {
                        return false;
                    }

                    parsed.Add(new(AtomicStepKind.SetRepeat, null, null, fs, null, 1, 1, capGroup));
                }
                else if (n.Kind == EcmaNodeKind.Repeat)
                {
                    // A nullable repeat (\s*) is as atomic as a non-nullable one under the same rule: it must be last, or
                    // followed by a literal whose first character it cannot match, so its run is always taken whole.
                    // Refusing it left split(/\s*,\s*/) on the VM at every candidate.
                    if (n.Lazy || n.CaptureCount > 0)
                    {
                        return false;
                    }

                    if (nodes[n.X].Kind != EcmaNodeKind.Set)
                    {
                        return false;
                    }

                    var fs = FastSets[n.X];
                    if (fs is null || !fs.BmpBitsIsExact)
                    {
                        return false;
                    }

                    parsed.Add(new(AtomicStepKind.SetRepeat, null, null, fs, null, n.Min, n.Max, capGroup));
                }
                else if (n.Kind == EcmaNodeKind.Alternative)
                {
                    var altList = new List<string>();
                    if (!CollectAltBranches(nodes, itemId, altList) || altList.Count < 2)
                    {
                        return false;
                    }

                    int minB = int.MaxValue, maxB = 0;
                    foreach (var b in altList)
                    {
                        if (!IsUnicodeSafeLiteral(b))
                        {
                            return false;
                        }

                        if (b.Length < minB)
                        {
                            minB = b.Length;
                        }

                        if (b.Length > maxB)
                        {
                            maxB = b.Length;
                        }
                    }
                    parsed.Add(new(AtomicStepKind.LiteralAlt, null, altList.ToArray(), null, null, minB, maxB, capGroup));
                }
                else
                {
                    return false;
                }
            }
            if (litBuf.Length > 0)
            {
                var s = litBuf.ToStringAndClear();
                parsed.Add(new(AtomicStepKind.Literal, s, null, null, null, s.Length, s.Length, 0));
            }
        }
        finally
        {
            litBuf.Dispose();
        }

        if (parsed.Count == 0)
        {
            return false;
        }

        // Verify non-overlapping / atomic property of each step
        for (var i = 0; i < parsed.Count; i++)
        {
            var step = parsed[i];
            if (step.Kind == AtomicStepKind.Literal && !IsUnicodeSafeLiteral(step.Literal!))
            {
                return false;
            }

            if (step.Kind == AtomicStepKind.SetRepeat && step.Min != step.Max)
            {
                if (i == parsed.Count - 1)
                {
                    if (step.HeadSet is not null && i != 0)
                    {
                        return false;
                    }

                    continue;
                }
                if (step.HeadSet is not null && i != 0)
                {
                    return false;
                }

                var next = parsed[i + 1];
                if (next.Kind != AtomicStepKind.Literal)
                {
                    return false;
                }

                var nextFirst = next.Literal![0];
                if (step.Set!.MatchesSingleChar(nextFirst))
                {
                    return false;
                }
            }
            else if (step.Kind == AtomicStepKind.LiteralAlt)
            {
                // Admit LiteralAlt only as Step 0 followed by a Literal step whose first char does not occur in any branch
                if (i != 0 || parsed.Count < 2 || parsed[1].Kind != AtomicStepKind.Literal)
                {
                    return false;
                }

                var anchorFirst = parsed[1].Literal![0];
                foreach (var b in step.Branches!)
                {
                    if (b.AsSpan().IndexOf(anchorFirst) >= 0)
                    {
                        return false;
                    }
                }
            }
        }

        var totalMinWide = 0L;
        for (var i = 0; i < parsed.Count; i++)
        {
            totalMinWide += parsed[i].Min;
        }

        // The offsets below are int sums of step minimums; /.{2147483647}11/ wrapped them negative and
        // the scan sliced past the input. No subject can satisfy such a length, so leave it to the VM.
        if (totalMinWide > int.MaxValue / 2)
        {
            return false;
        }

        var totalMin = (int)totalMinWide;
        minTotalLength = totalMin;

        // Choose anchor strategy if not start-anchored
        if (!hasStart)
        {
            var allFixedBefore = true;
            var fixedOff = 0;
            var bestFixedStep = -1;
            var bestFixedOff = 0;
            var bestFixedScore = int.MinValue;
            for (var i = 0; i < parsed.Count; i++)
            {
                var st = parsed[i];
                if (st.Kind == AtomicStepKind.Literal)
                {
                    if (allFixedBefore)
                    {
                        var score = ScoreAnchorLiteral(st.Literal!);
                        if (score > bestFixedScore)
                        {
                            bestFixedScore = score;
                            bestFixedStep = i;
                            bestFixedOff = fixedOff;
                        }
                    }
                    else if (i == 1 && bestFixedStep < 0)
                    {
                        anchorStepIndex = 1;
                        fixedAnchorOffset = -1;
                        minBeforeAnchor = parsed[0].Min;
                        var after = 0;
                        for (var j = 2; j < parsed.Count; j++)
                        {
                            after += parsed[j].Min;
                        }

                        minAfterAnchor = after;
                        break;
                    }
                }
                if (st.Min != st.Max)
                {
                    allFixedBefore = false;
                }
                else
                {
                    fixedOff += st.Min;
                }
            }
            if (bestFixedStep >= 0)
            {
                anchorStepIndex = bestFixedStep;
                fixedAnchorOffset = bestFixedOff;
                minBeforeAnchor = bestFixedOff;
                var after = 0;
                for (var j = bestFixedStep + 1; j < parsed.Count; j++)
                {
                    after += parsed[j].Min;
                }

                minAfterAnchor = after;
            }
            if (anchorStepIndex < 0)
            {
                return false;
            }
        }

        steps = parsed.ToArray();
        return true;
    }

    private static void FlattenSequence(ReadOnlySpan<EcmaNode> nodes, int id, List<int> items)
    {
        var n = nodes[id];
        if (n.Kind == EcmaNodeKind.Sequence)
        {
            FlattenSequence(nodes, n.X, items);
            FlattenSequence(nodes, n.Y, items);
        }
        else
        {
            items.Add(id);
        }
    }

    private static bool TryGetTrailingChar(ReadOnlySpan<EcmaNode> nodes, int id, out char ch)
    {
        var n = nodes[id];
        switch (n.Kind)
        {
            case EcmaNodeKind.Literal:
                if ((n.EffectiveFlags & EcmaScriptFlags.IgnoreCase) == 0 && n.Value <= 0xFFFF)
                {
                    ch = (char)n.Value;
                    return true;
                }
                ch = default;
                return false;
            case EcmaNodeKind.Capture:
                return TryGetTrailingChar(nodes, n.X, out ch);
            case EcmaNodeKind.Repeat:
                if (n.Min >= 1)
                {
                    return TryGetTrailingChar(nodes, n.X, out ch);
                }

                ch = default;
                return false;
            case EcmaNodeKind.Sequence:
                return TryGetTrailingChar(nodes, n.Y, out ch);
            case EcmaNodeKind.Alternative:
                if (TryGetTrailingChar(nodes, n.X, out var left) &&
                    TryGetTrailingChar(nodes, n.Y, out var right) && left == right)
                {
                    ch = left;
                    return true;
                }
                ch = default;
                return false;
            default:
                ch = default;
                return false;
        }
    }

    private bool TryExtractPrefixInfo(
        ReadOnlySpan<EcmaNode> nodes, int root,
        out bool startAnchoredNonMl,
        out string? prefixLit,
        out EcmaFastSet? firstSet)
    {
        startAnchoredNonMl = false;
        prefixLit = null;
        firstSet = null;

        var items = new List<int>();
        FlattenSequence(nodes, root, items);
        if (items.Count == 0)
        {
            return false;
        }

        var firstId = items[0];
        while (nodes[firstId].Kind == EcmaNodeKind.Capture)
        {
            firstId = nodes[firstId].X;
        }

        var first = nodes[firstId];
        if (first.Kind == EcmaNodeKind.Start && (first.EffectiveFlags & EcmaScriptFlags.Multiline) == 0)
        {
            startAnchoredNonMl = true;
            return true;
        }

        using var sb = new ValueStringBuilder();
        for (var i = 0; i < items.Count; i++)
        {
            var cur = items[i];
            while (nodes[cur].Kind == EcmaNodeKind.Capture)
            {
                cur = nodes[cur].X;
            }

            var n = nodes[cur];
            if (n.Kind == EcmaNodeKind.Literal && (n.EffectiveFlags & EcmaScriptFlags.IgnoreCase) == 0)
            {
                sb.AppendScalar(n.Value);
            }
            else
            {
                break;
            }
        }
        if (sb.Length > 0)
        {
            var s = sb.ToString();
            if (IsUnicodeSafeLiteral(s))
            {
                prefixLit = s;
                return true;
            }
        }

        if (first.Kind == EcmaNodeKind.Set)
        {
            var fs = FastSets[firstId];
            if (fs is not null && fs.BmpBitsIsExact)
            {
                firstSet = fs;
                return true;
            }
        }
        else if (first.Kind == EcmaNodeKind.Repeat && first.Min >= 1 && nodes[first.X].Kind == EcmaNodeKind.Set)
        {
            var fs = FastSets[first.X];
            if (fs is not null && fs.BmpBitsIsExact)
            {
                firstSet = fs;
                return true;
            }
        }
        return false;
    }

#if NET9_0_OR_GREATER
    private const int MaxStringsPerBranch = 64;
    private const int MaxFirstStrings = 256;

    private SearchValues<string>? TryBuildFirstStrings(ReadOnlySpan<EcmaNode> nodes, int root)
    {
        var unicodeAware = _program.Profile.IsUnicodeAware;
        var branches = new List<int>();
        CollectBranches(nodes, root, branches);
        var ignoreCase = false;
        var all = new List<string>();
        var items = new List<int>();
        var members = new List<int>();
        foreach (var branch in branches)
        {
            items.Clear();
            FlattenSequence(nodes, branch, items);
            var prefixes = new List<string> { string.Empty };
            foreach (var item in items)
            {
                var id = item;
                while (nodes[id].Kind == EcmaNodeKind.Capture)
                {
                    id = nodes[id].X;
                }

                var node = nodes[id];
                if (node.Kind is EcmaNodeKind.WordBoundary or EcmaNodeKind.NotWordBoundary or EcmaNodeKind.Lookahead or
                    EcmaNodeKind.NegativeLookahead or EcmaNodeKind.Lookbehind or EcmaNodeKind.NegativeLookbehind)
                {
                    // Zero-width: it only restricts where the strings may sit, and consumes nothing between them.
                    continue;
                }

                members.Clear();
                var atomIgnoreCase = (node.EffectiveFlags & EcmaScriptFlags.IgnoreCase) != 0;
                if (node.Kind == EcmaNodeKind.Literal)
                {
                    members.Add(node.Value);
                }
                else if (node.Kind == EcmaNodeKind.Set && _program.Sets[node.Value] is { StringCount: 0 } set)
                {
                    foreach (var interval in _program.Intervals.Slice(set.IntervalOffset, set.IntervalCount))
                    {
                        for (var point = interval.First; point <= interval.Last && members.Count <= MaxStringsPerBranch; point++)
                        {
                            members.Add(point);
                        }
                    }
                }
                else
                {
                    break;
                }

                if (!AdmitsPrefilter(members, atomIgnoreCase, unicodeAware) || prefixes.Count * members.Count > MaxStringsPerBranch)
                {
                    break;
                }

                ignoreCase |= atomIgnoreCase;
                var next = new List<string>(prefixes.Count * members.Count);
                foreach (var prefix in prefixes)
                {
                    foreach (var member in members)
                    {
                        next.Add(prefix + (char)member);
                    }
                }

                prefixes = next;
            }

            // A branch whose fixed start is shorter than two characters filters no better than FirstChars.
            if (prefixes[0].Length < 2)
            {
                return null;
            }

            all.AddRange(prefixes);
            if (all.Count > MaxFirstStrings)
            {
                return null;
            }
        }

        var distinct = new HashSet<string>(all, StringComparer.Ordinal);
        var strings = new string[distinct.Count];
        distinct.CopyTo(strings);
        return SearchValues.Create(strings,
            ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static void CollectBranches(ReadOnlySpan<EcmaNode> nodes, int id, List<int> branches)
    {
        while (nodes[id].Kind == EcmaNodeKind.Capture)
        {
            id = nodes[id].X;
        }

        if (nodes[id].Kind == EcmaNodeKind.Alternative)
        {
            CollectBranches(nodes, nodes[id].X, branches);
            CollectBranches(nodes, nodes[id].Y, branches);
        }
        else
        {
            branches.Add(id);
        }
    }

    // OrdinalIgnoreCase finds every match the specification's Canonicalize admits only where both agree on which
    // characters a point can match: ASCII, where a legacy Canonicalize never leaves ASCII, except k and s under u/v,
    // whose simple case folding also admits KELVIN SIGN and LATIN SMALL LETTER LONG S. Case-sensitive points need only
    // to be whole UTF-16 code units.
    private static bool AdmitsPrefilter(List<int> members, bool ignoreCase, bool unicodeAware)
    {
        if (members.Count == 0)
        {
            return false;
        }

        foreach (var point in members)
        {
            if (ignoreCase ? point >= 0x80 || (unicodeAware && point is 'k' or 'K' or 's' or 'S') : point > 0xFFFF || point is >= 0xD800 and <= 0xDFFF)
            {
                return false;
            }
        }

        return true;
    }

#endif
    private const int MaxFirstChars = 2048;
    // Per case-table instance, because the tables are per Unicode version: one slot per mode used to let a Unicode 16
    // program's inverse folds answer for a Unicode 17 program, which lost the 17.0 case pairs from its prefilter.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EcmaCaseTables, Dictionary<int, List<int>>?[]> InverseFolds = new();

    private SearchValues<char>? TryBuildFirstChars(ReadOnlySpan<EcmaNode> nodes, int root, EcmaCaseTables caseTables)
    {
        var items = new List<int>();
        FlattenSequence(nodes, root, items);
        if (items.Count == 0)
        {
            return null;
        }

        var unicodeAware = _program.Profile.IsUnicodeAware;
        var chars = new HashSet<char>();
        var astral = false;
        Dictionary<int, List<int>>? inverse = null;

        bool Add(int point)
        {
            if (point > 0xFFFF)
            {
                astral = true;
                return true;
            }

            chars.Add((char)point);
            return chars.Count <= MaxFirstChars;
        }

        // Every point whose canonical form is one of the member's: the member itself, its own canonical form, and
        // everything that folds to either. A superset is all a prefilter needs; the VM still verifies the match.
        bool AddVariants(int member, bool ignoreCase)
        {
            if (!Add(member))
            {
                return false;
            }

            if (!ignoreCase)
            {
                return true;
            }

            inverse ??= GetInverseFolds(caseTables, unicodeAware);
            var canonical = EcmaFastSet.Fold(member, unicodeAware, ignoreCase: true, caseTables);
            if (!Add(canonical))
            {
                return false;
            }

            foreach (var key in canonical == member ? new[] { member } : new[] { member, canonical })
            {
                if (inverse.TryGetValue(key, out var sources))
                {
                    foreach (var source in sources)
                    {
                        if (!Add(source))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        bool AddAtom(EcmaNode atom)
        {
            var ignoreCase = (atom.EffectiveFlags & EcmaScriptFlags.IgnoreCase) != 0;
            if (atom.Kind == EcmaNodeKind.Literal)
            {
                return AddVariants(atom.Value, ignoreCase);
            }

            var set = _program.Sets[atom.Value];
            if (set.StringCount != 0)
            {
                return false;
            }

            // Every BMP member is added, so a class with more of them than the cap fails anyway: \p{L} used to hash
            // 2,049 characters into the set first.
            var intervals = _program.Intervals.Slice(set.IntervalOffset, set.IntervalCount);
            var bmpMembers = 0L;
            foreach (var interval in intervals)
            {
                if (interval.First <= 0xFFFF)
                {
                    bmpMembers += Math.Min(interval.Last, 0xFFFF) - interval.First + 1;
                }
            }

            if (bmpMembers > MaxFirstChars)
            {
                return false;
            }

            foreach (var interval in intervals)
            {
                if ((long)interval.Last - interval.First >= MaxFirstChars)
                {
                    return false;
                }

                for (var point = interval.First; point <= interval.Last; point++)
                {
                    if (!AddVariants(point, ignoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // The characters an item can start with, through groups, alternations and inner sequences: /(?:abc|def)+/ had
        // no FIRST set and ran the VM at every position. A backreference can start with anything, so it has none.
        // `empty` says whether the item can match the empty string at a position after the input's start. A
        // non-multiline ^ cannot match there at all, so it contributes nothing and does not let the walk continue past
        // it; position 0, where it can, is always tried (FirstCharsTryStart). /(?:^|:|,)(?:\s*\[)+/ otherwise took
        // every space as a candidate, since the ^ branch let \s* lead.
        var startOnly = false;
        bool AddFirst(int id, ReadOnlySpan<EcmaNode> nodes, out bool empty)
        {
            var n = nodes[id];
            switch (n.Kind)
            {
                case EcmaNodeKind.Literal:
                case EcmaNodeKind.Set:
                    empty = false;
                    return AddAtom(n);
                case EcmaNodeKind.Capture:
                    return AddFirst(n.X, nodes, out empty);
                case EcmaNodeKind.Repeat:
                    if (n.Max == 0)
                    {
                        empty = true;
                        return true;
                    }

                    var repeated = AddFirst(n.X, nodes, out var bodyEmpty);
                    empty = bodyEmpty || n.Min == 0;
                    return repeated;
                case EcmaNodeKind.Alternative:
                {
                    var ok = AddFirst(n.X, nodes, out var leftEmpty) & AddFirst(n.Y, nodes, out var rightEmpty);
                    empty = leftEmpty || rightEmpty;
                    return ok;
                }
                case EcmaNodeKind.Sequence:
                {
                    if (!AddFirst(n.X, nodes, out var headEmpty))
                    {
                        empty = false;
                        return false;
                    }

                    if (!headEmpty)
                    {
                        empty = false;
                        return true;
                    }

                    return AddFirst(n.Y, nodes, out empty);
                }
                case EcmaNodeKind.Start when (n.EffectiveFlags & EcmaScriptFlags.Multiline) == 0:
                    startOnly = true;
                    empty = false;
                    return true;
                case EcmaNodeKind.Empty or EcmaNodeKind.Start or EcmaNodeKind.End or EcmaNodeKind.WordBoundary or
                    EcmaNodeKind.NotWordBoundary or EcmaNodeKind.Lookahead or EcmaNodeKind.NegativeLookahead or
                    EcmaNodeKind.Lookbehind or EcmaNodeKind.NegativeLookbehind:
                    empty = true;
                    return true;
                default:
                    empty = false;
                    return false;
            }
        }

        // FIRST set of the sequence: the leading items' characters, through every item that can match empty, up to
        // and including the first that cannot. Zero-width assertions only restrict where a match starts, so they add
        // nothing to a superset and are stepped over. A pattern that can match empty has no FIRST set.
        var mandatory = false;
        foreach (var item in items)
        {
            if (!AddFirst(item, nodes, out var itemEmpty))
            {
                return null;
            }

            if (!itemEmpty)
            {
                mandatory = true;
                break;
            }
        }

        if (!mandatory)
        {
            return null;
        }

        if (astral)
        {
            if (!unicodeAware)
            {
                return null;
            }

            // An astral member starts with one of its high surrogates; admitting them all keeps the superset cheap.
            for (var c = 0xD800; c <= 0xDBFF; c++)
            {
                chars.Add((char)c);
            }
        }

        if (chars.Count == 0)
        {
            return null;
        }

        var array = new char[chars.Count];
        chars.CopyTo(array);
        FirstCharsTryStart = startOnly;
        return System.Buffers.SearchValues.Create(array);
    }

    private static Dictionary<int, List<int>> GetInverseFolds(EcmaCaseTables caseTables, bool unicodeAware)
    {
        var slots = InverseFolds.GetValue(caseTables, static _ => new Dictionary<int, List<int>>?[2]);
        var slot = unicodeAware ? 1 : 0;
        if (Volatile.Read(ref slots[slot]) is { } cached)
        {
            return cached;
        }

        var inverse = new Dictionary<int, List<int>>();
        foreach (var mapping in unicodeAware ? caseTables.UnicodeFold : caseTables.LegacyUppercase)
        {
            if (!inverse.TryGetValue(mapping.Target, out var sources))
            {
                inverse[mapping.Target] = sources = [];
            }

            sources.Add(mapping.Source);
        }

        Interlocked.CompareExchange(ref slots[slot], inverse, null);
        return slots[slot]!;
    }

    private bool TryExtractRequiredLiteral(
        ReadOnlySpan<EcmaNode> nodes, int root,
        int[] minLengths, bool[] hasAmbiguousRepeat,
        out string? reqLit, out int reqMinOffset)
    {
        reqLit = null;
        reqMinOffset = 0;

        var items = new List<int>();
        FlattenSequence(nodes, root, items);
        if (items.Count < 2)
        {
            return false;
        }

        var minOffset = 0;
        using var sb = new ValueStringBuilder();
        string? best = null;
        var bestOffset = 0;
        var curOffset = 0;

        for (var i = 0; i < items.Count; i++)
        {
            var id = items[i];
            if (hasAmbiguousRepeat[id])
            {
                return false;
            }

            var cur = id;
            while (nodes[cur].Kind == EcmaNodeKind.Capture)
            {
                cur = nodes[cur].X;
            }

            var n = nodes[cur];
            if (n.Kind == EcmaNodeKind.Literal && (n.EffectiveFlags & EcmaScriptFlags.IgnoreCase) == 0)
            {
                if (sb.Length == 0)
                {
                    curOffset = minOffset;
                }

                sb.AppendScalar(n.Value);
                if (sb.Length > (best?.Length ?? 0))
                {
                    best = sb.ToString();
                    bestOffset = curOffset;
                }
            }
            else
            {
                sb.Clear();
            }
            minOffset = SaturatingAdd(minOffset, minLengths[id]);
        }

        if (best is not null && IsUnicodeSafeLiteral(best))
        {
            reqLit = best;
            reqMinOffset = bestOffset;
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TrySearch(
        ReadOnlySpan<char> input, int startAt, bool sticky,
        SearchOperation operation, int frames,
        ref ExecutionState state, Span<RegexCapture> groups, out RegexMatchRange match,
        out RegexSearchStatus status)
    {
        switch (_planKind)
        {
            case PlanKind.Literal:
                status = ExecuteLiteral(input, startAt, sticky, operation, ref state, groups, out match);
                return true;
            case PlanKind.LiteralAlternation:
                status = ExecuteLiteralAlternation(input, startAt, sticky, operation, ref state, groups, out match);
                return true;
            case PlanKind.SingleSetRepeat:
                status = ExecuteSingleSetRepeat(input, startAt, sticky, operation, frames, ref state, groups, out match);
                return true;
            case PlanKind.SetRepeatSuffixLiteral:
                status = ExecuteSetRepeatSuffixLiteral(input, startAt, sticky, operation, frames, ref state, groups, out match);
                return true;
            case PlanKind.AtomicSequence:
                status = ExecuteAtomicSequence(input, startAt, sticky, operation, frames, ref state, groups, out match);
                return true;
            default:
                match = default;
                status = default;
                return false;
        }
    }

    private RegexSearchStatus ExecuteLiteral(
        ReadOnlySpan<char> input, int startAt, bool sticky,
        SearchOperation operation, ref ExecutionState state,
        Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = default;
        if (_literalLeadBoundary || _literalTrailBoundary)
        {
            return ExecuteBoundedLiteral(input, startAt, sticky, operation, ref state, groups, out match);
        }

        var lit = _literal.AsSpan();
        var litLen = lit.Length;

        if (_hasEndAnchor && !_endMultiline)
        {
            var pos = input.Length - litLen;
            if (pos < startAt || (sticky && pos != startAt) || (_hasStartAnchor && !_startMultiline && pos != 0) ||
                (_hasStartAnchor && _startMultiline && pos > 0 && !IsLineTerminator(input[pos - 1])))
            {
                if (!TryChargeFast(ref state, startAt, input.Length, 1))
                {
                    return FinishFailure(state, operation, groups);
                }

                return FinishNoMatch(operation, groups);
            }
            if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, litLen)))
            {
                return FinishFailure(state, operation, groups);
            }

            if (!input.Slice(pos, litLen).SequenceEqual(lit))
            {
                return FinishNoMatch(operation, groups);
            }

            return CompleteLiteralMatch(pos, input.Length, operation, groups, out match);
        }

        if (_hasStartAnchor && !_startMultiline)
        {
            if (startAt > 0 || input.Length < litLen)
            {
                if (!TryChargeFast(ref state, startAt, input.Length, 1))
                {
                    return FinishFailure(state, operation, groups);
                }

                return FinishNoMatch(operation, groups);
            }
            if (!TryChargeFast(ref state, 0, input.Length, Math.Max(1, litLen)))
            {
                return FinishFailure(state, operation, groups);
            }

            if (!input.Slice(0, litLen).SequenceEqual(lit))
            {
                return FinishNoMatch(operation, groups);
            }

            if (_hasEndAnchor && _endMultiline && litLen < input.Length && !IsLineTerminator(input[litLen]))
            {
                return FinishNoMatch(operation, groups);
            }

            return CompleteLiteralMatch(0, litLen, operation, groups, out match);
        }

        if (sticky)
        {
            if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, litLen)))
            {
                return FinishFailure(state, operation, groups);
            }

            if (startAt + litLen > input.Length)
            {
                return FinishNoMatch(operation, groups);
            }

            if (_hasStartAnchor && startAt > 0 && !IsLineTerminator(input[startAt - 1]))
            {
                return FinishNoMatch(operation, groups);
            }

            if (!input.Slice(startAt, litLen).SequenceEqual(lit))
            {
                return FinishNoMatch(operation, groups);
            }

            var end = startAt + litLen;
            if (_hasEndAnchor && end < input.Length && !IsLineTerminator(input[end]))
            {
                return FinishNoMatch(operation, groups);
            }

            return CompleteLiteralMatch(startAt, end, operation, groups, out match);
        }

        var searchPos = startAt;
        var maxStartPos = input.Length - litLen;
        if (_hasRareLiteralChar && !_hasStartAnchor && !_hasEndAnchor && maxStartPos - searchPos >= 64)
        {
            var rareChar = _literalRareChar;
            var rareOff = _literalRareOffset;
            var falsePositives = 0;
            while (searchPos <= maxStartPos)
            {
                var rel = input.Slice(searchPos + rareOff, maxStartPos - searchPos + 1).IndexOf(rareChar);
                if (rel < 0)
                {
                    if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, input.Length - startAt)))
                    {
                        return FinishFailure(state, operation, groups);
                    }

                    return FinishNoMatch(operation, groups);
                }
                var pos = searchPos + rel;
                if (input.Slice(pos, litLen).SequenceEqual(lit))
                {
                    var end = pos + litLen;
                    if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, end - startAt)))
                    {
                        return FinishFailure(state, operation, groups);
                    }

                    return CompleteLiteralMatch(pos, end, operation, groups, out match);
                }
                searchPos = pos + 1;
                if (++falsePositives >= 8)
                {
                    break;
                }
            }
        }

        while (searchPos <= maxStartPos)
        {
            // A single-string SearchValues picks rarer anchor characters than IndexOf(string)'s first/last pair, which
            // is what .NET Regex searches with; IndexOf(string) took twice as long over common-letter text.
#if NET9_0_OR_GREATER
            var rel = litLen == 0 ? 0 : litLen == 1 ? input.Slice(searchPos).IndexOf(lit[0])
                : input.Slice(searchPos).IndexOfAny(_literalSearchValues ??= SearchValues.Create([_literal], StringComparison.Ordinal));
#else
            var rel = litLen == 0 ? 0 : litLen == 1 ? input.Slice(searchPos).IndexOf(lit[0]) : input.Slice(searchPos).IndexOf(lit);
#endif
            if (rel < 0)
            {
                if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, input.Length - startAt)))
                {
                    return FinishFailure(state, operation, groups);
                }

                return FinishNoMatch(operation, groups);
            }
            var pos = searchPos + rel;
            var end = pos + litLen;
            var startOk = !_hasStartAnchor || pos == 0 || IsLineTerminator(input[pos - 1]);
            var endOk = !_hasEndAnchor || end == input.Length || IsLineTerminator(input[end]);
            if (startOk && endOk)
            {
                if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, end - startAt)))
                {
                    return FinishFailure(state, operation, groups);
                }

                return CompleteLiteralMatch(pos, end, operation, groups, out match);
            }
            searchPos = pos + 1;
        }

        if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, input.Length - startAt)))
        {
            return FinishFailure(state, operation, groups);
        }

        return FinishNoMatch(operation, groups);
    }

    // The literal plan with \b on one or both sides (no anchors): each hit of the literal is a match when the boundary
    // holds there, so no hit starts the VM.
    private RegexSearchStatus ExecuteBoundedLiteral(
        ReadOnlySpan<char> input, int startAt, bool sticky,
        SearchOperation operation, ref ExecutionState state,
        Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = default;
        var lit = _literal.AsSpan();
        var litLen = lit.Length;
        if (sticky)
        {
            if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, litLen)))
            {
                return FinishFailure(state, operation, groups);
            }

            if (startAt + litLen <= input.Length && input.Slice(startAt, litLen).SequenceEqual(lit) &&
                BoundariesHold(input, startAt, startAt + litLen))
            {
                return CompleteLiteralMatch(startAt, startAt + litLen, operation, groups, out match);
            }

            return FinishNoMatch(operation, groups);
        }

        var searchPos = startAt;
        while (searchPos + litLen <= input.Length)
        {
#if NET9_0_OR_GREATER
            var rel = litLen == 1 ? input.Slice(searchPos).IndexOf(lit[0])
                : input.Slice(searchPos).IndexOfAny(_literalSearchValues ??= SearchValues.Create([_literal], StringComparison.Ordinal));
#else
            var rel = litLen == 1 ? input.Slice(searchPos).IndexOf(lit[0]) : input.Slice(searchPos).IndexOf(lit);
#endif
            if (rel < 0)
            {
                break;
            }

            var pos = searchPos + rel;
            var end = pos + litLen;
            if (BoundariesHold(input, pos, end))
            {
                if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, end - startAt)))
                {
                    return FinishFailure(state, operation, groups);
                }

                return CompleteLiteralMatch(pos, end, operation, groups, out match);
            }

            searchPos = pos + 1;
        }

        if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, input.Length - startAt)))
        {
            return FinishFailure(state, operation, groups);
        }

        return FinishNoMatch(operation, groups);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool BoundariesHold(ReadOnlySpan<char> input, int start, int end)
    {
        if (_literalLeadBoundary && (start > 0 && IsAsciiWordChar(input[start - 1])) == IsAsciiWordChar(input[start]))
        {
            return false;
        }

        return !_literalTrailBoundary || IsAsciiWordChar(input[end - 1]) != (end < input.Length && IsAsciiWordChar(input[end]));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RegexSearchStatus CompleteLiteralMatch(
        int start, int end, SearchOperation operation,
        Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = new(start, end - start);
        if (operation == SearchOperation.Captures)
        {
            groups[0] = new(true, start, end - start);
            if (_program.GroupCount > 1)
            {
                groups.Slice(1, _program.GroupCount - 1).Clear();
                foreach (var cap in _literalCaptures)
                {
                    groups[cap.Group] = new(true, start + cap.RelStart, cap.RelLength);
                }
            }
        }
        return RegexSearchStatus.Match;
    }

    private RegexSearchStatus ExecuteLiteralAlternation(
        ReadOnlySpan<char> input, int startAt, bool sticky,
        SearchOperation operation, ref ExecutionState state,
        Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = default;
        var branches = _altBranches;

        if (sticky)
        {
            var tail = input.Slice(startAt);
            for (var i = 0; i < branches.Length; i++)
            {
                var b = branches[i];
                if (tail.StartsWith(b.AsSpan()))
                {
                    if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, b.Length)))
                    {
                        return FinishFailure(state, operation, groups);
                    }

                    return CompleteSimpleMatch(startAt, startAt + b.Length, 0, operation, groups, out match);
                }
            }
            if (!TryChargeFast(ref state, startAt, input.Length, 1))
            {
                return FinishFailure(state, operation, groups);
            }

            return FinishNoMatch(operation, groups);
        }

        var searchPos = startAt;
        if (_altFirstCharValues is { } firstSv && input.Length - searchPos >= 32)
        {
            var falsePositives = 0;
            while (searchPos < input.Length)
            {
                var relFirst = input.Slice(searchPos).IndexOfAny(firstSv);
                if (relFirst < 0)
                {
                    if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, input.Length - startAt)))
                    {
                        return FinishFailure(state, operation, groups);
                    }

                    return FinishNoMatch(operation, groups);
                }
                var cand = searchPos + relFirst;
                var candSlice = input.Slice(cand);
                for (var i = 0; i < branches.Length; i++)
                {
                    var b = branches[i];
                    if (candSlice.StartsWith(b.AsSpan()))
                    {
                        var end = cand + b.Length;
                        if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, end - startAt)))
                        {
                            return FinishFailure(state, operation, groups);
                        }

                        return CompleteSimpleMatch(cand, end, 0, operation, groups, out match);
                    }
                }
                searchPos = cand + 1;
                if (++falsePositives >= 8)
                {
                    break;
                }
            }
        }

#if NET9_0_OR_GREATER
        var rel = input.Slice(searchPos).IndexOfAny(_altSearchValues!);
#else
        var rel = IndexOfAnyBranch(input.Slice(searchPos), _altSearchValues!);
#endif
        if (rel < 0)
        {
            if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, input.Length - startAt)))
            {
                return FinishFailure(state, operation, groups);
            }

            return FinishNoMatch(operation, groups);
        }

        var pos = searchPos + rel;
        var slice = input.Slice(pos);
        for (var i = 0; i < branches.Length; i++)
        {
            var b = branches[i];
            if (slice.StartsWith(b.AsSpan()))
            {
                var end = pos + b.Length;
                if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, end - startAt)))
                {
                    return FinishFailure(state, operation, groups);
                }

                return CompleteSimpleMatch(pos, end, 0, operation, groups, out match);
            }
        }

        return FinishNoMatch(operation, groups);
    }

    private RegexSearchStatus ExecuteSingleSetRepeat(
        ReadOnlySpan<char> input, int startAt, bool sticky,
        SearchOperation operation, int frames, ref ExecutionState state,
        Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = default;
        var fs = _repeatSet!;
        var min = _repeatMin;
        var max = _repeatMax;

        if (_hasStartAnchor && _hasEndAnchor)
        {
            if (startAt > 0 || input.Length < min)
            {
                if (!TryChargeFast(ref state, startAt, input.Length, 1))
                {
                    return FinishFailure(state, operation, groups);
                }

                return FinishNoMatch(operation, groups);
            }
            var count = fs.CountRun(input, max, _program, out var unitLen);
            if (!TryChargeFast(ref state, 0, input.Length, Math.Max(1, unitLen)))
            {
                return FinishFailure(state, operation, groups);
            }

            if (unitLen != input.Length || count < min)
            {
                return FinishNoMatch(operation, groups);
            }

            if (count - min >= frames)
            {
                return FailFrameCapacity(0, ref state, operation, groups);
            }

            return CompleteSimpleMatch(0, input.Length, _repeatCaptureGroup, operation, groups, out match);
        }

        if (sticky)
        {
            var count = fs.CountRun(input.Slice(startAt), max, _program, out var unitLen);
            if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, unitLen)))
            {
                return FinishFailure(state, operation, groups);
            }

            if (count < min || (_repeatLeadBoundary && startAt > 0 && fs.MatchesSingleChar(input[startAt - 1])))
            {
                return FinishNoMatch(operation, groups);
            }

            if (count - min >= frames)
            {
                return FailFrameCapacity(startAt, ref state, operation, groups);
            }

            return CompleteSimpleMatch(startAt, startAt + unitLen, _repeatCaptureGroup, operation, groups, out match);
        }

        var pos = startAt;
        while (pos <= input.Length - min)
        {
            var rel = fs.FindFirst(input.Slice(pos), _program);
            if (rel < 0)
            {
                break;
            }

            pos += rel;
            if (pos > input.Length - min)
            {
                break;
            }

            var count = fs.CountRun(input.Slice(pos), max, _program, out var unitLen);
            if (_repeatLeadBoundary && pos > 0 && fs.MatchesSingleChar(input[pos - 1]))
            {
                // A search that starts inside a run: the leading \b holds only where the next run starts.
                pos += Math.Max(1, unitLen);
                continue;
            }

            if (count >= min)
            {
                var end = pos + unitLen;
                if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, end - startAt)))
                {
                    return FinishFailure(state, operation, groups);
                }

                if (count - min >= frames)
                {
                    return FailFrameCapacity(pos, ref state, operation, groups);
                }

                return CompleteSimpleMatch(pos, end, _repeatCaptureGroup, operation, groups, out match);
            }
            pos += Math.Max(1, unitLen + 1);
        }

        if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, input.Length - startAt)))
        {
            return FinishFailure(state, operation, groups);
        }

        return FinishNoMatch(operation, groups);
    }

    private RegexSearchStatus ExecuteSetRepeatSuffixLiteral(
        ReadOnlySpan<char> input, int startAt, bool sticky,
        SearchOperation operation, int frames, ref ExecutionState state,
        Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = default;
        var fs = _repeatSet!;
        var min = _repeatMin;
        var lit = _literal.AsSpan();
        var litLen = lit.Length;

        if (sticky)
        {
            _ = fs.CountRun(input.Slice(startAt), -1, _program, out var unitLen);
            if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, unitLen)))
            {
                return FinishFailure(state, operation, groups);
            }

            if (unitLen < min + litLen)
            {
                return FinishNoMatch(operation, groups);
            }

            var lastRel = input.Slice(startAt + min, unitLen - min).LastIndexOf(lit);
            if (lastRel < 0)
            {
                return FinishNoMatch(operation, groups);
            }

            return CompleteSimpleMatch(startAt, startAt + min + lastRel + litLen, 0, operation, groups, out match);
        }

        var searchPos = startAt + min;
        while (searchPos <= input.Length - litLen)
        {
            var rel = input.Slice(searchPos).IndexOf(lit);
            if (rel < 0)
            {
                break;
            }

            var m = searchPos + rel;

            var runStart = m;
            while (runStart > startAt && fs.MatchesSingleChar(input[runStart - 1]))
            {
                runStart--;
            }

            if (m - runStart < min)
            {
                searchPos = m + 1;
                continue;
            }

            var runEnd = m + litLen;
            while (runEnd < input.Length && fs.MatchesSingleChar(input[runEnd]))
            {
                runEnd++;
            }

            var matchEnd = m + litLen;
            if (runEnd - matchEnd >= 1)
            {
                var lastRel = input.Slice(m + 1, runEnd - (m + 1)).LastIndexOf(lit);
                if (lastRel >= 0)
                {
                    matchEnd = m + 1 + lastRel + litLen;
                }
            }

            if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, matchEnd - startAt)))
            {
                return FinishFailure(state, operation, groups);
            }

            if ((matchEnd - litLen - runStart) - min >= frames)
            {
                return FailFrameCapacity(runStart, ref state, operation, groups);
            }

            return CompleteSimpleMatch(runStart, matchEnd, 0, operation, groups, out match);
        }

        if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, input.Length - startAt)))
        {
            return FinishFailure(state, operation, groups);
        }

        return FinishNoMatch(operation, groups);
    }

    private RegexSearchStatus ExecuteAtomicSequence(
        ReadOnlySpan<char> input, int startAt, bool sticky,
        SearchOperation operation, int frames, ref ExecutionState state,
        Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = default;
        var steps = _atomicSteps;

        if (_hasStartAnchor)
        {
            if (startAt > 0 || input.Length < _atomicMinTotalLength)
            {
                if (!TryChargeFast(ref state, startAt, input.Length, 1))
                {
                    return FinishFailure(state, operation, groups);
                }

                return FinishNoMatch(operation, groups);
            }
            if (TryMatchAtomicStepsAt(input, 0, operation, groups, out var matchEnd, out var maxOptRepeat))
            {
                if (!TryChargeFast(ref state, 0, input.Length, Math.Max(1, matchEnd)))
                {
                    return FinishFailure(state, operation, groups);
                }

                if (maxOptRepeat >= frames)
                {
                    return FailFrameCapacity(0, ref state, operation, groups);
                }

                match = new(0, matchEnd);
                return RegexSearchStatus.Match;
            }
            if (!TryChargeFast(ref state, 0, input.Length, 1))
            {
                return FinishFailure(state, operation, groups);
            }

            return FinishNoMatch(operation, groups);
        }

        if (sticky)
        {
            if (startAt + _atomicMinTotalLength <= input.Length &&
                TryMatchAtomicStepsAt(input, startAt, operation, groups, out var matchEnd, out var maxOptRepeat))
            {
                if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, matchEnd - startAt)))
                {
                    return FinishFailure(state, operation, groups);
                }

                if (maxOptRepeat >= frames)
                {
                    return FailFrameCapacity(startAt, ref state, operation, groups);
                }

                match = new(startAt, matchEnd - startAt);
                return RegexSearchStatus.Match;
            }
            if (!TryChargeFast(ref state, startAt, input.Length, 1))
            {
                return FinishFailure(state, operation, groups);
            }

            return FinishNoMatch(operation, groups);
        }

        var anchorLit = steps[_atomicAnchorStepIndex].Literal.AsSpan();
        var anchorLen = anchorLit.Length;
        var anchorChar0 = anchorLit[0];

        if (_atomicFixedAnchorOffset >= 0)
        {
            var c = startAt;
            var maxStart = input.Length - _atomicMinTotalLength;
            while (c <= maxStart)
            {
                var searchSlice = input.Slice(c + _atomicFixedAnchorOffset);
                var rel = anchorLen == 1 ? searchSlice.IndexOf(anchorChar0) : searchSlice.IndexOf(anchorLit);
                if (rel < 0)
                {
                    break;
                }

                c += rel;
                if (c > maxStart)
                {
                    break;
                }

                if (TryMatchAtomicStepsAt(input, c, operation, groups, out var matchEnd, out var maxOptRepeat))
                {
                    if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, matchEnd - startAt)))
                    {
                        return FinishFailure(state, operation, groups);
                    }

                    if (maxOptRepeat >= frames)
                    {
                        return FailFrameCapacity(c, ref state, operation, groups);
                    }

                    match = new(c, matchEnd - c);
                    return RegexSearchStatus.Match;
                }
                c++;
            }
        }
        else
        {
            // _atomicAnchorStepIndex == 1, Step 0 is variable SetRepeat or LiteralAlt
            var step0 = steps[0];
            var searchFrom = startAt + _atomicMinBeforeAnchor;
            var maxAnchorPos = input.Length - anchorLen - _atomicMinAfterAnchor;
            while (searchFrom <= maxAnchorPos)
            {
                var searchSlice = input.Slice(searchFrom);
                var rel = anchorLen == 1 ? searchSlice.IndexOf(anchorChar0) : searchSlice.IndexOf(anchorLit);
                if (rel < 0)
                {
                    break;
                }

                var m = searchFrom + rel;
                if (m > maxAnchorPos)
                {
                    break;
                }

                var c = -1;
                if (step0.Kind == AtomicStepKind.SetRepeat)
                {
                    var fs0 = step0.Set!;
                    var runStart = m;
                    var minStart = step0.Max >= 0 ? Math.Max(startAt, m - step0.Max) : startAt;
                    while (runStart > minStart && fs0.MatchesSingleChar(input[runStart - 1]))
                    {
                        runStart--;
                    }

                    if (step0.HeadSet is { } head0)
                    {
                        while (runStart < m && !head0.MatchesSingleChar(input[runStart]))
                        {
                            runStart++;
                        }
                    }
                    if (m - runStart >= step0.Min)
                    {
                        c = runStart;
                    }
                }
                else if (step0.Kind == AtomicStepKind.LiteralAlt)
                {
                    var branches = step0.Branches!;
                    for (var b = 0; b < branches.Length; b++)
                    {
                        var br = branches[b];
                        if (m - br.Length >= startAt && input.Slice(m - br.Length, br.Length).SequenceEqual(br.AsSpan()))
                        {
                            c = m - br.Length;
                            break;
                        }
                    }
                }

                if (c >= 0 && TryMatchAtomicSuffix(input, c, m, m + anchorLen, operation, groups, out var matchEnd, out var maxOptRepeat))
                {
                    if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, matchEnd - startAt)))
                    {
                        return FinishFailure(state, operation, groups);
                    }

                    if (maxOptRepeat >= frames)
                    {
                        return FailFrameCapacity(c, ref state, operation, groups);
                    }

                    match = new(c, matchEnd - c);
                    return RegexSearchStatus.Match;
                }

                searchFrom = m + 1;
            }
        }

        if (!TryChargeFast(ref state, startAt, input.Length, Math.Max(1, input.Length - startAt)))
        {
            return FinishFailure(state, operation, groups);
        }

        return FinishNoMatch(operation, groups);
    }

    private bool TryMatchAtomicStepsAt(
        ReadOnlySpan<char> input, int start,
        SearchOperation operation, Span<RegexCapture> groups,
        out int matchEnd, out int maxOptRepeat)
    {
        matchEnd = 0;
        maxOptRepeat = 0;
        var steps = _atomicSteps;
        var pos = start;
        var writeCaps = operation == SearchOperation.Captures;

        for (var i = 0; i < steps.Length; i++)
        {
            var st = steps[i];
            var stepStart = pos;
            if (st.Kind == AtomicStepKind.Literal)
            {
                var lit = st.Literal.AsSpan();
                if (lit.Length == 1)
                {
                    if (pos >= input.Length || input[pos] != lit[0])
                    {
                        return false;
                    }

                    pos++;
                }
                else
                {
                    if (pos + lit.Length > input.Length || !input.Slice(pos, lit.Length).SequenceEqual(lit))
                    {
                        return false;
                    }

                    pos += lit.Length;
                }
            }
            else if (st.Kind == AtomicStepKind.SetRepeat)
            {
                if (st.HeadSet is { } headSet)
                {
                    if (pos >= input.Length || !headSet.MatchesSingleChar(input[pos]))
                    {
                        return false;
                    }

                    var tailCount = st.Set!.CountRun(input.Slice(pos + 1), -1, _program, out var tailLen);
                    if (tailCount > maxOptRepeat)
                    {
                        maxOptRepeat = tailCount;
                    }

                    pos += 1 + tailLen;
                }
                else if (st.Min == 1 && st.Max == 1)
                {
                    if (pos >= input.Length || !st.Set!.MatchesSingleChar(input[pos]))
                    {
                        return false;
                    }

                    pos++;
                }
                else
                {
                    var count = st.Set!.CountRun(input.Slice(pos), st.Max, _program, out var unitLen);
                    if (count < st.Min)
                    {
                        return false;
                    }

                    if (count - st.Min > maxOptRepeat)
                    {
                        maxOptRepeat = count - st.Min;
                    }

                    pos += unitLen;
                }
            }
            else if (st.Kind == AtomicStepKind.LiteralAlt)
            {
                var branches = st.Branches!;
                var slice = input.Slice(pos);
                var matchedLen = -1;
                for (var b = 0; b < branches.Length; b++)
                {
                    if (slice.StartsWith(branches[b].AsSpan()))
                    {
                        matchedLen = branches[b].Length;
                        break;
                    }
                }
                if (matchedLen < 0)
                {
                    return false;
                }

                pos += matchedLen;
            }

            if (writeCaps && st.CaptureGroup > 0)
            {
                groups[st.CaptureGroup] = new(true, stepStart, pos - stepStart);
            }
        }

        if (_hasEndAnchor && pos != input.Length)
        {
            return false;
        }

        if (writeCaps)
        {
            groups[0] = new(true, start, pos - start);
        }

        matchEnd = pos;
        return true;
    }

    private bool TryMatchAtomicSuffix(
        ReadOnlySpan<char> input, int start, int step0End, int step2Start,
        SearchOperation operation, Span<RegexCapture> groups,
        out int matchEnd, out int maxOptRepeat)
    {
        matchEnd = 0;
        var steps = _atomicSteps;
        var step0 = steps[0];
        maxOptRepeat = step0.Kind == AtomicStepKind.SetRepeat ? (step0End - start) - step0.Min : 0;
        var pos = step2Start;
        var writeCaps = operation == SearchOperation.Captures;
        if (writeCaps)
        {
            if (step0.CaptureGroup > 0)
            {
                groups[step0.CaptureGroup] = new(true, start, step0End - start);
            }

            if (steps[1].CaptureGroup > 0)
            {
                groups[steps[1].CaptureGroup] = new(true, step0End, step2Start - step0End);
            }
        }

        for (var i = 2; i < steps.Length; i++)
        {
            var st = steps[i];
            var stepStart = pos;
            if (st.Kind == AtomicStepKind.Literal)
            {
                var lit = st.Literal.AsSpan();
                if (lit.Length == 1)
                {
                    if (pos >= input.Length || input[pos] != lit[0])
                    {
                        return false;
                    }

                    pos++;
                }
                else
                {
                    if (pos + lit.Length > input.Length || !input.Slice(pos, lit.Length).SequenceEqual(lit))
                    {
                        return false;
                    }

                    pos += lit.Length;
                }
            }
            else if (st.Kind == AtomicStepKind.SetRepeat)
            {
                if (st.Min == 1 && st.Max == 1)
                {
                    if (pos >= input.Length || !st.Set!.MatchesSingleChar(input[pos]))
                    {
                        return false;
                    }

                    pos++;
                }
                else
                {
                    var count = st.Set!.CountRun(input.Slice(pos), st.Max, _program, out var unitLen);
                    if (count < st.Min)
                    {
                        return false;
                    }

                    if (count - st.Min > maxOptRepeat)
                    {
                        maxOptRepeat = count - st.Min;
                    }

                    pos += unitLen;
                }
            }
            if (writeCaps && st.CaptureGroup > 0)
            {
                groups[st.CaptureGroup] = new(true, stepStart, pos - stepStart);
            }
        }

        if (_hasEndAnchor && pos != input.Length)
        {
            return false;
        }

        if (writeCaps)
        {
            groups[0] = new(true, start, pos - start);
        }

        matchEnd = pos;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RegexSearchStatus CompleteSimpleMatch(
        int start, int end, int captureGroup,
        SearchOperation operation, Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = new(start, end - start);
        if (operation == SearchOperation.Captures)
        {
            groups[0] = new(true, start, end - start);
            if (_program.GroupCount > 1)
            {
                if (_program.GroupCount == 2 && captureGroup == 1)
                {
                    groups[1] = new(true, start, end - start);
                }
                else
                {
                    groups.Slice(1, _program.GroupCount - 1).Clear();
                    if (captureGroup > 0)
                    {
                        groups[captureGroup] = new(true, start, end - start);
                    }
                }
            }
        }
        return RegexSearchStatus.Match;
    }

    private RegexSearchStatus FailFrameCapacity(
        int offset, ref ExecutionState state, SearchOperation operation, Span<RegexCapture> groups)
    {
        state.Failure = new(RegexExecutionFailureCode.WorkspaceCapacityExceeded, offset, state.Budget.WorkConsumed);
        if (operation == SearchOperation.Captures)
        {
            groups[.._program.GroupCount].Clear();
        }

        return RegexSearchStatus.LimitExceeded;
    }

    private RegexSearchStatus FinishNoMatch(SearchOperation operation, Span<RegexCapture> groups)
    {
        if (operation == SearchOperation.Captures)
        {
            groups[.._program.GroupCount].Clear();
        }

        return RegexSearchStatus.NoMatch;
    }

    private RegexSearchStatus FinishFailure(ExecutionState state, SearchOperation operation, Span<RegexCapture> groups)
    {
        if (operation == SearchOperation.Captures)
        {
            groups[.._program.GroupCount].Clear();
        }

        return state.Failure.Code switch
        {
            RegexExecutionFailureCode.Cancelled => RegexSearchStatus.Cancelled,
            RegexExecutionFailureCode.TimedOut => RegexSearchStatus.TimedOut,
            _ => RegexSearchStatus.LimitExceeded
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryChargeFast(ref ExecutionState state, int startAt, int endAt, int scanUnits)
    {
        var total = 2 * EcmaExecutor.TaskWidth + (long)scanUnits;
        if (total <= state.Budget.RemainingWork)
        {
            return state.TryCharge(total, startAt);
        }

        return TryChargeSlow(ref state, startAt, endAt, scanUnits);
    }

    private static bool TryChargeSlow(ref ExecutionState state, int startAt, int endAt, int scanUnits)
    {
        if (!state.TryCharge(EcmaExecutor.TaskWidth, startAt))
        {
            return false;
        }

        if (!state.TryCharge(EcmaExecutor.TaskWidth, startAt))
        {
            return false;
        }

        for (var i = 0; i < scanUnits; i++)
        {
            var pos = startAt + i;
            if (pos > endAt)
            {
                pos = endAt;
            }

            if (!state.TryCharge(1, pos))
            {
                return false;
            }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLineTerminator(char c) => c is '\n' or '\r' or '\u2028' or '\u2029';
}
