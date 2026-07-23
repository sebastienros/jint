using System.Diagnostics;
using Jint.Runtime;

namespace Jint.Constraints;

/// <summary>
/// Fails execution once a fixed interval has elapsed, measured against a deadline captured when the
/// constraint is reset.
/// </summary>
/// <remarks>
/// The deadline is compared inline rather than observed through a <c>CancellationTokenSource</c>
/// timer. A timer only makes the elapsed time visible once its callback has run on the thread pool,
/// so detection was bounded by scheduling rather than by the timeout itself — the same reason the
/// regular expression timeout moved to an inline deadline. Reading the timestamp costs a
/// <see cref="Stopwatch.GetTimestamp"/> per check, which the amortized check cadence already bounds,
/// and in exchange every execution stops allocating a token source and registering (then cancelling)
/// a timer.
/// </remarks>
internal sealed class TimeConstraint : Constraint
{
    private readonly long _timeoutTicks;

    // Stopwatch timestamp the current execution must not pass; 0 means "no execution has started",
    // which mirrors the previous null-CancellationTokenSource state where Check never failed.
    private long _deadline;

    internal TimeConstraint(TimeSpan timeout)
    {
        // Options.TimeoutInterval only constructs this for 0 < timeout < TimeSpan.MaxValue. The
        // clamp keeps `now + _timeoutTicks` from overflowing for very large intervals, which would
        // otherwise wrap to a deadline already in the past.
        var ticks = timeout.Ticks * ((double) Stopwatch.Frequency / TimeSpan.TicksPerSecond);
        _timeoutTicks = ticks >= long.MaxValue / 2.0 ? long.MaxValue / 2 : (long) ticks;
    }

    public override void Check()
    {
        var deadline = _deadline;
        if (deadline != 0 && Stopwatch.GetTimestamp() >= deadline)
        {
            Throw.TimeoutException();
        }
    }

    public override void Reset()
    {
        var deadline = Stopwatch.GetTimestamp() + _timeoutTicks;

        // 0 is the not-started sentinel, so never store it as a real deadline
        _deadline = deadline == 0 ? 1 : deadline;
    }
}
