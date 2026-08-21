#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="WebApiFeatures.GlobalEvents"/> from the outside: what a host has to name to get it, what its
/// globals are, and — the part a host actually has to be able to rely on — that a script registering an
/// <c>error</c> listener can neither silence the host's <see cref="DiagnosticsSink"/> nor swallow an execution
/// constraint.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party exactly as
/// written.
/// </remarks>
public class HostGlobalErrorEventTests
{
    private sealed class RecordingSink : DiagnosticsSink
    {
        internal List<DiagnosticEvent> Reports { get; } = new();

        public override void Report(DiagnosticEvent report) => Reports.Add(report);
    }

    /// <summary>A clock that only moves when the test moves it, so nothing here waits on wall time.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    /// <summary>
    /// The feature is part of <see cref="WebApiFeatures.Default"/>, so <c>UseWebApis()</c> is enough — it is a
    /// non-network, non-persistent API and the standing exceptions to <c>Default</c> are exactly the two that
    /// are not.
    /// </summary>
    [Fact]
    public void UseWebApisEnablesTheFeature()
    {
        WebApiFeatures.Default.HasFlag(WebApiFeatures.GlobalEvents).Should().BeTrue();

        using var engine = new Engine(options => options.UseWebApis());

        foreach (var name in new[] { "addEventListener", "removeEventListener", "dispatchEvent", "self", "ErrorEvent", "PromiseRejectionEvent" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().NotBe("undefined");
        }
    }

    /// <summary>
    /// Naming the flag on its own is enough: it brings <see cref="WebApiFeatures.Events"/> with it, because
    /// what its operations register on is an <c>EventTarget</c> and what they dispatch is an <c>Event</c>.
    /// </summary>
    [Fact]
    public void NamingTheFlagAloneBringsTheEventMachinery()
    {
        using var engine = new Engine(options => options.UseWebApis(WebApiFeatures.GlobalEvents));

        engine.Evaluate("typeof Event").AsString().Should().Be("function");
        engine.Evaluate("typeof EventTarget").AsString().Should().Be("function");
        engine.Evaluate("new ErrorEvent('error') instanceof Event").AsBoolean().Should().BeTrue();

        // The closure is computed when the engine is built, so the options still read back what was asked for.
        var options = new Options();
        options.UseWebApis(WebApiFeatures.GlobalEvents);
        options.WebApi.Features.Should().Be(WebApiFeatures.GlobalEvents);
    }

    /// <summary>
    /// <c>self</c> is the global object, and the global object is deliberately still not an
    /// <c>EventTarget</c>: the listener list lives beside the engine's timers, not on the object a host has
    /// been handed through <c>engine.Realm.GlobalObject</c>.
    /// </summary>
    [Fact]
    public void SelfIsGlobalThisAndTheGlobalIsNotAnEventTarget()
    {
        using var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("self === globalThis").AsBoolean().Should().BeTrue();
        engine.Evaluate("self").Should().BeSameAs(engine.Evaluate("globalThis"));
        engine.Evaluate("globalThis instanceof EventTarget").AsBoolean().Should().BeFalse();

        // And a host's own global of that name still wins: the install is non-clobbering.
        using var hosted = new Engine(options => options
            .Configure(e => e.SetValue("self", "mine"))
            .UseWebApis());

        hosted.Evaluate("self").AsString().Should().Be("mine");
    }

    /// <summary>
    /// The locked rule: the events <b>feed</b> the sink, they never replace it. HTML lets a listener's
    /// <c>preventDefault()</c> suppress the console report; a host's diagnostics channel is not something the
    /// script it is running may switch off, so the report happens anyway.
    /// </summary>
    [Fact]
    public void AListenerCannotSilenceTheSink()
    {
        var sink = new RecordingSink();
        var clock = new ManualClock();

        using var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Timers.TimeProvider = clock;
            webApi.Diagnostics.Sink = sink;
        }));

        engine.Execute("""
            var canceled = 0;
            addEventListener('error', function (e) { e.preventDefault(); canceled++; });
            addEventListener('unhandledrejection', function (e) { e.preventDefault(); canceled++; });
            reportError(new Error('reported'));
            Promise.reject(new Error('rejected'));
            setTimeout(function () { throw new Error('timed'); }, 1);
            """);

        clock.Advance(5);
        engine.Advanced.ProcessTasks();

        engine.Evaluate("canceled").AsNumber().Should().Be(3);

        sink.Reports.Select(r => r.Kind).Should().Equal(
            DiagnosticEventKind.ReportedError,
            DiagnosticEventKind.UnhandledPromiseRejection,
            DiagnosticEventKind.UncaughtCallbackError);
    }

    /// <summary>
    /// A global <c>error</c> listener must not become a way for script to observe — let alone continue past —
    /// the failures that exist to bound execution. Only a <see cref="JavaScriptException"/> is ever dispatched
    /// or reported.
    /// </summary>
    [Fact]
    public void AListenerNeverSeesAnExecutionConstraintFailure()
    {
        var sink = new RecordingSink();
        var clock = new ManualClock();

        using var engine = new Engine(options =>
        {
            options.UseWebApis(webApi =>
            {
                webApi.Timers.TimeProvider = clock;
                webApi.Diagnostics.Sink = sink;
            });
            options.LimitRecursion(8);
        });

        engine.Execute("""
            var seen = 0;
            addEventListener('error', function () { seen++; });
            function recurse() { return recurse(); }
            setTimeout(recurse, 1);
            """);

        clock.Advance(5);
        Assert.Throws<RecursionDepthOverflowException>(() => engine.Advanced.ProcessTasks());

        engine.Evaluate("seen").AsNumber().Should().Be(0);
        sink.Reports.Should().BeEmpty();
    }

    /// <summary>
    /// The other half of the sink contract, unchanged by this feature: with no sink an uncaught callback
    /// failure is not <i>reported</i> at all, it erupts — and since firing the <c>error</c> event is a step of
    /// reporting, a listener sees nothing either. A host that wants script to observe these installs a sink.
    /// </summary>
    [Fact]
    public void WithoutASinkAnUncaughtCallbackErrorStillErupts()
    {
        var clock = new ManualClock();
        using var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));

        engine.Execute("""
            var seen = 0;
            addEventListener('error', function () { seen++; });
            setTimeout(function () { throw new Error('boom'); }, 1);
            """);

        clock.Advance(5);
        Assert.Throws<JavaScriptException>(() => engine.Advanced.ProcessTasks()).Message.Should().Be("boom");

        engine.Evaluate("seen").AsNumber().Should().Be(0);

        // reportError is the exception, because the call itself is the request to report.
        engine.Execute("reportError(new Error('asked for it'));");
        engine.Evaluate("seen").AsNumber().Should().Be(1);
    }

    /// <summary>
    /// An engine that named no web API is byte-for-byte the engine it was before this feature existed.
    /// </summary>
    [Fact]
    public void ADefaultEngineIsUnchanged()
    {
        using var engine = new Engine();

        foreach (var name in new[] { "addEventListener", "removeEventListener", "dispatchEvent", "self", "ErrorEvent", "PromiseRejectionEvent" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("undefined");
        }
    }
}
#endif
