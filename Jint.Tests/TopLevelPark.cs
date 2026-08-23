#nullable enable

using System.Diagnostics;
using Xunit.Sdk;

namespace Jint.Tests;

/// <summary>
/// A <em>top-level</em> park on <see cref="Engine.AdvancedOperations.WaitForScheduledWork"/> run on a thread
/// of its own, together with the only observation that can tell a test the park is in force.
/// </summary>
/// <remarks>
/// <para>
/// The two halves live in one place because they are one handshake, not two. The pump runs no script, so a
/// parked wait has nothing to announce itself with: a test can only learn that the park owns the engine by
/// touching the engine from another thread and being refused. That probe is a claim attempt like any other,
/// and a claim attempt is the one thing that can refuse the park's <em>own</em> entry — a top-level park may
/// only be entered on an engine no other thread is holding, and the entry fails fast rather than waiting. In
/// a healthy test the detector is therefore the only thing that can stop the park it is looking for.
/// </para>
/// <para>
/// Only the entry is fragile, which is why nothing else here needs this treatment. Once the park owns the
/// engine it yields the thread for each idle wait and re-claims it afterwards by <em>waiting</em>, so a
/// probe — or an admitted callback — that claims the engine there delays the park rather than failing it.
/// The drain's window is not fragile at either end for the same reason, and its tests
/// (<c>HostDrainAdmissionTests</c>) need none of this.
/// </para>
/// <para>
/// The race is narrow and its dominant cause is systematic rather than random. Measured on an idle 32-core
/// machine over 50 rounds of exactly this shape: a warm probe holds the engine for 0–4 µs while the park
/// thread's entry lands 6–17 µs after the detection loop starts, so the two miss each other. The very first
/// probe in a process is the exception — it pays the JIT for <c>ProcessTasks</c> and holds for 1402 µs, which
/// the entry lands inside — and that was the one round of the fifty that lost the race.
/// <see cref="WarmTheProbePath"/> removes it. What remains is a probe preempted while holding, which is what
/// a loaded CI runner produces and what no ordering can remove; a refused entry is therefore retried, but
/// <em>only</em> when a detection probe was provably in flight across it. A refusal with the detector idle is
/// not this race, and is left to fail the test.
/// </para>
/// </remarks>
internal sealed class TopLevelPark
{
    /// <summary>
    /// How often the detection loop looks. It decides only when an observation is made, never what it has to
    /// be: every probe before the park is an empty <see cref="Engine.AdvancedOperations.ProcessTasks"/> loop.
    /// </summary>
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromMilliseconds(20);

    private readonly Engine _engine;

    /// <summary>
    /// Incremented on the way into a detection probe and again on the way out, so an odd value means a probe
    /// owns the engine right now and any change at all means one ran. It is what makes a refused entry
    /// attributable: the park thread samples it before an attempt and again if that attempt is refused.
    /// </summary>
    private long _probeState;

    private int _refusedStarts;

    /// <summary>
    /// The park this class runs itself.
    /// </summary>
    private TopLevelPark(Engine engine, TimeSpan ceiling, CancellationToken cancellationToken)
    {
        _engine = engine;

        // Last, and the thread's body reads nothing this constructor has not already set.
        Completed = DedicatedThread.RunAsync(() => Park(ceiling, cancellationToken));
    }

    /// <summary>
    /// A park the caller started itself.
    /// </summary>
    private TopLevelPark(Engine engine, Task parked)
    {
        _engine = engine;
        Completed = parked;
    }

    /// <summary>
    /// Parks on <paramref name="engine"/> from a thread of its own. The park ends when
    /// <paramref name="cancellationToken"/> is cancelled, when work arrives, or when
    /// <paramref name="ceiling"/> runs out — and <see cref="Completed"/> reports none of those as a failure,
    /// because which of them a test uses is the test's own business.
    /// </summary>
    public static TopLevelPark Start(Engine engine, TimeSpan ceiling, CancellationToken cancellationToken = default)
    {
        // Before the thread exists, so that nothing is racing the engine while this runs, and so that the
        // detection loop's first probe against the engine is a microsecond claim rather than a millisecond one.
        WarmTheProbePath();

        return new TopLevelPark(engine, ceiling, cancellationToken);
    }

    /// <summary>
    /// The detection half on its own, for a park the caller started itself — the asynchronous form, whose
    /// reservation is taken synchronously by the call that returns the task and which therefore has no start
    /// race to retry.
    /// </summary>
    public static void WaitUntilOwningTheEngine(Engine engine, Task parked)
        => new TopLevelPark(engine, parked).WaitUntilOwningTheEngine();

