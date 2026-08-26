#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>requestIdleCallback</c> / <c>cancelIdleCallback</c> against https://w3c.github.io/requestidlecallback/,
/// and against the embedding mapping Jint documents for them.
/// </summary>
/// <remarks>
/// <para>
/// The standard is written for a browser, where an idle period is the slack before the next frame is due.
/// Jint has no frames, so an idle period is a pump that has run out of everything else and its deadline is
/// <c>Options.WebApi.Timers.IdleBudget</c>. The tests that matter are therefore about <i>position</i> — a
/// callback runs after every job, every scheduler task at every priority, and every due timer — and about the
/// budget being a real boundary rather than decoration.
/// </para>
/// <para>
/// Every test drives a <see cref="ManualClock"/>, so nothing here waits on the wall clock: an idle deadline is
/// a number this suite chooses, and a <c>timeout</c> elapses exactly when a test says it does.
/// </para>
/// </remarks>
public class IdleCallbackTests
{
    /// <summary>
    /// A <see cref="TimeProvider"/> whose clock only moves when a test moves it.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    private static (Engine Engine, ManualClock Clock) IdleEngine(
        TimeSpan? idleBudget = null,
        int? maxActiveTimers = null)
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Timers.TimeProvider = clock;
            if (idleBudget is { } budget)
            {
                webApi.Timers.IdleBudget = budget;
            }

            if (maxActiveTimers is { } max)
            {
                webApi.Timers.MaxActiveTimers = max;
            }
        }));

        engine.Execute("var log = [];");
        return (engine, clock);
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    /// <summary>
    /// The happy path: a pump that has run out of everything else starts an idle period and runs the callback,
    /// handing it an <c>IdleDeadline</c> that says it is not a timeout and that the whole budget is left.
    /// </summary>
    /// <remarks>
    /// The clock does not move while the callback runs, so <c>timeRemaining()</c> is the budget exactly — which
    /// is what makes this assertable at all. Against a real clock it would be "50 minus however long the pump
    /// took", i.e. a number no test could name.
    /// </remarks>
    [Test]
    public void ARequestedCallbackRunsOnTheNextPumpWithTheWholeBudgetLeft()
    {
        var (engine, _) = IdleEngine();

        engine.Execute("requestIdleCallback(d => log.push(d.didTimeout, d.timeRemaining()));");

        Log(engine).Should().Be("false,50");
    }

    /// <summary>
    /// <c>Options.WebApi.Timers.IdleBudget</c> is what <c>timeRemaining()</c> counts down from, so a host that
    /// narrows it narrows what a callback believes it may do.
    /// </summary>
    [Test]
    public void TheDeadlineIsTheHostsIdleBudget()
    {
        var (engine, _) = IdleEngine(idleBudget: TimeSpan.FromMilliseconds(7));

        engine.Execute("requestIdleCallback(d => log.push(d.timeRemaining()));");

        Log(engine).Should().Be("7");
    }

    /// <summary>
    /// The idle period is a real budget: once the clock has passed the deadline, the period ends and whatever
    /// is left waits for the next pump rather than running the engine's idle list to exhaustion in one go.
    /// </summary>
    /// <remarks>
    /// The first callback moves the clock past the deadline itself — through the host callback the test
    /// installs — which is the only way a manual clock can model a callback that overruns. That is exactly the
    /// case the budget exists for.
    /// </remarks>
    [Test]
    public void ACallbackThatOverrunsTheBudgetEndsTheIdlePeriod()
    {
        var (engine, clock) = IdleEngine(idleBudget: TimeSpan.FromMilliseconds(50));
        engine.SetValue("burnTheBudget", new Action(() => clock.Advance(60)));

        engine.Execute("""
            requestIdleCallback(() => { log.push('first'); burnTheBudget(); });
            requestIdleCallback(() => log.push('second'));
            """);

        // The first ran and then spent the whole budget, so the pump returned to the host with the second
        // callback still waiting.
        Log(engine).Should().Be("first");

        engine.Tasks.ProcessTasks();
        Log(engine).Should().Be("first,second");
    }

    /// <summary>
    /// "Start an idle period" is what moves the pending list into the runnable list, so a callback requested
    /// from inside a callback belongs to the <i>next</i> period — which is what stops a self-re-arming
    /// <c>requestIdleCallback</c> monopolising the pump it was started from.
    /// </summary>
    [Test]
    public void ACallbackRequestedDuringAnIdlePeriodWaitsForTheNextPump()
    {
        var (engine, _) = IdleEngine();

        engine.Execute("""
            requestIdleCallback(() => {
                log.push('outer');
                requestIdleCallback(() => log.push('inner'));
            });
            """);

        Log(engine).Should().Be("outer");

        engine.Tasks.ProcessTasks();
        Log(engine).Should().Be("outer,inner");
    }

    /// <summary>
    /// The position the whole design turns on: idle callbacks are the lowest band the engine has. A microtask,
    /// a <c>background</c> scheduler task — the lowest priority the scheduler offers — and a due timer all run
    /// first, in that order.
    /// </summary>
    [Test]
    public void EveryOtherKindOfWorkRunsBeforeAnIdleCallback()
    {
        var (engine, _) = IdleEngine();

        engine.Execute("""
            requestIdleCallback(() => log.push('idle'));
            setTimeout(() => log.push('timer'), 0);
            scheduler.postTask(() => log.push('background'), { priority: 'background' });
            Promise.resolve().then(() => log.push('microtask'));
            """);

        Log(engine).Should().Be("microtask,background,timer,idle");
    }

    /// <summary>
    /// A timer that is not yet due does not hold an idle callback back — idleness is about having nothing to
    /// run <i>now</i>, not about having nothing scheduled at all.
    /// </summary>
    [Test]
    public void ATimerThatIsNotYetDueDoesNotHoldAnIdleCallbackBack()
    {
        var (engine, clock) = IdleEngine();

        engine.Execute("""
            setTimeout(() => log.push('timer'), 100);
            requestIdleCallback(() => log.push('idle'));
            """);

        Log(engine).Should().Be("idle");

        clock.Advance(100);
        engine.Tasks.ProcessTasks();
        Log(engine).Should().Be("idle,timer");
    }

    /// <summary>
    /// https://w3c.github.io/requestidlecallback/#the-cancelidlecallback-method — the entry is removed from
    /// both lists, so the callback never runs.
    /// </summary>
    [Test]
    public void CancelIdleCallbackStopsTheCallbackRunning()
    {
        var (engine, _) = IdleEngine();

        engine.Execute("""
            const handle = requestIdleCallback(() => log.push('never'));
            cancelIdleCallback(handle);
            """);

        Log(engine).Should().BeEmpty();

        engine.Tasks.ProcessTasks();
        Log(engine).Should().BeEmpty();
    }

    /// <summary>
    /// An unknown handle is not an error — "if there is such an entry" simply does not hold.
    /// </summary>
    [Test]
    public void CancellingAnUnknownHandleIsSilent()
    {
        var (engine, _) = IdleEngine();

        engine.Execute("cancelIdleCallback(9999); cancelIdleCallback(); cancelIdleCallback('nope');");

        Log(engine).Should().BeEmpty();
    }

    /// <summary>
    /// The invoke idle callback timeout algorithm,
    /// https://w3c.github.io/requestidlecallback/#invoke-idle-callback-timeout-algorithm: when the timeout
    /// elapses the callback runs whether or not the engine is idle, with <c>didTimeout</c> true and a
    /// <c>timeRemaining()</c> of zero — its deadline is <i>now</i>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A budget of zero is what makes this observable: it tells the engine the host never has idle time, so
    /// nothing but the timeout can reach the callback. Without it the very first pump would run the callback as
    /// an ordinary idle one and the timeout would be cancelled before it could ever fire.
    /// </para>
    /// <para>
    /// The timeout rides the timer queue, so the manual clock drives it and this test touches no wall clock.
    /// </para>
    /// </remarks>
    [Test]
    public void ATimeoutRunsTheCallbackOutsideAnyIdlePeriod()
    {
        var (engine, clock) = IdleEngine(idleBudget: TimeSpan.Zero);

        engine.Execute("requestIdleCallback(d => log.push(d.didTimeout, d.timeRemaining()), { timeout: 100 });");

        // A host with no idle time runs nothing idle, however often it pumps.
        Log(engine).Should().BeEmpty();
        engine.Tasks.ProcessTasks();
        Log(engine).Should().BeEmpty();

        clock.Advance(99);
        engine.Tasks.ProcessTasks();
        Log(engine).Should().BeEmpty();

        clock.Advance(1);
        engine.Tasks.ProcessTasks();
        Log(engine).Should().Be("true,0");
    }

    /// <summary>
    /// A callback that an idle period reached first must not run a second time when its timeout comes due —
    /// the idle path removes the entry from both lists, which is what the timeout algorithm looks for.
    /// </summary>
    [Test]
    public void ACallbackThatAlreadyRanDoesNotRunAgainWhenItsTimeoutElapses()
    {
        var (engine, clock) = IdleEngine();

        engine.Execute("requestIdleCallback(d => log.push('ran:' + d.didTimeout), { timeout: 10 });");

        Log(engine).Should().Be("ran:false");

        clock.Advance(1000);
        engine.Tasks.ProcessTasks();
        Log(engine).Should().Be("ran:false");
    }

    /// <summary>
    /// A callback with no <c>timeout</c> occupies no timer slot: it waits in a list for the next pump, exactly
    /// as a queued job does, so it is not something a script can exhaust the timer quota with.
    /// </summary>
    [Test]
    public void ACallbackWithoutATimeoutCostsNoTimerSlot()
    {
        var (engine, _) = IdleEngine(maxActiveTimers: 0);

        engine.Execute("requestIdleCallback(() => log.push('idle'));");

        Log(engine).Should().Be("idle");
    }

    /// <summary>
    /// A <c>timeout</c> is a timer, so it does count against <c>MaxActiveTimers</c> — a script that can request
    /// idle callbacks must not be able to register timers without bound through the back door.
    /// </summary>
    [Test]
    public void ATimeoutCountsAgainstTheTimerQuota()
    {
        var (engine, _) = IdleEngine(idleBudget: TimeSpan.Zero, maxActiveTimers: 1);

        engine.Execute("""
            requestIdleCallback(() => {}, { timeout: 100 });
            try {
                requestIdleCallback(() => {}, { timeout: 100 });
            } catch (e) {
                log.push(e.constructor.name, e.name, e.code, e.quota, e.requested);
            }
            """);

        // https://webidl.spec.whatwg.org/#quotaexceedederror — the interface, carrying the cap it hit and the
        // count the refused registration would have taken the engine to.
        Log(engine).Should().Be("QuotaExceededError,QuotaExceededError,22,1,2");
    }

    /// <summary>
    /// Cancelling a callback frees the timer slot its timeout held, rather than leaving it to expire into
    /// nothing.
    /// </summary>
    [Test]
    public void CancellingACallbackFreesItsTimeoutSlot()
    {
        var (engine, _) = IdleEngine(idleBudget: TimeSpan.Zero, maxActiveTimers: 1);

        engine.Execute("""
            const first = requestIdleCallback(() => {}, { timeout: 100 });
            cancelIdleCallback(first);
            requestIdleCallback(() => log.push('second scheduled'), { timeout: 100 });
            log.push('no quota error');
            """);

        Log(engine).Should().Be("no quota error");
    }

    /// <summary>
    /// A callback that is not callable is a <c>TypeError</c>, per the WebIDL <c>IdleRequestCallback</c>
    /// conversion.
    /// </summary>
    [Test]
    public void ANonCallableCallbackIsATypeError()
    {
        var (engine, _) = IdleEngine();

        var exception = Assert.Throws<JavaScriptException>(() => engine.Execute("requestIdleCallback(42);"))!;
        exception.Error.Get("name").Should().Be(new JsString("TypeError"));
    }

    /// <summary>
    /// The <c>IdleRequestOptions</c> dictionary's <c>timeout</c> member is a plain <c>unsigned long</c>, so the
    /// conversion is <c>ToUint32</c>'s and not <c>[EnforceRange]</c>'s refusal: a non-number becomes 0 rather
    /// than an exception.
    /// </summary>
    [Test]
    public void AnUnparsableTimeoutIsZeroRatherThanAnError()
    {
        var (engine, _) = IdleEngine(idleBudget: TimeSpan.Zero);

        // Zero means "no timeout", so nothing is ever scheduled and — the budget being zero — nothing runs.
        engine.Execute("log.push(typeof requestIdleCallback(() => {}, { timeout: NaN }));");

        Log(engine).Should().Be("number");
    }

    /// <summary>
    /// The WebIDL shape of the two operations: <c>length</c> counts the required arguments only, and both are
    /// writable, enumerable and configurable properties of the global, as operations of a global interface
    /// mixin are — https://webidl.spec.whatwg.org/#es-operations.
    /// </summary>
    [Test]
    public void TheOperationsHaveTheirWebIdlShape()
    {
        var (engine, _) = IdleEngine();

        engine.Evaluate("typeof requestIdleCallback").Should().Be(new JsString("function"));
        engine.Evaluate("requestIdleCallback.length").Should().Be(JsNumber.Create(1));
        engine.Evaluate("requestIdleCallback.name").Should().Be(new JsString("requestIdleCallback"));
        engine.Evaluate("cancelIdleCallback.length").Should().Be(JsNumber.Create(1));

        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("requestIdleCallback");
        descriptor.Enumerable.Should().BeTrue();
        descriptor.Writable.Should().BeTrue();
        descriptor.Configurable.Should().BeTrue();
    }

    /// <summary>
    /// <c>IdleDeadline</c> is exposed even though nothing but the engine can create one — that is what makes
    /// <c>deadline instanceof IdleDeadline</c> and feature detection work — and, being an interface object, it
    /// is non-enumerable and refuses to construct.
    /// </summary>
    [Test]
    public void TheIdleDeadlineInterfaceObjectIsExposedButNotConstructible()
    {
        var (engine, _) = IdleEngine();

        engine.Execute("""
            requestIdleCallback(d => {
                log.push(d instanceof IdleDeadline);
                log.push(Object.prototype.toString.call(d));
                log.push(Object.getPrototypeOf(d) === IdleDeadline.prototype);
                try { new IdleDeadline(); } catch (e) { log.push(e.constructor.name); }
            });
            """);

        Log(engine).Should().Be("true,[object IdleDeadline],true,TypeError");

        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("IdleDeadline");
        descriptor.Enumerable.Should().BeFalse();
        descriptor.Writable.Should().BeTrue();
        descriptor.Configurable.Should().BeTrue();
    }

    /// <summary>
    /// Both members brand-check their receiver, as WebIDL requires: <c>IdleDeadline.prototype</c> is not an
    /// <c>IdleDeadline</c>, and neither is a plain object borrowed into the receiver position.
    /// </summary>
    [Test]
    public void TheMembersBrandCheckTheirReceiver()
    {
        var (engine, _) = IdleEngine();

        engine.Execute("""
            try { IdleDeadline.prototype.timeRemaining(); } catch (e) { log.push(e.constructor.name); }
            const getter = Object.getOwnPropertyDescriptor(IdleDeadline.prototype, 'didTimeout').get;
            try { getter.call({}); } catch (e) { log.push(e.constructor.name); }
            """);

        Log(engine).Should().Be("TypeError,TypeError");
    }

    /// <summary>
    /// The globals are installed lazily, exactly as every other web-API global is: the property exists for
    /// enumeration and existence checks from the start, and the function object behind it is built only when
    /// something reads it.
    /// </summary>
    [Test]
    public void TheGlobalsAreInstalledLazily()
    {
        var (engine, _) = IdleEngine();

        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("requestIdleCallback");
        descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();

        // Still flagged CustomJsValue means the factory has not run: enabling the feature costs one descriptor
        // per global and nothing else.
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().NotBe(PropertyFlag.None);
        descriptor._value.Should().BeNull();

        engine.Evaluate("typeof requestIdleCallback");

        // Materialized once and then an ordinary data property, so later reads rejoin the caching lanes.
        descriptor._value.Should().NotBeNull();
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().Be(PropertyFlag.None);
    }

    /// <summary>
    /// A callback requested by the evaluation cycle a <c>RestoreGlobalSnapshot</c> ends must never run against
    /// the globals that restore put back — the same fence a timer and a scheduler task sit behind.
    /// </summary>
    [Test]
    public void ARestoreForgetsPendingIdleCallbacks()
    {
        var (engine, _) = IdleEngine();

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        // The outer callback runs during this Execute's own drain; the inner one it requests belongs to the
        // next idle period and is still waiting when the restore lands, which is the state under test.
        engine.Execute("""
            requestIdleCallback(() => {
                requestIdleCallback(() => { globalThis.ranAfterRestore = true; });
            });
            """);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Execute("globalThis.ranAfterRestore = false;");
        engine.Tasks.ProcessTasks();

        engine.Evaluate("ranAfterRestore").Should().Be(JsBoolean.False);
    }

    /// <summary>
    /// A callback that throws erupts from whatever was pumping — the contract a timer callback and a promise
    /// reaction already have — and the callbacks behind it still run on the next pump.
    /// </summary>
    [Test]
    public void AThrowingCallbackEruptsFromThePump()
    {
        var (engine, _) = IdleEngine();

        var exception = Assert.Throws<JavaScriptException>(() => engine.Execute("""
            requestIdleCallback(() => { throw new Error('boom'); });
            requestIdleCallback(() => log.push('after'));
            """))!;

        exception.Message.Should().Be("boom");

        // The throw does not wedge the queue: the callback behind it still runs on the next pump, and the one
        // that threw does not run twice.
        engine.Tasks.ProcessTasks();
        Log(engine).Should().Be("after");
    }
}
#endif
