#nullable enable
using System.Buffers;
using System.Collections.Concurrent;
using System.Numerics;
using Jint.Runtime.RegExp.GregsEx.Contracts;
using Jint.Runtime.RegExp.GregsEx.Contracts.EcmaScript;

namespace Jint.Runtime.RegExp.GregsEx.Execution.Backtracking;

// Coarse code-unit filters for searches the exact prefilters leave without one, as the Oniguruma flavor computes them
// (OnigCompiler.RequiredUnits and its StartBits): a unit is an ASCII member or "any non-ASCII unit". A class like
// [_\p{Alpha}] has too many members for EcmaAccelerator.FirstChars, but its ASCII half still rules out most positions
// of source text, and a lookahead such as (?=\[) behind optional atoms still names a unit every match must reach.
internal static class EcmaUnitFilters
{
    // Above this many ASCII members (or with non-ASCII ones) a required set rules out too little to pay for its scan.
    private const int MaxRequiredMembers = 24;

    internal readonly record struct Units(ulong Low, ulong High, bool NonAscii)
    {
        internal int Members => BitOperations.PopCount(Low) + BitOperations.PopCount(High);
        internal int Cost => Members + (NonAscii ? 64 : 0);
        internal Units Union(Units other) => new(Low | other.Low, High | other.High, NonAscii || other.NonAscii);
        internal bool Rejects => NonAscii ? Low != ulong.MaxValue || High != ulong.MaxValue : true;
        internal Units With(int c) => c >= 128 ? this with { NonAscii = true } :
            c < 64 ? this with { Low = Low | (1UL << c) } : this with { High = High | (1UL << (c - 64)) };
    }

    // fastSet: the accelerator's set for this node when it has one, whose ASCII words are this class's (folded) ASCII
    // membership already; testing all 128 units again for every atom, filter and greedy run was most of a compile.
    private static Units? AtomUnits(EcmaProgram program, in EcmaNode n, EcmaCaseTables caseTables, bool required, EcmaFastSet? fastSet)
    {
        var ignoreCase = (n.EffectiveFlags & EcmaScriptFlags.IgnoreCase) != 0;
        var unicodeAware = program.Profile.IsUnicodeAware;
        var units = default(Units);
        if (n.Kind == EcmaNodeKind.Literal)
        {
            if (!ignoreCase)
            {
                // An astral literal is matched by its pair, whose first unit is a (non-ASCII) high surrogate.
                return units.With(n.Value);
            }

            if (required)
            {
                return null;
            }

            // Any unit canonicalizing with this one: its ASCII variants, and whatever non-ASCII unit folds with it. An
            // ASCII unit canonicalizes with no ASCII unit but its other case, under either folding.
            if (n.Value < 128)
            {
                units = units.With(n.Value);
                if ((uint)((n.Value | 0x20) - 'a') <= 'z' - 'a')
                {
                    units = units.With(n.Value ^ 0x20);
                }

                return units with { NonAscii = true };
            }

            var canonical = EcmaFastSet.Fold(n.Value, unicodeAware, ignoreCase: true, caseTables);
            for (var c = 0; c < 128; c++)
            {
                if (c == n.Value || EcmaFastSet.Fold(c, unicodeAware, ignoreCase: true, caseTables) == canonical)
                {
                    units = units.With(c);
                }
            }

            return units with { NonAscii = true };
        }

        var set = program.Sets[n.Value];
        if (set.StringCount != 0 || (ignoreCase && required))
        {
            return null;
        }

        var intervals = program.Intervals.Slice(set.IntervalOffset, set.IntervalCount);
        // Under i a non-ASCII unit can fold into the class whatever its intervals hold.
        var nonAscii = ignoreCase || (intervals.Length > 0 && intervals[^1].Last >= 128);
        if (fastSet is not null)
        {
            return new Units(fastSet.AsciiLow, fastSet.AsciiHigh, nonAscii);
        }

        for (var c = 0; c < 128; c++)
        {
            if (EcmaFastSet.ContainsInterval(intervals, ignoreCase ? EcmaFastSet.Fold(c, unicodeAware, ignoreCase: true, caseTables) : c))
            {
                units = units.With(c);
            }
        }

        return units with { NonAscii = nonAscii };
    }

