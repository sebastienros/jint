using Jint.Runtime;

namespace Jint.Constraints;

public sealed class MemoryLimitConstraint : Constraint
{
    private readonly long _memoryLimit;
    private long _initialMemoryUsage;
    private int _initialThreadId;

    internal MemoryLimitConstraint(long memoryLimit)
    {
        _memoryLimit = memoryLimit;
    }

    /// <summary>
    /// Never amortizable: unlike a clock, allocation between two checks is irreversible and unbounded per
    /// statement — a single iteration can allocate arbitrarily much (exponential string growth, say) — so
    /// checking less often would let the process overshoot the configured cap, potentially to a real
    /// <see cref="OutOfMemoryException"/>, instead of merely noticing it late. Staying exact is what keeps
    /// the limit usable as a hard-ish bound when sandboxing untrusted code.
    /// </summary>
    public override bool IsAmortizable => false;

    public override void Check()
    {
        if (_memoryLimit <= 0)
        {
            return;
        }

        // GC.GetAllocatedBytesForCurrentThread() is per-thread. If an async continuation
        // resumed on a different thread pool thread, comparing counters across threads is
        // meaningless and can produce false positives or silently bypass the limit.
        // Skipping the check is the safe fallback for that case.
        if (Environment.CurrentManagedThreadId != _initialThreadId)
        {
            return;
        }

        var usage = GC.GetAllocatedBytesForCurrentThread();
        if (usage - _initialMemoryUsage > _memoryLimit)
        {
            Throw.MemoryLimitExceededException($"Script has allocated {usage - _initialMemoryUsage} but is limited to {_memoryLimit}");
        }
    }

    public override void Reset()
    {
        _initialThreadId = Environment.CurrentManagedThreadId;

        // Total on purpose. Engine.ExecuteWithConstraints resets the constraints from a finally, so an
        // exception raised here unwinds in place of the one the run is already carrying: the script's own
        // error disappears and the host is told about the platform instead. On a platform where the
        // allocation counter cannot be reached at all -- netstandard2.0 on a runtime that predates it, or
        // one whose linker removed it -- the baseline is simply unknown. Check() is where such a platform
        // gets reported, and it still reports it on the very first check.
        _initialMemoryUsage = GCPolyfills.TryGetAllocatedBytesForCurrentThread(out var allocatedBytes) ? allocatedBytes : 0;
    }
}
