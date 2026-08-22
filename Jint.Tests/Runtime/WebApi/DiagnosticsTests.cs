#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The engine's diagnostics channel: <c>reportError</c>, unhandled promise rejections, and the exceptions
/// that escape a callback the engine itself invoked.
/// </summary>
/// <remarks>
/// <para>
/// Half of what is asserted here is what happens <i>without</i> a sink, because setting one is what changes
/// the contract: with no sink a timer callback, a <c>queueMicrotask</c> callback or an event listener that
/// throws erupts, which is the
/// behaviour every other test in this folder was written against and which must stay exactly as it was.
/// Every report-and-continue test therefore has an erupts-without-a-sink twin.
/// </para>
/// <para>
/// The specifications behind the three kinds are HTML's
/// <see href="https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception">report an
/// exception</see> and
/// <see href="https://html.spec.whatwg.org/multipage/webappapis.html#unhandled-promise-rejections">unhandled
/// promise rejections</see>, HTML's timer initialization steps (which invoke a handler with exception
/// behavior <c>"report"</c>), and DOM's
/// <see href="https://dom.spec.whatwg.org/#concept-event-listener-inner-invoke">inner invoke</see>.
/// </para>
/// </remarks>
public class DiagnosticsTests
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

    // reportError — https://html.spec.whatwg.org/multipage/webappapis.html#dom-reporterror

    [Fact]
    public void ReportErrorHandsTheValueToTheSink()
    {
        var (engine, sink, _) = Reporting();

        engine.Execute("var e = new Error('boom'); reportError(e);");

        var report = Assert.Single(sink.Reports);
        report.Kind.Should().Be(DiagnosticEventKind.ReportedError);
        report.Value.Should().BeSameAs(engine.Evaluate("e"));

        // The other members belong to the other kinds and are empty here.
        report.Exception.Should().BeNull();
        report.CallbackSource.Should().BeNull();
        report.Promise.Should().BeNull();
        report.RejectionHandled.Should().BeFalse();
    }

    [Fact]
    public void ReportErrorReportsAnyValueAtAll()
    {
        // "report an exception exception which is a JavaScript value" — not necessarily an Error.
        var (engine, sink, _) = Reporting();

        engine.Execute("reportError('a string'); reportError(undefined); reportError(42);");

        sink.Reports.Should().HaveCount(3);
        sink.Reports[0].Value.AsString().Should().Be("a string");
        sink.Reports[1].Value.IsUndefined().Should().BeTrue();
        sink.Reports[2].Value.AsNumber().Should().Be(42);
    }

    [Fact]
    public void ReportErrorWithoutASinkIsANoOpThatNeverThrows()
    {
        // The feature installs the function whether or not anything is listening, so a script written for a
        // browser does not have to guard the call. The report simply goes nowhere.
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof reportError").AsString().Should().Be("function");
        engine.Evaluate("reportError(new Error('boom'))").IsUndefined().Should().BeTrue();
        engine.Evaluate("reportError(undefined)").IsUndefined().Should().BeTrue();
    }

    [Fact]
    public void ReportErrorRequiresItsArgument()
    {
        // WebIDL's arity check: `undefined reportError(any e)` has one required argument, so calling with
        // none is a TypeError — https://webidl.spec.whatwg.org/#dfn-create-operation-function. This is the
        // only way the function can fail, and it fails the same way with or without a sink.
        var (engine, sink, _) = Reporting();

        Assert.Throws<JavaScriptException>(() => engine.Execute("reportError()"))
            .Message.Should().Contain("1 argument required");

        sink.Reports.Should().BeEmpty();

        var silent = new Engine(options => options.UseWebApis());
        Assert.Throws<JavaScriptException>(() => silent.Execute("reportError()"));
    }

    [Fact]
    public void ReportErrorIsAWebIdlOperation()
    {
        var (engine, _, _) = Reporting();

        // An operation on the global is writable, enumerable and configurable, and its length counts the
        // required arguments only — https://webidl.spec.whatwg.org/#es-operations.
        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("reportError");
        descriptor.Writable.Should().BeTrue();
        descriptor.Enumerable.Should().BeTrue();
        descriptor.Configurable.Should().BeTrue();

        engine.Evaluate("reportError.length").AsNumber().Should().Be(1);
        engine.Evaluate("reportError.name").AsString().Should().Be("reportError");
    }

    [Fact]
    public void ReportErrorIsAbsentUnlessItsFeatureIsNamed()
    {
        new Engine().Evaluate("typeof reportError").AsString().Should().Be("undefined");

        // A sink alone arms the channel but never installs a global.
        var sink = new RecordingSink();
        new Engine(options => options.WebApi.Diagnostics.Sink = sink)
            .Evaluate("typeof reportError").AsString().Should().Be("undefined");

        // Nor does any other feature bring it along.
        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof reportError").AsString().Should().Be("undefined");

        new Engine(options => options.UseWebApis(WebApiFeatures.Reporting))
            .Evaluate("typeof reportError").AsString().Should().Be("function");
    }

    [Fact]
    public void DoesNotReachIntoAShadowRealm()
    {
        var (engine, _, _) = Reporting();

        // Only the principal realm's global object is ever touched.
        engine.Evaluate("new ShadowRealm().evaluate('typeof reportError')").AsString().Should().Be("undefined");
    }

    // Timer callbacks — https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#timer-initialisation-steps

    [Fact]
    public void AThrowingTimerCallbackIsReportedAndThePumpCarriesOn()
    {
        var (engine, sink, clock) = Reporting();

        engine.Execute("""
            setTimeout(() => { log.push('first'); throw new Error('boom'); }, 5);
            setTimeout(() => log.push('second'), 5);
            """);

        clock.Advance(5);

        // "invoke handler given arguments and "report"": the pump does not see the exception at all, and the
        // timer behind the failed one runs on its own checkpoint.
        engine.Advanced.ProcessTasks();
        engine.Advanced.ProcessTasks();

        Log(engine).Should().Be("first,second");

        var report = Assert.Single(sink.Reports);
        report.Kind.Should().Be(DiagnosticEventKind.UncaughtCallbackError);
        report.CallbackSource.Should().Be(DiagnosticCallbackSource.Timer);
        report.Exception.Should().NotBeNull();
        report.Exception!.Message.Should().Be("boom");
        report.Value.Should().BeSameAs(report.Exception.Error);
        report.Promise.Should().BeNull();
    }

    [Fact]
    public void AThrowingTimerCallbackEruptsWithoutASink()
    {
        // The twin of the test above, and the behaviour every engine without a sink keeps.
        var (engine, clock) = Silent();

        engine.Execute("setTimeout(() => { log.push('first'); throw new Error('boom'); }, 5);");
        clock.Advance(5);

        Assert.Throws<JavaScriptException>(() => engine.Advanced.ProcessTasks())
            .Message.Should().Be("boom");
    }

    [Fact]
    public void AReportedIntervalCallbackKeepsRunning()
    {
        var (engine, sink, clock) = Reporting();

        engine.Execute("var id = setInterval(() => { log.push('tick'); throw new Error('boom'); }, 10);");

        for (var i = 0; i < 3; i++)
        {
            clock.Advance(10);
            engine.Advanced.ProcessTasks();
        }

        Log(engine).Should().Be("tick,tick,tick");
        sink.Reports.Should().HaveCount(3);
        sink.Reports.Should().AllSatisfy(r => r.CallbackSource.Should().Be(DiagnosticCallbackSource.Timer));

        engine.Execute("clearInterval(id);");
    }

    [Fact]
    public void AThrowFromExecutesOwnDrainIsReportedRatherThanReturnedToTheHost()
    {
        // Execute drains the event loop once the script has finished, so without a sink the throw comes out
        // of Execute itself. With one it does not, which is the whole point for a host that runs untrusted
        // script and does not want a stray setTimeout to fail its request.
        var (engine, sink, _) = Reporting();

        engine.Execute("setTimeout(() => { throw new Error('boom'); }, 0);");

        Assert.Single(sink.Reports).Exception!.Message.Should().Be("boom");
    }

    [Fact]
    public void AConstraintFailureInATimerCallbackStillErupts()
    {
        // MustPropagate-class failures are never reported: a budget that turns into a diagnostic no longer
        // bounds anything. RecursionDepthOverflowException is a JintException but not a JavaScriptException,
        // which is exactly what the catch keys on.
        var (engine, sink, clock) = Reporting(options => options.LimitRecursion(8));

        engine.Execute("""
            function recurse() { return recurse(); }
            setTimeout(recurse, 5);
            """);

        clock.Advance(5);

        Assert.Throws<RecursionDepthOverflowException>(() => engine.Advanced.ProcessTasks());
        sink.Reports.Should().BeEmpty();
    }

    // queueMicrotask — https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-queuemicrotask

    [Fact]
    public void AThrowingMicrotaskCallbackIsReportedAndTheQueueCarriesOn()
    {
        var (engine, sink, _) = Reporting();

        engine.Execute("""
            queueMicrotask(() => { log.push('first'); throw new Error('boom'); });
            queueMicrotask(() => log.push('second'));
            """);

        // "Queue a microtask to invoke callback ... and "report"": Execute drains the queue on its way out,
        // so both callbacks have run by the time it returns and the exception never reaches the host.
        Log(engine).Should().Be("first,second");

        var report = Assert.Single(sink.Reports);
        report.Kind.Should().Be(DiagnosticEventKind.UncaughtCallbackError);
        report.CallbackSource.Should().Be(DiagnosticCallbackSource.Microtask);
        report.Exception.Should().NotBeNull();
        report.Exception!.Message.Should().Be("boom");
        report.Value.Should().BeSameAs(report.Exception.Error);
        report.Promise.Should().BeNull();
    }

    [Fact]
    public void AThrowingMicrotaskCallbackEruptsWithoutASink()
    {
        // The twin of the test above, and the behaviour every engine without a sink keeps. It erupts from
        // whatever is running the queue — here Execute's own drain, exactly as a timer's does.
        var (engine, _) = Silent();

        Assert.Throws<JavaScriptException>(
                () => engine.Execute("queueMicrotask(() => { log.push('first'); throw new Error('boom'); });"))
            .Message.Should().Be("boom");

        Log(engine).Should().Be("first");
    }

    [Fact]
    public void AReportedMicrotaskCallbackDoesNotStopTheJobsBehindIt()
    {
        // The engine's single job queue *is* the microtask queue, so a reported failure must leave everything
        // already queued — a promise reaction, a later microtask, a due timer — exactly where it was.
        var (engine, sink, clock) = Reporting();

        engine.Execute("""
            setTimeout(() => log.push('timeout'), 0);
            queueMicrotask(() => { log.push('micro1'); throw new Error('boom'); });
            Promise.resolve().then(() => log.push('promise'));
            queueMicrotask(() => log.push('micro2'));
            log.push('script');
            """);

        clock.Advance(1);
        engine.Advanced.ProcessTasks();

        Log(engine).Should().Be("script,micro1,promise,micro2,timeout");
        Assert.Single(sink.Reports).CallbackSource.Should().Be(DiagnosticCallbackSource.Microtask);
    }

    [Fact]
    public void AConstraintFailureInAMicrotaskCallbackStillErupts()
    {
        // The same MustPropagate rule the timer and listener sites keep: RecursionDepthOverflowException is a
        // JintException but not a JavaScriptException, so the catch never sees it and the budget still bounds.
        var (engine, sink, _) = Reporting(options => options.LimitRecursion(8));

        Assert.Throws<RecursionDepthOverflowException>(() => engine.Execute("""
            function recurse() { return recurse(); }
            queueMicrotask(() => { log.push('entered'); recurse(); });
            """));

        // The overflow happened inside the queued callback rather than before it, which is what makes this a
        // statement about the report site and not about the enqueue.
        Log(engine).Should().Be("entered");
        sink.Reports.Should().BeEmpty();
    }

    [Fact]
    public void AStatementBudgetTrippedInsideAMicrotaskCallbackStillErupts()
    {
        // The other half of the same rule, over the budget a runaway callback actually trips: a microtask
        // that never returns must not be able to spend the engine's statement budget and then have the
        // overflow filed as a diagnostic.
        var (engine, sink, _) = Reporting(options => options.MaxStatements(1000));

        Assert.Throws<StatementsCountOverflowException>(
            () => engine.Execute("queueMicrotask(() => { log.push('entered'); for (var i = 0; ; i++) { } });"));

        Log(engine).Should().Be("entered");
        sink.Reports.Should().BeEmpty();
    }

    // Event listeners — https://dom.spec.whatwg.org/#concept-event-listener-inner-invoke

    [Fact]
    public void AThrowingListenerIsReportedAndTheDispatchContinues()
    {
        var (engine, sink, _) = Reporting();

        engine.Execute("""
            var target = new EventTarget();
            target.addEventListener('ping', function () { log.push('first'); throw new Error('boom'); });
            target.addEventListener('ping', function () { log.push('second'); });
            var e = new Event('ping');
            var result = target.dispatchEvent(e);
            """);

        // Inner invoke step 2.10 reports and moves on, so the second listener runs and dispatchEvent still
        // reports "not canceled".
        Log(engine).Should().Be("first,second");
        engine.Evaluate("result").AsBoolean().Should().BeTrue();

        var report = Assert.Single(sink.Reports);
        report.Kind.Should().Be(DiagnosticEventKind.UncaughtCallbackError);
        report.CallbackSource.Should().Be(DiagnosticCallbackSource.EventListener);
        report.Exception!.Message.Should().Be("boom");

        // The dispatch state is unwound as it always was.
        engine.Evaluate("e.currentTarget").IsNull().Should().BeTrue();
        engine.Evaluate("e.eventPhase").AsNumber().Should().Be(0);
    }

    [Fact]
    public void AThrowingListenerEruptsWithoutASink()
    {
        var (engine, _) = Silent();

        engine.Execute("""
            var target = new EventTarget();
            target.addEventListener('ping', function () { log.push('first'); throw new Error('boom'); });
            target.addEventListener('ping', function () { log.push('second'); });
            var e = new Event('ping');
            """);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("target.dispatchEvent(e)"))
            .Message.Should().Be("boom");

        Log(engine).Should().Be("first");
    }

    [Fact]
    public void AThrowingAbortListenerIsReportedAndTheAbortCompletes()
    {
        var (engine, sink, _) = Reporting();

        engine.Execute("""
            var controller = new AbortController();
            controller.signal.addEventListener('abort', function () { throw new Error('boom'); });
            controller.abort('why');
            """);

        // controller.abort() no longer erupts, and the signal is aborted regardless.
        engine.Evaluate("controller.signal.aborted").AsBoolean().Should().BeTrue();
        engine.Evaluate("controller.signal.reason").AsString().Should().Be("why");

        Assert.Single(sink.Reports).CallbackSource.Should().Be(DiagnosticCallbackSource.EventListener);
    }

    [Fact]
    public void AConstraintFailureInAListenerStillErupts()
    {
        var (engine, sink, _) = Reporting(options => options.LimitRecursion(8));

        engine.Execute("""
            function recurse() { return recurse(); }
            var target = new EventTarget();
            target.addEventListener('ping', recurse);
            var e = new Event('ping');
            """);

        Assert.Throws<RecursionDepthOverflowException>(() => engine.Evaluate("target.dispatchEvent(e)"));
        sink.Reports.Should().BeEmpty();
    }

    // Unhandled promise rejections —
    // https://html.spec.whatwg.org/multipage/webappapis.html#unhandled-promise-rejections

    [Fact]
    public void AnUnhandledRejectionReachesTheSinkAndTheExistingEventBoth()
    {
        var (engine, sink, _) = Reporting();

        var tracked = new List<PromiseRejectionTrackerEventArgs>();
        engine.Advanced.PromiseRejectionTracker += (_, args) => tracked.Add(args);

        engine.Execute("var p = Promise.reject(new Error('boom'));");

        // The public event is untouched: it still fires, and it fires first.
        var trackedEvent = Assert.Single(tracked);
        trackedEvent.Operation.Should().Be(PromiseRejectionOperation.Reject);

        var report = Assert.Single(sink.Reports);
        report.Kind.Should().Be(DiagnosticEventKind.UnhandledPromiseRejection);
        report.RejectionHandled.Should().BeFalse();
        report.Promise.Should().BeSameAs(engine.Evaluate("p"));
        report.Promise.Should().BeSameAs(trackedEvent.Promise);
        report.Value.Should().BeSameAs(trackedEvent.Value);
        report.Exception.Should().BeNull();
        report.CallbackSource.Should().BeNull();
    }

    [Fact]
    public void AHandlerAttachedLaterIsReportedAsHandled()
    {
        var (engine, sink, _) = Reporting();

        engine.Execute("var p = Promise.reject(new Error('boom'));");
        sink.Reports.Should().HaveCount(1);

        engine.Execute("p.catch(() => {});");

        // HTML raises the two as unhandledrejection and rejectionhandled; here they are one kind and a flag.
        sink.Reports.Should().HaveCount(2);
        sink.Reports[1].Kind.Should().Be(DiagnosticEventKind.UnhandledPromiseRejection);
        sink.Reports[1].RejectionHandled.Should().BeTrue();
        sink.Reports[1].Promise.Should().BeSameAs(sink.Reports[0].Promise);
    }

    [Fact]
    public void ARejectionHandledInTheSameTurnIsReportedAndThenMarkedHandled()
    {
        var (engine, sink, _) = Reporting();

        // The channel reports at HostPromiseRejectionTracker's cadence, which is what the pre-existing
        // PromiseRejectionTracker event has always done: the rejection is reported the instant it happens
        // with nothing attached, and attaching .catch a moment later is a second report. HTML instead defers
        // its unhandledrejection event to the end of the microtask checkpoint and would raise nothing here,
        // so a host that wants that shape correlates the pair by promise identity itself.
        engine.Execute("Promise.reject(new Error('boom')).catch(() => {});");

        sink.Reports.Should().HaveCount(2);
        sink.Reports[0].RejectionHandled.Should().BeFalse();
        sink.Reports[1].RejectionHandled.Should().BeTrue();
        sink.Reports[1].Promise.Should().BeSameAs(sink.Reports[0].Promise);
    }

    [Fact]
    public void ASinkAloneArmsTheChannelAndInstallsNothing()
    {
        // No feature named at all: the host wanted the reports and nothing else. It must get no globals —
        // not console, not even DOMException, which every other feature brings along.
        var sink = new RecordingSink();
        var engine = new Engine(options => options.WebApi.Diagnostics.Sink = sink);

        engine.Evaluate("typeof console").AsString().Should().Be("undefined");
        engine.Evaluate("typeof DOMException").AsString().Should().Be("undefined");
        engine.Evaluate("typeof reportError").AsString().Should().Be("undefined");

        engine.Execute("Promise.reject('boom');");

        var report = Assert.Single(sink.Reports);
        report.Kind.Should().Be(DiagnosticEventKind.UnhandledPromiseRejection);
        report.Value.AsString().Should().Be("boom");
    }

    [Fact]
    public void ADefaultEngineReportsNothingAndKeepsErupting()
    {
        // The whole channel is opt-in: an engine nobody configured is the engine it always was.
        var engine = new Engine();
        var tracked = 0;
        engine.Advanced.PromiseRejectionTracker += (_, _) => tracked++;

        engine.Execute("Promise.reject('boom');");
        tracked.Should().Be(1);
    }

    // The two ways of saying nothing, which mean opposite things.

    [Fact]
    public void TheNullSinkStillSwitchesEruptToReport()
    {
        // DiagnosticsSink.Null is not the absence of a sink: it says "carry on, and tell me nothing".
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Timers.TimeProvider = clock;
            webApi.Diagnostics.Sink = DiagnosticsSink.Null;
        }));

        engine.Execute("var log = []; setTimeout(() => { throw new Error('boom'); }, 5); setTimeout(() => log.push('after'), 5);");
        clock.Advance(5);

        engine.Advanced.ProcessTasks();
        engine.Advanced.ProcessTasks();

        Log(engine).Should().Be("after");
    }

    [Fact]
    public void ASinkThatThrowsIsNotCaught()
    {
        // Documented: the engine does not guard the sink, so a failing sink is the host's own problem and is
        // never mistaken for the script's.
        var engine = new Engine(options => options.UseDiagnostics(new ThrowingSink()));

        Assert.Throws<InvalidOperationException>(() => engine.Execute("reportError(new Error('boom'))"))
            .Message.Should().Be("sink failed");
    }

    private sealed class ThrowingSink : DiagnosticsSink
    {
        public override void Report(DiagnosticEvent report) => throw new InvalidOperationException("sink failed");
    }

    [Fact]
    public void UseDiagnosticsEnablesReportingAndSetsTheSink()
    {
        var sink = new RecordingSink();
        var engine = new Engine(options => options.UseDiagnostics(sink));

        engine.Evaluate("typeof reportError").AsString().Should().Be("function");
        engine.Execute("reportError('x');");

        Assert.Single(sink.Reports).Value.AsString().Should().Be("x");

        // It adds a feature rather than replacing the set, like every other extension in that file.
        engine.Evaluate("typeof console").AsString().Should().Be("undefined");
    }
}
#endif