    // Units of which every match contains one at or after its start, in the text it consumes or a lookahead reads; null
    // when none is known. Alternatives unite their branches' sets; a sequence keeps its most selective side. Nodes
    // list children first, so one pass computes every node.
    internal static SearchValues<char>? BuildRequired(EcmaProgram program, ReadOnlySpan<EcmaNode> nodes, int root, EcmaCaseTables caseTables,
        EcmaFastSet?[] fastSets)
    {
        var req = new Units?[nodes.Length];
        for (var i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            req[i] = n.Kind switch
            {
                EcmaNodeKind.Literal or EcmaNodeKind.Set => AtomUnits(program, n, caseTables, required: true, FastSet(fastSets, i)),
                EcmaNodeKind.Capture or EcmaNodeKind.Lookahead => req[n.X],
                EcmaNodeKind.Repeat => n.Min > 0 ? req[n.X] : null,
                EcmaNodeKind.Alternative => req[n.X] is { } x && req[n.Y] is { } y ? x.Union(y) : null,
                EcmaNodeKind.Sequence => req[n.X] is { } x ? (req[n.Y] is { } y && y.Cost < x.Cost ? y : x) : req[n.Y],
                // A lookbehind reads before the start, a negative assertion nothing, and a backreference what its group
                // matched, which the group's own node already accounts for.
                _ => null,
            };
        }

        if (req[root] is not { } units || units.NonAscii || units.Members == 0 || units.Members > MaxRequiredMembers)
        {
            return null;
        }

        return Shared(units.Low, units.High);
    }

    // Units a match can start with, through nullable atoms and zero-width assertions (a positive lookahead's body
    // constrains the start too). Except is true when the result lists the ASCII units that cannot start one, for
    // IndexOfAnyExcept, since non-ASCII units can. TryStart: a non-multiline ^ branch is left out, so position 0 is
    // always a candidate.
    internal static SearchValues<char>? BuildFirst(EcmaProgram program, ReadOnlySpan<EcmaNode> nodes, int root, EcmaCaseTables caseTables,
        EcmaFastSet?[] fastSets, out bool except, out bool tryStart)
    {
        except = false;
        var startOnly = false;
        var units = default(Units);
        if (!First(program, nodes, root, caseTables, fastSets, ref units, ref startOnly, out var empty, 0) || empty || !units.Rejects)
        {
            tryStart = false;
            return null;
        }

        tryStart = startOnly;
        if (!units.NonAscii)
        {
            return units.Members == 0 ? null : Shared(units.Low, units.High);
        }

        except = true;
        return Shared(~units.Low, ~units.High);
    }

    // Units the rest of the match can start with right after node: what follows it in each enclosing sequence, through
    // nullable atoms, and a repeat's own body again. Null when the match can end there or an assertion encloses node,
    // since anything may then follow. A greedy run gives back only to a position where one of these stands, as
    // Oniguruma's GiveBack does: /\w+(?=\.)/ retried its lookahead after every character it gave back.
    internal static SearchValues<char>? BuildFollow(EcmaProgram program, ReadOnlySpan<EcmaNode> nodes, int[] parents, int node,
        EcmaCaseTables caseTables, EcmaFastSet?[] fastSets, out bool except)
    {
        except = false;
        var units = default(Units);
        var startOnly = false;
        var cur = node;
        while (true)
        {
            var parent = parents[cur];
            if (parent < 0)
            {
                return null;
            }

            var p = nodes[parent];
            switch (p.Kind)
            {
                case EcmaNodeKind.Sequence when p.X == cur:
                    if (!First(program, nodes, p.Y, caseTables, fastSets, ref units, ref startOnly, out var empty, 0) || startOnly)
                    {
                        return null;
                    }

                    if (!empty)
                    {
                        return Values(units, out except);
                    }

                    break;
                case EcmaNodeKind.Sequence or EcmaNodeKind.Alternative or EcmaNodeKind.Capture:
                    break;
                case EcmaNodeKind.Repeat:
                    if (!First(program, nodes, cur, caseTables, fastSets, ref units, ref startOnly, out _, 0) || startOnly)
                    {
                        return null;
                    }

                    break;
                default:
                    return null;
            }

            cur = parent;
        }
    }

    internal static int[] Parents(ReadOnlySpan<EcmaNode> nodes)
    {
        var parents = new int[nodes.Length];
        parents.AsSpan().Fill(-1);
        for (var i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n.Kind is EcmaNodeKind.Sequence or EcmaNodeKind.Alternative)
            {
                parents[n.X] = i;
                parents[n.Y] = i;
            }
            else if (n.Kind is EcmaNodeKind.Repeat or EcmaNodeKind.Capture or EcmaNodeKind.Lookahead or EcmaNodeKind.NegativeLookahead or
                EcmaNodeKind.Lookbehind or EcmaNodeKind.NegativeLookbehind && n.X >= 0)
            {
                parents[n.X] = i;
            }
        }

