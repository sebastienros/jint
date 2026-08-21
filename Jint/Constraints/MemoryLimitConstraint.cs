using System.Runtime.InteropServices;
using Jint.Runtime;

namespace Jint.Constraints;

/// <summary>
/// Describes how accurately <see cref="MemoryLimitConstraint"/> can account managed allocations on the
/// current runtime.
/// </summary>
public enum MemoryLimitAccuracy
{
    /// <summary>
    /// The runtime does not expose a per-thread managed-allocation counter. Execution fails explicitly
    /// instead of silently running without the configured limit.
    /// </summary>
    Unavailable,

    /// <summary>
    /// Managed allocations are measured on every thread while that thread is actively executing work for
    /// the engine. Idle time between asynchronous continuations is excluded.
    /// </summary>
    ExecutionThread
}

/// <summary>
/// Bounds managed allocations performed while an engine executes one operation.
/// </summary>
/// <remarks>
/// <para>
/// The runtime only exposes allocation counters per managed thread. Jint therefore divides an operation
/// into synchronous execution segments, snapshots the current thread at each segment boundary, and carries
/// the accumulated total with promise reactions and module-load continuations. A continuation may resume on
/// any thread without losing the bytes allocated before the hop, and allocations performed by unrelated
/// work while the operation is suspended are not charged.
/// </para>
/// <para>
/// This is an allocation budget, not a retained-memory or process-memory limit. It includes managed
/// allocations made by synchronous host callbacks invoked by script, because those callbacks are part of
/// the engine operation. It cannot include unmanaged memory, allocations made by worker threads a host
/// callback starts, or work an asynchronous producer performs before handing its result back to Jint.
/// </para>
/// <para>
/// By default the budget is reset for every top-level engine entry. Use <see cref="Begin"/> and
/// <see cref="End"/> to cover a host operation made from several entries with one shared budget.
/// </para>
/// </remarks>
public sealed class MemoryLimitConstraint : Constraint
{
    private readonly long _memoryLimit;
    private OperationState? _reusableState;
    private OperationState? _lastState;
    private OperationState? _explicitOperation;
    private OperationState? _activeState;
    private long _segmentStart;
    private int _segmentThreadId;
    private int _segmentDepth;
    private Engine? _engine;

    public MemoryLimitConstraint(long memoryLimit)
    {
        if (memoryLimit <= 0 || memoryLimit == long.MaxValue)
        {
            Throw.ArgumentOutOfRangeException(nameof(memoryLimit), "Memory limit must be between 1 and Int64.MaxValue - 1.");
        }

        _memoryLimit = memoryLimit;
    }

    /// <summary>The managed-allocation budget in bytes.</summary>
    public long MemoryLimit => _memoryLimit;

    /// <summary>
    /// Managed bytes attributed to the current operation, or to the most recently entered operation when
    /// the engine is idle.
    /// </summary>
    public long AllocatedBytes
    {
        get
        {
            using var ownership = EnterHostCall();
            return GetUsage(_activeState ?? _lastState);
        }
    }

    /// <summary>The accounting accuracy available on the current runtime.</summary>
    public static MemoryLimitAccuracy Accuracy => GCPolyfills.AllocatedBytesForCurrentThreadIsSupported
        ? MemoryLimitAccuracy.ExecutionThread
        : MemoryLimitAccuracy.Unavailable;

    /// <summary>Whether a host-defined multi-entry operation is currently armed.</summary>
    public bool IsOperationActive
    {
        get
        {
            using var ownership = EnterHostCall();
            return _explicitOperation is not null;
        }
    }

    /// <summary>
    /// Starts a host-defined operation. Every engine entry and asynchronous continuation until
    /// <see cref="End"/> shares one allocation budget.
    /// </summary>
    /// <remarks>
    /// Calling <see cref="Begin"/> again re-arms the constraint with a fresh budget. Bytes allocated by host
    /// code between engine entries are not charged; only synchronous segments in which the engine is active
    /// are measurable without also charging unrelated process activity.
    /// </remarks>
    public void Begin()
    {
        using var ownership = EnterHostCall();
        if (_segmentDepth != 0)
        {
            Throw.InvalidOperationException("A memory-limit operation cannot be started while the engine is executing.");
        }

        EnsureSupported();
        if (_explicitOperation is { } previous)
        {
            previous.Enabled = false;
        }
        _explicitOperation = RentState();
        _lastState = _explicitOperation;
    }

    /// <summary>
    /// Ends the host-defined operation. A continuation that runs afterwards no longer shares this operation's
    /// accumulated total; it is treated as a fresh ordinary operation and receives a fresh budget.
    /// </summary>
    /// <remarks>Safe to call when no operation is active and safe to call more than once.</remarks>
    public void End()
    {
        using var ownership = EnterHostCall();
        if (_explicitOperation is { } state)
        {
            state.Enabled = false;
            _explicitOperation = null;
        }
    }

    /// <summary>
    /// Never amortizable: allocation between two checks is irreversible and unbounded per statement.
    /// </summary>
    public override bool IsAmortizable => false;

