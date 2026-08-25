#nullable enable

using System.Diagnostics;
using Xunit.Sdk;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Which callbacks a host parked on <see cref="Engine.TaskOperations.WaitForScheduledWork"/> — or on its
/// asynchronous sibling — may be interrupted by, and which it may not. The pump is a callback-admission
/// window: a host that parks its engine's own thread there has said "run scheduled work here", which is the
/// undertaking to yield that lets an authorized cross-thread callback wait for its turn instead of being
/// refused (sebastienros/jint#3242, sebastienros/jint#3240).
/// </summary>
/// <remarks>
/// <para>
/// The window is deliberately scoped to a <em>top-level</em> park. A pump reached from inside a running
/// evaluation has undertaken nothing — the thread is in the middle of somebody else's script — so it keeps
/// refusing, which <see cref="ANestedParkInsideARunningEvaluationAdmitsNothing"/> is the pin for.
/// </para>
/// <para>
/// Every handshake here is released by a thread the test owns. The one place a park is detected rather than
/// signalled is <see cref="TopLevelPark"/>: the pump runs no script, so the only thing it can announce itself
/// with is the refusal it hands an unrelated public entry — which is also why that detection and the park it
/// detects have to be one object, since the probe is the only thing in a healthy run that can refuse the
/// park's own entry.
/// </para>
/// </remarks>
public class HostPumpAdmissionTests
{
    private const string ConcurrentUseMessage =
        "*already in use by another thread or has an asynchronous operation in progress*";

    /// <summary>
    /// Reached only by a genuine wedge — every wait below is released by a thread the test owns.
    /// </summary>
    private static readonly TimeSpan WedgeCeiling = TestBudgets.WedgeCeiling;

    /// <summary>
    /// How often a detection loop looks. It decides only when an observation is made, never what it has to be.
    /// </summary>
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// A park short enough that an admitted callback outlives it, for the one test about what a ceiling
    /// actually bounds.
    /// </summary>
    private static readonly TimeSpan ShortParkCeiling = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The bound on waiting for something an admitted callback is supposed to do. Nothing spends it on a
    /// working engine — it is what turns "the callback was never admitted" into a report rather than into a
    /// two-minute wedge.
    /// </summary>
    private static readonly TimeSpan AdmissionCeiling = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How many times the nested-park test tries the callback, and how long it leaves between tries. Neither
    /// number selects the state being asserted about — see that test's remarks — so they only decide how many
    /// observations are made and how far apart.
    /// </summary>
    private const int NestedProbeCount = 12;

    private static readonly TimeSpan NestedProbeInterval = TimeSpan.FromMilliseconds(40);

    /// <summary>
    /// A callback the engine authorized under an operation that has since <em>ended</em> — the stale case, and
    /// the only one the pump's old reservation identity could never match. The conversion happens while
    /// <c>capture(...)</c> is on the stack, so the authorization names that
    /// <see cref="Engine.Execute(string, string?)"/>'s token; by the time it returns, that token is dead and
    /// nothing but the anonymous wildcard can admit it.
    /// </summary>
    private static Func<int> AuthorizeStaleCallback(Engine engine, string body = "42")
    {
        Func<int>? callback = null;
        engine.SetValue("capture", new Action<Func<int>>(value => callback = value));
        engine.Execute($"capture(() => {body});");
        callback.Should().NotBeNull("the host method was handed a converted callback");
        return callback!;
    }

