#nullable enable
using Jint.Runtime.RegExp.GregsEx.Contracts;
using Jint.Runtime.RegExp.GregsEx.Contracts.EcmaScript;

namespace Jint.Runtime.RegExp.GregsEx.Execution.Backtracking;

// Immutable lowered node instructions. Every mutable continuation and choice snapshot
// belongs to the caller's finite integer partitions; no managed call-stack recursion.
internal sealed class EcmaExecutor : IEcmaExecutor
{
    private readonly EcmaCaseTables _caseTables;
    private readonly EcmaNode[] _instructions;
    private readonly int _taskCapacity;
    private readonly int _taskSlots;
    private readonly int _registerSlots;
    private readonly int _registerTrailPerFrame;
    private readonly EcmaAccelerator _accelerator;
    // Per node, whether it is a greedy repeat of a single-character atom (see Machine.GreedyRun).
    private readonly bool[] _greedyRun;
    // Per greedy run, the units its continuation can start with (see EcmaUnitFilters.BuildFollow), and whether they
    // are the ASCII units it cannot start with instead.
    private readonly System.Buffers.SearchValues<char>?[]? _giveBack;
    private readonly bool[]? _giveBackExcept;
    // Canonicalize for ASCII, from the same case tables Machine.Canonical searches: an i-flag literal compare used to
    // binary-search the whole table twice per character.
    private readonly char[] _asciiCanonical;
    internal const int TaskWidth = 6;
    // Kind (and whether an appended task waits in the trail), task-stack height, position, a greedy run's lower bound
    // (ChoiceKind.GreedyRun), and where the frame's task and register trails begin.
    internal const int ChoiceWidth = 6;
    // A choice frame no longer copies the task stack and the registers. It keeps what it needs to undo: the tasks
    // popped from under its height (Machine.Pop) and the registers written after it (Machine.SetRegister), so its
    // size varies. The trails are pooled: each admitted frame adds this many ints to a pool any frame may draw from,
    // and an empty pool is WorkspaceCapacityExceeded like an exhausted choice stack.
    internal const int TaskTrailPerFrame = 2 * TaskWidth;
    // A register trail entry: register, previous value, previous stamp (Machine.SetRegister).
    private const int RegisterEntryWidth = 3;
    private const int MaxTrailedRegistersPerFrame = 4;
    public EcmaProgram Program { get; }

    // Whether a host-sized search (SearchPrevalidated with hostSized) runs the position matcher for this pattern.
    internal bool UsesPositionMatcher => _accelerator.PositionMatcher is not null;

    // Per node, the ASCII characters it can start with in the forward direction, and whether it may start with a
    // non-ASCII unit or match empty; a node it says nothing about may start anywhere. An alternative skips a branch
    // whose start excludes the current character instead of saving a choice for it, which is most of what a
    // first-character-dispatched alternation (/"[^"]*"|true|false|null|-?\d+/) costs.
    private readonly StartBits[] _startBits;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct StartBits(ulong Low, ulong High, bool NonAscii, bool Empty)
    {
        public static readonly StartBits Any = new(ulong.MaxValue, ulong.MaxValue, true, true);
        public static readonly StartBits EmptyOnly = new(0, 0, false, true);

        public StartBits Union(StartBits other) => new(Low | other.Low, High | other.High, NonAscii || other.NonAscii, Empty || other.Empty);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool CanStartWith(char c)
            => Empty || (c < 128 ? ((c < 64 ? Low : High) & (1UL << (c & 63))) != 0 : NonAscii);
    }

    internal EcmaExecutor(EcmaProgram program, EcmaCaseTables tables, EcmaNode[] instructions, int taskCapacity)
    {
        Program = program;
        _caseTables = tables;
        _instructions = instructions;
        _taskCapacity = taskCapacity;
        _taskSlots = checked(TaskWidth * _taskCapacity);
        _registerSlots = checked(2 * Program.GroupCount);
        // Group 0 is never written by the VM, so a pattern without other groups never trails a register.
        _registerTrailPerFrame = Program.GroupCount > 1 ? RegisterEntryWidth * Math.Min(_registerSlots, MaxTrailedRegistersPerFrame) : 0;
        _accelerator = new EcmaAccelerator(program, _caseTables);
        _asciiCanonical = AsciiCanonical(tables, program.Profile.IsUnicodeAware);

        _startBits = BuildStartBits(instructions, program.Profile.IsUnicodeAware, tables);
        _greedyRun = new bool[instructions.Length];
        int[]? parents = null;
        for (var i = 0; i < instructions.Length; i++)
        {
            var node = instructions[i];
            if (node.Kind == EcmaNodeKind.Repeat && !node.Lazy && node.X >= 0)
            {
                var atom = instructions[node.X];
                _greedyRun[i] = atom.Kind == EcmaNodeKind.Literal ||
                    (atom.Kind == EcmaNodeKind.Set && _accelerator.FastSets[node.X] is { } fastSet && fastSet.Slice.StringCount == 0);
                if (_greedyRun[i])
                {
                    parents ??= EcmaUnitFilters.Parents(instructions);
                    if (EcmaUnitFilters.BuildFollow(program, instructions, parents, i, tables, _accelerator.FastSets, out var except) is { } follow)
                    {
                        _giveBack ??= new System.Buffers.SearchValues<char>?[instructions.Length];
                        _giveBackExcept ??= new bool[instructions.Length];
                        _giveBack[i] = follow;
                        _giveBackExcept[i] = except;
                    }
                }
            }
        }
    }

    // Per case-table instance and Unicode mode, like the accelerator's case caches: it was 128 folds per compile.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EcmaCaseTables, char[]?[]> AsciiCanonicals = new();

    private static char[] AsciiCanonical(EcmaCaseTables tables, bool unicodeAware)
    {
        var slots = AsciiCanonicals.GetValue(tables, static _ => new char[]?[2]);
        var slot = unicodeAware ? 1 : 0;
        if (System.Threading.Volatile.Read(ref slots[slot]) is { } cached)
        {
            return cached;
        }

        var canonical = new char[128];
        for (var c = 0; c < canonical.Length; c++)
        {
            canonical[c] = (char) EcmaFastSet.Fold(c, unicodeAware, ignoreCase: true, tables);
        }

        System.Threading.Interlocked.CompareExchange(ref slots[slot], canonical, null);
        return slots[slot]!;
    }

