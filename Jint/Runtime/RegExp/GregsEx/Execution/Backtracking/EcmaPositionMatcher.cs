#nullable enable
using System.Runtime.CompilerServices;
using Jint.Runtime.RegExp.GregsEx.Contracts;
using Jint.Runtime.RegExp.GregsEx.Contracts.EcmaScript;

namespace Jint.Runtime.RegExp.GregsEx.Execution.Backtracking;

// A backtracking matcher over the pattern's position (Glushkov) automaton, run by the executor in place of the VM at each
// candidate the prefilters admit. Its states are the pattern's single-unit atoms, and each state lists what may follow it
// in the VM's backtracking order: a move to another atom, carrying the capture actions and assertions crossed on the way,
// or acceptance. A step tries the list in order and, when a later item could also apply at this position, keeps it as a
// choice (with the registers) to come back to; that is the VM's search tree with its epsilon steps compiled away, so the
// match and the captures are the VM's. Where every move of a state has a class of its own (the token alternation
// /"[^"\\\n\r]*"|true|false|null|-?\d+(?:\.\d*)?(:?[eE][+\-]?\d+)?/), no choice but an acceptance is ever kept.
//
// Admitted: literals and classes matching exactly one UTF-16 unit (no u/v, exact bitmap), sequences, alternations,
// captures, greedy and lazy repeats of a non-nullable body (counted ones unrolled), ^, $ and \b. A search whose choices
// outgrow the stack the matcher keeps hands the candidate back to the VM.
internal sealed class EcmaPositionMatcher
{
    private const byte GuardStart = 1;
    private const byte GuardEnd = 2;
    private const byte GuardLineStart = 4;
    private const byte GuardLineEnd = 8;
    private const byte GuardWord = 16;
    private const byte GuardNotWord = 32;
    private const int Accept = -1;
    private const int Exit = -2;

    private const int MaxPositions = 256;
    private const int MaxListItems = 64;
    private const int MaxTotalItems = 4096;
    internal const int MaxGroups = 16;

    private const int OpOpen = 0;
    private const int OpClose = 1;
    private const int OpReset = 2;

    private readonly EcmaProgram _program;
    // Per position.
    private readonly ulong[] _low;
    private readonly ulong[] _high;
    private readonly ulong[]?[] _bmp;
    private readonly int[] _single;
    // A class's own fast set, for measuring a self-loop run vectorized; null for a literal.
    private readonly EcmaFastSet?[] _runSet;
    // Per state (0 = before the first unit, p + 1 = after position p): its items, and whether its first item is a move back
    // to itself that carries no action, so a run of it can be measured in one call.
    private readonly int[] _stateItems;
    private readonly bool[] _selfRun;
    // Whether the state's first item is the move back to itself with no action or guard, while a later item may take the
    // same units: a run of its class is then measured in one call and kept as one counted choice that gives the run back
    // a unit at a time (the VM's GreedyRun), where stepping it kept a choice per unit (/a.*a/ over 64 KiB: 60x slower).
    private readonly bool[] _countedRun;
    // Whether the state's moves have pairwise disjoint classes: then only an acceptance can be a second choice.
    private readonly bool[] _deterministic;
    // Every state deterministic: MatchAt runs the lean one-pass loop, which keeps a single fallback acceptance.
    private readonly bool _onePass;
    // Per state with no guarded item, 128 entries: the first item an ASCII unit takes, or -1; null for other states.
    private readonly short[]?[] _ascii;
    // Per item.
    private readonly int[] _target;
    private readonly byte[] _guard;
    private readonly int[] _laterAccept;
    private readonly int[] _actionStart;
    private readonly int[] _actions;

