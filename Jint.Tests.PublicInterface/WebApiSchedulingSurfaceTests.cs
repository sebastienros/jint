#if NET8_0_OR_GREATER
#nullable enable

using System.Threading;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The scheduling surface a host driving its own loop actually touches:
/// <see cref="Engine.TaskOperations.TimeUntilNextScheduledWork"/> over the web-API sources,
/// <see cref="Engine.WebApiOperations.CreateAbortSignal"/> as the way its own cancellation reaches script,
/// and the <c>requestIdleCallback</c> globals.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is asserted through the public options
/// surface and through script, exactly as an embedder would have to. Nothing waits on the wall clock: the
/// timer-driven tests hand the engine a <see cref="ManualClock"/>, and the cross-thread test synchronises on a
/// thread's completion rather than on an interval.
/// </remarks>
public class WebApiSchedulingSurfaceTests
{
    /// <summary>
    /// The one wall-clock bound in this class, and it is reached only by a wedge: the thread it waits for is
    /// one the test started itself, doing nothing but <c>Cancel()</c>, so no amount of runner load can lose
    /// the race and only a cancellation that blocks forever — the very thing being ruled out — can spend two
    /// minutes here. Deliberately not the thirty-second interval it replaced (sebastienros/jint#3213): a
    /// bound that a slow machine can plausibly reach is a flake, and one it cannot is a check.
    /// </summary>
    private static readonly TimeSpan HandoffCeiling = TimeSpan.FromMinutes(2);