    [Fact]
    public async Task AStaleAuthorizedCallbackIsAdmittedAtASynchronousPark()
    {
        using var engine = new Engine();
        var callback = AuthorizeStaleCallback(engine);
        using var dispatcher = new CallbackDispatcher(() => callback());
        using var releasePark = new CancellationTokenSource();

        var park = TopLevelPark.Start(engine, WedgeCeiling, releasePark.Token);
        park.WaitUntilOwningTheEngine();

        dispatcher.Release();
        await dispatcher.Attempted;
        releasePark.Cancel();
        await park.Completed;

        dispatcher.Failure.Should().BeNull("a top-level park is a window an authorized callback may wait in");
        dispatcher.Result.Should().Be(42);
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Fact]
    public async Task AStaleAuthorizedCallbackIsAdmittedAtAnAsynchronousPark()
    {
        using var engine = new Engine();
        var callback = AuthorizeStaleCallback(engine);
        using var dispatcher = new CallbackDispatcher(() => callback());
        using var releasePark = new CancellationTokenSource();

        // Its reservation is taken synchronously by the call above, so — unlike the synchronous form — the
        // detector's first probe cannot land before the park owns the engine.
        var parked = engine.Tasks.WaitForScheduledWorkAsync(WedgeCeiling, releasePark.Token);
        TopLevelPark.WaitUntilOwningTheEngine(engine, parked);

        dispatcher.Release();
        await dispatcher.Attempted;
        releasePark.Cancel();
        await Awaiting(() => parked).Should().ThrowAsync<OperationCanceledException>();

        dispatcher.Failure.Should().BeNull("the asynchronous park reserves under the same anonymous identity");
        dispatcher.Result.Should().Be(42);
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The boundary the change does not move: a host blocking its engine's thread in an ordinary synchronous
    /// call has undertaken nothing, so the same authorized callback is refused there exactly as before.
    /// </summary>
    [Fact]
    public async Task AStaleAuthorizedCallbackIsRefusedInAnUnrelatedBlockingHostCall()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var engine = new Engine();
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait(WedgeCeiling);
        }));

        var callback = AuthorizeStaleCallback(engine);
        var running = DedicatedThread.RunAsync(() => engine.Execute("block();"));
        WaitUntilSignalled(entered, running);
        try
        {
            Invoking(() => callback())
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The fail-fast guarantee is untouched: an unrelated public entry from another thread is refused for the
    /// whole park, which is what keeps one-drainer-per-engine self-enforcing.
    /// </summary>
    [Fact]
    public async Task AnUnrelatedPublicEntryIsRefusedWhileTheParkHoldsTheEngine()
    {
        using var engine = new Engine();
        using var releasePark = new CancellationTokenSource();

        var park = TopLevelPark.Start(engine, WedgeCeiling, releasePark.Token);
        park.WaitUntilOwningTheEngine();
        try
        {
            Invoking(() => engine.Evaluate("1 + 1"))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            releasePark.Cancel();
        }

        await park.Completed;
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The guard's pin. A pump called from host code the script itself invoked is <em>not</em> a window: the
    /// engine thread is in the middle of an evaluation that was never handed this callback, and admitting one
    /// there would interleave its script into the middle of that evaluation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every attempt provably lands inside the evaluation, and no clock decides that. The probing thread is
    /// started by a host call the script makes immediately before the park, so the engine is owned by the
    /// evaluating thread from before the first attempt; and the park is ended by <em>the prober</em>, which
    /// cancels it only once its last attempt has returned, so the evaluation cannot finish while an attempt is
    /// still outstanding. The interval and the count above therefore choose only how many observations are
    /// made and how far apart, never which state they are made in.
    /// </para>
    /// <para>
    /// A ceiling used to end the park, and that was a bug in this test rather than in the engine: an attempt
    /// landing after the ceiling ran out met an engine <see cref="Engine.Execute(string, string?)"/> had
    /// already released, where an authorized callback is admitted by claiming a free engine and nothing about
    /// the pump is involved. On a slow enough runner the probing loop outlived the park, and those admissions
    /// were reported as if the guard had failed.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ANestedParkInsideARunningEvaluationAdmitsNothing()
    {
        using var engine = new Engine();
        using var endPark = new CancellationTokenSource();
        var callback = AuthorizeStaleCallback(engine);

        var attempts = 0;
        var admitted = 0;
        var refused = 0;
        Task? prober = null;

        engine.SetValue("armProber", new Action(() => prober = DedicatedThread.RunAsync(() =>
        {
            for (var i = 0; i < NestedProbeCount; i++)
            {
                Thread.Sleep(NestedProbeInterval);
                attempts++;
                try
                {
                    callback();
                    admitted++;
                }
                catch (InvalidOperationException)
                {
                    refused++;
                }
            }

            // Only now, with every attempt returned, may the evaluation be allowed to finish.
            endPark.Cancel();
        })));
        engine.SetValue("park", new Func<bool>(() =>
        {
            try
            {
                return engine.Tasks.WaitForScheduledWork(WedgeCeiling, endPark.Token);
            }
            catch (OperationCanceledException)
            {
                // How the prober ends the park; what this test asserts is what happened while it was parked.
                return false;
            }
        }));

        engine.Execute("armProber(); globalThis.reported = park();");

        await prober!;

        attempts.Should().Be(NestedProbeCount);
        admitted.Should().Be(0, "a pump inside a running evaluation has undertaken nothing");
        refused.Should().Be(NestedProbeCount);
        engine.Evaluate("reported").AsBoolean().Should().BeFalse();
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// An admitted callback is an ordinary engine turn, so work it queues wakes the park it was admitted into
    /// and is reported as work for <see cref="Engine.TaskOperations.ProcessTasks"/> — which is the whole
    /// point of admitting it there.
    /// </summary>
    [Fact]
    public async Task WorkQueuedByAnAdmittedCallbackIsReportedByThePark()
    {
        using var engine = new Engine();
        engine.Execute("globalThis.ran = false;");
        var callback = AuthorizeStaleCallback(engine, "(Promise.resolve().then(() => { globalThis.ran = true; }), 7)");
        using var dispatcher = new CallbackDispatcher(() => callback());

        var park = TopLevelPark.Start(engine, AdmissionCeiling);
        park.WaitUntilOwningTheEngine();

        dispatcher.Release();
        await dispatcher.Attempted;
        await park.Completed;

        dispatcher.Failure.Should().BeNull();
        dispatcher.Result.Should().Be(7);
        park.Reported.Should().BeTrue("the reaction the callback queued is work the pump can run");

        engine.Evaluate("ran").AsBoolean().Should().BeFalse("the wait does not pump");
        engine.Tasks.ProcessTasks();
        engine.Evaluate("ran").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The one genuinely new observable: the ceiling bounds the <em>idle wait</em>, not the call. An admitted
    /// callback holds the engine, and the park cannot return until it has the thread back — so a
    /// frame-budgeted host can be handed control back well after its ceiling.
    /// </summary>
    [Fact]
    public async Task AnAdmittedCallbackHoldsTheParkPastItsCeiling()
    {
        using var engine = new Engine();
        using var holding = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        engine.SetValue("hold", new Action(() =>
        {
            holding.Set();
            releaseCallback.Wait(WedgeCeiling);
        }));

        var callback = AuthorizeStaleCallback(engine, "(hold(), 3)");
        using var dispatcher = new CallbackDispatcher(() => callback());

        var park = TopLevelPark.Start(engine, ShortParkCeiling);
        park.WaitUntilOwningTheEngine();

        dispatcher.Release();
        holding.Wait(AdmissionCeiling).Should().BeTrue("the callback has to be admitted for this to mean anything");

        // The ceiling has run out several times over by now, and the park is still not back: it is waiting for
        // the engine thread the admitted callback holds.
        Thread.Sleep(ShortParkCeiling + ShortParkCeiling + ShortParkCeiling);
        park.Completed.IsCompleted.Should().BeFalse("the ceiling bounds the idle wait, not the call");

        releaseCallback.Set();
        await dispatcher.Attempted;
        await park.Completed;

        dispatcher.Failure.Should().BeNull();
        dispatcher.Result.Should().Be(3);
        park.Reported.Should().BeFalse("the callback queued nothing, so there is still no work to report");
        park.Elapsed.Should().BeGreaterThan(ShortParkCeiling + ShortParkCeiling);
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    private static void WaitUntilSignalled(ManualResetEventSlim entered, Task running)
    {
        var elapsed = Stopwatch.StartNew();
        while (!entered.Wait(ProbeInterval))
        {
            if (running.IsCompleted)
            {
                // Deliberately not `await running`: that throws whatever stopped the call and loses the
                // sentence saying what was being waited for, which is what a reader needs first.
                var failure = running.Exception?.GetBaseException();
                throw new XunitException(
                    "the owning call returned without ever entering block(): "
                    + (failure is null ? "it returned normally" : $"it failed with {failure.GetType().Name}: {failure.Message}"),
                    failure);
            }

            if (elapsed.Elapsed > WedgeCeiling)
            {
                throw new XunitException($"the owning thread did not enter block() within {WedgeCeiling}");
            }
        }
    }

    /// <summary>
    /// A thread of the test's own, parked until <see cref="Release"/>, which then invokes the engine callback
    /// it was handed exactly once and records what came back. Deliberately not <c>Task.Run</c>: a pool worker
    /// has to be injected first and would land wherever the pool feels like, which is the wall-clock race
    /// every handshake here exists to avoid.
    /// </summary>
    private sealed class CallbackDispatcher : IDisposable
    {
        private readonly ManualResetEventSlim _release = new();
        private readonly TaskCompletionSource<bool> _attempted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Thread _thread;
        private volatile bool _abandoned;

        internal CallbackDispatcher(Func<int> invoke)
        {
            _thread = new Thread(() =>
            {
                _release.Wait(WedgeCeiling);
                if (_abandoned)
                {
                    // The test failed before it got as far as releasing this dispatcher, so there is nothing
                    // to dispatch and nothing to record. Leaving instead lets the failure be reported now
                    // rather than after a two-minute join.
                    return;
                }

                try
                {
                    Result = invoke();
                }
                catch (Exception exception)
                {
                    Failure = exception;
                }
                finally
                {
                    _attempted.TrySetResult(true);
                }
            })
            {
                IsBackground = true,
                Name = "jint-pump-admission-dispatcher",
            };

            _thread.Start();
        }

        /// <summary>
        /// Completes once the callback has been invoked, whatever the outcome — so a regression reports rather
        /// than hangs.
        /// </summary>
        internal Task Attempted => _attempted.Task;

        internal Exception? Failure { get; private set; }

        internal int Result { get; private set; }

        internal void Release() => _release.Set();

        public void Dispose()
        {
            // Abandoned rather than released: a test that failed before dispatching leaves this thread
            // waiting out the wedge ceiling, and joining it would spend those two minutes reporting nothing.
            _abandoned = !_release.IsSet;
            _release.Set();
            _thread.Join(WedgeCeiling);
            _release.Dispose();
        }
    }
}
