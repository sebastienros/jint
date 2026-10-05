using Jint.Runtime;

namespace Jint.Constraints;

/// <remarks>
/// A long string concatenation that defers its copy is charged when it is built, for the characters it
/// appends, and one whose result alone exceeds the limit fails at once. The measured allocation can therefore
/// include characters that nothing has allocated yet.
/// </remarks>
public sealed class MemoryLimitConstraint : Constraint
{
    private readonly long _memoryLimit;
    private long _initialMemoryUsage;
    private int _initialThreadId;

    // Characters a deferred concatenation stands for that the allocation counter has not seen: charged by
    // ChargeDeferredConcatenation, counted by Check, cleared by Reset with the rest of the entry's baseline.
    private long _deferredBytes;

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
        var allocated = SaturatingAdd(usage - _initialMemoryUsage, _deferredBytes);
        if (allocated > _memoryLimit)
        {
            Throw.MemoryLimitExceededException($"Script has allocated {allocated} but is limited to {_memoryLimit}");
        }
    }

    /// <summary>
    /// Charges the current entry for a concatenation that defers its copy instead of making it
    /// (<see cref="Native.JsString.Concat"/>), as if the characters had been allocated where the copy would
    /// have been (sebastienros/jint#4162).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The charge is what the concatenation <em>appends</em>, its shorter operand: the same thing the result
    /// costs when built with <c>+=</c>, and what keeps an accumulator linear, because charging every node its
    /// whole length charges <c>s = s + x</c> for a copy of <c>s</c> on every iteration — the quadratic cost
    /// the deferral exists to remove. It is also enough to bound every read of the value: following the
    /// longer operand down from a node reaches one real string, and each node on that path added no more
    /// than its shorter operand, so a node never stands for more than that string plus what its path was
    /// charged, whoever flattens it and whenever.
    /// </para>
    /// <para>
    /// That argument cannot bound a path charged across several entries, each within its own budget, or
    /// a real string the script never allocated — a host string with a character appended. So a result
    /// whose flat form alone exceeds the whole budget is charged in full, exactly as the copy it replaced
    /// would have been, and refused before the node exists: no entry leaves behind a value larger than
    /// its budget. That refusal does not depend on the allocation counter, so it holds on a thread
    /// <see cref="Check"/> cannot measure as well.
    /// </para>
    /// </remarks>
    internal void ChargeDeferredConcatenation(int leftLength, int rightLength)
    {
        if (_memoryLimit <= 0)
        {
            return;
        }

        var length = (long) leftLength + rightLength;
        if (length * sizeof(char) <= _memoryLimit)
        {
            // Seen by the next check — the statement's own, or the one closing the entry.
            _deferredBytes = SaturatingAdd(_deferredBytes, Math.Min(leftLength, rightLength) * (long) sizeof(char));
            return;
        }

        _deferredBytes = SaturatingAdd(_deferredBytes, length * sizeof(char));
        Check();

        // Check() measures nothing on a thread other than the one the entry started on; the value alone is
        // still over the budget.
        Throw.MemoryLimitExceededException($"Script has allocated at least {length * sizeof(char)} but is limited to {_memoryLimit}");
    }

    private static long SaturatingAdd(long left, long right)
    {
        var sum = left + right;
        return ((left ^ sum) & (right ^ sum)) < 0 ? long.MaxValue : sum;
    }

    public override void Reset()
    {
        _initialThreadId = Environment.CurrentManagedThreadId;
        _deferredBytes = 0;

        // Total on purpose. Engine.ExecuteWithConstraints resets the constraints from a finally, so an
        // exception raised here unwinds in place of the one the run is already carrying: the script's own
        // error disappears and the host is told about the platform instead. On a platform where the
        // allocation counter cannot be reached at all -- netstandard2.0 on a runtime that predates it, or
        // one whose linker removed it -- the baseline is simply unknown. Check() is where such a platform
        // gets reported, and it still reports it on the very first check.
        _initialMemoryUsage = GCPolyfills.TryGetAllocatedBytesForCurrentThread(out var allocatedBytes) ? allocatedBytes : 0;
    }
}
