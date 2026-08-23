#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The global scope's event surface: <c>addEventListener</c>, <c>removeEventListener</c>,
/// <c>dispatchEvent</c> and <c>self</c>, and the <c>error</c>, <c>unhandledrejection</c> and
/// <c>rejectionhandled</c> events the engine fires at them.
/// </summary>
/// <remarks>
/// <para>
/// The specifications are HTML's
/// <see href="https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception">report an
/// exception</see> and
/// <see href="https://html.spec.whatwg.org/multipage/webappapis.html#unhandled-promise-rejections">unhandled
/// promise rejections</see>.
/// </para>
/// <para>
/// Four things here are load-bearing and are asserted from both sides. The events <b>feed</b> the
/// <see cref="DiagnosticsSink"/> and never replace it, so <c>preventDefault()</c> cannot starve a host's log.
/// Only a <see cref="JavaScriptException"/> is ever dispatched, so a listener cannot swallow a budget. A
/// report does not recurse, so a throwing <c>error</c> listener cannot loop. And an engine without the flag —
/// or without any web API at all — is exactly the engine it was.
/// </para>
/// </remarks>
public class GlobalErrorEventTests
{
    private sealed class RecordingSink : DiagnosticsSink
    {
        internal List<DiagnosticEvent> Reports { get; } = new();

        public override void Report(DiagnosticEvent report) => Reports.Add(report);
    }

    /// <summary>A clock that only moves when a test moves it, so the timer tests are exact and instant.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    private static (Engine Engine, RecordingSink Sink, ManualClock Clock) Reporting(Action<Options>? configure = null)
    {
        var sink = new RecordingSink();
        var clock = new ManualClock();
        var engine = new Engine(options =>
        {
            options.UseWebApis(webApi =>
            {
                webApi.Timers.TimeProvider = clock;
                webApi.Diagnostics.Sink = sink;
            });
            configure?.Invoke(options);
        });

        engine.Execute("var log = [];");
        return (engine, sink, clock);
    }

    private static (Engine Engine, ManualClock Clock) Silent(Action<Options>? configure = null)
    {
        var clock = new ManualClock();
        var engine = new Engine(options =>
        {
            options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock);
            configure?.Invoke(options);
        });

        engine.Execute("var log = [];");
        return (engine, clock);
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    // The globals themselves

    [Fact]
    public void SelfIsGlobalThis()
    {
        var (engine, _) = Silent();

        engine.Evaluate("self === globalThis").AsBoolean().Should().BeTrue();

        // A [Replaceable] readonly attribute, simplified to an ordinary enumerable data property exactly as
        // console, crypto and navigator are.
        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("self");
        descriptor.Writable.Should().BeTrue();
        descriptor.Enumerable.Should().BeTrue();
        descriptor.Configurable.Should().BeTrue();
    }

    /// <summary>
    /// <c>[Replaceable]</c> means an assignment <i>replaces</i> the attribute rather than being refused —
    /// https://webidl.spec.whatwg.org/#Replaceable — and the writable data property above is that behaviour
    /// simplified. It holds in both modes, because a writable property gives strict mode nothing to refuse.
    /// </summary>
    /// <remarks>
    /// The counterpart of <c>WorkerMechanismTests.TheWorkerGlobalsSelfIsAReadOnlyAttribute</c>, and the
    /// reason that one is installed on the worker's global scope rather than here: HTML gives the two globals
    /// different IDL — <c>[Replaceable] readonly attribute WindowProxy self</c> on Window
    /// (https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-self) against a plain
    /// <c>readonly attribute</c> on <c>WorkerGlobalScope</c>
    /// (https://html.spec.whatwg.org/multipage/workers.html#dom-workerglobalscope-self) — so making this one
    /// read-only too would be wrong, and would break shadowing a script may rely on.
    /// </remarks>
    [Fact]
    public void TheTopLevelSelfIsReplaceable()
    {
        var (sloppy, _) = Silent();
        sloppy.Execute("self = 'shadowed';");
        sloppy.Evaluate("self").AsString().Should().Be("shadowed");

        var (strict, _) = Silent();
        strict.Execute("'use strict'; self = 'shadowed';");
        strict.Evaluate("self").AsString().Should().Be("shadowed");
    }