    private EcmaPositionMatcher(EcmaProgram program, Builder b)
    {
        _program = program;
        var positions = b.Classes.Count;
        _low = new ulong[positions];
        _high = new ulong[positions];
        _bmp = new ulong[]?[positions];
        _single = new int[positions];
        _runSet = new EcmaFastSet?[positions];
        for (var p = 0; p < positions; p++)
        {
            var c = b.Classes[p];
            _low[p] = c.Low;
            _high[p] = c.High;
            _bmp[p] = c.Bmp;
            _single[p] = c.Single;
            _runSet[p] = c.Set;
        }

        var lists = new List<Item>[positions + 1];
        lists[0] = b.Start;
        for (var p = 0; p < positions; p++)
        {
            lists[p + 1] = b.Follow[p];
        }

        var itemCount = 0;
        var actionCount = 0;
        foreach (var list in lists)
        {
            itemCount += list.Count;
            foreach (var item in list)
            {
                actionCount += item.Actions?.Length ?? 0;
            }
        }

        _stateItems = new int[lists.Length + 1];
        _selfRun = new bool[lists.Length];
        _countedRun = new bool[lists.Length];
        _deterministic = new bool[lists.Length];
        _target = new int[itemCount];
        _guard = new byte[itemCount];
        _laterAccept = new int[itemCount];
        _actionStart = new int[itemCount + 1];
        _actions = new int[actionCount];
        var i = 0;
        var a = 0;
        for (var s = 0; s < lists.Length; s++)
        {
            _stateItems[s] = i;
            var list = lists[s];
            var first = i;
            foreach (var item in list)
            {
                _target[i] = item.Target;
                _guard[i] = item.Guard;
                _actionStart[i] = a;
                if (item.Actions is { } acts)
                {
                    acts.CopyTo(_actions, a);
                    a += acts.Length;
                }

                i++;
            }

            var nextAccept = -1;
            for (var k = i - 1; k >= first; k--)
            {
                _laterAccept[k] = nextAccept;
                if (_target[k] == Accept)
                {
                    nextAccept = k;
                }
            }

            _deterministic[s] = b.Disjoint(list);
            // A run of the state's own class can be measured in one call when its first item is the move back to itself,
            // with no action, nothing else takes the same units, and the acceptance below it, if any, always holds.
            var firstAccept = list.Count > 0 ? _laterAccept[first] : -1;
            _selfRun[s] = s > 0 && _deterministic[s] && list.Count > 0 && list[0].Target == s - 1 && list[0].Guard == 0 &&
                list[0].Actions is null && _runSet[s - 1] is not null && (firstAccept < 0 || _guard[firstAccept] == 0);
            _countedRun[s] = !_selfRun[s] && s > 0 && list.Count > 1 && list[0].Target == s - 1 && list[0].Guard == 0 &&
                list[0].Actions is null && _runSet[s - 1] is not null;
        }

        _stateItems[lists.Length] = i;
        _actionStart[itemCount] = a;
        _onePass = Array.TrueForAll(_deterministic, d => d);
        _ascii = new short[]?[lists.Length];
        for (var st = 0; st < lists.Length; st++)
        {
            var from = _stateItems[st];
            var to = _stateItems[st + 1];
            var guarded = to - from > short.MaxValue;
            for (var k = from; k < to && !guarded; k++)
            {
                guarded = _guard[k] != 0;
            }

            if (guarded || to == from)
            {
                continue;
            }

            var table = new short[128];
            for (var c = 0; c < 128; c++)
            {
                table[c] = -1;
                for (var k = from; k < to; k++)
                {
                    if (_target[k] == Accept || Matches(_target[k], (char) c))
                    {
                        table[c] = (short) k;
                        break;
                    }
                }
            }

            _ascii[st] = table;
        }
    }

