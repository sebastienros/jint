#nullable enable
using System.Threading;
using System.Diagnostics;
using Jint.Runtime.RegExp.GregsEx.IR;

namespace Jint.Runtime.RegExp.GregsEx.Contracts;

internal struct CompileBudget
{
    internal RegexCompileLimits Limits { get; }
    internal long WorkConsumed { get; private set; }
    internal long ReservedBytes { get; private set; }
    internal long PeakBytes { get; private set; }

    internal CompileBudget(RegexCompileLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        Limits = limits;
        WorkConsumed = 0;
        ReservedBytes = 0;
        PeakBytes = 0;
    }

    internal bool TryCharge(long units, SourceRange source, out RegexDiagnostic diagnostic)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        diagnostic = default;
        if (units > Limits.MaxCompileWork - WorkConsumed)
        {
            diagnostic = new(RegexDiagnosticCode.CompileWorkLimitExceeded,
                source.Offset, source.Length, source.Unit);
            return false;
        }
        WorkConsumed += units;
        return true;
    }

    // Call before every input-dependent allocation/growth, including temporary AST/lowering
    // buffers. Growth reserves the replacement before releasing the old capacity.
    internal bool TryReserveBytes(long bytes, SourceRange source, out RegexDiagnostic diagnostic)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        diagnostic = default;
        if (bytes > Limits.MaxCompileMemoryBytes - ReservedBytes)
        {
            diagnostic = new(RegexDiagnosticCode.CompileMemoryLimitExceeded,
                source.Offset, source.Length, source.Unit);
            return false;
        }
        ReservedBytes += bytes;
        PeakBytes = Math.Max(PeakBytes, ReservedBytes);
        return true;
    }

    internal void ReleaseBytes(long bytes)
    {
        if (bytes < 0 || bytes > ReservedBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes));
        }

        ReservedBytes -= bytes;
    }
}

// Entire invocation state, never recreated for candidates, closure passes or capture recovery.
internal struct RunBudget
{
    private readonly long _maxWork;
    private readonly CancellationToken _cancellation;
    private readonly long _startedAt;
    private readonly double _timeoutTicks;
    private long _nextClockCheck;
    internal long WorkConsumed { get; private set; }
    internal readonly long StartedAt => _startedAt;
    internal readonly long RemainingWork => _maxWork - WorkConsumed;
    internal readonly bool IsUnconstrained(long units) =>
        double.IsPositiveInfinity(_timeoutTicks) && !_cancellation.CanBeCanceled && units <= _maxWork - WorkConsumed;

    internal RunBudget(RegexMatchLimits ceiling, RegexMatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(ceiling);
        _maxWork = options.Resolve(ceiling).MaxWork;
        _cancellation = options.CancellationToken;
        // Elapsed subtraction avoids overflowing an absolute deadline for TimeSpan.MaxValue.
        if (options.Timeout is { } timeout)
        {
            _startedAt = Stopwatch.GetTimestamp();
            _timeoutTicks = timeout.TotalSeconds * Stopwatch.Frequency;
        }
        else
        {
            _startedAt = 0;
            _timeoutTicks = double.PositiveInfinity;
        }
        WorkConsumed = 0;
    }

    internal RunBudget(RegexMatchLimits resolvedLimits, CancellationToken cancellation, TimeSpan? timeout)
        : this(resolvedLimits.MaxWork, cancellation, timeout)
    {
    }

    internal RunBudget(long maxWork, CancellationToken cancellation, TimeSpan? timeout)
    {
        _maxWork = maxWork;
        _cancellation = cancellation;
        if (timeout is { } t)
        {
            _startedAt = Stopwatch.GetTimestamp();
            _timeoutTicks = t.TotalSeconds * Stopwatch.Frequency;
        }
        else
        {
            _startedAt = 0;
            _timeoutTicks = double.PositiveInfinity;
        }
        WorkConsumed = 0;
    }

    // Jint: an absolute Stopwatch deadline (long.MaxValue for none), so a search starts without reading the clock;
    // the host already holds the deadline, and converting it to a TimeSpan and back read the clock twice per search.
    internal RunBudget(long maxWork, CancellationToken cancellation, long absoluteDeadline)
    {
        _maxWork = maxWork;
        _cancellation = cancellation;
        _startedAt = 0;
        _timeoutTicks = absoluteDeadline == long.MaxValue ? double.PositiveInfinity : absoluteDeadline;
        // The caller read the clock to compute the deadline, so the first stride needs no read of its own.
        _nextClockCheck = ClockStride;
        WorkConsumed = 0;
    }

    // Clock reads cost far more than a charge. Without a timeout the clock is never read; with one it is
    // read on the first charge, on every zero-unit checkpoint, and then once per ClockStride work units.
    // Deadline overshoot is therefore bounded by ClockStride units of work, and cancellation and the
    // work limit are still checked on every charge. When the deadline is within ShortDeadlineSeconds the
    // clock is read on every charge, so tiny timeouts fire as promptly as before amortization.
    internal const long ClockStride = 256;
    private const double ShortDeadlineSeconds = 0.001;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal bool TryCharge(long units, int offset, out RegexExecutionFailure failure)
    {
        // Jint: with a timeout the fast path used to be skipped on every charge, so a timed match (every match
        // under Jint's default RegexTimeout) paid the slow path per unit of work - 30-50% of a scan. A charge that
        // stays short of the next clock read decides nothing the slow path would decide differently.
        if ((double.IsPositiveInfinity(_timeoutTicks) || WorkConsumed + units < _nextClockCheck) &&
            !_cancellation.IsCancellationRequested && (ulong)units <= (ulong)(_maxWork - WorkConsumed))
        {
            WorkConsumed += units;
            failure = default;
            return true;
        }
        return TryChargeSlow(units, offset, out failure);
    }

    private bool TryChargeSlow(long units, int offset, out RegexExecutionFailure failure)
    {
        long timestamp;
        if (!double.IsPositiveInfinity(_timeoutTicks) && (units == 0 || WorkConsumed >= _nextClockCheck))
        {
            timestamp = Stopwatch.GetTimestamp();
            var remaining = _timeoutTicks - (timestamp - _startedAt);
            _nextClockCheck = remaining < ShortDeadlineSeconds * Stopwatch.Frequency ? WorkConsumed : WorkConsumed + ClockStride;
        }
        else
        {
            timestamp = _startedAt;
        }

        return TryChargeAt(units, offset, timestamp, out failure);
    }

    // Deterministic deadline-boundary testing; production obtains time only above.
    internal bool TryChargeAt(long units, int offset, long timestamp, out RegexExecutionFailure failure)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        var code = _cancellation.IsCancellationRequested ? RegexExecutionFailureCode.Cancelled :
            timestamp - _startedAt >= _timeoutTicks ? RegexExecutionFailureCode.TimedOut :
            units > _maxWork - WorkConsumed ? RegexExecutionFailureCode.WorkLimitExceeded :
            RegexExecutionFailureCode.None;
        failure = code == RegexExecutionFailureCode.None ? default : new(code, offset, WorkConsumed);
        if (code != RegexExecutionFailureCode.None)
        {
            return false;
        }

        WorkConsumed += units;
        return true;
    }
}
