#if NET8_0_OR_GREATER
#nullable enable

using System.Diagnostics;
using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The timer globals against HTML's timers section —
/// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#timers.
/// </summary>
/// <remarks>
/// <para>
/// Most of these drive a <see cref="ManualClock"/> rather than sleeping, because what they are about is
/// ordering and due-time arithmetic, not latency. The few that do use the real clock are the ones about the
/// blocking and asynchronous drains, where the point is precisely that the wait ends by itself.
/// </para>
/// <para>
/// The other half of every assertion here is <i>when</i> the callbacks run: a timer fires only while the
/// engine is being pumped. <c>Engine.Execute</c> drains the event loop once it has finished the script, which
/// is why a zero-delay timer has already run when <c>Execute</c> returns, and why a later one needs an
/// explicit <c>Advanced.ProcessTasks()</c>.
/// </para>
/// </remarks>
public class TimerTests
{
    /// <summary>
    /// A <see cref="TimeProvider"/> whose clock only moves when a test moves it. Ticks are the unit, so
    /// <see cref="TimeProvider.GetElapsedTime(long, long)"/> is exact.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    private static (Engine Engine, ManualClock Clock) TimerEngine(int? maxActiveTimers = null)
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Timers.TimeProvider = clock;
            if (maxActiveTimers is { } max)
            {
                webApi.Timers.MaxActiveTimers = max;
            }
        }));

        engine.Execute("var log = [];");
        return (engine, clock);
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    [Fact]
    public void AMicrotaskRunsBeforeATimerThatIsAlreadyDue()
    {
        var (engine, _) = TimerEngine();

        engine.Execute("""
            setTimeout(() => log.push('timeout'), 0);
            Promise.resolve().then(() => log.push('microtask'));
            log.push('script');
            """);

        // The single job queue is the microtask queue: a timer is promoted onto it only once it has run dry.
        Log(engine).Should().Be("script,microtask,timeout");
    }

    [Fact]
    public void EachTimerGetsItsOwnMicrotaskCheckpoint()
    {
        var (engine, _) = TimerEngine();

        engine.Execute("""
            setTimeout(() => { log.push('t1'); Promise.resolve().then(() => log.push('p1')); }, 0);
            setTimeout(() => { log.push('t2'); Promise.resolve().then(() => log.push('p2')); }, 0);
            """);

        // Not t1,t2,p1,p2: everything the first timer queues runs before the second timer is even looked at.
        Log(engine).Should().Be("t1,p1,t2,p2");
    }

    [Fact]
    public void TimersDueAtTheSameInstantFireInRegistrationOrder()
    {
        var (engine, clock) = TimerEngine();

        engine.Execute("""
            setTimeout(() => log.push('a'), 5);
            setTimeout(() => log.push('b'), 5);
            setTimeout(() => log.push('c'), 5);
            """);

        Log(engine).Should().BeEmpty();

        clock.Advance(5);
        engine.Advanced.ProcessTasks();

        Log(engine).Should().Be("a,b,c");
    }

    [Fact]
    public void AShorterDelayFiresFirstHoweverItWasRegistered()
    {
        var (engine, clock) = TimerEngine();

        engine.Execute("""
            setTimeout(() => log.push('late'), 20);
            setTimeout(() => log.push('early'), 5);
            setTimeout(() => log.push('middle'), 10);
            """);

        clock.Advance(20);
        engine.Advanced.ProcessTasks();

        Log(engine).Should().Be("early,middle,late");
    }

    [Fact]
    public void TheExtraArgumentsAreForwardedToTheCallback()
    {
        var (engine, clock) = TimerEngine();

        engine.Execute("""
            setTimeout((a, b, c) => log.push(a + ':' + b + ':' + typeof c), 0, 'x', 7);
            setTimeout(function () { log.push('none:' + arguments.length); }, 0);
            """);

        Log(engine).Should().Be("x:7:undefined,none:0");

        // An interval hands them over on every firing, not only the first.
        engine.Execute("var id = setInterval(v => log.push('i' + v), 5, 42);");
        clock.Advance(5);
        engine.Advanced.ProcessTasks();
        clock.Advance(5);
        engine.Advanced.ProcessTasks();
        engine.Execute("clearInterval(id);");

        Log(engine).Should().Be("x:7:undefined,none:0,i42,i42");
    }

    [Fact]
    public void ADelayThatIsNaNOrNegativeOrMissingIsZero()
    {
        var (engine, _) = TimerEngine();

        engine.Execute("""
            setTimeout(() => log.push('nan'), NaN);
            setTimeout(() => log.push('negative'), -1000);
            setTimeout(() => log.push('missing'));
            setTimeout(() => log.push('string'), '0');
            """);

        Log(engine).Should().Be("nan,negative,missing,string");
    }

    [Fact]
    public void TheDelayIsCoercedAsAWebIdlLong()
    {
        var (engine, _) = TimerEngine();

        // WebIDL types timeout as `long`, so 2^31 wraps to a negative value and is then clamped to zero —
        // the same thing a browser does with it.
        engine.Execute("setTimeout(() => log.push('wrapped'), 2147483648);");

        Log(engine).Should().Be("wrapped");
    }

    [Fact]
    public void IdsAreMonotonicAndSharedBetweenTimeoutsAndIntervals()
    {
        var (engine, _) = TimerEngine();

        engine.Evaluate("setTimeout(function () {}, 1000)").AsNumber().Should().Be(1);
        engine.Evaluate("setInterval(function () {}, 1000)").AsNumber().Should().Be(2);
        engine.Evaluate("setTimeout(function () {}, 1000)").AsNumber().Should().Be(3);
    }

    [Fact]
    public void ClearTimeoutAndClearIntervalAreInterchangeable()
    {
        var (engine, clock) = TimerEngine();

        engine.Execute("""
            var timeout = setTimeout(() => log.push('timeout'), 5);
            var interval = setInterval(() => log.push('interval'), 5);
            clearInterval(timeout);
            clearTimeout(interval);
            """);

        clock.Advance(50);
        engine.Advanced.ProcessTasks();

        Log(engine).Should().BeEmpty();
    }

    [Fact]
    public void ClearingAnUnknownIdIsSilentlyIgnored()
    {
        var (engine, _) = TimerEngine();

        // None of these is an error, and none of them may throw.
        engine.Execute("""
            clearTimeout();
            clearTimeout(0);
            clearTimeout(-1);
            clearTimeout(9999);
            clearTimeout('not a number');
            clearTimeout({});
            clearTimeout(null);
            clearInterval(undefined);
            log.push('survived');
            """);

        Log(engine).Should().Be("survived");
    }

    [Fact]
    public void ATimerCanClearItselfFromInsideItsOwnCallback()
    {
        var (engine, clock) = TimerEngine();

        engine.Execute("""
            var interval = setInterval(() => { log.push('tick'); clearInterval(interval); }, 5);
            var timeout = setTimeout(() => { log.push('once'); clearTimeout(timeout); }, 5);
            """);

        clock.Advance(5);
        engine.Advanced.ProcessTasks();
        clock.Advance(100);
        engine.Advanced.ProcessTasks();

        // The interval stopped after its first firing, and the one-shot clearing its own already-removed id
        // was the no-op it should be rather than an error.
        Log(engine).Should().Be("tick,once");
    }

    [Fact]
    public void AnIntervalRepeatsUntilItIsCleared()
    {
        var (engine, clock) = TimerEngine();

        engine.Execute("var id = setInterval(() => log.push('tick'), 10);");

        for (var i = 0; i < 3; i++)
        {
            clock.Advance(10);
            engine.Advanced.ProcessTasks();
        }

        Log(engine).Should().Be("tick,tick,tick");

        engine.Execute("clearInterval(id);");
        clock.Advance(100);
        engine.Advanced.ProcessTasks();

        Log(engine).Should().Be("tick,tick,tick");
    }

    [Fact]
    public void AThrowingCallbackDoesNotStopTheInterval()
    {
        var (engine, clock) = TimerEngine();

        engine.Execute("var id = setInterval(() => { log.push('tick'); throw new Error('boom'); }, 10);");

        for (var i = 0; i < 2; i++)
        {
            clock.Advance(10);

            // The interval is re-armed before the callback runs, which is the whole reason a throw cannot
            // silently stop it.
            var thrown = Assert.Throws<JavaScriptException>(() => engine.Advanced.ProcessTasks());
            thrown.Message.Should().Contain("boom");
        }

        Log(engine).Should().Be("tick,tick");

        engine.Execute("clearInterval(id);");
    }

    [Fact]
    public void AThrowingCallbackEruptsFromThePumpAndTheRestOfTheQueueSurvives()
    {
        var (engine, clock) = TimerEngine();

        engine.Execute("""
            setTimeout(() => { throw new Error('boom'); }, 5);
            setTimeout(() => log.push('after'), 5);
            """);

        clock.Advance(5);

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Advanced.ProcessTasks());
        thrown.Message.Should().Contain("boom");
        Log(engine).Should().BeEmpty();

        // The timer behind the failed one was never promoted, so the next pump runs it.
        engine.Advanced.ProcessTasks();
        Log(engine).Should().Be("after");
    }

    [Fact]
    public void AThrowingCallbackEruptsFromExecute()
    {
        var (engine, _) = TimerEngine();

        // Execute drains the event loop once the script has finished, so the throw comes out of Execute.
        var thrown = Assert.Throws<JavaScriptException>(
            () => engine.Execute("setTimeout(() => { throw new Error('boom'); }, 0);"));

        thrown.Message.Should().Contain("boom");
    }

    [Fact]
    public void ANestedChainIsClampedToFourMillisecondsBeyondTheFifthLevel()
    {
        var (engine, clock) = TimerEngine();

        // https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#timer-initialisation-steps,
        // step 8: "If nesting level is greater than 5, and timeout is less than 4, then set timeout to 4."
        engine.Execute("""
            var levels = 0;
            function nest() { levels++; if (levels < 8) { setTimeout(nest, 0); } }
            setTimeout(nest, 0);
            """);

        // Levels 1 to 6 were all scheduled at nesting levels 5 or below, so they were all due immediately and
        // the drain at the end of Execute ran the lot. The seventh was scheduled from a callback at nesting
        // level 6, so it is 4ms away.
        engine.Evaluate("levels").AsNumber().Should().Be(6);

        engine.Advanced.ProcessTasks();
        engine.Evaluate("levels").AsNumber().Should().Be(6);

        clock.Advance(3);
        engine.Advanced.ProcessTasks();
        engine.Evaluate("levels").AsNumber().Should().Be(6);

        clock.Advance(1);
        engine.Advanced.ProcessTasks();
        engine.Evaluate("levels").AsNumber().Should().Be(7);

        clock.Advance(4);
        engine.Advanced.ProcessTasks();
        engine.Evaluate("levels").AsNumber().Should().Be(8);
    }

    [Fact]
    public void AZeroDelayIntervalIsClampedOnceItHasRepeatedEnoughTimes()
    {
        var (engine, clock) = TimerEngine();

        // The interval re-runs the initialization steps on every firing, at one nesting level deeper each
        // time, so a setInterval(f, 0) stops being a hot loop after five turns.
        engine.Execute("var id = setInterval(() => log.push('t'), 0);");

        Log(engine).Should().Be("t,t,t,t,t,t");

        engine.Advanced.ProcessTasks();
        Log(engine).Should().Be("t,t,t,t,t,t");

        clock.Advance(4);
        engine.Advanced.ProcessTasks();
        Log(engine).Should().Be("t,t,t,t,t,t,t");

        engine.Execute("clearInterval(id);");
    }

    [Fact]
    public void ANonCallableHandlerIsATypeError()
    {
        var (engine, _) = TimerEngine();

        Assert.Throws<JavaScriptException>(() => engine.Execute("setTimeout(undefined, 0)"))
            .Message.Should().Contain("setTimeout");
        Assert.Throws<JavaScriptException>(() => engine.Execute("setTimeout(42, 0)"));
        Assert.Throws<JavaScriptException>(() => engine.Execute("setTimeout({}, 0)"));
        Assert.Throws<JavaScriptException>(() => engine.Execute("setInterval(undefined, 0)"))
            .Message.Should().Contain("setInterval");

        engine.Evaluate("(() => { try { setTimeout(null, 0); } catch (e) { return e instanceof TypeError; } })()")
            .AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AStringHandlerIsATypeErrorRatherThanCompiledCode()
    {
        var (engine, clock) = TimerEngine();

        // Deliberately unsupported: the string form is eval by another name.
        engine.Evaluate("""
            (() => {
                try { setTimeout('log.push("compiled")', 0); }
                catch (e) { return e instanceof TypeError; }
                return false;
            })()
            """).AsBoolean().Should().BeTrue();

        clock.Advance(100);
        engine.Advanced.ProcessTasks();
        Log(engine).Should().BeEmpty();
    }

    [Fact]
    public void ExceedingMaxActiveTimersThrowsAQuotaExceededError()
    {
        var (engine, clock) = TimerEngine(maxActiveTimers: 2);

        engine.Execute("""
            setTimeout(() => log.push('one'), 10);
            setTimeout(() => log.push('two'), 10);
            """);

        engine.Evaluate("""
            (() => {
                try { setTimeout(() => {}, 10); }
                catch (e) { return [e instanceof DOMException, e.name, e.code].join('|'); }
                return 'no error';
            })()
            """).AsString().Should().Be("true|QuotaExceededError|22");

        // A fired timeout frees its slot, so the engine is not stuck once the queue drains.
        clock.Advance(10);
        engine.Advanced.ProcessTasks();
        Log(engine).Should().Be("one,two");

        engine.Execute("setTimeout(() => log.push('three'), 10);");
        clock.Advance(10);
        engine.Advanced.ProcessTasks();
        Log(engine).Should().Be("one,two,three");
    }

    [Fact]
    public void AClearedTimerFreesItsSlot()
    {
        var (engine, _) = TimerEngine(maxActiveTimers: 1);

        engine.Execute("var id = setInterval(() => {}, 10); clearInterval(id);");
        engine.Execute("var second = setTimeout(() => {}, 10);");

        engine.Evaluate("second").AsNumber().Should().Be(2);
    }

    [Fact]
    public void QueueMicrotaskRunsAfterTheCurrentScriptAndBeforeAnyTimer()
    {
        var (engine, _) = TimerEngine();

        engine.Execute("""
            setTimeout(() => log.push('timeout'), 0);
            queueMicrotask(() => log.push('micro1'));
            Promise.resolve().then(() => log.push('promise'));
            queueMicrotask(() => log.push('micro2'));
            log.push('script');
            """);

        Log(engine).Should().Be("script,micro1,promise,micro2,timeout");
    }

    [Fact]
    public void QueueMicrotaskCallsTheCallbackWithNoArgumentsAndUndefinedThis()
    {
        var (engine, _) = TimerEngine();

        // Strict, so `this` is left exactly as the caller passed it rather than coerced to the global object.
        engine.Execute("queueMicrotask(function () { 'use strict'; log.push(arguments.length + ':' + (this === undefined)); });");

        Log(engine).Should().Be("0:true");
    }

    [Fact]
    public void QueueMicrotaskRejectsANonCallableCallback()
    {
        var (engine, _) = TimerEngine();

        engine.Evaluate("""
            (() => {
                try { queueMicrotask('log.push(1)'); }
                catch (e) { return e instanceof TypeError; }
                return false;
            })()
            """).AsBoolean().Should().BeTrue();

        Assert.Throws<JavaScriptException>(() => engine.Execute("queueMicrotask(undefined)"));
    }

    [Fact]
    public void ATimerNeverFiresWithoutAPump()
    {
        var (engine, clock) = TimerEngine();

        engine.Execute("setTimeout(() => log.push('fired'), 5);");
        clock.Advance(1000);

        // Time passing is not enough: no thread exists to notice, which is exactly the contract.
        Log(engine).Should().BeEmpty();

        engine.Advanced.ProcessTasks();
        Log(engine).Should().Be("fired");
    }

    [Fact]
    public void ARestoredEngineForgetsTheTimersOfTheCycleThatEnded()
    {
        var (engine, clock) = TimerEngine();
        var ran = false;
        engine.SetValue("mark", new Action(() => ran = true));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Execute("setTimeout(mark, 10); setInterval(mark, 10);");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        clock.Advance(1000);
        engine.Advanced.ProcessTasks();

        ran.Should().BeFalse();
        engine._webApi!.Timers!.Count.Should().Be(0);
    }

    [Fact]
    public void ABlockingUnwrapWaitsOutARealTimer()
    {
        // The real clock here: the point is that the blocking drain ends by itself when the timer comes due.
        var engine = new Engine(options => options.UseWebApis());

        var stopwatch = Stopwatch.StartNew();
        var result = engine
            .Evaluate("(async () => { await new Promise(r => setTimeout(r, 100)); return 42; })()")
            .UnwrapIfPromise();

        result.AsNumber().Should().Be(42);
        stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(80));
    }

    [Fact]
    public async Task AnAsynchronousUnwrapWaitsOutARealTimer()
    {
        var engine = new Engine(options => options.UseWebApis());

        var result = await engine.EvaluateAsync("(async () => { await new Promise(r => setTimeout(r, 100)); return 42; })()");

        result.AsNumber().Should().Be(42);
    }

    [Fact]
    public void AHostPollingLoopIsEnoughToRunTimers()
    {
        var engine = new Engine(options => options.UseWebApis());
        var done = false;
        engine.SetValue("markDone", new Action(() => done = true));

        engine.Execute("(async () => { await new Promise(r => setTimeout(r, 50)); markDone(); })();");

        var deadline = Stopwatch.StartNew();
        while (!done && deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            // Nothing but the host's own pump: no engine thread, no background timer.
            engine.Advanced.ProcessTasks();
            Thread.Sleep(5);
        }

        done.Should().BeTrue();
    }

    [Fact]
    public void ATimerLongerThanTheUnwrapTimeoutTimesOutByDesign()
    {
        var engine = new Engine(options => options.UseWebApis());

        var pending = engine.Evaluate("new Promise(r => setTimeout(r, 5000))");

        // Documented interaction: the blocking unwrap's own timeout still bounds the wait.
        Assert.Throws<PromiseRejectedException>(() => pending.UnwrapIfPromise(TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void TheTimerGlobalsAreOrdinaryConfigurableWritableProperties()
    {
        var (engine, _) = TimerEngine();

        foreach (var name in new[] { "setTimeout", "setInterval", "clearTimeout", "clearInterval", "queueMicrotask" })
        {
            var descriptor = engine.Evaluate($"Object.getOwnPropertyDescriptor(globalThis, '{name}')").AsObject();
            descriptor.Get("writable").AsBoolean().Should().BeTrue();
            descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
            descriptor.Get("configurable").AsBoolean().Should().BeTrue();
        }

        engine.Evaluate("[setTimeout.name, setTimeout.length, clearTimeout.length, queueMicrotask.length].join(',')")
            .AsString().Should().Be("setTimeout,1,0,1");
    }

    [Fact]
    public void AShadowRealmHasNoTimers()
    {
        var (engine, _) = TimerEngine();

        engine.Evaluate("new ShadowRealm().evaluate('typeof setTimeout')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof queueMicrotask')").AsString().Should().Be("undefined");
    }
}
#endif
