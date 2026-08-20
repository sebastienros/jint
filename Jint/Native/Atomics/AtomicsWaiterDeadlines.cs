using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Jint.Native.Atomics;

/// <summary>
/// The deadlines of one engine's pending finite-timeout <c>Atomics.waitAsync</c> waits: a min-heap keyed on a
/// <see cref="Stopwatch"/> timestamp, which the event-loop pump consults so that a wait times out without any
/// thread but the engine's own being involved.
/// <para>
/// https://tc39.es/ecma262/#sec-dowait
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Engine-thread only, and deliberately lock-free.</b> Registration happens while <c>Atomics.waitAsync</c>
/// is running, and every other member is reached from the pump or from the two idle waits that bound
/// themselves by <see cref="TimeUntilNextDeadline"/> — all of which are the single thread that owns the
/// engine. Nothing here starts a thread, a <see cref="System.Threading.Timer"/> or a <c>Task</c>: a wait
/// times out on the first pump at or after its deadline, and an engine nobody pumps never times one out —
/// which it never did anyway, since the settlement has always had to run as an event-loop job.
/// </para>
/// <para>
/// The one thing that <em>is</em> cross-thread is a wait ending the other way: <c>Atomics.notify</c> may be
/// called by any agent sharing the block, and settles the waiter from that agent's thread. It does not reach
/// this heap at all — the entry is left behind, marked by the waiter's own settle-once flag, and discarded
/// when it next surfaces, exactly as a cancelled timer is. Reading that flag here without synchronization is
/// deliberate: a stale <see langword="false"/> only costs one more pass, because the compare-and-swap inside
/// <see cref="AtomicsInstance.AsyncWaiter.Resolve"/> is what actually decides the outcome.
/// </para>
/// </remarks>
internal sealed class AtomicsWaiterDeadlines
{
    /// <summary>How many <see cref="TimeSpan"/> ticks one <see cref="Stopwatch"/> tick is worth.</summary>
    private static readonly double TicksPerStopwatchTick = (double) TimeSpan.TicksPerSecond / Stopwatch.Frequency;

    /// <summary>How many <see cref="Stopwatch"/> ticks one millisecond is worth.</summary>
    private static readonly double StopwatchTicksPerMillisecond = Stopwatch.Frequency / 1000.0;

    private Entry[] _entries = new Entry[4];
    private int _count;

    /// <summary>Breaks ties between waits due at the same instant, so equal deadlines settle FIFO.</summary>
    private long _nextSequence;

    /// <summary>
    /// Registers <paramref name="waiter"/> to time out <paramref name="timeoutMilliseconds"/> from now.
    /// </summary>
    internal void Add(AtomicsInstance.AsyncWaiter waiter, double timeoutMilliseconds)
    {
        var entry = new Entry(DeadlineFromNow(timeoutMilliseconds), _nextSequence++, waiter);

        if (_count == _entries.Length)
        {
            System.Array.Resize(ref _entries, _entries.Length * 2);
        }

        var index = _count++;
        while (index > 0)
        {
            var parent = (index - 1) / 2;
            if (!entry.IsBefore(in _entries[parent]))
            {
                break;
            }

            _entries[index] = _entries[parent];
            index = parent;
        }

        _entries[index] = entry;
    }

    /// <summary>
    /// Settles every wait whose deadline has passed, and discards the entries of waits that
    /// <c>Atomics.notify</c> has already settled.
    /// </summary>
    /// <returns>
    /// Whether nothing is pending any more, which is what lets the engine drop the whole registry and take
    /// its per-job cost back down to a single null test.
    /// </returns>
    internal bool SettleDue()
    {
        if (_count == 0)
        {
            return true;
        }

        // One clock read per call rather than one per entry: a settlement only enqueues a job, so no time
        // that matters passes inside the loop.
        var now = Stopwatch.GetTimestamp();

        while (_count > 0)
        {
            var waiter = _entries[0].Waiter;
            if (!waiter.Resolved)
            {
                if (_entries[0].Deadline > now)
                {
                    return false;
                }

                RemoveRoot();
                waiter.SettleTimedOut();
                continue;
            }

            RemoveRoot();
        }

        return true;
    }