    public override void Check()
    {
        using var ownership = EnterHostCall();
        var state = _activeState;
        if (state is null || !state.Enabled)
        {
            return;
        }

        EnsureSupported();

        var usage = GetUsage(state);
        if (state.Exceeded || usage > _memoryLimit)
        {
            state.Exceeded = true;
            Throw.MemoryLimitExceededException($"Script has allocated {usage} but is limited to {_memoryLimit}");
        }
    }

    /// <summary>
    /// Clears the ordinary operation's accounting state. An explicitly armed
    /// <see cref="Begin"/>/<see cref="End"/> window remains host-owned and is not reset.
    /// The engine starts each ordinary entry through its internal segment lifecycle rather than calling this
    /// method on the way out, so the completed operation remains available through <see cref="AllocatedBytes"/>.
    /// </summary>
    public override void Reset()
    {
        using var ownership = EnterHostCall();
        if (_explicitOperation is null)
        {
            _lastState = null;
        }
    }

    internal OperationState BeginEntry()
    {
        EnsureSupported();
        var state = _explicitOperation ?? RentState();
        _lastState = state;
        return state;
    }

    internal OperationState ContinueOrBeginEntry(OperationState? capturedState)
        => capturedState is { Enabled: true } ? capturedState : BeginEntry();

    internal OperationState? CaptureOperationState()
    {
        var state = _activeState;
        if (state is null || !state.Enabled)
        {
            return null;
        }

        state.Captured = true;
        return state;
    }

    internal OperationState? CurrentOperationState => _activeState;

    internal void Attach(Engine engine)
    {
        if (_engine is not null && !ReferenceEquals(_engine, engine))
        {
            Throw.InvalidOperationException(
                "A MemoryLimitConstraint instance can only be registered with one Engine. Register a constraint factory when Options is shared.");
        }

        _engine = engine;
    }

    private Engine.HostCallScope? EnterHostCall() => _engine?.EnterHostCallIfNeeded();

    internal SegmentToken BeginSegment(OperationState? state)
    {
        if (ReferenceEquals(state, _activeState))
        {
            if (state is not null)
            {
                _segmentDepth++;
            }

            return new SegmentToken(Switched: false, null, 0);
        }

        var previousState = _activeState;
        var previousDepth = _segmentDepth;
        AccumulateActiveSegment();

        _activeState = state is { Enabled: true } ? state : null;
        _segmentDepth = _activeState is null ? 0 : 1;
        if (_activeState is not null)
        {
            StartSegment();
        }

        return new SegmentToken(Switched: true, previousState, previousDepth);
    }

    internal void EndSegment(in SegmentToken token)
    {
        if (!token.Switched)
        {
            if (_activeState is not null)
            {
                _segmentDepth--;
            }
            return;
        }

        AccumulateActiveSegment();
        _activeState = token.PreviousState;
        _segmentDepth = token.PreviousDepth;
        if (_activeState is not null && _segmentDepth > 0)
        {
            StartSegment();
        }
    }

    private OperationState RentState()
    {
        var state = _reusableState;
        if (state is null || state.Captured || ReferenceEquals(state, _activeState))
        {
            state = new OperationState();
            _reusableState = state;
        }
        else
        {
            state.AllocatedBytes = 0;
            state.Exceeded = false;
            state.Enabled = true;
        }

        return state;
    }

    private long GetUsage(OperationState? state)
    {
        if (state is null)
        {
            return 0;
        }

        var usage = state.AllocatedBytes;
        if (ReferenceEquals(state, _activeState)
            && _segmentDepth > 0
            && Environment.CurrentManagedThreadId == _segmentThreadId)
        {
            var current = GC.GetAllocatedBytesForCurrentThread();
            usage = SaturatingAdd(usage, current - _segmentStart);
        }

        return usage;
    }

    private void StartSegment()
    {
        EnsureSupported();
        _segmentThreadId = Environment.CurrentManagedThreadId;
        _segmentStart = GC.GetAllocatedBytesForCurrentThread();
    }

    private void AccumulateActiveSegment()
    {
        if (_activeState is null || _segmentDepth == 0)
        {
            return;
        }

        if (Environment.CurrentManagedThreadId != _segmentThreadId)
        {
            Throw.InvalidOperationException("A synchronous engine execution segment changed managed threads.");
        }

        var current = GC.GetAllocatedBytesForCurrentThread();
        _activeState.AllocatedBytes = SaturatingAdd(_activeState.AllocatedBytes, current - _segmentStart);
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (right <= 0)
        {
            return left;
        }

        return left >= long.MaxValue - right ? long.MaxValue : left + right;
    }

    private static void EnsureSupported()
    {
        if (!GCPolyfills.AllocatedBytesForCurrentThreadIsSupported)
        {
            Throw.PlatformNotSupportedException(
                "The current runtime does not expose GC.GetAllocatedBytesForCurrentThread, so Jint cannot enforce a memory allocation limit without charging unrelated process allocations.");
        }
    }

    internal sealed class OperationState
    {
        internal long AllocatedBytes;
        internal bool Captured;
        internal volatile bool Enabled = true;
        internal bool Exceeded;
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct SegmentToken(bool Switched, OperationState? PreviousState, int PreviousDepth);
}
