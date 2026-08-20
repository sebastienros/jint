#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;
using Jint.WebApi.Abort;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>AbortController</c> and <c>AbortSignal</c> against the DOM standard —
/// https://dom.spec.whatwg.org/#aborting-ongoing-activities.
/// </summary>
public class AbortTests
{
    /// <summary>
    /// A <see cref="TimeProvider"/> whose clock only moves when a test moves it, so
    /// <c>AbortSignal.timeout()</c> can be exercised without sleeping.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    private static Engine WebEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));
        engine.Execute("var log = [];");
        return engine;
    }

    private static (Engine Engine, ManualClock Clock) TimeoutEngine(int? maxActiveTimers = null)
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Features = WebApiFeatures.Events;
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
    public void AControllerStartsWithAFreshUnabortedSignal()
    {
        var engine = WebEngine();
        engine.Execute("var controller = new AbortController();");

        engine.Evaluate("controller.signal === controller.signal").AsBoolean().Should().BeTrue();
        engine.Evaluate("controller.signal.aborted").AsBoolean().Should().BeFalse();
        engine.Evaluate("controller.signal.reason").IsUndefined().Should().BeTrue();
        engine.Evaluate("controller.signal instanceof AbortSignal").AsBoolean().Should().BeTrue();
        engine.Evaluate("controller.signal instanceof EventTarget").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AbortWithoutAReasonGivesAnAbortErrorDomException()
    {
        var engine = WebEngine();
        engine.Execute("var controller = new AbortController(); controller.abort();");

        engine.Evaluate("controller.signal.aborted").AsBoolean().Should().BeTrue();
        engine.Evaluate("controller.signal.reason instanceof DOMException").AsBoolean().Should().BeTrue();
        engine.Evaluate("controller.signal.reason.name").AsString().Should().Be("AbortError");

        // An explicitly passed undefined is treated as "not given", which is what implementations do and what
        // keeps an aborted signal's reason from ever being undefined.
        engine.Execute("var second = new AbortController(); second.abort(undefined);");
        engine.Evaluate("second.signal.reason.name").AsString().Should().Be("AbortError");
    }

    [Fact]
    public void AbortCarriesTheGivenReason()
    {
        var engine = WebEngine();

        engine.Execute("var controller = new AbortController(); controller.abort('because');");
        engine.Evaluate("controller.signal.reason").AsString().Should().Be("because");

        engine.Execute("var nulled = new AbortController(); nulled.abort(null);");
        engine.Evaluate("nulled.signal.reason").IsNull().Should().BeTrue();
        engine.Evaluate("nulled.signal.aborted").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void FiresTheAbortEventExactlyOnce()
    {
        var engine = WebEngine();

        engine.Execute("""
            var controller = new AbortController();
            controller.signal.addEventListener('abort', function (e) {
                log.push(e.type, e.isTrusted, e.target === controller.signal, controller.signal.aborted);
            });
            controller.abort();
            controller.abort('ignored');
            """);

        // The engine created and dispatched the event, so it is trusted; a second abort is a no-op.
        Log(engine).Should().Be("abort,true,true,true");
        engine.Evaluate("controller.signal.reason.name").AsString().Should().Be("AbortError");
    }

    [Fact]
    public void ThrowIfAbortedThrowsTheReasonItself()
    {
        var engine = WebEngine();

        engine.Execute("var controller = new AbortController();");
        engine.Execute("controller.signal.throwIfAborted();");

        engine.Execute("controller.abort({ marker: 1 });");
        engine.Execute("""
            var caught = null;
            try { controller.signal.throwIfAborted(); } catch (e) { caught = e; }
            """);

        engine.Evaluate("caught === controller.signal.reason").AsBoolean().Should().BeTrue();
        engine.Evaluate("caught.marker").AsNumber().Should().Be(1);

        // The reason is thrown as it is, so a primitive one is thrown as a primitive.
        engine.Execute("""
            var primitive = new AbortController();
            primitive.abort('plain string');
            var caughtPrimitive = null;
            try { primitive.signal.throwIfAborted(); } catch (e) { caughtPrimitive = e; }
            """);
        engine.Evaluate("caughtPrimitive").AsString().Should().Be("plain string");
    }

    [Fact]
    public void AnAbortListenerThatThrowsEruptsFromAbortButTheAbortIsComplete()
    {
        // The same choice dispatchEvent makes: with no reportError channel the exception propagates rather
        // than being swallowed. Every abort algorithm has already run by then, because they run before the
        // event does — which is what makes an operation observing the signal stop regardless.
        var engine = WebEngine();

        engine.Execute("""
            var controller = new AbortController();
            var target = new EventTarget();
            target.addEventListener('ping', function () { log.push('ping'); }, { signal: controller.signal });
            controller.signal.addEventListener('abort', function () { throw new Error('listener blew up'); });
            """);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("controller.abort('reason')"))
            .Message.Should().Be("listener blew up");

        engine.Evaluate("controller.signal.aborted").AsBoolean().Should().BeTrue();
        engine.Evaluate("controller.signal.reason").AsString().Should().Be("reason");

        // The signal option's abort algorithm ran, so the listener it guarded is gone.
        engine.Execute("target.dispatchEvent(new Event('ping'));");
        Log(engine).Should().Be("");
    }

    [Fact]
    public void OnAbortTakesItsTurnInRegistrationOrder()
    {
        var engine = WebEngine();

        engine.Execute("""
            var controller = new AbortController();
            var signal = controller.signal;
            signal.addEventListener('abort', function () { log.push('first'); });
            signal.onabort = function () { log.push('handler'); };
            signal.addEventListener('abort', function () { log.push('last'); });
            controller.abort();
            """);

        Log(engine).Should().Be("first,handler,last");
    }

    [Fact]
    public void ReassigningOnAbortKeepsThePositionAndClearingItRemovesTheListener()
    {
        var engine = WebEngine();

        engine.Execute("""
            var controller = new AbortController();
            var signal = controller.signal;
            signal.onabort = function () { log.push('one'); };
            signal.addEventListener('abort', function () { log.push('other'); });
            signal.onabort = function () { log.push('two'); };
            controller.abort();
            """);

        // "Activate an event handler" does nothing once a listener exists, so the entry keeps its place.
        Log(engine).Should().Be("two,other");

        engine.Execute("""
            var second = new AbortController();
            second.signal.onabort = function () { log.push('never'); };
            second.signal.onabort = null;
            second.abort();
            """);

        Log(engine).Should().Be("two,other");
    }

    [Fact]
    public void OnAbortReadsBackWhatWasAssignedAndIgnoresNonObjects()
    {
        var engine = WebEngine();
        engine.Execute("var signal = new AbortController().signal;");

        engine.Evaluate("signal.onabort").IsNull().Should().BeTrue();

        engine.Execute("var f = function () {}; signal.onabort = f;");
        engine.Evaluate("signal.onabort === f").AsBoolean().Should().BeTrue();

        // EventHandler is [LegacyTreatNonObjectAsNull]: a non-object clears it rather than throwing.
        engine.Execute("signal.onabort = 42;");
        engine.Evaluate("signal.onabort").IsNull().Should().BeTrue();
    }

    [Fact]
    public void RemoveEventListenerCannotRemoveTheOnAbortHandler()
    {
        var engine = WebEngine();

        engine.Execute("""
            var controller = new AbortController();
            var f = function () { log.push('handler'); };
            controller.signal.onabort = f;
            controller.signal.removeEventListener('abort', f);
            controller.abort();
            """);

        // In the specification the handler's callback is HTML's processing algorithm, never the assigned
        // function, so removeEventListener has nothing to match.
        Log(engine).Should().Be("handler");
    }

    [Fact]
    public void StaticAbortReturnsAnAlreadyAbortedSignal()
    {
        var engine = WebEngine();

        engine.Evaluate("AbortSignal.abort().aborted").AsBoolean().Should().BeTrue();
        engine.Evaluate("AbortSignal.abort().reason.name").AsString().Should().Be("AbortError");
        engine.Evaluate("AbortSignal.abort('why').reason").AsString().Should().Be("why");
        engine.Evaluate("AbortSignal.abort() instanceof AbortSignal").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ASignalOptionRemovesTheListenerWhenItAborts()
    {
        var engine = WebEngine();

        engine.Execute("""
            var target = new EventTarget();
            var controller = new AbortController();
            target.addEventListener('ping', function () { log.push('ping'); }, { signal: controller.signal });
            target.dispatchEvent(new Event('ping'));
            controller.abort();
            target.dispatchEvent(new Event('ping'));
            """);

        Log(engine).Should().Be("ping");
    }

    [Fact]
    public void AnAlreadyAbortedSignalOptionNeverAddsTheListener()
    {
        var engine = WebEngine();

        engine.Execute("""
            var target = new EventTarget();
            target.addEventListener('ping', function () { log.push('ping'); }, { signal: AbortSignal.abort() });
            target.dispatchEvent(new Event('ping'));
            """);

        Log(engine).Should().Be("");
    }

    [Fact]
    public void RefusesASignalOptionThatIsNotAnAbortSignal()
    {
        var engine = WebEngine();

        engine.Execute("var target = new EventTarget();");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("target.addEventListener('ping', function () {}, { signal: {} })"))
            .Message.Should().Contain("AbortSignal");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("target.addEventListener('ping', function () {}, { signal: null })"));

        // An absent member is not a signal at all, which is not an error.
        engine.Execute("target.addEventListener('ping', function () { log.push('ok'); }, { signal: undefined }); target.dispatchEvent(new Event('ping'));");
        Log(engine).Should().Be("ok");
    }

    [Fact]
    public void AnyAbortsWithWhicheverSourceAbortsFirst()
    {
        var engine = WebEngine();

        engine.Execute("""
            var a = new AbortController();
            var b = new AbortController();
            var any = AbortSignal.any([a.signal, b.signal]);
            any.addEventListener('abort', function () { log.push('aborted'); });

            any.aborted;
            b.abort('from b');
            a.abort('from a');
            """);

        engine.Evaluate("any.aborted").AsBoolean().Should().BeTrue();
        engine.Evaluate("any.reason").AsString().Should().Be("from b");
        Log(engine).Should().Be("aborted");
    }

    [Fact]
    public void AnyIsAlreadyAbortedWhenASourceIs()
    {
        var engine = WebEngine();

        engine.Execute("""
            var later = new AbortController();
            var any = AbortSignal.any([later.signal, AbortSignal.abort('already')]);
            any.addEventListener('abort', function () { log.push('never'); });
            later.abort('too late');
            """);

        engine.Evaluate("any.aborted").AsBoolean().Should().BeTrue();
        engine.Evaluate("any.reason").AsString().Should().Be("already");

        // The signal was aborted before anyone could listen, so no event was ever fired.
        Log(engine).Should().Be("");
    }

    [Fact]
    public void AnyFlattensADependentSourceIntoItsOwnSources()
    {
        var engine = WebEngine();

        engine.Execute("""
            var a = new AbortController();
            var inner = AbortSignal.any([a.signal]);
            var outer = AbortSignal.any([inner]);
            a.abort('root');
            """);

        engine.Evaluate("inner.aborted").AsBoolean().Should().BeTrue();
        engine.Evaluate("outer.aborted").AsBoolean().Should().BeTrue();
        engine.Evaluate("outer.reason").AsString().Should().Be("root");
    }

    [Fact]
    public void AnyOverNoSignalsNeverAborts()
    {
        var engine = WebEngine();

        engine.Evaluate("AbortSignal.any([]).aborted").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void AnyRequiresASequenceOfAbortSignals()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("AbortSignal.any([{}])"))
            .Message.Should().Contain("AbortSignal");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("AbortSignal.any(42)"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("AbortSignal.any()"));

        // Any iterable is a sequence, not just an array.
        engine.Evaluate("AbortSignal.any(new Set([AbortSignal.abort('s')])).reason").AsString().Should().Be("s");
    }

    [Fact]
    public void TimeoutAbortsOnlyOnceTheEngineIsPumpedPastTheDueTime()
    {
        var (engine, clock) = TimeoutEngine();

        engine.Execute("""
            var signal = AbortSignal.timeout(50);
            signal.addEventListener('abort', function () { log.push(signal.reason.name); });
            """);

        engine.Advanced.ProcessTasks();
        engine.Evaluate("signal.aborted").AsBoolean().Should().BeFalse();

        clock.Advance(49);
        engine.Advanced.ProcessTasks();
        engine.Evaluate("signal.aborted").AsBoolean().Should().BeFalse();

        clock.Advance(1);
        engine.Advanced.ProcessTasks();

        engine.Evaluate("signal.aborted").AsBoolean().Should().BeTrue();
        engine.Evaluate("signal.reason instanceof DOMException").AsBoolean().Should().BeTrue();
        Log(engine).Should().Be("TimeoutError");
    }

    [Fact]
    public void TimeoutNeverFiresOnAnEngineNobodyPumps()
    {
        var (engine, clock) = TimeoutEngine();

        // Read through the CLR object rather than through script, because evaluating a script is itself a
        // pump — it drains the event loop when it finishes.
        var signal = (JsAbortSignal) engine.Evaluate("var signal = AbortSignal.timeout(50); signal");
        clock.Advance(1000);

        // Long past due, and nothing has pumped: no abort. The same contract setTimeout has.
        signal.Aborted.Should().BeFalse();

        engine.Advanced.ProcessTasks();
        signal.Aborted.Should().BeTrue();
    }

    [Fact]
    public void TimeoutEnforcesTheRangeOfItsArgument()
    {
        var (engine, _) = TimeoutEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("AbortSignal.timeout(-1)"))
            .Message.Should().Contain("range");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("AbortSignal.timeout(NaN)"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("AbortSignal.timeout(Infinity)"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("AbortSignal.timeout()"));

        // A finite value that is not an integer is truncated, not refused.
        engine.Execute("AbortSignal.timeout(1.9);");
    }

    [Fact]
    public void TimeoutCountsAgainstTheTimerBudget()
    {
        var (engine, _) = TimeoutEngine(maxActiveTimers: 1);

        engine.Execute("AbortSignal.timeout(1000);");

        engine.Execute("""
            var caught = null;
            try { AbortSignal.timeout(1000); } catch (e) { caught = e; }
            """);

        engine.Evaluate("caught.name").AsString().Should().Be("QuotaExceededError");
    }

    [Fact]
    public void ASignalCannotBeConstructedDirectly()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new AbortSignal()"))
            .Message.Should().Contain("Illegal constructor");
    }

    [Fact]
    public void HasTheIdlShape()
    {
        var engine = WebEngine();

        engine.Evaluate("AbortController.length").AsNumber().Should().Be(0);
        engine.Evaluate("AbortController.prototype.abort.length").AsNumber().Should().Be(0);
        engine.Evaluate("AbortSignal.abort.length").AsNumber().Should().Be(0);
        engine.Evaluate("AbortSignal.timeout.length").AsNumber().Should().Be(1);
        engine.Evaluate("AbortSignal.any.length").AsNumber().Should().Be(1);
        engine.Evaluate("AbortSignal.prototype.throwIfAborted.length").AsNumber().Should().Be(0);

        engine.Evaluate("Object.getPrototypeOf(AbortSignal) === EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(AbortSignal.prototype) === EventTarget.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(AbortController) === Function.prototype").AsBoolean().Should().BeTrue();

        engine.Evaluate("Object.prototype.toString.call(new AbortController())").AsString().Should().Be("[object AbortController]");
        engine.Evaluate("Object.prototype.toString.call(new AbortController().signal)").AsString().Should().Be("[object AbortSignal]");
    }

    [Fact]
    public void ExposesItsAttributesAsPrototypeAccessors()
    {
        var engine = WebEngine();

        engine.Evaluate("Object.getOwnPropertyNames(new AbortController()).length").AsNumber().Should().Be(0);
        engine.Evaluate("Object.getOwnPropertyNames(new AbortController().signal).length").AsNumber().Should().Be(0);

        var aborted = "Object.getOwnPropertyDescriptor(AbortSignal.prototype, 'aborted')";
        engine.Evaluate($"typeof {aborted}.get").AsString().Should().Be("function");
        engine.Evaluate($"{aborted}.set").IsUndefined().Should().BeTrue();
        engine.Evaluate($"{aborted}.enumerable").AsBoolean().Should().BeTrue();

        // onabort is the one attribute with a setter.
        var onabort = "Object.getOwnPropertyDescriptor(AbortSignal.prototype, 'onabort')";
        engine.Evaluate($"typeof {onabort}.get").AsString().Should().Be("function");
        engine.Evaluate($"typeof {onabort}.set").AsString().Should().Be("function");
    }

    [Fact]
    public void RefusesAReceiverThatIsNotTheRightInterface()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("AbortSignal.prototype.aborted"))
            .Message.Should().Contain("AbortSignal");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("AbortController.prototype.signal"))
            .Message.Should().Contain("AbortController");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("AbortController.prototype.abort.call(new AbortController().signal)"));
    }

    [Fact]
    public void SupportsBeingSubclassed()
    {
        var engine = WebEngine();

        engine.Execute("""
            class MyController extends AbortController {}
            var controller = new MyController();
            controller.signal.addEventListener('abort', function () { log.push('aborted'); });
            controller.abort();
            """);

        engine.Evaluate("controller instanceof MyController").AsBoolean().Should().BeTrue();
        engine.Evaluate("controller instanceof AbortController").AsBoolean().Should().BeTrue();

        // "Let signal be a new AbortSignal object" — the subclass affects the controller, not its signal.
        engine.Evaluate("Object.getPrototypeOf(controller.signal) === AbortSignal.prototype").AsBoolean().Should().BeTrue();
        Log(engine).Should().Be("aborted");
    }
}
#endif