    // Whether the plan may apply, from the node kinds alone: cheap enough for every compile.
    internal static bool MayApply(EcmaProgram program, ReadOnlySpan<EcmaNode> nodes)
    {
        if (program.Profile.IsUnicodeAware || program.GroupCount > MaxGroups)
        {
            return false;
        }

        foreach (var n in nodes)
        {
            switch (n.Kind)
            {
                case EcmaNodeKind.Empty:
                case EcmaNodeKind.Sequence:
                case EcmaNodeKind.Alternative:
                case EcmaNodeKind.Capture:
                case EcmaNodeKind.Set:
                    break;
                case EcmaNodeKind.Literal:
                    if ((n.EffectiveFlags & EcmaScriptFlags.IgnoreCase) != 0 && n.Value >= 128)
                    {
                        return false;
                    }

                    break;
                case EcmaNodeKind.Repeat:
                case EcmaNodeKind.Start:
                case EcmaNodeKind.End:
                case EcmaNodeKind.WordBoundary:
                case EcmaNodeKind.NotWordBoundary:
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    internal static EcmaPositionMatcher? TryCreate(EcmaProgram program, ReadOnlySpan<EcmaNode> nodes, EcmaFastSet?[] fastSets, EcmaCaseTables caseTables)
    {
        if (!MayApply(program, nodes))
        {
            return null;
        }

        var b = new Builder(program, nodes, fastSets, caseTables);
        return b.Build() ? new EcmaPositionMatcher(program, b) : null;
    }

    private readonly record struct Item(int Target, byte Guard, int[]? Actions);

    private readonly record struct Class(ulong Low, ulong High, ulong[]? Bmp, int Single, EcmaFastSet? Set);

    private sealed class Builder
    {
        private readonly EcmaProgram _program;
        private readonly EcmaNode[] _nodes;
        private readonly EcmaFastSet?[] _fastSets;
        private readonly EcmaCaseTables _caseTables;
        internal readonly List<Class> Classes = [];
        internal readonly List<List<Item>> Follow = [];
        internal List<Item> Start = [];
        private int _totalItems;

        internal Builder(EcmaProgram program, ReadOnlySpan<EcmaNode> nodes, EcmaFastSet?[] fastSets, EcmaCaseTables caseTables)
        {
            _program = program;
            _nodes = nodes.ToArray();
            _fastSets = fastSets;
            _caseTables = caseTables;
        }

        internal bool Build()
        {
            var first = Fragment(_program.Root);
            if (first is null)
            {
                return false;
            }

            List<Item> accept = [new(Accept, 0, null)];
            for (var p = 0; p < Follow.Count; p++)
            {
                if (Substitute(Follow[p], accept, afterUnit: true) is not { } follow)
                {
                    return false;
                }

                Follow[p] = Prune(follow);
            }

            if (Substitute(first, accept, afterUnit: false) is not { } start)
            {
                return false;
            }

            Start = Prune(start);
            return true;
        }

        // Nothing below an acceptance that always holds can be reached.
        private static List<Item> Prune(List<Item> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Target == Accept && list[i].Guard == 0)
                {
                    if (i + 1 < list.Count)
                    {
                        list.RemoveRange(i + 1, list.Count - i - 1);
                    }

                    break;
                }
            }

            return list;
        }

        internal bool Disjoint(List<Item> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Target < 0)
                {
                    continue;
                }

                for (var j = i + 1; j < list.Count; j++)
                {
                    if (list[j].Target >= 0 && Intersects(Classes[list[i].Target], Classes[list[j].Target]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool Intersects(Class a, Class b)
        {
            if ((a.Low & b.Low) != 0 || (a.High & b.High) != 0)
            {
                return true;
            }

            if (a.Single >= 128)
            {
                return b.Single == a.Single || (b.Bmp is { } bb && (bb[a.Single >> 6] & (1UL << (a.Single & 63))) != 0);
            }

            if (b.Single >= 128)
            {
                return a.Bmp is { } ab && (ab[b.Single >> 6] & (1UL << (b.Single & 63))) != 0;
            }

            if (a.Bmp is { } x && b.Bmp is { } y && !ReferenceEquals(x, y))
            {
                for (var w = 2; w < x.Length; w++)
                {
                    if ((x[w] & y[w]) != 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            return a.Bmp is not null && b.Bmp is not null && (a.Bmp[0] | a.Bmp[1]) != 0;
        }

        // The ordered items that can begin node, an Exit item standing for leaving it; the follow lists of the positions
        // it creates hold Exit items the same way. Null when the node cannot be expressed.
        private List<Item>? Fragment(int id)
        {
            var n = _nodes[id];
            switch (n.Kind)
            {
                case EcmaNodeKind.Empty:
                    return [new(Exit, 0, null)];
                case EcmaNodeKind.Start:
                    return [new(Exit, (n.EffectiveFlags & EcmaScriptFlags.Multiline) != 0 ? GuardLineStart : GuardStart, null)];
                case EcmaNodeKind.End:
                    return [new(Exit, (n.EffectiveFlags & EcmaScriptFlags.Multiline) != 0 ? GuardLineEnd : GuardEnd, null)];
                case EcmaNodeKind.WordBoundary:
                    return [new(Exit, GuardWord, null)];
                case EcmaNodeKind.NotWordBoundary:
                    return [new(Exit, GuardNotWord, null)];
                case EcmaNodeKind.Literal:
                case EcmaNodeKind.Set:
                {
                    if (!TryClass(id, n, out var cls) || Classes.Count == MaxPositions)
                    {
                        return null;
                    }

                    var p = Classes.Count;
                    Classes.Add(cls);
                    Follow.Add([new(Exit, 0, null)]);
                    return [new(p, 0, null)];
                }
                case EcmaNodeKind.Sequence:
                {
                    var xFrom = Classes.Count;
                    var x = Fragment(n.X);
                    if (x is null)
                    {
                        return null;
                    }

                    var yFrom = Classes.Count;
                    var y = Fragment(n.Y);
                    if (y is null)
                    {
                        return null;
                    }

                    return Concatenate(x, xFrom, yFrom, y);
                }
                case EcmaNodeKind.Alternative:
                {
                    var x = Fragment(n.X);
                    var y = x is null ? null : Fragment(n.Y);
                    if (y is null)
                    {
                        return null;
                    }

                    x!.AddRange(y);
                    return Charge(x);
                }
                case EcmaNodeKind.Capture:
                {
                    var from = Classes.Count;
                    var x = Fragment(n.X);
                    if (x is null)
                    {
                        return null;
                    }

                    int[] open = [OpOpen, n.Value, 0];
                    int[] close = [OpClose, n.Value, 0];
                    for (var i = 0; i < x.Count; i++)
                    {
                        x[i] = x[i] with { Actions = Concat(open, x[i].Actions) };
                    }

                    CloseExits(x, close);
                    for (var p = from; p < Classes.Count; p++)
                    {
                        CloseExits(Follow[p], close);
                    }

                    return x;
                }
                case EcmaNodeKind.Repeat:
                    return Repeat(n);
                default:
                    return null;
            }
        }

        private static void CloseExits(List<Item> list, int[] close)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Target == Exit)
                {
                    list[i] = list[i] with { Actions = Concat(list[i].Actions, close) };
                }
            }
        }

        // x, whose positions are [xFrom, yFrom), then y: every exit from x continues into y. The positions of fragments
        // built earlier may still hold exits of their own, which belong to whatever encloses them.
        private List<Item>? Concatenate(List<Item> x, int xFrom, int yFrom, List<Item> y)
        {
            for (var p = xFrom; p < yFrom; p++)
            {
                if (!HasExit(Follow[p]))
                {
                    continue;
                }

                if (Substitute(Follow[p], y, afterUnit: true) is not { } follow)
                {
                    return null;
                }

                Follow[p] = follow;
            }

            return Substitute(x, y, afterUnit: false);
        }

        private static bool HasExit(List<Item> list)
        {
            foreach (var item in list)
            {
                if (item.Target == Exit)
                {
                    return true;
                }
            }

            return false;
        }

        private List<Item>? Repeat(EcmaNode n)
        {
            var min = n.Min;
            var max = n.Max;
            if (max == 0)
            {
                return [new(Exit, 0, null)];
            }

            // Unroll counted repeats; keep the copies few. Bounds go up to int.MaxValue, so test before adding.
            if (min > 15 || max > 16)
            {
                return null;
            }

            var copies = max == -1 ? min + 1 : max;

            var reset = n.CaptureCount > 0 ? new[] { OpReset, n.FirstCapture, n.CaptureCount } : null;
            var firsts = new List<Item>[copies];
            var froms = new int[copies + 1];
            for (var c = 0; c < copies; c++)
            {
                froms[c] = Classes.Count;
                var body = Fragment(n.X);
                if (body is null || HasExit(body))
                {
                    // A nullable body brings the empty-iteration rule into play.
                    return null;
                }

                if (reset is not null)
                {
                    for (var i = 0; i < body.Count; i++)
                    {
                        body[i] = body[i] with { Actions = Concat(reset, body[i].Actions) };
                    }
                }

                firsts[c] = body;
            }

            froms[copies] = Classes.Count;

            // The tail after the mandatory copies, built from the right.
            List<Item> tail;
            if (max == -1)
            {
                // The last copy loops: its exits go back to its start, ahead of leaving (after it, when lazy).
                var last = firsts[copies - 1];
                List<Item> loop = n.Lazy ? [new(Exit, 0, null), .. last] : [.. last, new(Exit, 0, null)];
                for (var p = froms[copies - 1]; p < froms[copies]; p++)
                {
                    if (Substitute(Follow[p], loop, afterUnit: true) is not { } follow)
                    {
                        return null;
                    }

                    Follow[p] = follow;
                }

                tail = loop;
                copies--;
            }
            else
            {
                tail = [new(Exit, 0, null)];
                for (var c = max - 1; c >= min; c--)
                {
                    for (var p = froms[c]; p < froms[c + 1]; p++)
                    {
                        if (Substitute(Follow[p], tail, afterUnit: true) is not { } follow)
                        {
                            return null;
                        }

                        Follow[p] = follow;
                    }

                    tail = n.Lazy ? [new(Exit, 0, null), .. firsts[c]] : [.. firsts[c], new(Exit, 0, null)];
                }

                copies = min;
            }

            for (var c = copies - 1; c >= 0; c--)
            {
                for (var p = froms[c]; p < froms[c + 1]; p++)
                {
                    if (Substitute(Follow[p], tail, afterUnit: true) is not { } follow)
                    {
                        return null;
                    }

                    Follow[p] = follow;
                }

                if (Substitute(firsts[c], tail, afterUnit: false) is not { } first)
                {
                    return null;
                }

                tail = first;
            }

            return Charge(tail);
        }

        // Replaces every Exit item of list by the items of next, in place and in order. After a unit has been read the
        // position is not 0, so a move guarded by ^ is dropped; at the end of input no unit is left, so a move guarded by
        // $ is dropped.
        private List<Item>? Substitute(List<Item> list, List<Item> next, bool afterUnit)
        {
            List<Item>? result = null;
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item.Target != Exit)
                {
                    result?.Add(item);
                    continue;
                }

                if (result is null)
                {
                    result = new List<Item>(list.Count + next.Count);
                    for (var k = 0; k < i; k++)
                    {
                        result.Add(list[k]);
                    }
                }

                foreach (var n in next)
                {
                    var guard = (byte) (item.Guard | n.Guard);
                    if ((afterUnit && (guard & GuardStart) != 0) || ((guard & GuardEnd) != 0 && n.Target >= 0))
                    {
                        continue;
                    }

                    result.Add(new(n.Target, guard, Concat(item.Actions, n.Actions)));
                }
            }

            return result is null ? list : Charge(result);
        }

        private List<Item>? Charge(List<Item> list)
        {
            _totalItems += list.Count;
            return list.Count > MaxListItems || _totalItems > MaxTotalItems ? null : list;
        }

        private static int[]? Concat(int[]? a, int[]? b)
            => a is null ? b : b is null ? a : [.. a, .. b];

        private bool TryClass(int id, EcmaNode n, out Class cls)
        {
            cls = default;
            if (n.Kind == EcmaNodeKind.Literal)
            {
                var v = n.Value;
                if (v > 0xFFFF)
                {
                    return false;
                }

                if ((n.EffectiveFlags & EcmaScriptFlags.IgnoreCase) == 0)
                {
                    cls = v < 128
                        ? new(v < 64 ? 1UL << v : 0, v >= 64 ? 1UL << (v - 64) : 0, null, -1, null)
                        : new(0, 0, null, v, null);
                    return true;
                }

                // Legacy Canonicalize never maps a non-ASCII unit to an ASCII one, so an ASCII literal's matches are the
                // ASCII units sharing its canonical form.
                var canonical = EcmaFastSet.Fold(v, unicodeAware: false, ignoreCase: true, _caseTables);
                ulong low = 0, high = 0;
                for (var c = 0; c < 128; c++)
                {
                    if (EcmaFastSet.Fold(c, unicodeAware: false, ignoreCase: true, _caseTables) == canonical)
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

                cls = new(low, high, null, -1, null);
                return true;
            }

            if (_fastSets[id] is not { BmpBits: { } bmp, BmpBitsIsExact: true, Slice.StringCount: 0 } fs)
            {
                return false;
            }

            var nonAscii = false;
            for (var w = 2; w < bmp.Length; w++)
            {
                if (bmp[w] != 0)
                {
                    nonAscii = true;
                    break;
                }
            }

            cls = new(bmp[0], bmp[1], nonAscii ? bmp : null, -1, fs);
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Matches(int position, char c)
    {
        if (c < 128)
        {
            return ((c < 64 ? _low[position] : _high[position]) & (1UL << (c & 63))) != 0;
        }

        return _bmp[position] is { } bmp ? (bmp[c >> 6] & (1UL << (c & 63))) != 0 : _single[position] == c;
    }

    private static bool Holds(byte guard, ReadOnlySpan<char> input, int pos)
    {
        if ((guard & GuardStart) != 0 && pos != 0)
        {
            return false;
        }

        if ((guard & GuardEnd) != 0 && pos != input.Length)
        {
            return false;
        }

        if ((guard & GuardLineStart) != 0 && pos != 0 && !IsLineTerminator(input[pos - 1]))
        {
            return false;
        }

        if ((guard & GuardLineEnd) != 0 && pos != input.Length && !IsLineTerminator(input[pos]))
        {
            return false;
        }

        if ((guard & (GuardWord | GuardNotWord)) != 0)
        {
            var boundary = (pos > 0 && IsWord(input[pos - 1])) != (pos < input.Length && IsWord(input[pos]));
            if ((guard & GuardWord) != 0 && !boundary || (guard & GuardNotWord) != 0 && boundary)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLineTerminator(char c) => c is '\n' or '\r' or '\u2028' or '\u2029';

    private static bool IsWord(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    // Whether item can be taken at pos, whose unit is c (-1 at the end).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Applies(int item, int c, ReadOnlySpan<char> input, int pos)
    {
        var guard = _guard[item];
        if (guard != 0)
        {
            // ^ and $ inline; the line and word assertions out of line.
            if ((guard & GuardStart) != 0 && pos != 0 || (guard & GuardEnd) != 0 && pos != input.Length ||
                (guard & ~(GuardStart | GuardEnd)) != 0 && !Holds(guard, input, pos))
            {
                return false;
            }
        }

        var target = _target[item];
        return target == Accept || (c >= 0 && Matches(target, (char) c));
    }

    internal int GroupCount => _program.GroupCount;

    // The registers and the open positions, then the pending acceptance's copy of both: 8 ints per group.
    internal static int RegisterLength(int groupCount) => 8 * groupCount;

    // An item below this on the stack is a counted run, CountedChoice minus its first position.
    private const int CountedChoice = -2;

    internal const int NoMatch = -1;
    internal const int BudgetFailure = -2;
    // The choices outgrew the stack the matcher keeps; the VM takes the candidate.
    internal const int GiveUp = -3;

    // As for the VM's storage (JintRegExpEngine): a thread keeps a choice stack of up to 1 MiB between searches, a
    // search may grow its own to 64 MiB, and one needing more goes to the VM, which then fails as it would have.
    private const int RetainedStackInts = 1 << 18;
    private const int MaxStackInts = 1 << 24;
    [ThreadStatic] private static int[]? t_stack;

    // Fills groups from the registers MatchAt left, for a match [start, end).
    internal void Fill(Span<RegexCapture> groups, ReadOnlySpan<int> registers, int start, int end)
    {
        groups[0] = new(true, start, end - start);
        for (var g = 1; g < _program.GroupCount; g++)
        {
            var begin = registers[2 * g];
            var stop = registers[2 * g + 1];
            groups[g] = begin >= 0 && stop >= begin ? new(true, begin, stop - begin) : default;
        }
    }

    // The end of the match starting at start, or NoMatch, BudgetFailure or GiveUp. With registers (RegisterLength ints,
    // the first 2 * GroupCount of them -1) the captures are tracked too. Steps not yet charged to the budget come back in
    // steps, for the caller to charge.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int MatchAt(ReadOnlySpan<char> input, int start, scoped Span<int> allRegisters, ref RunState budget, out int steps)
        => _onePass
            ? MatchOnePass(input, start, allRegisters, ref budget, out steps)
            : MatchBacktracking(input, start, allRegisters, ref budget, out steps);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Run(int state, ReadOnlySpan<char> input, int pos)
        => _runSet[state - 1]!.CountRun(input.Slice(pos), -1, _program, out _);

    // MatchBacktracking where no state has a second move to keep: the only choice is the most recent acceptance ranked
    // below a move taken, kept in locals.
    private int MatchOnePass(ReadOnlySpan<char> input, int start, scoped Span<int> allRegisters, ref RunState budget, out int steps)
    {
        var saved = allRegisters.Length / 2;
        var registers = allRegisters.Slice(0, saved);
        var acceptRegisters = allRegisters.Slice(saved);
        var state = 0;
        var pos = start;
        var fallbackPos = -1;
        var fallbackItem = -1;
        var count = 0;
        while (true)
        {
            if (_selfRun[state])
            {
                var run = Run(state, input, pos);
                if (run > 0)
                {
                    pos += run;
                    count += run;
                    var accept = _laterAccept[_stateItems[state]];
                    if (accept >= 0)
                    {
                        fallbackPos = pos - 1;
                        fallbackItem = accept;
                        if (saved > 0)
                        {
                            registers.CopyTo(acceptRegisters);
                        }
                    }
                }
            }

            var c = (uint) pos < (uint) input.Length ? input[pos] : -1;
            var chosen = -1;
            if ((uint) c < 128 && _ascii[state] is { } table)
            {
                chosen = table[c];
            }
            else
            {
                var end = _stateItems[state + 1];
                for (var i = _stateItems[state]; i < end; i++)
                {
                    if (Applies(i, c, input, pos))
                    {
                        chosen = i;
                        break;
                    }
                }
            }

            if (chosen < 0)
            {
                steps = count;
                if (fallbackPos < 0)
                {
                    return NoMatch;
                }

                if (saved > 0)
                {
                    acceptRegisters.CopyTo(registers);
                    Apply(fallbackItem, fallbackPos, registers);
                }

                return fallbackPos;
            }

            var target = _target[chosen];
            if (target == Accept)
            {
                if (saved > 0)
                {
                    Apply(chosen, pos, registers);
                }

                steps = count;
                return pos;
            }

            for (var j = _laterAccept[chosen]; j >= 0; j = _laterAccept[j])
            {
                if (_guard[j] == 0 || Holds(_guard[j], input, pos))
                {
                    fallbackPos = pos;
                    fallbackItem = j;
                    if (saved > 0)
                    {
                        registers.CopyTo(acceptRegisters);
                    }

                    break;
                }
            }

            if (saved > 0)
            {
                Apply(chosen, pos, registers);
            }

            pos++;
            state = target + 1;
            if (++count >= 4096)
            {
                if (!ChargeSteps(ref budget, count, pos))
                {
                    steps = 0;
                    return BudgetFailure;
                }

                count = 0;
            }
        }
    }

    private int MatchBacktracking(ReadOnlySpan<char> input, int start, scoped Span<int> allRegisters, ref RunState budget, out int steps)
    {
        // A choice: state, item, position, then the registers and open positions when they are tracked. An acceptance
        // always succeeds once resumed, so of the acceptances kept only the most recent can be reached: it is kept
        // apart, with the stack height it was kept at, instead of on the stack.
        var saved = allRegisters.Length / 2;
        var registers = allRegisters.Slice(0, saved);
        var acceptRegisters = allRegisters.Slice(saved);
        var acceptSp = -1;
        var acceptPos = 0;
        var acceptItem = 0;
        var width = 3 + saved;
        // Fetched on the first choice: most candidates of a selective pattern keep none.
        int[]? stack = null;
        var sp = 0;
        var state = 0;
        var pos = start;
        var count = 0;
        while (true)
        {
            if (_selfRun[state])
            {
                var run = Run(state, input, pos);
                if (run > 0)
                {
                    count += run;
                    pos += run;
                    var accept = _laterAccept[_stateItems[state]];
                    if (accept >= 0)
                    {
                        // The acceptance each unit of the run would have kept; only the last can be reached.
                        acceptSp = sp;
                        acceptPos = pos - 1;
                        acceptItem = accept;
                        registers.CopyTo(acceptRegisters);
                    }
                }
            }
            else if (_countedRun[state])
            {
                var run = Run(state, input, pos);
                if (run > 0)
                {
                    // The choices each unit of the run would have kept, as one: at each position from the last unit
                    // back to the first, the state's items after its move to itself.
                    if (stack is null || sp + width > stack.Length)
                    {
                        stack = Grow(stack, sp + width);
                        if (stack is null)
                        {
                            steps = count;
                            return GiveUp;
                        }
                    }

                    stack[sp] = state;
                    stack[sp + 1] = CountedChoice - pos;
                    stack[sp + 2] = pos + run - 1;
                    if (saved > 0)
                    {
                        registers.CopyTo(stack.AsSpan(sp + 3));
                    }

                    sp += width;
                    count += run;
                    pos += run;
                }
            }

            var chosen = -1;
            var end = _stateItems[state + 1];
            var c = (uint) pos < (uint) input.Length ? input[pos] : -1;
            if ((uint) c < 128 && _ascii[state] is { } table)
            {
                chosen = table[c];
            }
            else
            {
                for (var i = _stateItems[state]; i < end; i++)
                {
                    if (Applies(i, c, input, pos))
                    {
                        chosen = i;
                        break;
                    }
                }
            }

            while (true)
            {
                if (chosen >= 0)
                {
                    break;
                }

                // Back to the most recent choice.
                if (sp == acceptSp)
                {
                    if (saved > 0)
                    {
                        acceptRegisters.CopyTo(registers);
                        Apply(acceptItem, acceptPos, registers);
                    }

                    steps = count;
                    return acceptPos;
                }

                if (sp == 0)
                {
                    steps = count;
                    return NoMatch;
                }

                sp -= width;
                state = stack![sp];
                chosen = stack[sp + 1];
                pos = stack[sp + 2];
                if (saved > 0)
                {
                    stack.AsSpan(sp + 3, saved).CopyTo(registers);
                }

                end = _stateItems[state + 1];
                c = (uint) pos < (uint) input.Length ? input[pos] : -1;
                if (chosen <= CountedChoice)
                {
                    // A counted run: give back one unit, staying armed down to its first unit, and try the state's other
                    // items there. Its move to itself writes nothing, so the registers kept are every position's.
                    if (pos > CountedChoice - chosen)
                    {
                        stack[sp + 2] = pos - 1;
                        sp += width;
                    }

                    chosen = -1;
                    count++;
                    for (var i = _stateItems[state] + 1; i < end; i++)
                    {
                        if (Applies(i, c, input, pos))
                        {
                            chosen = i;
                            break;
                        }
                    }
                }
            }

            var target = _target[chosen];
            if (target != Accept)
            {
                // The next item that also applies here is the choice the VM would leave.
                var next = -1;
                if (_deterministic[state])
                {
                    for (var j = _laterAccept[chosen]; j >= 0; j = _laterAccept[j])
                    {
                        if (_guard[j] == 0 || Holds(_guard[j], input, pos))
                        {
                            next = j;
                            break;
                        }
                    }
                }
                else
                {
                    for (var j = chosen + 1; j < end; j++)
                    {
                        if (Applies(j, c, input, pos))
                        {
                            next = j;
                            break;
                        }
                    }
                }

                if (next >= 0)
                {
                    if (_target[next] == Accept)
                    {
                        acceptSp = sp;
                        acceptPos = pos;
                        acceptItem = next;
                        registers.CopyTo(acceptRegisters);
                    }
                    else
                    {
                        if (stack is null || sp + width > stack.Length)
                        {
                            stack = Grow(stack, sp + width);
                            if (stack is null)
                            {
                                steps = count;
                                return GiveUp;
                            }
                        }

                        stack[sp] = state;
                        stack[sp + 1] = next;
                        stack[sp + 2] = pos;
                        if (saved > 0)
                        {
                            registers.CopyTo(stack.AsSpan(sp + 3));
                        }

                        sp += width;
                    }
                }
            }

            if (saved > 0)
            {
                Apply(chosen, pos, registers);
            }

            if (target == Accept)
            {
                steps = count;
                return pos;
            }

            pos++;
            state = target + 1;
            if (++count >= 4096)
            {
                if (!ChargeSteps(ref budget, count, pos))
                {
                    steps = 0;
                    return BudgetFailure;
                }

                count = 0;
            }
        }
    }

    // A long attempt charges as it goes, so a timeout can fire inside it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ChargeSteps(ref RunState budget, int steps, int pos) => budget.TryCharge(steps, pos);

    // A stack holding needed ints with what stack held copied over: the thread's while it fits what a thread may keep,
    // otherwise the search's own. Null past MaxStackInts.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int[]? Grow(int[]? stack, int needed)
    {
        var current = stack ?? (t_stack ??= new int[256]);
        if (needed <= current.Length)
        {
            return current;
        }

        if (needed > MaxStackInts)
        {
            return null;
        }

        var grown = new int[Math.Min(MaxStackInts, Math.Max(needed, current.Length * 2))];
        current.AsSpan().CopyTo(grown);
        if (grown.Length <= RetainedStackInts)
        {
            t_stack = grown;
        }

        return grown;
    }

    private void Apply(int item, int pos, Span<int> registers)
    {
        var opens = registers.Slice(2 * _program.GroupCount);
        for (var a = _actionStart[item]; a < _actionStart[item + 1]; a += 3)
        {
            var op = _actions[a];
            var g = _actions[a + 1];
            switch (op)
            {
                case OpOpen:
                    opens[g] = pos;
                    break;
                case OpClose:
                    registers[2 * g] = opens[g];
                    registers[2 * g + 1] = pos;
                    break;
                default:
                    for (var r = 2 * g; r < 2 * (g + _actions[a + 2]); r++)
                    {
                        registers[r] = -1;
                    }

                    break;
            }
        }
    }
}
