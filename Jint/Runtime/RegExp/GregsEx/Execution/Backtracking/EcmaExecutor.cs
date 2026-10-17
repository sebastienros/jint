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
    private readonly EcmaAccelerator _accelerator;
    internal const int TaskWidth = 6;
    internal const int ChoiceWidth = 3;
    public EcmaProgram Program { get; }

    internal EcmaExecutor(EcmaProgram program, EcmaCaseTables tables, EcmaNode[] instructions, int taskCapacity)
    {
        Program = program;
        _caseTables = tables;
        _instructions = instructions;
        _taskCapacity = taskCapacity;
        _taskSlots = checked(TaskWidth * _taskCapacity);
        _registerSlots = checked(2 * Program.GroupCount);
        _accelerator = new EcmaAccelerator(program, _caseTables);
    }

    public RegexWorkspaceCapacity GetRequiredCapacity(SearchOperation operation, int frameCapacity)
    {
        if ((uint)operation > (uint)SearchOperation.Captures)
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameCapacity);
        return new(checked(_taskSlots + ChoiceWidth * frameCapacity),
            checked(_registerSlots * checked(frameCapacity + 1)), checked(_taskSlots * frameCapacity));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal int ComputeAdmittedFrames(RegexWorkspaceCapacity capacity)
    {
        var tasks = _taskSlots;
        var registers = _registerSlots;
        return Math.Min(Math.Max(0, capacity.StateSlots - tasks) / ChoiceWidth,
            Math.Min(Math.Max(0, capacity.CaptureSlots / registers - 1), capacity.ScratchSlots / tasks));
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
        return SearchCore(input, startAt, sticky, startPolicy, operation, frames, ref state, groups, out match);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal RegexSearchStatus SearchPrevalidated(ReadOnlySpan<char> input, int startAt, bool sticky,
        SearchOperation operation, int frames, ref RunState state,
        Span<RegexCapture> groups, out RegexMatchRange match)
    {
        match = default;
        return SearchCore(input, startAt, sticky, EcmaStartPolicy.BuiltinLastIndex, operation, frames, ref state, groups, out match);
    }

    private RegexSearchStatus SearchCore(ReadOnlySpan<char> input, int startAt, bool sticky,
        EcmaStartPolicy startPolicy, SearchOperation operation, int frames, ref RunState state,
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

        if (_accelerator.TrySearch(input, startAt, sticky, operation, frames, ref state, groups, out match, out var fastStatus))
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

        var saveRegisters = Program.GroupCount > 1 && (_accelerator.HasBackreferences || operation == SearchOperation.Captures);
        var machine = new Machine(this, input, state.Buffers, frames, saveRegisters);
        var candidate = startAt;
        while (true)
        {
            if (!sticky && candidate < input.Length)
            {
                if (_accelerator.PrefixLiteral is { } pfx)
                {
                    var rel = input.Slice(candidate).IndexOf(pfx.AsSpan());
                    if (rel < 0)
                    {
                        if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, Math.Max(1, input.Length - candidate)))
                        {
                            return Finish(FailureStatus(state), operation, groups);
                        }

                        return Finish(RegexSearchStatus.NoMatch, operation, groups);
                    }
                    if (rel > 0)
                    {
                        if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, rel))
                        {
                            return Finish(FailureStatus(state), operation, groups);
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
                            return Finish(FailureStatus(state), operation, groups);
                        }

                        return Finish(RegexSearchStatus.NoMatch, operation, groups);
                    }
                    if (rel > 0)
                    {
                        if (!EcmaAccelerator.TryChargeFast(ref state, candidate, input.Length, rel))
                        {
                            return Finish(FailureStatus(state), operation, groups);
                        }

                        candidate += rel;
                    }
                }
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

    private RegexSearchStatus Finish(RegexSearchStatus status, SearchOperation operation, Span<RegexCapture> groups)
    { if (operation == SearchOperation.Captures) { groups[..Program.GroupCount].Clear(); } return status; }
    private static RegexSearchStatus FailureStatus(in RunState state) => state.Failure.Code switch
    {
        RegexExecutionFailureCode.Cancelled => RegexSearchStatus.Cancelled,
        RegexExecutionFailureCode.TimedOut => RegexSearchStatus.TimedOut,
        _ => RegexSearchStatus.LimitExceeded
    };

    private enum TaskKind { Evaluate, Repeat, RepeatBody, RepeatAfter, CaptureEnd, AssertionEnd, SetResume }
    private enum ChoiceKind { Branch, PositiveAssertionFailure, NegativeAssertionFailure }
    // A/B/C interpretation is specific to Kind. Direction is always explicit, including
    // tasks saved before entering a nested assertion with a different direction.
    private readonly record struct Task(TaskKind Kind, int Node, int A, int B, int Direction, int C = 0);

    private ref struct Machine
    {
        private readonly EcmaExecutor _owner;
        private readonly ReadOnlySpan<char> _input;
        private readonly Span<int> _tasks, _choices, _savedTasks, _savedRegisters;
        private readonly int _frames, _taskSlots;
        private readonly bool _saveRegisters;
        private int _taskCount, _choiceCount;
        internal readonly Span<int> Registers;
        internal int Position;
        internal Machine(EcmaExecutor owner, ReadOnlySpan<char> input, WorkspaceBuffers buffers, int frames, bool saveRegisters)
        {
            _owner = owner;
            _input = input;
            _frames = frames;
            _saveRegisters = saveRegisters;
            _taskSlots = owner._taskSlots;
            _tasks = buffers.States[.._taskSlots];
            _choices = buffers.States.Slice(_taskSlots, frames * ChoiceWidth);
            Registers = buffers.Captures[..(2 * owner.Program.GroupCount)];
            _savedRegisters = buffers.Captures.Slice(Registers.Length, frames * Registers.Length);
            _savedTasks = buffers.Scratch[..(frames * _taskSlots)];
            _taskCount = _choiceCount = Position = 0;
        }

        internal int Run(int candidate, ref RunState state)
        {
            Position = candidate;
            _taskCount = _choiceCount = 0;
            if (!state.TryCharge(Registers.Length, Position))
            {
                return -1;
            }

            if (_saveRegisters)
            {
                Registers.Fill(-1);
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

                        Registers[2 * node.Value] = Math.Min(task.A, Position);
                        Registers[2 * node.Value + 1] = Math.Max(task.A, Position);
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
                            succeeds = false;
                        }
                        else
                        {
                            _choiceCount = task.C;
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
                    var index = --_choiceCount;
                    var kind = (ChoiceKind)_choices[index * ChoiceWidth];
                    if (!Restore(index, ref state))
                    {
                        return -1;
                    }

                    if (kind == ChoiceKind.PositiveAssertionFailure)
                    {
                        continue;
                    }

                    resumed = true;
                    break; // ordinary retry or successful negative assertion
                }
                if (!resumed)
                {
                    return 0;
                }
            }
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

                Registers.Slice(2 * node.FirstCapture, 2 * node.CaptureCount).Fill(-1);
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
        private bool Pop(ref RunState state, out Task task)
        {
            task = default;
            if (!state.TryCharge(TaskWidth, Position))
            {
                return false;
            }

            var s = _tasks.Slice(--_taskCount * TaskWidth, TaskWidth);
            task = new((TaskKind)s[0], s[1], s[2], s[3], s[4], s[5]);
            return true;
        }
        private bool Save(ChoiceKind kind, Task? append, ref RunState state)
        {
            if (_choiceCount == _frames)
            {
                return Exhaust(ref state);
            }

            if (append.HasValue && !Push(append.Value, ref state))
            {
                return false;
            }

            var used = _taskCount * TaskWidth;
            if (!state.TryCharge((long)used + Registers.Length + ChoiceWidth, Position))
            {
                return false;
            }

            _tasks[..used].CopyTo(_savedTasks.Slice(_choiceCount * _taskSlots, used));
            if (_saveRegisters)
            {
                Registers.CopyTo(_savedRegisters.Slice(_choiceCount * Registers.Length, Registers.Length));
            }

            var s = _choices.Slice(_choiceCount++ * ChoiceWidth, ChoiceWidth);
            s[0] = (int)kind;
            s[1] = _taskCount;
            s[2] = Position;
            if (append.HasValue)
            {
                _taskCount--;
            }

            return true;
        }
        private bool Restore(int index, ref RunState state)
        {
            var s = _choices.Slice(index * ChoiceWidth, ChoiceWidth);
            var count = s[1];
            if (!state.TryCharge((long)count * TaskWidth + Registers.Length + ChoiceWidth, Position))
            {
                return false;
            }

            _savedTasks.Slice(index * _taskSlots, count * TaskWidth).CopyTo(_tasks);
            if (_saveRegisters)
            {
                _savedRegisters.Slice(index * Registers.Length, Registers.Length).CopyTo(Registers);
            }

            _taskCount = count;
            Position = s[2];
            return true;
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