    private StartBits[] BuildStartBits(EcmaNode[] instructions, bool unicodeAware, EcmaCaseTables tables)
    {
        // Children have lower ids than their parent, so one pass in id order sees every child first.
        var bits = new StartBits[instructions.Length];
        for (var i = 0; i < instructions.Length; i++)
        {
            var n = instructions[i];
            bits[i] = n.Kind switch
            {
                EcmaNodeKind.Literal => LiteralStart(n),
                EcmaNodeKind.Set => _accelerator.FastSets[i] is { Slice.StringCount: 0 } fastSet
                    ? new StartBits(fastSet.AsciiLow, fastSet.AsciiHigh, true, false)
                    : StartBits.Any,
                EcmaNodeKind.Capture => bits[n.X],
                EcmaNodeKind.Repeat => n.Max == 0 ? StartBits.EmptyOnly
                    : n.Min == 0 ? bits[n.X].Union(StartBits.EmptyOnly) : bits[n.X],
                EcmaNodeKind.Alternative => bits[n.X].Union(bits[n.Y]),
                EcmaNodeKind.Sequence => bits[n.X].Empty
                    ? new StartBits(bits[n.X].Low | bits[n.Y].Low, bits[n.X].High | bits[n.Y].High,
                        bits[n.X].NonAscii || bits[n.Y].NonAscii, bits[n.Y].Empty)
                    : bits[n.X],
                EcmaNodeKind.Empty or EcmaNodeKind.Start or EcmaNodeKind.End or EcmaNodeKind.WordBoundary or
                    EcmaNodeKind.NotWordBoundary or EcmaNodeKind.Lookahead or EcmaNodeKind.NegativeLookahead or
                    EcmaNodeKind.Lookbehind or EcmaNodeKind.NegativeLookbehind => StartBits.EmptyOnly,
                _ => StartBits.Any,
            };
        }

        return bits;

        StartBits LiteralStart(EcmaNode n)
        {
            if ((n.EffectiveFlags & EcmaScriptFlags.IgnoreCase) == 0)
            {
                return n.Value < 128 ? Bit(n.Value) : new StartBits(0, 0, true, false);
            }

            // Every ASCII unit whose canonical form is the literal's; anything non-ASCII may fold to it too.
            var canonical = EcmaFastSet.Fold(n.Value, unicodeAware, ignoreCase: true, tables);
            var result = new StartBits(0, 0, true, false);
            for (var c = 0; c < 128; c++)
            {
                if (_asciiCanonical[c] == canonical)
                {
                    result = result.Union(Bit(c));
                }
            }

            return result;
        }

        static StartBits Bit(int c) => c < 64 ? new StartBits(1UL << c, 0, false, false) : new StartBits(0, 1UL << (c - 64), false, false);
    }

    public RegexWorkspaceCapacity GetRequiredCapacity(SearchOperation operation, int frameCapacity)
    {
        if ((uint)operation > (uint)SearchOperation.Captures)
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameCapacity);
        // States: the task stack, then the choice frames. Captures: the registers, their stamps, then the register
        // trail. Scratch: the task trail.
        return new(checked(_taskSlots + ChoiceWidth * frameCapacity),
            checked(2 * _registerSlots + _registerTrailPerFrame * frameCapacity), checked(TaskTrailPerFrame * frameCapacity));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal int ComputeAdmittedFrames(RegexWorkspaceCapacity capacity)
    {
        var spareCaptures = capacity.CaptureSlots - 2 * _registerSlots;
        if (spareCaptures < 0)
        {
            return 0;
        }

        var frames = Math.Min(Math.Max(0, capacity.StateSlots - _taskSlots) / ChoiceWidth, capacity.ScratchSlots / TaskTrailPerFrame);
        return _registerTrailPerFrame == 0 ? frames : Math.Min(frames, spareCaptures / _registerTrailPerFrame);
    }

