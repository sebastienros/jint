using System.Diagnostics;
using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Constraints;

/// <summary>
/// Fails execution once a fixed interval has elapsed, measured against a deadline captured when the
/// constraint is reset.
/// </summary>
/// <remarks>
/// <para>
/// The deadline is compared inline rather than observed through a <c>CancellationTokenSource</c>
/// timer. A timer only makes the elapsed time visible once its callback has run on the thread pool,
/// so detection was bounded by scheduling rather than by the timeout itself — the same reason the
/// regular expression timeout moved to an inline deadline. Reading the timestamp costs a
/// <see cref="Stopwatch.GetTimestamp"/> per check, which the amortized check cadence already bounds,
/// and in exchange every execution stops allocating a token source and registering (then cancelling)
/// a timer.
/// </para>
/// <para>
/// The clock is <see cref="Stopwatch"/> unless <c>Options.Constraints.TimeProvider</c> named another one,
/// in which case that provider answers every timestamp this constraint reads. The field is
/// <see langword="null"/> for a default engine — see <c>ConstraintClock.Resolve</c> for the fold and
/// for why the branch it costs is the shape this seam had to take.
/// </para>
/// <para>
/// <b>The clock arrives from the engine, not from the options the interval was configured on.</b>
/// <c>LimitExecutionTime</c> registers a factory, and a factory can be replayed onto a <em>different</em>
/// <see cref="Options"/> instance — <c>WorkerRequest.CreateDefaultOptions</c> is the one thing that does it,
/// and it did so onto a worker that had a clock of its own. A closure over the configuring group therefore
/// put one engine's two time budgets on two different clocks
/// (<see href="https://github.com/sebastienros/jint/issues/3481">#3481</see>). <c>BindClock</c> is called by
/// <c>Engine</c> with the very provider <c>Engine.GetWaitTimestamp</c> reads, so the execution timeout and
/// the <c>PromiseTimeout</c> drain cannot disagree about what time it is.
/// </para>
/// </remarks>
internal sealed class TimeConstraint : Constraint
{
    // Options.LimitExecutionTime only constructs this for 0 < timeout < TimeSpan.MaxValue. The clamp inside
    // ToTimestampTicks keeps `now + _timeoutTicks` from overflowing for very large intervals, which would
    // otherwise wrap to a deadline already in the past.
#if NET8_0_OR_GREATER
    // Kept, and the ticks are mutable, because binding a clock re-expresses the interval on that clock's
    // tick scale. On the targets with no TimeProvider there is one scale and both are fixed at construction.
    private readonly TimeSpan _timeout;
    private long _timeoutTicks;

    // Null in every engine that did not name a clock, which is what keeps Check() on the direct
    // Stopwatch read it has always used.
    private TimeProvider? _timeProvider;
#else
    private readonly long _timeoutTicks;
#endif

    // Timestamp the current execution must not pass, on whichever clock this constraint reads; 0 means
    // "no execution has started", which mirrors the previous null-CancellationTokenSource state where
    // Check never failed.
    private long _deadline;

    internal TimeConstraint(TimeSpan timeout)
    {
#if NET8_0_OR_GREATER
        _timeout = timeout;
#endif
        _timeoutTicks = ConstraintClock.ToTimestampTicks(timeout, Stopwatch.Frequency);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Points this constraint at the clock of the engine it was built for, and re-expresses the interval on
    /// that clock's tick scale.
    /// </summary>
    /// <param name="resolvedProvider">
    /// A provider already put through <c>ConstraintClock.Resolve</c> — so <see langword="null"/> both for
    /// "no clock named" and for <see cref="TimeProvider.System"/>, and validated for a usable frequency at
    /// the point the host supplied it rather than here.
    /// </param>
    /// <remarks>
    /// Called once, from the <see cref="Engine"/> constructor, before the constraint has seen an execution.
    /// A constraint that is never bound keeps the <see cref="Stopwatch"/> scale its constructor computed,
    /// which is the same answer binding <see langword="null"/> gives.
    /// </remarks>
    internal void BindClock(TimeProvider? resolvedProvider)
    {
        _timeProvider = resolvedProvider;
        _timeoutTicks = ConstraintClock.ToTimestampTicks(_timeout, ConstraintClock.FrequencyOf(resolvedProvider));
    }
#endif

    /// <summary>
    /// A deadline is external state that <see cref="Check"/> only reads, and a clock only advances, so
    /// checking less often bounds how late the timeout is noticed rather than changing what is measured.
    /// </summary>
    public override bool IsAmortizable => true;

    public override void Check()
    {
        var deadline = _deadline;
        if (deadline != 0 && GetTimestamp() >= deadline)
        {
            Throw.TimeoutException();
        }
    }

    public override void Reset()
    {
        var deadline = GetTimestamp() + _timeoutTicks;

        // 0 is the not-started sentinel, so never store it as a real deadline
        _deadline = deadline == 0 ? 1 : deadline;
    }

#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetTimestamp()
    {
        var provider = _timeProvider;
        return provider is null ? Stopwatch.GetTimestamp() : provider.GetTimestamp();
    }
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetTimestamp() => Stopwatch.GetTimestamp();
#endif
}