    [Fact]
    public void TheThreeOperationsAreWebIdlOperations()
    {
        var (engine, _) = Silent();

        foreach (var (name, length) in new[] { ("addEventListener", 2), ("removeEventListener", 2), ("dispatchEvent", 1) })
        {
            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);
            descriptor.Writable.Should().BeTrue();
            descriptor.Enumerable.Should().BeTrue();
            descriptor.Configurable.Should().BeTrue();

            engine.Evaluate($"{name}.length").AsNumber().Should().Be(length);
            engine.Evaluate($"{name}.name").AsString().Should().Be(name);
        }
    }

    [Fact]
    public void TheGlobalObjectItselfIsNotAnEventTarget()
    {
        var (engine, _) = Silent();

        // The whole point of the synthetic target: the global object gains no prototype and no brand, so
        // nothing about its own property model changes.
        engine.Evaluate("globalThis instanceof EventTarget").AsBoolean().Should().BeFalse();

        // And EventTarget.prototype's own methods still refuse it, because it is not one.
        Assert.Throws<JavaScriptException>(() =>
            engine.Execute("EventTarget.prototype.addEventListener.call(globalThis, 'x', function () {})"))
            .Message.Should().Contain("not an EventTarget");
    }

    [Fact]
    public void DispatchesToTheGlobalScopeWithTheGlobalObjectAsTarget()
    {
        var (engine, _) = Silent();

        engine.Execute("""
            addEventListener('ping', function (e) {
                log.push(e.type, e.target === globalThis, e.currentTarget === globalThis, this === globalThis, e.isTrusted);
            });
            var result = dispatchEvent(new Event('ping'));
            """);

        // A script's own dispatch is never trusted, and target/currentTarget/this are the global object —
        // not the synthetic listener list, which script has no way to name.
        Log(engine).Should().Be("ping,true,true,true,false");
        engine.Evaluate("result").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void RemoveEventListenerMatchesOnIdentity()
    {
        var (engine, _) = Silent();

        engine.Execute("""
            function handler() { log.push('ran'); }
            self.addEventListener('ping', handler);
            self.removeEventListener('ping', handler);
            self.dispatchEvent(new Event('ping'));
            """);

        Log(engine).Should().Be("");
    }

    [Fact]
    public void TheOperationsAreReachableThroughSelfAndGlobalThisAlike()
    {
        var (engine, _) = Silent();

        engine.Execute("""
            var seen = 0;
            globalThis.addEventListener('ping', function () { seen++; });
            self.dispatchEvent(new Event('ping'));
            dispatchEvent(new Event('ping'));
            """);

        engine.Evaluate("seen").AsNumber().Should().Be(2);
    }

    // error — HTML's report an exception, step 5

    [Fact]
    public void AnUncaughtTimerErrorFiresATrustedErrorEvent()
    {
        var (engine, sink, clock) = Reporting();

        engine.Execute("""
            var seen = null;
            addEventListener('error', function (e) { seen = e; log.push(e.type, e.isTrusted, e.cancelable, e.bubbles); });
            setTimeout(function () { throw new TypeError('boom'); }, 1);
            """);

        clock.Advance(5);
        engine.Advanced.ProcessTasks();

        Log(engine).Should().Be("error,true,true,false");
        engine.Evaluate("seen instanceof ErrorEvent").AsBoolean().Should().BeTrue();
        engine.Evaluate("seen.message").AsString().Should().Be("boom");
        engine.Evaluate("seen.error instanceof TypeError").AsBoolean().Should().BeTrue();

        // The location the engine knew at the throw, which for a script executed here is a real line.
        engine.Evaluate("seen.lineno > 0").AsBoolean().Should().BeTrue();
        engine.Evaluate("seen.colno > 0").AsBoolean().Should().BeTrue();

        // And the sink still hears it — the event feeds it, it does not replace it.
        var report = Assert.Single(sink.Reports);
        report.Kind.Should().Be(DiagnosticEventKind.UncaughtCallbackError);
        report.CallbackSource.Should().Be(DiagnosticCallbackSource.Timer);
    }

    [Fact]
    public void AnUncaughtListenerErrorFiresATrustedErrorEvent()
    {
        var (engine, sink, _) = Reporting();

        engine.Execute("""
            addEventListener('error', function (e) { log.push('global:' + e.message); });
            var target = new EventTarget();
            target.addEventListener('ping', function () { throw new Error('inner'); });
            target.addEventListener('ping', function () { log.push('second'); });
            target.dispatchEvent(new Event('ping'));
            """);

        // Inner invoke reports and carries on, and reporting is what fires the global error event.
        Log(engine).Should().Be("global:inner,second");
        Assert.Single(sink.Reports).CallbackSource.Should().Be(DiagnosticCallbackSource.EventListener);
    }

    [Fact]
    public void AnUncaughtIdleCallbackErrorFiresATrustedErrorEvent()
    {
        // requestIdleCallback invokes its callback with "report" too, so the same report an idle failure makes
        // reaches the global error event before it reaches the sink —
        // https://w3c.github.io/requestidlecallback/#invoke-idle-callbacks-algorithm.
        var (engine, sink, _) = Reporting();

        engine.Execute("""
            addEventListener('error', function (e) { log.push('global:' + e.message); });
            requestIdleCallback(function () { throw new Error('idle'); });
            requestIdleCallback(function () { log.push('second'); });
            """);

        Log(engine).Should().Be("global:idle,second");
        Assert.Single(sink.Reports).CallbackSource.Should().Be(DiagnosticCallbackSource.IdleCallback);
    }

    [Fact]
    public void ReportErrorFiresTheErrorEventAndStillFeedsTheSink()
    {
        var (engine, sink, _) = Reporting();

        engine.Execute("""
            var seen = null;
            addEventListener('error', function (e) { seen = e; });
            var boom = new Error('boom');
            reportError(boom);
            """);

        engine.Evaluate("seen instanceof ErrorEvent").AsBoolean().Should().BeTrue();
        engine.Evaluate("seen.error === boom").AsBoolean().Should().BeTrue();
        engine.Evaluate("seen.message").AsString().Should().Be("boom");
        engine.Evaluate("seen.isTrusted").AsBoolean().Should().BeTrue();

        Assert.Single(sink.Reports).Kind.Should().Be(DiagnosticEventKind.ReportedError);
    }

    [Fact]
    public void ReportErrorFiresTheErrorEventWithNoSinkAtAll()
    {
        // reportError is itself the request to report, so unlike a callback failure it does not need the
        // sink to be armed before anything happens.
        var (engine, _) = Silent();

        engine.Execute("""
            var seen = null;
            addEventListener('error', function (e) { seen = e; });
            reportError('a string');
            """);

        engine.Evaluate("seen.message").AsString().Should().Be("a string");
        engine.Evaluate("seen.error").AsString().Should().Be("a string");
    }

    [Fact]
    public void ReportErrorDescribesAnObjectWithoutRunningScript()
    {
        // Rendering an arbitrary object would mean calling its own toString, which is exactly the hazard
        // Throw.SafeToDisplayString exists for. An in-box Error's own `message` data property is safe to
        // read; anything else is reported as its shape. event.error carries the value itself either way.
        var (engine, _) = Silent();

        engine.Execute("""
            var messages = [];
            addEventListener('error', function (e) { messages.push(e.message); });
            reportError({ toString: function () { throw new Error('never called'); } });
            reportError(new RangeError('real message'));
            """);

        engine.Evaluate("messages[0]").AsString().Should().Be("[object]");
        engine.Evaluate("messages[1]").AsString().Should().Be("real message");
    }

    [Fact]
    public void PreventDefaultDoesNotStarveTheSink()
    {
        var (engine, sink, clock) = Reporting();

        engine.Execute("""
            addEventListener('error', function (e) { e.preventDefault(); log.push('canceled:' + e.defaultPrevented); });
            reportError(new Error('reported'));
            setTimeout(function () { throw new Error('timed'); }, 1);
            """);

        clock.Advance(5);
        engine.Advanced.ProcessTasks();

        // The listener really did cancel — this is HTML's notHandled going false — and the host's channel is
        // told anyway. That is the locked divergence: a script may observe a failure, never hide one.
        Log(engine).Should().Be("canceled:true,canceled:true");
        sink.Reports.Should().HaveCount(2);
        sink.Reports[0].Kind.Should().Be(DiagnosticEventKind.ReportedError);
        sink.Reports[1].Kind.Should().Be(DiagnosticEventKind.UncaughtCallbackError);
    }

    [Fact]
    public void AThrowingErrorListenerDoesNotReDispatch()
    {
        var (engine, sink, _) = Reporting();

        engine.Execute("""
            var errors = 0;
            addEventListener('error', function () { errors++; throw new Error('from the reporter'); });
            var target = new EventTarget();
            target.addEventListener('ping', function () { throw new Error('original'); });
            target.dispatchEvent(new Event('ping'));
            """);

        // HTML's re-entrancy rule: an exception thrown while reporting one goes to the sink alone. Without
        // the guard this would recurse until the stack or the recursion budget gave out.
        engine.Evaluate("errors").AsNumber().Should().Be(1);

        // Both reach the sink, the nested one first — step 5 (fire the event, inside which the listener's own
        // failure is reported whole) completes before step 6 reports the original, which is the order a
        // browser's console shows them in too.
        sink.Reports.Should().HaveCount(2);
        sink.Reports[0].Exception!.Message.Should().Be("from the reporter");
        sink.Reports[1].Exception!.Message.Should().Be("original");
    }

    [Fact]
    public void AnErrorListenerNeverSeesAConstraintFailure()
    {
        // Only a JavaScriptException is ever dispatched or reported. A budget that a listener could observe
        // is a budget a listener could be tempted to swallow, so the ones that bound execution keep erupting
        // past both the event and the sink.
        var (engine, sink, clock) = Reporting(options => options.Constraints.MaxRecursionDepth = 8);

        engine.Execute("""
            addEventListener('error', function () { log.push('seen'); });
            function recurse() { return recurse(); }
            setTimeout(recurse, 1);
            """);

        clock.Advance(5);
        Assert.Throws<RecursionDepthOverflowException>(() => engine.Advanced.ProcessTasks());

        Log(engine).Should().Be("");
        sink.Reports.Should().BeEmpty();
    }

    [Fact]
    public void AnUncaughtTimerErrorStillEruptsWithoutASink()
    {
        // The sink is what turns an uncaught callback failure into a *report*, and report an exception is the
        // algorithm that fires the event. With no sink there is no report, so there is no event either and
        // the exception erupts exactly as it always has.
        var (engine, clock) = Silent();

        engine.Execute("""
            addEventListener('error', function () { log.push('seen'); });
            setTimeout(function () { throw new Error('boom'); }, 1);
            """);

        clock.Advance(5);
        Assert.Throws<JavaScriptException>(() => engine.Advanced.ProcessTasks()).Message.Should().Be("boom");

        Log(engine).Should().Be("");
    }

    // unhandledrejection / rejectionhandled

    [Fact]
    public void AnUnhandledRejectionFiresAPromiseRejectionEvent()
    {
        var (engine, sink, _) = Reporting();

        engine.Execute("""
            var seen = null;
            addEventListener('unhandledrejection', function (e) { seen = e; log.push(e.type, e.isTrusted, e.cancelable); });
            var reason = new Error('nope');
            var p = Promise.reject(reason);
            """);

        Log(engine).Should().Be("unhandledrejection,true,true");
        engine.Evaluate("seen instanceof PromiseRejectionEvent").AsBoolean().Should().BeTrue();
        engine.Evaluate("seen.promise === p").AsBoolean().Should().BeTrue();
        engine.Evaluate("seen.reason === reason").AsBoolean().Should().BeTrue();

        Assert.Single(sink.Reports).RejectionHandled.Should().BeFalse();
    }

    [Fact]
    public void AHandlerAttachedLaterFiresRejectionHandled()
    {
        var (engine, _) = Silent();

        engine.Execute("""
            addEventListener('unhandledrejection', function (e) { log.push('unhandled'); });
            addEventListener('rejectionhandled', function (e) { log.push('handled:' + e.cancelable); });
            var p = Promise.reject(new Error('nope'));
            p.catch(function () {});
            """);

        // The tracker's cadence, not HTML's microtask checkpoint: a browser would raise neither of these for
        // a rejection handled this soon. rejectionhandled is fired without a cancelable initializer.
        Log(engine).Should().Be("unhandled,handled:false");
    }

    [Fact]
    public void CancellingUnhandledRejectionDoesNotStarveTheSink()
    {
        var (engine, sink, _) = Reporting();

        engine.Execute("""
            addEventListener('unhandledrejection', function (e) { e.preventDefault(); });
            Promise.reject(new Error('nope'));
            """);

        Assert.Single(sink.Reports).Kind.Should().Be(DiagnosticEventKind.UnhandledPromiseRejection);
    }

    // Gating and lifetime

    [Fact]
    public void TheGlobalsAreAbsentWithoutTheFlag()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events | WebApiFeatures.Timers | WebApiFeatures.Reporting));

        foreach (var name in new[] { "addEventListener", "removeEventListener", "dispatchEvent", "self", "ErrorEvent", "PromiseRejectionEvent" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("undefined");
        }
    }

    [Fact]
    public void AnEngineWithoutTheFlagFiresNothingAndPaysNothing()
    {
        // The hook sites are null-guarded on the synthetic target, which only the three global operations can
        // create — so an engine without them behaves exactly as it did before this feature existed.
        var sink = new RecordingSink();
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Features = WebApiFeatures.Events | WebApiFeatures.Timers;
            webApi.Timers.TimeProvider = clock;
            webApi.Diagnostics.Sink = sink;
        }));

        engine.Execute("setTimeout(function () { throw new Error('boom'); }, 1);");
        clock.Advance(5);
        engine.Advanced.ProcessTasks();

        Assert.Single(sink.Reports).CallbackSource.Should().Be(DiagnosticCallbackSource.Timer);
    }

    [Fact]
    public void ADefaultEngineHasNoneOfIt()
    {
        var engine = new Engine();

        foreach (var name in new[] { "addEventListener", "self", "ErrorEvent", "PromiseRejectionEvent", "reportError" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("undefined");
        }
    }

    [Fact]
    public void ARestoreDropsTheListeners()
    {
        var (engine, _) = Silent();

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        // A sloppy-mode assignment to a free identifier, so a listener that survived the restore would write
        // into the *next* cycle's `seen` and be caught red-handed.
        engine.Execute("addEventListener('error', function () { seen = 'stale'; });");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The listener is a closure over the ended cycle — over globals the restore has just replaced — so a
        // failure in the next cycle must reach none of them.
        engine.Execute("var seen = 'none'; reportError(new Error('boom'));");
        engine.Evaluate("seen").AsString().Should().Be("none");

        // And the next cycle registers its own on a target built fresh.
        engine.Execute("addEventListener('error', function (e) { seen = e.message; }); reportError(new Error('after'));");
        engine.Evaluate("seen").AsString().Should().Be("after");
    }

    [Fact]
    public void TheFeatureCanBeEnabledOnALiveEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        engine.Evaluate("typeof addEventListener").AsString().Should().Be("undefined");

        engine.Advanced.EnableWebApis(WebApiFeatures.GlobalEvents | WebApiFeatures.Reporting);

        // GlobalEvents implies Events, so the machinery it needs arrives with it.
        engine.Evaluate("typeof Event").AsString().Should().Be("function");
        engine.Evaluate("self === globalThis").AsBoolean().Should().BeTrue();

        engine.Execute("""
            var seen = null;
            addEventListener('error', function (e) { seen = e.message; });
            reportError(new Error('live'));
            """);

        engine.Evaluate("seen").AsString().Should().Be("live");
    }
}
#endif