    public RegexSearchStatus Search(ReadOnlySpan<char> input, int startAt, bool sticky,
        EcmaStartPolicy startPolicy, SearchOperation operation, ref RunState state,
        Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = default;
        if ((uint)startAt > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startAt));
        }

        if ((uint)startPolicy > (uint)EcmaStartPolicy.BuiltinLastIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(startPolicy));
        }

        if ((uint)operation > (uint)SearchOperation.Captures)
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (operation == SearchOperation.Captures && groups.Length < Program.GroupCount)
        {
            state.Failure = new(RegexExecutionFailureCode.DestinationTooSmall, startAt,
                state.Budget.WorkConsumed, Program.GroupCount);
            return RegexSearchStatus.DestinationTooSmall;
        }
        // Derive the admitted choice capacity from ALL partitions, never spare capacity
        // in just one partition. No hidden growth or input-sized stack allocation.
        var frames = ComputeAdmittedFrames(state.Buffers.Capacity);
        if (frames == 0)
        {
            state.Failure = new(RegexExecutionFailureCode.WorkspaceCapacityExceeded, startAt, state.Budget.WorkConsumed);
            return Finish(RegexSearchStatus.LimitExceeded, operation, groups);
        }
        return SearchCore(input, startAt, sticky, startPolicy, operation, frames, hostSized: false, ref state, groups, out match);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    // hostSized: true only for a caller that sizes the workspace itself and grows it on WorkspaceCapacityExceeded (Jint),
    // which lets the position matcher run (see SearchCore).
    internal RegexSearchStatus SearchPrevalidated(ReadOnlySpan<char> input, int startAt, bool sticky,
        SearchOperation operation, int frames, ref RunState state,
        Span<RegexCapture> groups, out RegexMatchRange match, bool hostSized = false)
    {
        match = default;
        return SearchCore(input, startAt, sticky, EcmaStartPolicy.BuiltinLastIndex, operation, frames, hostSized, ref state, groups, out match);
    }

    // hostSized: the caller sizes the workspace and grows it on WorkspaceCapacityExceeded. Only then
    // does the position matcher run: it keeps its choices outside the caller's partitions and needs no VM frames, so under
    // a fixed reservation it would succeed where the VM reports the reservation exhausted, which Search must not do.
    private RegexSearchStatus SearchCore(ReadOnlySpan<char> input, int startAt, bool sticky,
        EcmaStartPolicy startPolicy, SearchOperation operation, int frames, bool hostSized, ref RunState state,
        Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = default;
        if (!state.TryCharge(0, startAt))
        {
            return Finish(FailureStatus(state), operation, groups);
        }

        if (Program.Profile.IsUnicodeAware && startAt > 0 && startAt < input.Length)
        {
            if (!state.TryCharge(2, startAt))
            {
                return Finish(FailureStatus(state), operation, groups);
            }

            if (char.IsLowSurrogate(input[startAt]) && char.IsHighSurrogate(input[startAt - 1]))
            {
                if (startPolicy == EcmaStartPolicy.LowerBound)
                {
                    state.Failure = new(RegexExecutionFailureCode.InvalidStartBoundary, startAt, state.Budget.WorkConsumed);
                    return Finish(RegexSearchStatus.InvalidInput, operation, groups);
                }
                startAt--;
            }
        }

        // The plans read frames only to refuse a run the VM could not have backtracked within its reservation. Since
        // the VM keeps a greedy single-character run in one counted choice (Machine.GreedyRun), the frame count no longer
        // bounds such a run, and refusing it made /a.*a/ over 64 KiB fail capacity in any reservation shorter than the run.
        if (_accelerator.TrySearch(input, startAt, sticky, operation, int.MaxValue, ref state, groups, out match, out var fastStatus))
        {
            return fastStatus;
        }

        if (_accelerator.HasEndTerminalChar)
        {
            if (input.Length == 0 || input[^1] != _accelerator.EndTerminalChar)
            {
                if (!EcmaAccelerator.TryChargeFast(ref state, startAt, input.Length, 1))
                {
                    return Finish(FailureStatus(state), operation, groups);
                }

                return Finish(RegexSearchStatus.NoMatch, operation, groups);
            }
        }

        if (_accelerator.StartAnchoredNonMultiline && startAt > 0)
        {
            if (!EcmaAccelerator.TryChargeFast(ref state, startAt, input.Length, 1))
            {
                return Finish(FailureStatus(state), operation, groups);
            }

            return Finish(RegexSearchStatus.NoMatch, operation, groups);
        }

        if (_accelerator.RequiredLiteral is { } reqLit)
        {
            var minReqPos = startAt + _accelerator.RequiredLiteralMinOffset;
            if (minReqPos > input.Length || input.Slice(minReqPos).IndexOf(reqLit.AsSpan()) < 0)
            {
                if (!EcmaAccelerator.TryChargeFast(ref state, startAt, input.Length, Math.Max(1, input.Length - startAt)))
                {
                    return Finish(FailureStatus(state), operation, groups);
                }

                return Finish(RegexSearchStatus.NoMatch, operation, groups);
            }
        }

        // Every match contains one of the required units at or after its start (see EcmaUnitFilters.BuildRequired),
        // so none starts after the last: /\.?\w*(?=\[)/ otherwise ran the VM from each position of a line without [.
        var lastStart = input.Length;
        if (_accelerator.RequiredUnits is { } requiredUnits)
        {
            var at = input.Slice(startAt).LastIndexOfAny(requiredUnits);
            if (!EcmaAccelerator.TryChargeFast(ref state, startAt, input.Length, Math.Max(1, at < 0 ? input.Length - startAt : input.Length - startAt - at)))
            {
                return Finish(FailureStatus(state), operation, groups);
            }

            if (at < 0)
            {
                return Finish(RegexSearchStatus.NoMatch, operation, groups);
            }

            lastStart = startAt + at;
        }

        if (hostSized && _accelerator.PositionMatcher is { } positions)
        {
            return SearchPositions(positions, input, startAt, lastStart, sticky, operation, frames, ref state, groups, out match);
        }

        return SearchMachine(input, startAt, lastStart, sticky, operation, frames, ref state, groups, out match);
    }

    // Kept out of SearchCore: the Machine is a large ref struct, and a method holding one zeroes its whole frame on
    // entry. Inline in SearchCore, every search paid that even when an accelerated plan answered first - 12 ns of a
    // 25 ns split(/a/) step.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private RegexSearchStatus SearchMachine(ReadOnlySpan<char> input, int startAt, int lastStart, bool sticky,
        SearchOperation operation, int frames, ref RunState state,
        Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = default;
        var saveRegisters = Program.GroupCount > 1 && (_accelerator.HasBackreferences || operation == SearchOperation.Captures);
        var machine = new Machine(this, input, state.Buffers, frames, saveRegisters);
        var candidate = startAt;
        while (true)
        {
            if (!sticky && candidate < input.Length)
            {
                candidate = Advance(input, candidate, ref state);
                if (candidate < 0)
                {
                    return Finish(candidate == NoCandidate ? RegexSearchStatus.NoMatch : FailureStatus(state), operation, groups);
                }
            }

            if (candidate > lastStart)
            {
                return Finish(RegexSearchStatus.NoMatch, operation, groups);
            }

            var result = machine.Run(candidate, ref state);
            if (result < 0)
            {
                return Finish(FailureStatus(state), operation, groups);
            }

            if (result > 0)
            {
                if (!state.TryCharge(operation == SearchOperation.Captures ? Program.GroupCount : 1, machine.Position))
                {
                    return Finish(FailureStatus(state), operation, groups);
                }

                match = new(candidate, machine.Position - candidate);
                if (operation == SearchOperation.Captures)
                {
                    groups[0] = new(true, candidate, match.Length);
                    for (var g = 1; g < Program.GroupCount; g++)
                    {
                        var begin = machine.Registers[2 * g];
                        var end = machine.Registers[2 * g + 1];
                        groups[g] = begin >= 0 && end >= begin ? new(true, begin, end - begin) : default;
                    }
                }
                return RegexSearchStatus.Match;
            }
            if (sticky || candidate == input.Length || _accelerator.StartAnchoredNonMultiline)
            {
                return Finish(RegexSearchStatus.NoMatch, operation, groups);
            }

            if (!machine.Read(candidate, 1, ref state, out _, out var next))
            {
                return Finish(FailureStatus(state), operation, groups);
            }

            candidate = next;
        }
    }

    private const int NoCandidate = -1;
    private const int CandidateFailure = -2;

    // The first position from candidate the prefilters admit; NoCandidate when none is left, CandidateFailure when the
    // budget ran out.
    private int Advance(ReadOnlySpan<char> input, int candidate, ref RunState state)
    {
        if (_accelerator.PrefixLiteral is { } pfx)
        {
            var rel = input.Slice(candidate).IndexOf(pfx.AsSpan());
            if (rel < 0)
            {
                if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, Math.Max(1, input.Length - candidate)))
                {
                    return CandidateFailure;
                }

                return NoCandidate;
            }
            if (rel > 0)
            {
                if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, rel))
                {
                    return CandidateFailure;
                }

                candidate += rel;
            }
        }
        else if (_accelerator.FirstStrings is { } firstStrings)
        {
            // See EcmaAccelerator.FirstStrings.
            var rel = input.Slice(candidate).IndexOfAny(firstStrings);
            if (rel < 0)
            {
                if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, Math.Max(1, input.Length - candidate)))
                {
                    return CandidateFailure;
                }

                return NoCandidate;
            }
            if (rel > 0)
            {
                if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, rel))
                {
                    return CandidateFailure;
                }

                candidate += rel;
            }
        }
        else if (_accelerator.FirstChars is { } firstChars && (candidate > 0 || !_accelerator.FirstCharsTryStart))
        {
            // See EcmaAccelerator.FirstChars. A hit on the trail half of a surrogate pair is not a
            // candidate under u/v, where the pair is one code point and matching never starts inside it.
            var rel = input.Slice(candidate).IndexOfAny(firstChars);
            while (rel > 0 && Program.Profile.IsUnicodeAware && char.IsLowSurrogate(input[candidate + rel]) &&
                char.IsHighSurrogate(input[candidate + rel - 1]))
            {
                var further = input.Slice(candidate + rel + 1).IndexOfAny(firstChars);
                rel = further < 0 ? -1 : rel + 1 + further;
            }

            if (rel < 0)
            {
                if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, Math.Max(1, input.Length - candidate)))
                {
                    return CandidateFailure;
                }

                return NoCandidate;
            }
            if (rel > 0)
            {
                if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, rel))
                {
                    return CandidateFailure;
                }

                candidate += rel;
            }
        }
        else if (_accelerator.FirstUnits is { } units && (candidate > 0 || !_accelerator.FirstUnitsTryStart))
        {
            // See EcmaAccelerator.FirstUnits. Non-ASCII units are candidates under FirstUnitsExcept, so a hit is a
            // whole character's start whenever the search stood on one.
            var rest = input.Slice(candidate);
            var rel = _accelerator.FirstUnitsExcept ? rest.IndexOfAnyExcept(units) : rest.IndexOfAny(units);
            if (rel < 0)
            {
                if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, Math.Max(1, input.Length - candidate)))
                {
                    return CandidateFailure;
                }

                return NoCandidate;
            }
            if (rel > 0)
            {
                if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, rel))
                {
                    return CandidateFailure;
                }

                candidate += rel;
            }
        }
        else if (_accelerator.FirstSet is { } fset)
        {
            var rel = fset.FindFirst(input.Slice(candidate), Program);
            if (rel < 0)
            {
                if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, Math.Max(1, input.Length - candidate)))
                {
                    return CandidateFailure;
                }

                return NoCandidate;
            }
            if (rel > 0)
            {
                if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, rel))
                {
                    return CandidateFailure;
                }

                candidate += rel;
            }
        }

        return candidate;
    }

    // SearchMachine's candidate loop around the position matcher (see EcmaPositionMatcher), which needs no Machine.
    private RegexSearchStatus SearchPositions(EcmaPositionMatcher matcher, ReadOnlySpan<char> input, int startAt, int lastStart, bool sticky,
        SearchOperation operation, int frames, ref RunState state, Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = default;
        var captures = operation == SearchOperation.Captures && matcher.GroupCount > 1;
        Span<int> registers = captures ? stackalloc int[EcmaPositionMatcher.RegisterLength(matcher.GroupCount)] : default;
        var candidate = startAt;
        while (true)
        {
            if (!sticky && candidate < input.Length)
            {
                candidate = Advance(input, candidate, ref state);
                if (candidate < 0)
                {
                    return Finish(candidate == NoCandidate ? RegexSearchStatus.NoMatch : FailureStatus(state), operation, groups);
                }
            }

            if (candidate > lastStart)
            {
                return Finish(RegexSearchStatus.NoMatch, operation, groups);
            }

            if (captures)
            {
                registers.Slice(0, 2 * matcher.GroupCount).Fill(-1);
            }

            var end = matcher.MatchAt(input, candidate, registers, ref state, out var steps);
            if (end == EcmaPositionMatcher.BudgetFailure || !state.TryCharge(TaskWidth + steps, candidate))
            {
                return Finish(FailureStatus(state), operation, groups);
            }

            if (end >= 0)
            {
                match = new(candidate, end - candidate);
                if (operation == SearchOperation.Captures)
                {
                    if (captures)
                    {
                        matcher.Fill(groups, registers, candidate, end);
                    }
                    else
                    {
                        groups[0] = new(true, candidate, end - candidate);
                    }
                }

                return RegexSearchStatus.Match;
            }

            if (end == EcmaPositionMatcher.GiveUp)
            {
                // The VM searches on from this candidate, which is where this search stands.
                return SearchMachine(input, candidate, lastStart, sticky, operation, frames, ref state, groups, out match);
            }

            if (sticky || candidate == input.Length || _accelerator.StartAnchoredNonMultiline)
            {
                return Finish(RegexSearchStatus.NoMatch, operation, groups);
            }

            candidate++;
        }
    }

    private RegexSearchStatus Finish(RegexSearchStatus status, SearchOperation operation, Span<RegexCapture> groups)
    { if (operation == SearchOperation.Captures) { groups[..Program.GroupCount].Clear(); } return status; }
    private static RegexSearchStatus FailureStatus(in RunState state) => state.Failure.Code switch
    {
        RegexExecutionFailureCode.Cancelled => RegexSearchStatus.Cancelled,
        RegexExecutionFailureCode.TimedOut => RegexSearchStatus.TimedOut,
        _ => RegexSearchStatus.LimitExceeded
    };

    private enum TaskKind { Evaluate, Repeat, RepeatBody, RepeatAfter, CaptureEnd, AssertionEnd, SetResume }
    private enum ChoiceKind { Branch, PositiveAssertionFailure, NegativeAssertionFailure, GreedyRun }
    // Set on a frame's kind when its task to resume with waits at the start of its task trail segment.
    private const int AppendFlag = 1 << 8;
    private const int ChoiceKindMask = AppendFlag - 1;
    // A greedy run's choice keeps its repeat node above the flags, for its give-back filter (EcmaExecutor._giveBack).
    private const int ChoiceNodeShift = 9;
    private const int MaxChoiceNode = (1 << (31 - ChoiceNodeShift)) - 1;
    // A/B/C interpretation is specific to Kind. Direction is always explicit, including
    // tasks saved before entering a nested assertion with a different direction.
    private readonly record struct Task(TaskKind Kind, int Node, int A, int B, int Direction, int C = 0);

    private ref struct Machine
    {
        private readonly EcmaExecutor _owner;
        private readonly ReadOnlySpan<char> _input;
        private readonly Span<int> _tasks, _choices, _taskTrail, _registerTrail, _stamps;
        private readonly int _frames;
        private readonly bool _saveRegisters;
        private int _taskCount, _choiceCount, _taskTrailCount, _registerTrailCount;
        // The top choice's low-water mark: every task slot from here up to the top choice's height has had its value
        // at that choice copied to the task trail (see Pop). No choice: 0, so nothing is trailed.
        private int _low;
        internal readonly Span<int> Registers;
        internal int Position;
        internal Machine(EcmaExecutor owner, ReadOnlySpan<char> input, WorkspaceBuffers buffers, int frames, bool saveRegisters)
        {
            _owner = owner;
            _input = input;
            _frames = frames;
            _saveRegisters = saveRegisters;
            _tasks = buffers.States[..owner._taskSlots];
            _choices = buffers.States.Slice(owner._taskSlots, frames * ChoiceWidth);
            Registers = buffers.Captures[..owner._registerSlots];
            _stamps = buffers.Captures.Slice(owner._registerSlots, owner._registerSlots);
            _registerTrail = buffers.Captures.Slice(2 * owner._registerSlots, frames * owner._registerTrailPerFrame);
            _taskTrail = buffers.Scratch[..(frames * TaskTrailPerFrame)];
            _taskCount = _choiceCount = _taskTrailCount = _registerTrailCount = _low = Position = 0;
        }

        internal int Run(int candidate, ref RunState state)
        {
            Position = candidate;
            _taskCount = _choiceCount = _taskTrailCount = _registerTrailCount = _low = 0;
            if (!state.TryCharge(Registers.Length, Position))
            {
                return -1;
            }

            if (_saveRegisters)
            {
                Registers.Fill(-1);
                _stamps.Fill(0);
            }

            if (!Push(new(TaskKind.Evaluate, _owner.Program.Root, 0, 0, 1), ref state))
            {
                return -1;
            }

            while (true)
            {
                if (!state.TryCharge(1, Position))
                {
                    return -1; // polls even empty acceptance
                }

                if (_taskCount == 0)
                {
                    return 1;
                }

                if (!Pop(ref state, out var task))
                {
                    return -1;
                }

                var node = _owner._instructions[task.Node];
                var succeeds = true;
                switch (task.Kind)
                {
                    case TaskKind.CaptureEnd:
                        if (!state.TryCharge(2, Position))
                        {
                            return -1;
                        }

                        if (!SetRegister(2 * node.Value, Math.Min(task.A, Position), ref state) ||
                            !SetRegister(2 * node.Value + 1, Math.Max(task.A, Position), ref state))
                        {
                            return -1;
                        }

                        break;
                    case TaskKind.Repeat:
                        if (node.Max != -1 && task.A == node.Max)
                        {
                            break;
                        }

                        if (task.A < node.Min)
                        { if (!Body(task, ref state)) { return -1; } break; }
                        if (node.Lazy)
                        {
                            if (!Save(ChoiceKind.Branch, new(TaskKind.RepeatBody, task.Node, task.A, 0, task.Direction), ref state))
                            {
                                return -1;
                            }
                        }
                        else if (_owner._greedyRun[task.Node] && task.Direction > 0)
                        {
                            if (!GreedyRun(node, task.Node, task.A, ref state))
                            {
                                return -1;
                            }
                        }
                        else
                        {
                            if (!Save(ChoiceKind.Branch, null, ref state) || !Body(task, ref state))
                            {
                                return -1;
                            }
                        }
                        break;
                    case TaskKind.RepeatBody:
                        if (!Body(task, ref state))
                        {
                            return -1;
                        }

                        break;
                    case TaskKind.RepeatAfter:
                        // The guard is tested against min BEFORE this attempted iteration.
                        // Required empty iterations participate; optional empty iterations fail.
                        if (task.A >= node.Min && Position == task.B)
                        {
                            succeeds = false;
                        }
                        else if (!Push(new(TaskKind.Repeat, task.Node, node.Max == -1 && task.A >= node.Min ? task.A : checked(task.A + 1), 0, task.Direction), ref state))
                        {
                            return -1;
                        }

                        break;
                    case TaskKind.AssertionEnd:
                        if (node.Kind is EcmaNodeKind.NegativeLookahead or EcmaNodeKind.NegativeLookbehind)
                        {
                            if (!Restore(task.C, ref state))
                            {
                                return -1;
                            }

                            _choiceCount = task.C;
                            ResetLow();
                            succeeds = false;
                        }
                        else
                        {
                            Discard(task.C);
                            Position = task.B;
                        }
                        break;
                    case TaskKind.SetResume:
                        succeeds = MatchSet(node.Value, task.A, task.Direction, task.Node, ref state);
                        break;
                    default:
                        var evalNode = task.Node;
                        var evalDir = task.Direction;
                    EvaluateNode:
                        node = _owner._instructions[evalNode];
                        switch (node.Kind)
                        {
                            case EcmaNodeKind.Empty:
                                break;
                            case EcmaNodeKind.Sequence:
                                if (!Push(new(TaskKind.Evaluate, evalDir > 0 ? node.Y : node.X, 0, 0, evalDir), ref state))
                                {
                                    return -1;
                                }

                                evalNode = evalDir > 0 ? node.X : node.Y;
                                goto EvaluateNode;
                            case EcmaNodeKind.Alternative:
                                if (evalDir > 0)
                                {
                                    // A branch that cannot start here fails without being tried: skip it, and save no
                                    // choice for a second branch that would fail the same way (see _startBits).
                                    var startBits = _owner._startBits;
                                    var atEnd = Position >= _input.Length;
                                    var current = atEnd ? '\0' : _input[Position];
                                    var left = startBits[node.X];
                                    if (atEnd ? !left.Empty : !left.CanStartWith(current))
                                    {
                                        evalNode = node.Y;
                                        goto EvaluateNode;
                                    }

                                    var right = startBits[node.Y];
                                    if (atEnd ? !right.Empty : !right.CanStartWith(current))
                                    {
                                        evalNode = node.X;
                                        goto EvaluateNode;
                                    }
                                }

                                if (!Save(ChoiceKind.Branch, new(TaskKind.Evaluate, node.Y, 0, 0, evalDir), ref state))
                                {
                                    return -1;
                                }

                                evalNode = node.X;
                                goto EvaluateNode;
                            case EcmaNodeKind.Capture:
                                if (_saveRegisters && !Push(new(TaskKind.CaptureEnd, evalNode, Position, 0, evalDir), ref state))
                                {
                                    return -1;
                                }

                                evalNode = node.X;
                                goto EvaluateNode;
                            case EcmaNodeKind.Repeat:
                                if (!Push(new(TaskKind.Repeat, evalNode, 0, 0, evalDir), ref state))
                                {
                                    return -1;
                                }

                                break;
                            case EcmaNodeKind.Literal:
                                succeeds = Read(Position, evalDir, ref state, out var point, out var next) &&
                                    Equal(point, node.Value, node.EffectiveFlags, ref state);
                                if (succeeds)
                                {
                                    Position = next;
                                }

                                break;
                            case EcmaNodeKind.Set:
                                if (evalDir > 0 && _owner._accelerator.FastSets[evalNode] is { } fastSet && fastSet.Slice.StringCount == 0)
                                {
                                    if ((uint)Position >= (uint)_input.Length)
                                    {
                                        succeeds = false;
                                        break;
                                    }
                                    var ch = _input[Position];
                                    if (fastSet.MatchesSingleChar(ch))
                                    {
                                        if (!state.TryCharge(2, Position))
                                        {
                                            return -1;
                                        }

                                        Position++;
                                        break;
                                    }
                                    if (fastSet.BmpBits is not null && (!_owner.Program.Profile.IsUnicodeAware || !char.IsSurrogate(ch)))
                                    {
                                        if (!state.TryCharge(2, Position))
                                        {
                                            return -1;
                                        }

                                        succeeds = false;
                                        break;
                                    }
                                }
                                succeeds = MatchSet(node.Value, 0, evalDir, evalNode, ref state);
                                break;
                            case EcmaNodeKind.Backreference:
                                succeeds = Backreference(node, evalDir, ref state);
                                break;
                            case EcmaNodeKind.Start:
                                succeeds = Position == 0 || (node.EffectiveFlags & EcmaScriptFlags.Multiline) != 0 &&
                                    IsLine(Position - 1, ref state);
                                break;
                            case EcmaNodeKind.End:
                                succeeds = Position == _input.Length || (node.EffectiveFlags & EcmaScriptFlags.Multiline) != 0 &&
                                    IsLine(Position, ref state);
                                break;
                            case EcmaNodeKind.WordBoundary:
                            case EcmaNodeKind.NotWordBoundary:
                                succeeds = Word(Position, -1, node.EffectiveFlags, ref state) != Word(Position, 1, node.EffectiveFlags, ref state);
                                if (node.Kind == EcmaNodeKind.NotWordBoundary)
                                {
                                    succeeds = !succeeds;
                                }

                                break;
                            case EcmaNodeKind.Lookahead:
                            case EcmaNodeKind.Lookbehind:
                            case EcmaNodeKind.NegativeLookahead:
                            case EcmaNodeKind.NegativeLookbehind:
                                var sentinel = _choiceCount;
                                var negative = node.Kind is EcmaNodeKind.NegativeLookahead or EcmaNodeKind.NegativeLookbehind;
                                if (!Save(negative ? ChoiceKind.NegativeAssertionFailure : ChoiceKind.PositiveAssertionFailure, null, ref state) ||
                                    !Push(new(TaskKind.AssertionEnd, evalNode, 0, Position, evalDir, sentinel), ref state))
                                {
                                    return -1;
                                }

                                evalNode = node.X;
                                evalDir = node.Kind is EcmaNodeKind.Lookbehind or EcmaNodeKind.NegativeLookbehind ? -1 : 1;
                                goto EvaluateNode;
                        }
                        break;
                }
                if (state.Failure.Code != RegexExecutionFailureCode.None)
                {
                    return -1;
                }

                if (succeeds)
                {
                    continue;
                }

                var resumed = false;
                while (_choiceCount > 0)
                {
                    var index = _choiceCount - 1;
                    var kind = (ChoiceKind)(_choices[index * ChoiceWidth] & ChoiceKindMask);
                    if (!Restore(index, ref state))
                    {
                        return -1;
                    }

                    _choiceCount = index;
                    if (kind == ChoiceKind.PositiveAssertionFailure)
                    {
                        ResetLow();
                        continue;
                    }

                    if (kind == ChoiceKind.GreedyRun)
                    {
                        // Give back one character (one code point when Unicode-aware) and stay armed until the
                        // run is back at its lower bound, which is the last position the run can offer.
                        var run = _choices.Slice(index * ChoiceWidth, ChoiceWidth);
                        var low = run[3];
                        var position = Position - 1;
                        if ((run[0] >> ChoiceNodeShift) is var runNode and > 0 && _owner._giveBack![runNode] is { } follow && position > low)
                        {
                            // Straight to the last position the continuation can start at; the lower bound is always
                            // tried, as it ends the run.
                            var window = _input.Slice(low + 1, position - low);
                            var hit = _owner._giveBackExcept![runNode] ? window.LastIndexOfAnyExcept(follow) : window.LastIndexOfAny(follow);
                            if (!state.TryCharge(Math.Max(1, window.Length - hit - 1), Position))
                            {
                                return -1;
                            }

                            position = hit < 0 ? low : low + 1 + hit;
                        }

                        if (_owner.Program.Profile.IsUnicodeAware && position > low &&
                            char.IsLowSurrogate(_input[position]) && char.IsHighSurrogate(_input[position - 1]))
                        {
                            position--;
                        }

                        Position = position;
                        if (position > low)
                        {
                            run[2] = position;
                            _choiceCount++;
                        }
                    }

                    ResetLow();
                    resumed = true;
                    break; // ordinary retry or successful negative assertion
                }
                if (!resumed)
                {
                    return 0;
                }
            }
        }

        // A greedy repeat of a single-character atom (/.*/, /[^"]*/, /a+/ once past its minimum) consumes
        // its whole run in one loop and saves ONE choice that gives the run back a character at a time, where the
        // generic path saved a choice per character. Equivalent, because every one of those choices restored the same
        // task stack and the same registers: the atom captures nothing, and each was taken after the Repeat task had
        // been popped.
        private bool GreedyRun(EcmaNode node, int nodeId, int iterations, ref RunState state)
        {
            var start = Position;
            var limit = node.Max == -1 ? int.MaxValue : node.Max - iterations;
            var count = 0;
            if (limit == int.MaxValue && _owner._instructions[node.X].Kind == EcmaNodeKind.Set &&
                _owner._accelerator.FastSets[node.X] is { UnicodeRunStops: { } stops })
            {
                // Only the run's extent matters when it is unbounded; see EcmaFastSet.UnicodeRunStops.
                var rest = _input.Slice(Position);
                var units = _owner._accelerator.FastSets[node.X]!.StopsAtLineTerminatorsOnly
                    ? EcmaFastSet.IndexOfLineTerminator(rest, stops) : rest.IndexOfAny(stops);
                units = units < 0 ? rest.Length : units;
                if (!state.TryCharge(Math.Max(1, units), Position))
                {
                    return false;
                }

                Position += units;
                count = units > 0 ? 1 : 0;
                limit = 0;
            }
            else if (_owner._instructions[node.X].Kind == EcmaNodeKind.Set && _owner._accelerator.FastSets[node.X] is { BmpBits: not null } fastSet)
            {
                // A case-sensitive class has an exact membership bitmap, which the accelerator scans vectorized.
                count = fastSet.CountRun(_input.Slice(Position), limit == int.MaxValue ? -1 : limit, _owner.Program, out var units);
                if (!state.TryCharge(Math.Max(1, units), Position))
                {
                    return false;
                }

                Position += units;
                limit = 0;
            }

            while (count < limit)
            {
                var matched = MatchAtom(node.X, ref state);
                if (state.Failure.Code != RegexExecutionFailureCode.None)
                {
                    return false;
                }

                if (!matched)
                {
                    break;
                }

                count++;
            }

            if (count == 0)
            {
                return true;
            }

            if (!Save(ChoiceKind.GreedyRun, null, ref state))
            {
                return false;
            }

            _choices[(_choiceCount - 1) * ChoiceWidth + 3] = start;
            if (_owner._giveBack is not null && nodeId <= MaxChoiceNode)
            {
                _choices[(_choiceCount - 1) * ChoiceWidth] |= nodeId << ChoiceNodeShift;
            }
            return true;
        }

        private bool MatchAtom(int nodeId, ref RunState state)
        {
            var atom = _owner._instructions[nodeId];
            if (atom.Kind == EcmaNodeKind.Literal)
            {
                if (Read(Position, 1, ref state, out var point, out var next) && Equal(point, atom.Value, atom.EffectiveFlags, ref state))
                {
                    Position = next;
                    return true;
                }

                return false;
            }

            if (_owner._accelerator.FastSets[nodeId] is { } fastSet)
            {
                if ((uint)Position >= (uint)_input.Length)
                {
                    return false;
                }

                var ch = _input[Position];
                if (fastSet.MatchesSingleChar(ch))
                {
                    if (!state.TryCharge(2, Position))
                    {
                        return false;
                    }

                    Position++;
                    return true;
                }

                // The ASCII bitmap is exact whenever the set has no strings: it is built with the same case folding
                // MatchSet applies, so under i - where there is no BMP bitmap - an ASCII miss is still final. Without
                // this every non-member ASCII character went through two binary searches (/[a-z]+@/i: 45x slower).
                if ((fastSet.BmpBits is not null && (!_owner.Program.Profile.IsUnicodeAware || !char.IsSurrogate(ch))) ||
                    (ch < 128 && fastSet.Slice.StringCount == 0))
                {
                    return state.TryCharge(2, Position) && false;
                }
            }

            return MatchSet(atom.Value, 0, 1, nodeId, ref state);
        }

        private bool Body(Task task, ref RunState state)
        {
            var node = _owner._instructions[task.Node];
            if (_saveRegisters && node.CaptureCount > 0)
            {
                if (!state.TryCharge(2L * node.CaptureCount, Position))
                {
                    return false;
                }

                for (var r = 2 * node.FirstCapture; r < 2 * (node.FirstCapture + node.CaptureCount); r++)
                {
                    if (!SetRegister(r, -1, ref state))
                    {
                        return false;
                    }
                }
            }
            if (_owner._accelerator.NonNullable[task.Node] && task.A >= node.Min && node.Max == -1)
            {
                return Push(new(TaskKind.Repeat, task.Node, task.A, 0, task.Direction), ref state) &&
                    Push(new(TaskKind.Evaluate, node.X, 0, 0, task.Direction), ref state);
            }
            return Push(new(TaskKind.RepeatAfter, task.Node, task.A, Position, task.Direction), ref state) &&
                Push(new(TaskKind.Evaluate, node.X, 0, 0, task.Direction), ref state);
        }

        private bool Push(Task task, ref RunState state)
        {
            if (_taskCount == _owner._taskCapacity)
            {
                return Exhaust(ref state);
            }

            if (!state.TryCharge(TaskWidth, Position))
            {
                return false;
            }

            var s = _tasks.Slice(_taskCount++ * TaskWidth, TaskWidth);
            s[0] = (int)task.Kind;
            s[1] = task.Node;
            s[2] = task.A;
            s[3] = task.B;
            s[4] = task.Direction;
            s[5] = task.C;
            return true;
        }
        // Copy-on-pop: the first time a slot under the top choice's height is popped, its value at that choice goes to
        // the trail. The stack moves one slot at a time, so those slots are popped in descending order from the choice's
        // height: the trail segment needs no slot numbers, only the height, and holds at most the choice's stack. A
        // slot pushed and popped again above the low-water mark is not the choice's and is not kept.
        private bool Pop(ref RunState state, out Task task)
        {
            task = default;
            var slot = _taskCount - 1;
            var trail = slot < _low;
            if (trail && _taskTrailCount + TaskWidth > _taskTrail.Length)
            {
                return Exhaust(ref state);
            }

            if (!state.TryCharge(trail ? 2 * TaskWidth : TaskWidth, Position))
            {
                return false;
            }

            var s = _tasks.Slice(slot * TaskWidth, TaskWidth);
            if (trail)
            {
                CopyTask(s, _taskTrail.Slice(_taskTrailCount, TaskWidth));
                _taskTrailCount += TaskWidth;
                _low = slot;
            }

            _taskCount = slot;
            task = new((TaskKind)s[0], s[1], s[2], s[3], s[4], s[5]);
            return true;
        }
        private bool Save(ChoiceKind kind, Task? append, ref RunState state)
        {
            if (_choiceCount == _frames)
            {
                return Exhaust(ref state);
            }

            // The task to resume with is pushed by Restore, at this height: refuse it here, as pushing it did.
            var appended = append.HasValue ? TaskWidth : 0;
            if ((append.HasValue && _taskCount == _owner._taskCapacity) || _taskTrailCount + appended > _taskTrail.Length)
            {
                return Exhaust(ref state);
            }

            if (!state.TryCharge(ChoiceWidth + appended, Position))
            {
                return false;
            }

            if (append.HasValue)
            {
                var task = append.Value;
                var t = _taskTrail.Slice(_taskTrailCount, TaskWidth);
                t[0] = (int)task.Kind;
                t[1] = task.Node;
                t[2] = task.A;
                t[3] = task.B;
                t[4] = task.Direction;
                t[5] = task.C;
                _taskTrailCount += TaskWidth;
            }

            var s = _choices.Slice(_choiceCount++ * ChoiceWidth, ChoiceWidth);
            s[0] = (int)kind | (append.HasValue ? AppendFlag : 0);
            s[1] = _taskCount;
            s[2] = Position;
            s[3] = 0;
            s[4] = _taskTrailCount;
            s[5] = _registerTrailCount;
            _low = _taskCount;
            return true;
        }
        // Rewinds to the state the choice at index saved, undoing every choice above it, and pushes its appended task.
        // The caller decides whether the choice survives (_choiceCount) and then calls ResetLow.
        private bool Restore(int index, ref RunState state)
        {
            var s = _choices.Slice(index * ChoiceWidth, ChoiceWidth);
            if (!state.TryCharge((long)(_taskTrailCount - s[4]) + (_registerTrailCount - s[5]) + ChoiceWidth, Position))
            {
                return false;
            }

            var end = _taskTrailCount;
            for (var i = _choiceCount - 1; i >= index; i--)
            {
                var frame = _choices.Slice(i * ChoiceWidth, ChoiceWidth);
                var mark = frame[4];
                for (int from = mark, slot = frame[1] - 1; from < end; from += TaskWidth, slot--)
                {
                    CopyTask(_taskTrail.Slice(from, TaskWidth), _tasks.Slice(slot * TaskWidth, TaskWidth));
                }

                end = (frame[0] & AppendFlag) != 0 ? mark - TaskWidth : mark;
            }

            for (var e = _registerTrailCount - RegisterEntryWidth; e >= s[5]; e -= RegisterEntryWidth)
            {
                var r = _registerTrail[e];
                Registers[r] = _registerTrail[e + 1];
                _stamps[r] = _registerTrail[e + 2];
            }

            _registerTrailCount = s[5];
            _taskCount = s[1];
            Position = s[2];
            if ((s[0] & AppendFlag) != 0)
            {
                CopyTask(_taskTrail.Slice(end, TaskWidth), _tasks.Slice(_taskCount++ * TaskWidth, TaskWidth));
            }

            _taskTrailCount = end;
            return true;
        }
        // A positive assertion succeeded: drop the choices from index up without rewinding. Its body never popped
        // below its AssertionEnd task, so the task trail above the assertion's frame belongs to the dropped choices
        // alone. The register trail does not: a capture inside the assertion stays set, and the first value it
        // overwrote is what the surviving top choice must restore. Keep one entry per register, the oldest, unless
        // that choice already trailed the register.
        private void Discard(int index)
        {
            var s = _choices.Slice(index * ChoiceWidth, ChoiceWidth);
            _taskTrailCount = s[4];
            var write = s[5];
            for (var read = write; read < _registerTrailCount; read += RegisterEntryWidth)
            {
                // Every register trailed above the frame is stamped deeper than index until this loop visits it.
                var r = _registerTrail[read];
                if (_stamps[r] <= index)
                {
                    continue;
                }

                _stamps[r] = index;
                var previous = _registerTrail[read + 2];
                if (index > 0 && previous != index)
                {
                    _registerTrail[write] = r;
                    _registerTrail[write + 1] = _registerTrail[read + 1];
                    _registerTrail[write + 2] = previous;
                    write += RegisterEntryWidth;
                }
            }

            _registerTrailCount = write;
            _choiceCount = index;
            ResetLow();
        }
        // The top choice's trail segment holds its slots from its height down to its low-water mark.
        private void ResetLow()
        {
            if (_choiceCount == 0)
            {
                _low = 0;
                return;
            }

            var s = _choices.Slice((_choiceCount - 1) * ChoiceWidth, ChoiceWidth);
            _low = s[1] - (_taskTrailCount - s[4]) / TaskWidth;
        }
        // A register write the top choice must be able to undo is trailed once per choice: _stamps holds the choice
        // count (the depth) at the register's last trailing, and an undo puts the previous stamp back.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool SetRegister(int register, int value, ref RunState state)
        {
            if (_choiceCount > 0 && _stamps[register] != _choiceCount && !TrailRegister(register, ref state))
            {
                return false;
            }

            Registers[register] = value;
            return true;
        }
        private bool TrailRegister(int register, ref RunState state)
        {
            var count = _registerTrailCount;
            if (count + RegisterEntryWidth > _registerTrail.Length)
            {
                return Exhaust(ref state);
            }

            if (!state.TryCharge(RegisterEntryWidth, Position))
            {
                return false;
            }

            var entry = _registerTrail.Slice(count, RegisterEntryWidth);
            entry[2] = _stamps[register];
            entry[1] = Registers[register];
            entry[0] = register;
            _registerTrailCount = count + RegisterEntryWidth;
            _stamps[register] = _choiceCount;
            return true;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void CopyTask(ReadOnlySpan<int> from, Span<int> to)
        {
            to[5] = from[5];
            to[4] = from[4];
            to[3] = from[3];
            to[2] = from[2];
            to[1] = from[1];
            to[0] = from[0];
        }
        private readonly bool Exhaust(ref RunState state)
        {
            state.Failure = new(RegexExecutionFailureCode.WorkspaceCapacityExceeded, Position, state.Budget.WorkConsumed);
            return false;
        }

        internal readonly bool Read(int position, int direction, ref RunState state, out int point, out int next)
        {
            point = 0;
            next = position;
            var index = direction > 0 ? position : position - 1;
            if ((uint)index >= (uint)_input.Length)
            {
                return false;
            }

            if (!state.TryCharge(1, index))
            {
                return false;
            }

            point = _input[index];
            next = position + direction;
            if (!_owner.Program.Profile.IsUnicodeAware)
            {
                return true;
            }

            var other = index + direction;
            if ((uint)other >= (uint)_input.Length ||
                !(direction > 0 ? char.IsHighSurrogate((char)point) : char.IsLowSurrogate((char)point)))
            {
                return true;
            }

            if (!state.TryCharge(1, other))
            {
                return false;
            }

            var c = _input[other];
            if (direction > 0 ? char.IsLowSurrogate(c) : char.IsHighSurrogate(c))
            {
                point = direction > 0 ? char.ConvertToUtf32((char)point, c) : char.ConvertToUtf32(c, (char)point);
                next += direction;
            }
            return true;
        }
        private readonly bool Equal(int a, int b, EcmaScriptFlags flags, ref RunState state)
        {
            if ((flags & EcmaScriptFlags.IgnoreCase) != 0)
            { a = Canonical(a, ref state); b = Canonical(b, ref state); }
            return state.Failure.Code == RegexExecutionFailureCode.None && a == b;
        }
        private readonly int Canonical(int point, ref RunState state)
        {
            if ((uint) point < 128)
            {
                return _owner._asciiCanonical[point];
            }

            var mappings = _owner.Program.Profile.IsUnicodeAware ? _owner._caseTables.UnicodeFold : _owner._caseTables.LegacyUppercase;
            var lo = 0;
            var hi = mappings.Length - 1;
            while (lo <= hi)
            {
                if (!state.TryCharge(1, Position))
                {
                    return point;
                }

                var mid = lo + (hi - lo) / 2;
                var item = mappings[mid];
                if (point < item.Source)
                {
                    hi = mid - 1;
                }
                else if (point > item.Source)
                {
                    lo = mid + 1;
                }
                else
                {
                    return item.Target;
                }
            }
            return point;
        }
        private readonly bool IsLine(int index, ref RunState state)
        {
            if ((uint)index >= (uint)_input.Length)
            {
                return false;
            }

            if (!state.TryCharge(1, index))
            {
                return false;
            }

            return _input[index] is '\n' or '\r' or '\u2028' or '\u2029';
        }
        private readonly bool Word(int position, int direction, EcmaScriptFlags flags, ref RunState state)
        {
            if (!Read(position, direction, ref state, out var point, out _))
            {
                return false;
            }

            if (_owner.Program.Profile.IsUnicodeAware && (flags & EcmaScriptFlags.IgnoreCase) != 0)
            {
                point = Canonical(point, ref state);
            }

            return point is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';
        }
        private bool Backreference(EcmaNode node, int direction, ref RunState state)
        {
            var group = node.Value;
            if (node.NameIndex >= 0)
            {
                group = 0;
                var name = _owner.Program.Names[node.NameIndex];
                foreach (var id in _owner.Program.NameAliases.Slice(name.Offset, name.Length))
                {
                    if (!state.TryCharge(2, Position))
                    {
                        return false;
                    }

                    if (Registers[2 * id] >= 0)
                    { group = id; break; }
                }
            }
            if (group == 0)
            {
                return true;
            }

            if (!state.TryCharge(2, Position))
            {
                return false;
            }

            var begin = Registers[2 * group];
            var end = Registers[2 * group + 1];
            if (begin < 0)
            {
                return true;
            }

            var cursor = direction > 0 ? begin : end;
            var target = Position;
            while (direction > 0 ? cursor < end : cursor > begin)
            {
                if (!Read(cursor, direction, ref state, out var a, out var after) ||
                    !Read(target, direction, ref state, out var b, out var next) ||
                    !Equal(a, b, node.EffectiveFlags, ref state))
                {
                    return false;
                }
                // Registers always hold legal element boundaries produced by this kernel.
                cursor = after;
                target = next;
            }
            Position = target;
            return true;
        }
        private bool MatchSet(int setId, int from, int direction, int nodeId, ref RunState state)
        {
            var set = _owner.Program.Sets[setId];
            var nonempty = set.StringCount;
            while (nonempty > 0)
            {
                if (!state.TryCharge(1, Position))
                {
                    return false;
                }

                if (_owner.Program.Strings[set.StringOffset + nonempty - 1].Length != 0)
                {
                    break;
                }

                nonempty--;
            }
            var selected = -1;
            var end = Position;
            // At most one deferred resume: preserve normative longest-first ordering,
            // including interval (one point) before the empty finite string.
            for (var option = from; option <= set.StringCount; option++)
            {
                if (!state.TryCharge(1, Position))
                {
                    return false;
                }

                var target = Position;
                var accepts = false;
                if (option == nonempty)
                {
                    if (Read(Position, direction, ref state, out var point, out target))
                    {
                        if ((_owner._instructions[nodeId].EffectiveFlags & EcmaScriptFlags.IgnoreCase) != 0)
                        {
                            point = Canonical(point, ref state);
                        }

                        accepts = Contains(set, point, ref state);
                    }
                }
                else
                {
                    var index = option < nonempty ? option : option - 1;
                    var str = _owner.Program.Strings[set.StringOffset + index];
                    accepts = true;
                    for (var i = 0; i < str.Length; i++)
                    {
                        if (!state.TryCharge(1, target) || !Read(target, direction, ref state, out var point, out var next))
                        { accepts = false; break; }
                        var expected = _owner.Program.StringCodePoints[str.Offset + (direction > 0 ? i : str.Length - 1 - i)];
                        if (!Equal(point, expected, _owner._instructions[nodeId].EffectiveFlags, ref state))
                        { accepts = false; break; }
                        target = next;
                    }
                }
                if (state.Failure.Code != RegexExecutionFailureCode.None)
                {
                    return false;
                }

                if (!accepts)
                {
                    continue;
                }

                if (selected < 0)
                { selected = option; end = target; }
                else
                {
                    if (!Save(ChoiceKind.Branch, new(TaskKind.SetResume, nodeId, option, 0, direction), ref state))
                    {
                        return false;
                    }

                    break;
                }
            }
            if (selected < 0)
            {
                return false;
            }

            Position = end;
            return true;
        }
        private readonly bool Contains(EcmaSetSlice set, int point, ref RunState state)
        {
            // Input is canonicalized above. Set folding/complement order is parser-owned;
            // do not transform its resolved intervals or use ambient host categories.
            var ranges = _owner.Program.Intervals.Slice(set.IntervalOffset, set.IntervalCount);
            var lo = 0;
            var hi = ranges.Length - 1;
            while (lo <= hi)
            {
                if (!state.TryCharge(1, Position))
                {
                    return false;
                }

                var mid = lo + (hi - lo) / 2;
                var r = ranges[mid];
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
    }
}