    /// <summary>
    /// Completes when the park returns, whatever ended it. Faults only on something this class does not
    /// recognise — including a refusal the detector cannot account for.
    /// </summary>
    public Task Completed { get; }

    /// <summary>
    /// What the park answered: whether it found work for <see cref="Engine.AdvancedOperations.ProcessTasks"/>.
    /// Read after awaiting <see cref="Completed"/>, which is what publishes it.
    /// </summary>
    public bool Reported { get; private set; }

    /// <summary>
    /// How long the park that actually held the engine took — an attempt refused at entry is not counted,
    /// because it never waited for anything.
    /// </summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>
    /// How many entries lost the start race to the detector's own probe. Zero on a healthy run; reported in
    /// the failure messages so a suite that starts losing it says so.
    /// </summary>
    public int RefusedStarts => Volatile.Read(ref _refusedStarts);

    /// <summary>
    /// Blocks until the park provably owns the engine, which the engine proves by refusing an unrelated
    /// public entry from this thread. Ends on that refusal, on the park returning without ever having owned
    /// the engine — reporting what stopped it rather than hanging — or on <see cref="TestBudgets.WedgeCeiling"/>.
    /// </summary>
    public void WaitUntilOwningTheEngine()
    {
        var elapsed = Stopwatch.StartNew();
        while (!ProbeIsRefused())
        {
            if (Completed.IsCompleted)
            {
                // Deliberately not `await Completed`: that throws whatever stopped the park and loses the
                // sentence saying what was being waited for, which is the whole of what a reader needs here.
                throw new XunitException(
                    "the park returned without ever owning the engine: " + Describe(),
                    Completed.Exception?.GetBaseException());
            }

            if (elapsed.Elapsed > TestBudgets.WedgeCeiling)
            {
                throw new XunitException($"the park did not claim the engine within {TestBudgets.WedgeCeiling}");
            }

            Thread.Sleep(ProbeInterval);
        }
    }

    private void Park(TimeSpan ceiling, CancellationToken cancellationToken)
    {
        var retrying = Stopwatch.StartNew();
        var backoff = new SpinWait();
        while (true)
        {
            var probes = Volatile.Read(ref _probeState);
            var attempt = Stopwatch.StartNew();
            try
            {
                Reported = _engine.Advanced.WaitForScheduledWork(ceiling, cancellationToken);
                Elapsed = attempt.Elapsed;
                return;
            }
            catch (OperationCanceledException)
            {
                // How a test ends a park it has finished with; what it asserts is what happened while the
                // park was in force.
                Elapsed = attempt.Elapsed;
                return;
            }
            catch (InvalidOperationException) when (ADetectionProbeWasInFlight(probes) && retrying.Elapsed < TestBudgets.WedgeCeiling)
            {
                // The start race, and the only refusal this class hides: the detector's own probe owned the
                // engine across this attempt, so the engine was in use and the entry was right to refuse.
                // The backoff is what keeps a retry from spinning a core for as long as that probe holds.
                Interlocked.Increment(ref _refusedStarts);
                backoff.SpinOnce();
            }
        }
    }

    private bool ProbeIsRefused()
    {
        // Bracketed so that a refused entry can be attributed to this probe rather than to the engine.
        Interlocked.Increment(ref _probeState);
        try
        {
            // An engine with nothing queued, so a probe landing before the park costs an empty loop.
            _engine.Advanced.ProcessTasks();
            return false;
        }
        catch (InvalidOperationException e) when (e.Message.Contains("already in use", StringComparison.Ordinal))
        {
            return true;
        }
        finally
        {
            Interlocked.Increment(ref _probeState);
        }
    }

    /// <summary>
    /// Whether a detection probe held the engine at any point between <paramref name="before"/> being sampled
    /// and now: it was already inside one (an odd sample), or one has started or finished since.
    /// </summary>
    private bool ADetectionProbeWasInFlight(long before)
        => (before & 1) == 1 || Volatile.Read(ref _probeState) != before;

    private string Describe()
    {
        var refused = RefusedStarts;
        var retried = refused == 0 ? "" : $", having lost the start race {refused} time(s) first";

        return Completed.Exception?.GetBaseException() is { } failure
            ? $"it failed with {failure.GetType().Name}: {failure.Message}{retried}"
            : $"it returned normally{retried}";
    }

    /// <summary>
    /// Pays the JIT for the detection probe's path on an engine of its own, rather than on the one under
    /// test — where it would drain a queue a test may have filled deliberately. See the class remarks for the
    /// measurement that makes this the difference between the first park in a process losing the start race
    /// and not.
    /// </summary>
    private static void WarmTheProbePath()
    {
        using var warmup = new Engine();
        warmup.Advanced.ProcessTasks();
    }
}
