#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The diagnostics channel seen from outside the assembly: what a host writes to be told about the script
/// errors nobody caught, and what changes about the engine when it does.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party — which is
/// the point for a channel whose whole purpose is to be implemented by one. The pins that matter most are the
/// pair <see cref="AThrowingTimerCallbackIsReportedWhenASinkIsSet"/> /
/// <see cref="AThrowingTimerCallbackStillEruptsWithoutASink"/>: installing a sink is what changes the
/// contract, and an engine that installed none must behave exactly as it did before this existed.
/// </remarks>
public class WebApiDiagnosticsTests
{
    private sealed class RecordingSink : DiagnosticsSink
    {
        internal List<DiagnosticEvent> Reports { get; } = new();

        public override void Report(DiagnosticEvent report) => Reports.Add(report);
    }

    /// <summary>A clock a test moves by hand, so nothing here waits on a real timer.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    [Test]
    public void ADefaultEngineHasNoDiagnosticsChannel()
    {
        var engine = new Engine();

        engine.Evaluate("typeof reportError").AsString().Should().Be("undefined");
        engine.Evaluate("'reportError' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void UseWebApisInstallsReportError()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof reportError").AsString().Should().Be("function");

        // A WebIDL operation on the global: writable, enumerable, configurable, length counting the required
        // arguments only — https://webidl.spec.whatwg.org/#es-operations.
        engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'reportError').writable").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'reportError').enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'reportError').configurable").AsBoolean().Should().BeTrue();
        engine.Evaluate("reportError.length").AsNumber().Should().Be(1);
    }

    [Test]
    public void UseDiagnosticsWiresTheSinkAndTheFunctionTogether()
    {
        var sink = new RecordingSink();
        var engine = new Engine(options => options.UseDiagnostics(sink));

        engine.Execute("reportError(new Error('boom'));");

        var report = sink.Reports.Should().ContainSingle().Which;
        report.Kind.Should().Be(DiagnosticEventKind.ReportedError);
        report.Value.AsObject().Get("message").AsString().Should().Be("boom");
    }

    [Test]
    public void ReportErrorWithoutASinkNeverThrows()
    {
        // Documented as a no-op rather than an error, so a script written for a browser needs no guard.
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("reportError(new Error('boom'))").IsUndefined().Should().BeTrue();
    }

    [Test]
    public void AHostRegisteredGlobalStillWins()
    {
        // The install is non-clobbering, which for this name matters twice over: a host that already exposes
        // its own reportError is a host that already has an error channel.
        var sink = new RecordingSink();
        var engine = new Engine(options =>
        {
            options.UseDiagnostics(sink);
            options.Configure(e => e.SetValue("reportError", new Action<JsValue>(_ => { })));
        });

        engine.Execute("reportError('mine');");

        sink.Reports.Should().BeEmpty();
    }

    [Test]
    public void AThrowingTimerCallbackIsReportedWhenASinkIsSet()
    {
        var sink = new RecordingSink();
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Timers.TimeProvider = clock;
            webApi.Diagnostics.Sink = sink;
        }));

        engine.Execute("var ran = false; setTimeout(() => { throw new Error('boom'); }, 5); setTimeout(() => { ran = true; }, 5);");
        clock.Advance(5);

        // Nothing erupts from the pump, and the timer behind the failed one still runs.
        engine.Tasks.ProcessTasks();
        engine.Tasks.ProcessTasks();

        engine.Evaluate("ran").AsBoolean().Should().BeTrue();

        var report = sink.Reports.Should().ContainSingle().Which;
        report.Kind.Should().Be(DiagnosticEventKind.UncaughtCallbackError);
        report.CallbackSource.Should().Be(DiagnosticCallbackSource.Timer);
        report.Exception!.Message.Should().Be("boom");
    }

    [Test]
    public void AThrowingTimerCallbackStillEruptsWithoutASink()
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));

        engine.Execute("setTimeout(() => { throw new Error('boom'); }, 5);");
        clock.Advance(5);

        Assert.Throws<JavaScriptException>(() => engine.Tasks.ProcessTasks())!
            .Message.Should().Be("boom");
    }

    [Test]
    public void AThrowingMicrotaskCallbackIsReportedWhenASinkIsSet()
    {
        // queueMicrotask is invoked with the same WebIDL "report" exception behavior a timer handler is —
        // https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-queuemicrotask.
        var sink = new RecordingSink();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Diagnostics.Sink = sink));

        engine.Execute("""
            var second = false;
            queueMicrotask(() => { throw new Error('boom'); });
            queueMicrotask(() => { second = true; });
            """);

        // Execute drains the queue on its way out, so nothing erupts at the host and the microtask behind the
        // failed one still ran.
        engine.Evaluate("second").AsBoolean().Should().BeTrue();

        var report = sink.Reports.Should().ContainSingle().Which;
        report.Kind.Should().Be(DiagnosticEventKind.UncaughtCallbackError);
        report.CallbackSource.Should().Be(DiagnosticCallbackSource.Microtask);
        report.Exception!.Message.Should().Be("boom");
    }

    [Test]
    public void AThrowingMicrotaskCallbackStillEruptsWithoutASink()
    {
        var engine = new Engine(options => options.UseWebApis());

        Assert.Throws<JavaScriptException>(
                () => engine.Execute("queueMicrotask(() => { throw new Error('boom'); });"))!
            .Message.Should().Be("boom");
    }

    [Test]
    public void AThrowingListenerIsReportedWhenASinkIsSet()
    {
        var sink = new RecordingSink();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Diagnostics.Sink = sink));

        engine.Execute("""
            var second = false;
            var target = new EventTarget();
            target.addEventListener('ping', () => { throw new Error('boom'); });
            target.addEventListener('ping', () => { second = true; });
            var result = target.dispatchEvent(new Event('ping'));
            """);

        engine.Evaluate("second").AsBoolean().Should().BeTrue();
        engine.Evaluate("result").AsBoolean().Should().BeTrue();

        sink.Reports.Should().ContainSingle().Which.CallbackSource.Should().Be(DiagnosticCallbackSource.EventListener);
    }

    [Test]
    public void AThrowingListenerStillEruptsWithoutASink()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Execute("""
            var target = new EventTarget();
            target.addEventListener('ping', () => { throw new Error('boom'); });
            var e = new Event('ping');
            """);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("target.dispatchEvent(e)"))!
            .Message.Should().Be("boom");
    }

    [Test]
    public void AThrowingIdleCallbackIsReportedWhenASinkIsSet()
    {
        // requestIdleCallback invokes its callback with the same WebIDL "report" exception behavior a timer
        // handler and a queueMicrotask callback are invoked with —
        // https://w3c.github.io/requestidlecallback/#invoke-idle-callbacks-algorithm.
        var sink = new RecordingSink();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Diagnostics.Sink = sink));

        engine.Execute("""
            var second = false;
            requestIdleCallback(() => { throw new Error('boom'); });
            requestIdleCallback(() => { second = true; });
            """);

        // Execute drains on its way out, so nothing erupts at the host and the callback behind the failed one
        // still ran in the same idle period.
        engine.Evaluate("second").AsBoolean().Should().BeTrue();

        var report = sink.Reports.Should().ContainSingle().Which;
        report.Kind.Should().Be(DiagnosticEventKind.UncaughtCallbackError);
        report.CallbackSource.Should().Be(DiagnosticCallbackSource.IdleCallback);
        report.Exception!.Message.Should().Be("boom");
    }

    [Test]
    public void AThrowingIdleCallbackStillEruptsWithoutASink()
    {
        var engine = new Engine(options => options.UseWebApis());

        Assert.Throws<JavaScriptException>(
                () => engine.Execute("requestIdleCallback(() => { throw new Error('boom'); });"))!
            .Message.Should().Be("boom");
    }

    [Test]
    public void AThrowingSchedulerTaskIsNotReportedAsACallbackError()
    {
        // scheduler.postTask is governed by https://wicg.github.io/scheduling-apis/, not by a WHATWG living
        // standard, and it invokes its callback with "rethrow": "If that threw an exception, then reject
        // result with that." So the throw belongs in the promise the host was handed, and reporting it as an
        // uncaught callback error would be telling the host about a failure it already has a channel for.
        var sink = new RecordingSink();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Diagnostics.Sink = sink));

        engine.Execute("""
            var reason = null;
            scheduler.postTask(() => { throw new RangeError('boom'); }).catch(e => { reason = e.message; });
            """);

        engine.Evaluate("reason").AsString().Should().Be("boom");
        sink.Reports.Should().BeEmpty();
    }

    [Test]
    public void AConstraintFailureIsNeverReportedAndAlwaysErupts()
    {
        // A budget that turned into a diagnostic would no longer bound anything, so only the class of failure
        // a script could have caught itself — a JavaScriptException — is ever reported.
        var sink = new RecordingSink();
        var clock = new ManualClock();
        var engine = new Engine(options =>
        {
            options.UseWebApis(webApi =>
            {
                webApi.Timers.TimeProvider = clock;
                webApi.Diagnostics.Sink = sink;
            });
            options.Constraints.MaxRecursionDepth = 8;
        });

        engine.Execute("function recurse() { return recurse(); } setTimeout(recurse, 5);");
        clock.Advance(5);

        Assert.Throws<RecursionDepthOverflowException>(() => engine.Tasks.ProcessTasks());
        sink.Reports.Should().BeEmpty();
    }

    [Test]
    public void AnUnhandledRejectionReachesBothChannels()
    {
        var sink = new RecordingSink();
        var engine = new Engine(options => options.UseDiagnostics(sink));

        var tracked = new List<PromiseRejectionTrackerEventArgs>();
        engine.Tasks.PromiseRejectionTracker += (_, args) => tracked.Add(args);

        engine.Execute("var p = Promise.reject(new Error('boom'));");

        // The pre-existing event is untouched — adding the sink adds a channel, it does not move one.
        tracked.Should().ContainSingle().Which.Promise.Should().BeSameAs(engine.Evaluate("p"));

        var report = sink.Reports.Should().ContainSingle().Which;
        report.Kind.Should().Be(DiagnosticEventKind.UnhandledPromiseRejection);
        report.RejectionHandled.Should().BeFalse();
        report.Promise.Should().BeSameAs(engine.Evaluate("p"));

        // Attaching a handler afterwards is the second half of HostPromiseRejectionTracker, told apart by the
        // flag rather than by a separate kind.
        engine.Execute("p.catch(() => {});");
        sink.Reports.Should().HaveCount(2);
        sink.Reports[1].RejectionHandled.Should().BeTrue();
    }

    [Test]
    public void ASinkAloneArmsTheChannelWithoutInstallingAnyGlobal()
    {
        // A host that wants to hear about unhandled rejections and nothing else does not have to take on any
        // web API to get it.
        var sink = new RecordingSink();
        var engine = new Engine(options => options.WebApi.Diagnostics.Sink = sink);

        engine.Evaluate("typeof console").AsString().Should().Be("undefined");
        engine.Evaluate("typeof DOMException").AsString().Should().Be("undefined");
        engine.Evaluate("typeof reportError").AsString().Should().Be("undefined");

        engine.Execute("Promise.reject('boom');");

        sink.Reports.Should().ContainSingle().Which.Value.AsString().Should().Be("boom");
    }

    [Test]
    public void OneSinkServesEveryEngineBuiltFromSharedOptions()
    {
        // Options are meant to be shared; nothing about the sink is engine-affine, which is why the
        // thread-safety obligation is documented on DiagnosticsSink rather than designed away.
        var sink = new RecordingSink();
        var options = new Options().UseDiagnostics(sink);

        new Engine(options).Execute("reportError('first');");
        new Engine(options).Execute("reportError('second');");

        sink.Reports.Should().HaveCount(2);
        sink.Reports[0].Value.AsString().Should().Be("first");
        sink.Reports[1].Value.AsString().Should().Be("second");
    }

    [Test]
    public void AShadowRealmGetsNoReportError()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("new ShadowRealm().evaluate('typeof reportError')").AsString().Should().Be("undefined");
    }
}
#endif
