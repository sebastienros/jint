#nullable enable
using System.Threading;

namespace Jint.Runtime.RegExp.GregsEx.Contracts;

internal enum SearchOperation { Boolean, Range, Captures }

internal readonly ref struct WorkspaceBuffers
{
    internal Span<int> States { get; }
    internal Span<int> Captures { get; }
    internal Span<int> Scratch { get; }
    internal RegexWorkspaceCapacity Capacity => new(States.Length, Captures.Length, Scratch.Length);

    internal WorkspaceBuffers(Span<int> states, Span<int> captures, Span<int> scratch)
    {
        States = states;
        Captures = captures;
        Scratch = scratch;
    }
}

internal ref struct RunState
{
    internal WorkspaceBuffers Buffers { get; }
    internal RunBudget Budget;
    internal RegexExecutionFailure Failure;
    internal bool PrevalidatedCleanSubject;
    internal bool ValidatedCleanSubject;

    internal RunState(WorkspaceBuffers buffers, RegexMatchLimits limits, RegexMatchOptions options = default)
    {
        Buffers = buffers;
        Budget = new(limits, options);
        Failure = default;
    }

    internal RunState(WorkspaceBuffers buffers, RegexMatchLimits resolvedLimits, CancellationToken cancellation, TimeSpan? timeout)
    {
        Buffers = buffers;
        Budget = new(resolvedLimits.MaxWork, cancellation, timeout);
        Failure = default;
    }

    internal RunState(WorkspaceBuffers buffers, long maxWork, CancellationToken cancellation, TimeSpan? timeout)
    {
        Buffers = buffers;
        Budget = new(maxWork, cancellation, timeout);
        Failure = default;
    }

    // One charge for a run of one-unit-per-offset work that is known to fit the remaining budget;
    // otherwise per-unit charges reproduce the exact failing offset and consumed work.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal bool TryChargeRun(int firstOffset, int length)
    {
        if (length <= Budget.RemainingWork)
        {
            return TryCharge(length, firstOffset);
        }

        return TryChargeRunSlow(firstOffset, length);
    }

    private bool TryChargeRunSlow(int firstOffset, int length)
    {
        for (var i = 0; i < length; i++)
        {
            if (!TryCharge(1, firstOffset + i))
            {
                return false;
            }
        }

        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal bool TryCharge(long units, int offset)
    {
        if (Failure.Code != RegexExecutionFailureCode.None)
        {
            return false;
        }

        if (Budget.TryCharge(units, offset, out var failure))
        {
            return true;
        }

        Failure = failure;
        return false;
    }
}
