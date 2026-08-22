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
/// </remarks>
internal sealed class TimeConstraint : Constraint
{
    private readonly long _timeoutTicks;

#if NET8_0_OR_GREATER
    // Null in every engine that did not name a clock, which is what keeps Check() on the direct
    // Stopwatch read it has always used.
    private readonly TimeProvider? _timeProvider;
#endif

    // Timestamp the current execution must not pass, on whichever clock this constraint reads; 0 means
    // "no execution has started", which mirrors the previous null-CancellationTokenSource state where
    // Check never failed.
    private long _deadline;

#if NET8_0_OR_GREATER
    internal TimeConstraint(TimeSpan timeout, TimeProvider? timeProvider)
    {
        _timeProvider = ConstraintClock.Resolve(timeProvider);

        // Options.TimeoutInterval only constructs this for 0 < timeout < TimeSpan.MaxValue. The clamp
        // inside keeps `now + _timeoutTicks` from overflowing for very large intervals, which would
        // otherwise wrap to a deadline already in the past.
        _timeoutTicks = ConstraintClock.ToTimestampTicks(timeout, ConstraintClock.FrequencyOf(_timeProvider));
    }
#else
    internal TimeConstraint(TimeSpan timeout)
    {
        // Options.TimeoutInterval only constructs this for 0 < timeout < TimeSpan.MaxValue. The clamp
        // inside keeps `now + _timeoutTicks` from overflowing for very large intervals, which would
        // otherwise wrap to a deadline already in the past.
        _timeoutTicks = ConstraintClock.ToTimestampTicks(timeout, Stopwatch.Frequency);
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
