#if NET8_0_OR_GREATER
#nullable enable

using System.Threading;
using Jint.Native;
using Jint.WebApi.Abort;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>Engine.WebApi.CreateAbortSignal(CancellationToken)</c> — the bridge from the host's own cancellation to
/// the <c>AbortSignal</c> script consumes.
/// </summary>
/// <remarks>
/// This file is inside the assembly, so it can check the half a host cannot see: the signal's internal
/// <see cref="CancellationToken"/>, which is what a CLR-side operation such as <c>fetch</c> links its HTTP
/// request against. The host-visible half — that nothing ever runs on the cancelling thread — is pinned from
/// outside in <c>Jint.Tests.PublicInterface.WebApiSchedulingSurfaceTests</c>.
/// </remarks>
public class HostAbortSignalTests
{
    private static Engine EventsEngine() => new(options => options.UseWebApis(WebApiFeatures.Events));

    /// <summary>
    /// A token that is already cancelled produces an already-aborted signal, right here and with no pump — the
    /// creation runs on the engine's thread, which is the only thread that may abort a signal, so there is
    /// nothing to defer.
    /// </summary>
    [Test]
    public void AnAlreadyCancelledTokenYieldsAnAlreadyAbortedSignal()
    {
        using var engine = EventsEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var signal = (JsAbortSignal) engine.WebApi.CreateAbortSignal(cts.Token);

        signal.Aborted.Should().BeTrue();

        // The standard's default reason, exactly as AbortSignal.abort() with no argument produces.
        engine.SetValue("s", signal);
        engine.Evaluate("s.reason.constructor.name").Should().Be(new JsString("DOMException"));
        engine.Evaluate("s.reason.name").Should().Be(new JsString("AbortError"));

        // And the handle a fetch would link against is already cancelled, so such an operation refuses without
        // ever issuing a request.
        signal.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }

    /// <summary>
    /// A live token aborts nothing until the engine is pumped, and then aborts everything at once: the abort
    /// algorithms — the engine's own CLR-side cancellation among them — run before the <c>abort</c> event, so
    /// an in-flight CLR operation is stopped before any script observes the abort.
    /// </summary>
    [Test]
    public void CancellationAbortsTheSignalAndItsClrTokenOnTheNextPump()
    {
        using var engine = EventsEngine();
        using var cts = new CancellationTokenSource();

        var signal = (JsAbortSignal) engine.WebApi.CreateAbortSignal(cts.Token);

        // Taken before the abort, which is what a fetch does when it starts a request.
        var clrToken = signal.CancellationToken;

        engine.SetValue("s", signal);
        engine.Execute("var seen = null; s.addEventListener('abort', () => { seen = s.reason.name; });");

        cts.Cancel();

        // Nothing has run: cancelling only enqueued a job.
        signal.Aborted.Should().BeFalse();
        clrToken.IsCancellationRequested.Should().BeFalse();

        engine.Tasks.ProcessTasks();

        signal.Aborted.Should().BeTrue();
        clrToken.IsCancellationRequested.Should().BeTrue();
        engine.Evaluate("seen").Should().Be(new JsString("AbortError"));
    }

    /// <summary>
    /// A token that can never be cancelled — <see cref="CancellationToken.None"/>, or a struct default — still
    /// produces a usable signal; it simply never aborts, and nothing is registered on the host's side.
    /// </summary>
    [Test]
    public void ATokenThatCanNeverBeCancelledYieldsASignalThatNeverAborts()
    {
        using var engine = EventsEngine();

        var signal = (JsAbortSignal) engine.WebApi.CreateAbortSignal(CancellationToken.None);

        signal.Aborted.Should().BeFalse();

        engine.SetValue("s", signal);
        engine.Evaluate("s instanceof AbortSignal").Should().Be(JsBoolean.True);

        engine.Tasks.ProcessTasks();
        signal.Aborted.Should().BeFalse();
    }

    /// <summary>
    /// The signal belongs to the principal realm's <c>AbortSignal</c>, so everything script does with it — the
    /// <c>instanceof</c>, the listeners, handing it to an API that takes a signal — works.
    /// </summary>
    [Test]
    public void TheSignalIsAnOrdinaryAbortSignalOfThePrincipalRealm()
    {
        using var engine = EventsEngine();
        using var cts = new CancellationTokenSource();

        engine.SetValue("s", engine.WebApi.CreateAbortSignal(cts.Token));

        engine.Evaluate("Object.getPrototypeOf(s) === AbortSignal.prototype").Should().Be(JsBoolean.True);
        engine.Evaluate("s instanceof EventTarget").Should().Be(JsBoolean.True);
        engine.Evaluate("s.aborted").Should().Be(JsBoolean.False);
        engine.Evaluate("typeof s.throwIfAborted").Should().Be(new JsString("function"));
    }

    /// <summary>
    /// A signal bridged in one evaluation cycle must not abort into the next: a
    /// <c>RestoreGlobalSnapshot</c> ends the cycle, and the bridge is released with everything else that cycle
    /// registered.
    /// </summary>
    [Test]
    public void ARestoreReleasesTheBridge()
    {
        using var engine = EventsEngine();
        using var cts = new CancellationTokenSource();

        var signal = (JsAbortSignal) engine.WebApi.CreateAbortSignal(cts.Token);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        cts.Cancel();
        engine.Tasks.ProcessTasks();

        signal.Aborted.Should().BeFalse();
    }

    /// <summary>
    /// The feature gate: <c>AbortSignal</c> belongs to <see cref="WebApiFeatures.Events"/>, and an engine that
    /// did not ask for it is told which feature to enable rather than silently handed an object whose interface
    /// object no script can name.
    /// </summary>
    [Test]
    public void AnEngineWithoutTheEventsFeatureIsRefused()
    {
        using var engine = new Engine();

        var exception = Assert.Throws<InvalidOperationException>(() => engine.WebApi.CreateAbortSignal(CancellationToken.None))!;
        exception.Message.Should().Contain("WebApiFeatures.Events");

        // Enabling some other feature is not enough either.
        using var consoleOnly = new Engine(options => options.UseWebApis(WebApiFeatures.Console));
        Assert.Throws<InvalidOperationException>(() => consoleOnly.WebApi.CreateAbortSignal(CancellationToken.None));
    }
}
#endif
