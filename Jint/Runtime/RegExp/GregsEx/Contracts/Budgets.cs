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

    // A charge that fits, without the diagnostic: the parser charges several units per pattern character, and building
    // the failure's diagnostic on every one of them was a measurable share of compiling a short pattern. False means
    // TryCharge decides (a negative charge fails here too, and throws there).
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal bool TryChargeFast(long units)
    {
        if ((ulong)units <= (ulong)(Limits.MaxCompileWork - WorkConsumed))
        {
            WorkConsumed += units;
            return true;
        }

        return false;
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
    // Where the relative timeout began: construction, or for a deferred start the first clock read the stride asks for.
    private long _startedAt;
    private readonly double _timeoutTicks;
    private long _nextClockCheck;
    // Set by the absolute-deadline constructor, whose caller reads the clock per search, and by a deferred start, which
    // reads it at the first stride: a zero-unit checkpoint then waits for the stride like any other charge, instead of
    // reading the clock again at the start of every search.
    private readonly bool _checkpointsAmortized;
    // A deferred start the clock has not been read for yet; _startedAt stays 0 until it is.
    private bool _startPending;
    internal long WorkConsumed { get; private set; }
    // The timestamp the relative timeout is measured from; 0 while a deferred start is pending (see StartDeadline).
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
        : this(maxWork, cancellation, timeout, deferStart: false)
    {
    }

    // A relative timeout whose clock is first read by the stride, not here: a call that finishes before its first
    // ClockStride work units never reads the clock at all, where the eager constructors read it here and again on the
    // first charge. The deadline is measured from that first read, which comes on the first charge after the work
    // consumed has reached ClockStride. When the charge that reaches it is a bulk one (a run charged at once, as
    // TryChargeRun and the accelerators do) the read waits for the next charge, so the deadline can be late by up to
    // the time ClockStride units of work take plus the time of one bulk charge: still linear in the subject, and the
    // order of the overshoot the stride already allows past a deadline. A timeout within ShortDeadlineSeconds starts
    // here as before, so a tiny timeout stays as prompt as it was.
    // Only a call that runs from its construction to its end may defer. A session outlives its construction, and its
    // deadline counts the time between its steps from the moment it was opened, so it keeps the eager start.
    internal RunBudget(long maxWork, CancellationToken cancellation, TimeSpan? timeout, bool deferStart)
    {
        _maxWork = maxWork;
        _cancellation = cancellation;
        if (timeout is { } t)
        {
            _timeoutTicks = t.TotalSeconds * Stopwatch.Frequency;
            if (deferStart && t.TotalSeconds > ShortDeadlineSeconds)
            {
                _startedAt = 0;
                _nextClockCheck = ClockStride;
                _checkpointsAmortized = true;
                _startPending = true;
            }
            else
            {
                _startedAt = Stopwatch.GetTimestamp();
            }
        }
        else
        {
            _startedAt = 0;
            _timeoutTicks = double.PositiveInfinity;
        }
        WorkConsumed = 0;
    }

    // An absolute Stopwatch deadline (long.MaxValue for none), so a search starts without reading the clock when its
    // host already holds the deadline; converting it to a TimeSpan and back read the clock twice per search.
    internal RunBudget(long maxWork, CancellationToken cancellation, long absoluteDeadline)
    {
        _maxWork = maxWork;
        _cancellation = cancellation;
        _startedAt = 0;
        _timeoutTicks = absoluteDeadline == long.MaxValue ? double.PositiveInfinity : absoluteDeadline;
        // The caller read the clock to compute the deadline, so the first stride needs no read of its own.
        _nextClockCheck = ClockStride;
        _checkpointsAmortized = true;
        WorkConsumed = 0;
    }

    // Clock reads cost far more than a charge. Without a timeout the clock is never read; with one it is
    // read on the first charge, on every zero-unit checkpoint, and then once per ClockStride work units.
    // Deadline overshoot is therefore bounded by ClockStride units of work, and cancellation and the
    // work limit are still checked on every charge. When the deadline is within ShortDeadlineSeconds the
    // clock is read on every charge, so tiny timeouts fire as promptly as before amortization.
    // A deferred start (see the constructor above) reads the clock for the first time when the work consumed first
    // reaches ClockStride and the next charge arrives, and that read is where its timeout begins; every read after it
    // is as described above.
    internal const long ClockStride = 16384;
    private const double ShortDeadlineSeconds = 0.001;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal bool TryCharge(long units, int offset, out RegexExecutionFailure failure)
    {
        // With a timeout the fast path used to be skipped on every charge, so every timed match paid the slow path per
        // unit of work - 30-50% of a scan. A positive charge that stays short of the next clock read decides nothing
        // the slow path would decide differently; a zero-unit checkpoint still reads the clock, unless the deadline
        // was handed in absolute or its start is deferred (see _checkpointsAmortized).
        if ((double.IsPositiveInfinity(_timeoutTicks) ||
                ((units > 0 || _checkpointsAmortized) && WorkConsumed + units < _nextClockCheck)) &&
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
        if (!double.IsPositiveInfinity(_timeoutTicks) && ((units == 0 && !_checkpointsAmortized) || WorkConsumed >= _nextClockCheck))
        {
            timestamp = Stopwatch.GetTimestamp();
            if (_startPending)
            {
                // The first read of a deferred start is where its timeout begins.
                _startedAt = timestamp;
                _startPending = false;
            }

            var remaining = _timeoutTicks - (timestamp - _startedAt);
            _nextClockCheck = remaining < ShortDeadlineSeconds * Stopwatch.Frequency ? WorkConsumed : WorkConsumed + ClockStride;
        }
        else
        {
            timestamp = _startedAt;
        }

        return Evaluate(units, offset, timestamp, out failure);
    }

    // Begins a deferred start now, for a holder that copies the budget and polls it with timestamps of its own (a
    // kernel's guard): the copy would otherwise begin the timeout alone, and whatever the original runs afterwards
    // (the interpreter, after the kernel gives up) would begin it again.
    internal void StartDeadline()
    {
        if (_startPending)
        {
            _startedAt = Stopwatch.GetTimestamp();
            _startPending = false;
        }
    }

    // Deterministic deadline-boundary testing; production obtains time only above. A timestamp handed in is a clock
    // read, so the first one begins a deferred start instead of being measured from a start that has not happened.
    internal bool TryChargeAt(long units, int offset, long timestamp, out RegexExecutionFailure failure)
    {
        if (_startPending)
        {
            _startedAt = timestamp;
            _startPending = false;
        }

        return Evaluate(units, offset, timestamp, out failure);
    }

    private bool Evaluate(long units, int offset, long timestamp, out RegexExecutionFailure failure)
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