    /// <summary>A host-supplied clock, so that a suite exercising timed work need not sleep.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    private static (Engine Engine, ManualClock Clock) WebEngine(TimeSpan? idleBudget = null)
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Timers.TimeProvider = clock;
            if (idleBudget is { } budget)
            {
                webApi.Timers.IdleBudget = budget;
            }
        }));

        return (engine, clock);
    }

    /// <summary>
    /// A web-API engine with nothing outstanding answers <see langword="null"/>, so a host loop can tell "there
    /// is nothing for me to do" from "there is something, in a while".
    /// </summary>
    [Test]
    public void NothingScheduledIsReportedAsNothingScheduled()
    {
        var (engine, _) = WebEngine();

        engine.Execute("var x = 1;");

        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    /// <summary>
    /// A pending timer reports exactly how long is left, which is what lets a host sleep rather than poll. The
    /// clock is the host's own, so the answer is a number this test can name.
    /// </summary>
    [Test]
    public void APendingTimerReportsItsRemainingDelay()
    {
        var (engine, clock) = WebEngine();

        engine.Execute("setTimeout(() => {}, 100);");

        engine.Tasks.TimeUntilNextScheduledWork.Should().Be(TimeSpan.FromMilliseconds(100));

        clock.Advance(40);
        engine.Tasks.TimeUntilNextScheduledWork.Should().Be(TimeSpan.FromMilliseconds(60));
    }

    /// <summary>
    /// A timer that came due while nobody was pumping reports zero rather than a negative span: the host is
    /// being told to pump, and "how late am I" is not something it could act on.
    /// </summary>
    [Test]
    public void ATimerThatIsAlreadyDueReportsZero()
    {
        var (engine, clock) = WebEngine();

        engine.Execute("globalThis.fired = false; setTimeout(() => { globalThis.fired = true; }, 100);");

        clock.Advance(250);

        engine.Tasks.TimeUntilNextScheduledWork.Should().Be(TimeSpan.Zero);

        engine.Tasks.ProcessTasks();

        engine.Evaluate("fired").Should().Be(JsBoolean.True);
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    /// <summary>
    /// A delayed <c>scheduler.postTask</c> rides the timer queue, so it is reported exactly as a
    /// <c>setTimeout</c> is — a host does not have to know which of the two a script used.
    /// </summary>
    [Test]
    public void ADelayedSchedulerTaskIsReportedLikeATimer()
    {
        var (engine, clock) = WebEngine();

        engine.Execute("globalThis.ran = false; scheduler.postTask(() => { globalThis.ran = true; }, { delay: 250 });");

        engine.Tasks.TimeUntilNextScheduledWork.Should().Be(TimeSpan.FromMilliseconds(250));

        clock.Advance(250);
        engine.Tasks.ProcessTasks();

        engine.Evaluate("ran").Should().Be(JsBoolean.True);
    }

    /// <summary>
    /// An idle callback becomes runnable the moment a pump drains, so it is work for <i>now</i> — a host that
    /// slept on a positive answer would be sleeping through work it could already be doing.
    /// </summary>
    /// <remarks>
    /// The outer callback runs during <c>Execute</c>'s own drain and requests the inner one, which — the
    /// pending list being what the <i>next</i> idle period picks up — is left waiting for the next pump. That
    /// is the state this property has to describe.
    /// </remarks>
    [Test]
    public void APendingIdleCallbackIsWorkForNow()
    {
        var (engine, _) = WebEngine();

        engine.Execute("""
            globalThis.inner = false;
            requestIdleCallback(() => { requestIdleCallback(() => { globalThis.inner = true; }); });
            """);

        engine.Tasks.TimeUntilNextScheduledWork.Should().Be(TimeSpan.Zero);

        engine.Tasks.ProcessTasks();

        engine.Evaluate("inner").Should().Be(JsBoolean.True);
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    /// <summary>
    /// A host that declares it has no idle time is not told there is idle work to do: with a zero budget no
    /// idle period can ever start, so reporting zero would spin the host's loop over a callback that can never
    /// run.
    /// </summary>
    [Test]
    public void AZeroIdleBudgetMeansAnIdleCallbackIsNotWorkAtAll()
    {
        var (engine, _) = WebEngine(idleBudget: TimeSpan.Zero);

        engine.Execute("globalThis.ran = false; requestIdleCallback(() => { globalThis.ran = true; });");

        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();

        engine.Tasks.ProcessTasks();
        engine.Evaluate("ran").Should().Be(JsBoolean.False);
    }

    /// <summary>
    /// <c>Options.WebApi.Timers.IdleBudget</c> is reachable from outside the assembly and is what an idle
    /// callback's <c>timeRemaining()</c> counts down from.
    /// </summary>
    [Test]
    public void TheIdleBudgetIsTheHostsToChoose()
    {
        var (engine, _) = WebEngine(idleBudget: TimeSpan.FromMilliseconds(12));

        engine.Execute("globalThis.remaining = null; requestIdleCallback(d => { globalThis.remaining = d.timeRemaining(); });");

        engine.Evaluate("remaining").Should().Be(JsNumber.Create(12));

        // The default, for a host that says nothing.
        new Options().WebApi.Timers.IdleBudget.Should().Be(TimeSpan.FromMilliseconds(50));
    }

    /// <summary>
    /// The whole point of the bridge, and the one thing that must never be true of it: cancelling the host's
    /// token does not run a single line of script on the cancelling thread. It enqueues, and the abort happens
    /// on the thread that pumps.
    /// </summary>
    /// <remarks>
    /// A dedicated <see cref="Thread"/> rather than <c>Task.Run</c>, because a task can be inlined onto the
    /// waiting thread and the test would then be asserting nothing. The waits are liveness guards, not
    /// intervals: nothing here is measured against a clock.
    /// </remarks>
    [Test]
    public void CancellingTheTokenNeverRunsScriptOnTheCancellingThread()
    {
        var (engine, _) = WebEngine();
        using var cts = new CancellationTokenSource();

        engine.SetValue("hostSignal", engine.WebApi.CreateAbortSignal(cts.Token));

        var abortThread = 0;
        engine.SetValue("recordAbortThread", new Action(() => abortThread = Environment.CurrentManagedThreadId));
        engine.Execute("hostSignal.addEventListener('abort', () => recordAbortThread());");

        var engineThread = Environment.CurrentManagedThreadId;
        var cancellingThread = 0;

        var canceller = new Thread(() =>
        {
            cancellingThread = Environment.CurrentManagedThreadId;
            cts.Cancel();
        })
        {
            IsBackground = true,
            Name = "host-cancellation",
        };

        canceller.Start();
        canceller.Join(HandoffCeiling).Should().BeTrue("the cancelling thread must not block");

        // Cancel() has returned, so every registration it runs has run. Nothing observed the abort, because
        // the registration only enqueued.
        cancellingThread.Should().NotBe(engineThread);
        abortThread.Should().Be(0);

        // ... and the engine has work waiting, which is what a host loop is told to look for.
        engine.Tasks.TimeUntilNextScheduledWork.Should().Be(TimeSpan.Zero);

        engine.Tasks.ProcessTasks();

        abortThread.Should().Be(engineThread);
        engine.Evaluate("hostSignal.aborted").Should().Be(JsBoolean.True);
    }

    /// <summary>
    /// An engine that has been disposed lets go of the host's token, so an application-lifetime token cancelled
    /// long afterwards reaches nothing.
    /// </summary>
    /// <remarks>
    /// Observable from outside through the property: had the registration survived, the cancellation would have
    /// enqueued a job and the engine would report work to do.
    /// </remarks>
    [Test]
    public void DisposingTheEngineReleasesTheHostToken()
    {
        var (engine, _) = WebEngine();
        using var cts = new CancellationTokenSource();

        engine.SetValue("hostSignal", engine.WebApi.CreateAbortSignal(cts.Token));

        engine.Dispose();

        cts.Cancel();

        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    /// <summary>
    /// The idle-callback globals are behind their own flag, and a default engine has none of them.
    /// </summary>
    [Test]
    public void TheIdleCallbackGlobalsAreOptIn()
    {
        var names = new[] { "requestIdleCallback", "cancelIdleCallback", "IdleDeadline" };

        using var untouched = new Engine();
        using var consoleOnly = new Engine(options => options.UseWebApis(WebApiFeatures.Console));
        using var idle = new Engine(options => options.UseWebApis(WebApiFeatures.IdleCallback));

        foreach (var name in names)
        {
            untouched.Evaluate($"typeof {name}").Should().Be(new JsString("undefined"));
            consoleOnly.Evaluate($"typeof {name}").Should().Be(new JsString("undefined"));
            idle.Evaluate($"typeof {name}").Should().Be(new JsString("function"));

            // Deliberately more conservative than a browser, where these are [Exposed=*].
            idle.Evaluate($"new ShadowRealm().evaluate('typeof {name}')").Should().Be(new JsString("undefined"));
        }
    }

    /// <summary>
    /// <see cref="WebApiFeatures.Default"/> — what <c>UseWebApis()</c> enables — includes the idle callbacks,
    /// since they need no network and no host decision beyond the budget.
    /// </summary>
    [Test]
    public void TheDefaultFeatureSetIncludesTheIdleCallbacks()
    {
        WebApiFeatures.Default.Should().HaveFlag(WebApiFeatures.IdleCallback);

        var (engine, _) = WebEngine();
        engine.Evaluate("typeof requestIdleCallback").Should().Be(new JsString("function"));
    }
}
#endif