        return parents;
    }

    private static SearchValues<char>? Values(Units units, out bool except)
    {
        except = units.NonAscii;
        if (!units.Rejects)
        {
            return null;
        }

        return except ? Shared(~units.Low, ~units.High) : Shared(units.Low, units.High);
    }

    private const int MaxDepth = 256;

    private static bool First(EcmaProgram program, ReadOnlySpan<EcmaNode> nodes, int id, EcmaCaseTables caseTables,
        EcmaFastSet?[] fastSets, ref Units units, ref bool startOnly, out bool empty, int depth)
    {
        empty = false;
        if (depth > MaxDepth)
        {
            return false;
        }

        var n = nodes[id];
        switch (n.Kind)
        {
            case EcmaNodeKind.Literal:
            case EcmaNodeKind.Set:
                if (AtomUnits(program, n, caseTables, required: false, FastSet(fastSets, id)) is not { } atom)
                {
                    return false;
                }

                units = units.Union(atom);
                return true;
            case EcmaNodeKind.Capture:
                return First(program, nodes, n.X, caseTables, fastSets, ref units, ref startOnly, out empty, depth + 1);
            case EcmaNodeKind.Repeat:
                if (n.Max == 0)
                {
                    empty = true;
                    return true;
                }

                if (!First(program, nodes, n.X, caseTables, fastSets, ref units, ref startOnly, out var bodyEmpty, depth + 1))
                {
                    return false;
                }

                empty = bodyEmpty || n.Min == 0;
                return true;
            case EcmaNodeKind.Alternative:
            {
                if (!First(program, nodes, n.X, caseTables, fastSets, ref units, ref startOnly, out var leftEmpty, depth + 1) ||
                    !First(program, nodes, n.Y, caseTables, fastSets, ref units, ref startOnly, out var rightEmpty, depth + 1))
                {
                    return false;
                }

                empty = leftEmpty || rightEmpty;
                return true;
            }
            case EcmaNodeKind.Sequence:
                if (!First(program, nodes, n.X, caseTables, fastSets, ref units, ref startOnly, out var headEmpty, depth + 1))
                {
                    return false;
                }

                return !headEmpty || First(program, nodes, n.Y, caseTables, fastSets, ref units, ref startOnly, out empty, depth + 1);
            case EcmaNodeKind.Lookahead:
            {
                // The body starts where the match does. One that can match empty, or that has no known start,
                // constrains nothing here and is stepped over like any other assertion.
                var body = default(Units);
                var bodyStartOnly = false;
                if (First(program, nodes, n.X, caseTables, fastSets, ref body, ref bodyStartOnly, out var lookEmpty, depth + 1) && !lookEmpty && !bodyStartOnly)
                {
                    units = units.Union(body);
                    return true;
                }

                empty = true;
                return true;
            }
            case EcmaNodeKind.Start when (n.EffectiveFlags & EcmaScriptFlags.Multiline) == 0:
                startOnly = true;
                return true;
            case EcmaNodeKind.Empty or EcmaNodeKind.Start or EcmaNodeKind.End or EcmaNodeKind.WordBoundary or
                EcmaNodeKind.NotWordBoundary or EcmaNodeKind.NegativeLookahead or EcmaNodeKind.Lookbehind or
                EcmaNodeKind.NegativeLookbehind:
                empty = true;
                return true;
            default:
                return false;
        }
    }

    private static EcmaFastSet? FastSet(EcmaFastSet?[] fastSets, int id) => (uint)id < (uint)fastSets.Length ? fastSets[id] : null;

    // The SearchValues of the ASCII units in (low, high), shared: a filter is one of few distinct sets, and creating one
    // per filter on every compile cost more than the rest of building it.
    private const int MaxShared = 4096;
    private static readonly ConcurrentDictionary<(ulong Low, ulong High), SearchValues<char>> SharedValues = new();

    private static SearchValues<char> Shared(ulong low, ulong high)
    {
        if (SharedValues.TryGetValue((low, high), out var values))
        {
            return values;
        }

        var chars = new char[BitOperations.PopCount(low) + BitOperations.PopCount(high)];
        var count = 0;
        for (var w = 0; w < 2; w++)
        {
            for (var word = w == 0 ? low : high; word != 0; word &= word - 1)
            {
                chars[count++] = (char)((w << 6) + BitOperations.TrailingZeroCount(word));
            }
        }

        values = SearchValues.Create(chars);
        return SharedValues.Count < MaxShared ? SharedValues.GetOrAdd((low, high), values) : values;
    }
}