    /// <summary>
    /// How long until the next wait times out: <see langword="null"/> when none is pending, and
    /// <see cref="TimeSpan.Zero"/> when one is due already. The idle waits use it to bound an otherwise
    /// unbounded sleep, since a deadline coming due enqueues nothing and so wakes nobody.
    /// </summary>
    internal TimeSpan? TimeUntilNextDeadline()
    {
        while (_count > 0)
        {
            if (_entries[0].Waiter.Resolved)
            {
                RemoveRoot();
                continue;
            }

            var remaining = _entries[0].Deadline - Stopwatch.GetTimestamp();
            if (remaining <= 0)
            {
                return TimeSpan.Zero;
            }

            // Built from ticks rather than through TimeSpan.FromSeconds, which is documented as accurate only
            // to the nearest millisecond: a remainder of a few hundred microseconds would round to zero, the
            // wait loops would read that as "due now" and skip their wait, and the pump would spin hot until
            // the deadline genuinely arrived. TimeProvider.GetElapsedTime, which the timer queue uses for the
            // same question, has no such rounding.
            return TimeSpan.FromTicks((long) (remaining * TicksPerStopwatchTick));
        }

        return null;
    }

    /// <summary>
    /// The deadline of a wait asking for <paramref name="timeoutMilliseconds"/>, on the same monotonic clock
    /// <c>Atomics.wait</c> uses. Clamped exactly where the timeout task clamped it, so a wait asking for more
    /// than <see cref="int.MaxValue"/> milliseconds keeps the ~24-day bound it always had; the ceiling is
    /// what stops a sub-millisecond request rounding down to no wait at all.
    /// </summary>
    /// <remarks>
    /// A deadline on a monotonic timestamp cannot fire early, which is the property the old re-delay loop
    /// existed to reconstruct: <c>Task.Delay</c> may complete slightly ahead of the interval it was given,
    /// and script can observe the lapse — test262 asserts it is at least the timeout asked for.
    /// </remarks>
    private static long DeadlineFromNow(double timeoutMilliseconds)
    {
        var milliseconds = System.Math.Min(System.Math.Ceiling(timeoutMilliseconds), int.MaxValue);
        return Stopwatch.GetTimestamp() + (long) (milliseconds * StopwatchTicksPerMillisecond);
    }

    private void RemoveRoot()
    {
        var last = --_count;
        if (last == 0)
        {
            _entries[0] = default;
            return;
        }

        var moved = _entries[last];

        // Cleared rather than left behind: the entry holds the waiter, and through it the engine, the realm
        // and the promise capability of a wait that is over.
        _entries[last] = default;

        var index = 0;
        while (true)
        {
            var child = index * 2 + 1;
            if (child >= last)
            {
                break;
            }

            var right = child + 1;
            if (right < last && _entries[right].IsBefore(in _entries[child]))
            {
                child = right;
            }

            if (!_entries[child].IsBefore(in moved))
            {
                break;
            }

            _entries[index] = _entries[child];
            index = child;
        }

        _entries[index] = moved;
    }

    /// <summary>
    /// One pending wait's place in the heap: when it times out, and — for waits due at the same instant —
    /// the order they were registered in.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly struct Entry
    {
        internal Entry(long deadline, long sequence, AtomicsInstance.AsyncWaiter waiter)
        {
            Deadline = deadline;
            Sequence = sequence;
            Waiter = waiter;
        }

        /// <summary>A <see cref="Stopwatch.GetTimestamp"/> reading, so monotonic and clock-change proof.</summary>
        internal long Deadline { get; }

        internal long Sequence { get; }

        internal AtomicsInstance.AsyncWaiter Waiter { get; }

        internal bool IsBefore(in Entry other)
        {
            return Deadline != other.Deadline ? Deadline < other.Deadline : Sequence < other.Sequence;
        }
    }
}
