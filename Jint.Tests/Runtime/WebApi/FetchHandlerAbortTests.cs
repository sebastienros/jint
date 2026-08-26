#if NET8_0_OR_GREATER
#nullable enable

using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Abort;
using Jint.WebApi.Fetch;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The host's own "this request has been abandoned" token reaching the script as <c>request.signal</c> —
/// <c>Engine.WebApi.InvokeFetchHandler(request, requestAborted)</c> and the token
/// <c>InvokeFetchHandlerAsync</c> already took.
/// </summary>
/// <remarks>
/// This file is inside the assembly, so it pins the two halves a host cannot see: the signal's internal
/// <see cref="CancellationToken"/>, which is the handle an outbound <c>fetch</c> links its HTTP request
/// against, and the engine's registration list, which is what a pooled engine must not let grow. The
/// host-visible story is pinned from outside in
/// <c>Jint.Tests.PublicInterface.WebApiFetchHandlerAbortTests</c>.
/// </remarks>
public class FetchHandlerAbortTests
{
    private const WebApiFeatures ModelFeatures = WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files;

    private static Engine Handler(string source, WebApiFeatures extra = WebApiFeatures.None)
    {
        var engine = new Engine(options => options.UseWebApis(ModelFeatures | extra));
        engine.Execute(source);
        engine.WebApi.SetFetchHandler(engine.GetValue("handler"));
        return engine;
    }

    private static HttpRequestMessage Get() => new(HttpMethod.Get, "https://example.org/");

    private static string Body(HttpResponseMessage response)
    {
        using var stream = response.Content.ReadAsStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void Pump(Engine engine, FetchHandlerOperation operation)
    {
        for (var i = 0; i < 100 && !operation.IsCompleted; i++)
        {
            engine.Tasks.ProcessTasks();
        }
    }

    /// <summary>
    /// The whole point of the feature: the client goes away, the token fires, and on the next pump the
    /// handler's own <c>request.signal</c> aborts with the standard's default reason — an <c>AbortError</c>
    /// <c>DOMException</c>, exactly what a script-side <c>controller.abort()</c> produces.
    /// </summary>
    [Test]
    public void TheHostTokenAbortsTheRequestSignalOnTheNextPump()
    {
        var engine = Handler("""
            globalThis.handler = request => new Promise(resolve => {
                globalThis.signal = request.signal;
                request.signal.addEventListener('abort', () => {
                    resolve(new Response([
                        request.signal.aborted,
                        request.signal.reason.constructor.name,
                        request.signal.reason.name,
                    ].join(',')));
                });
            });
            """);

        using var cts = new CancellationTokenSource();
        var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);

        var signal = (JsAbortSignal) engine.GetValue("signal");

        // Taken before the abort, which is what an outbound fetch chained on request.signal does when it
        // starts its request.
        var clrToken = signal.CancellationToken;

        operation.IsCompleted.Should().BeFalse();

        cts.Cancel();

        // Cancelling only enqueued a job: aborting a signal dispatches a JavaScript event, and script runs on
        // the engine's thread and on no other.
        signal.Aborted.Should().BeFalse();
        clrToken.IsCancellationRequested.Should().BeFalse();
        operation.IsCompleted.Should().BeFalse();

        Pump(engine, operation);

        signal.Aborted.Should().BeTrue();

        // The abort algorithms run before the abort event, so the CLR-side handle an in-flight fetch holds is
        // already cancelled by the time any listener runs.
        clrToken.IsCancellationRequested.Should().BeTrue();
        using var response = operation.GetResult();
        Body(response).Should().Be("true,DOMException,AbortError");
    }

    /// <summary>
    /// A token that is already cancelled when the invocation starts gives the handler a request whose signal
    /// is aborted from its very first statement — https://dom.spec.whatwg.org/#abortsignal-aborted is a flag
    /// a signal may be created with, and it is what lets a handler refuse without doing any work.
    /// </summary>
    [Test]
    public void AnAlreadyCancelledTokenIsSeenBeforeTheHandlerRunsAStatement()
    {
        var engine = Handler("""
            globalThis.handler = request => {
                globalThis.signal = request.signal;
                return new Response([request.signal.aborted, request.signal.reason.name].join(','));
            };
            """);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);

        // Synchronous: no job and no pump, because building the request already runs on the engine's thread.
        operation.IsCompleted.Should().BeTrue();
        using var response = operation.GetResult();
        Body(response).Should().Be("true,AbortError");

        var signal = (JsAbortSignal) engine.GetValue("signal");
        signal.CancellationToken.IsCancellationRequested.Should().BeTrue();

        // Nothing was registered on the host's token, because there is nothing left to wait for.
        engine._webApi!.HostAbortBridgeCount.Should().Be(0);
    }

    /// <summary>
    /// The <c>FetchEvent</c> route carries the same signal, so a Workers-shaped script reading
    /// <c>event.request.signal</c> is told the same truth the handler route is told.
    /// </summary>
    [Test]
    public void TheFetchEventRouteCarriesTheSameSignal()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.FetchEvents));
        engine.Execute("""
            addEventListener('fetch', event => {
                globalThis.signal = event.request.signal;
                event.respondWith(new Promise(resolve => {
                    event.request.signal.addEventListener('abort', () => {
                        resolve(new Response('aborted:' + event.request.signal.reason.name));
                    });
                }));
            });
            """);

        using var cts = new CancellationTokenSource();
        var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);

        var signal = (JsAbortSignal) engine.GetValue("signal");
        signal.Aborted.Should().BeFalse();

        cts.Cancel();
        Pump(engine, operation);

        signal.Aborted.Should().BeTrue();
        using var response = operation.GetResult();
        Body(response).Should().Be("aborted:AbortError");
    }

    /// <summary>
    /// The retention pin that matters. A pooled engine serving request after request from one long-lived host
    /// token — an application lifetime's, or a token source the host reuses — must accumulate no registrations
    /// and be retained by none of them, so every invocation gives its registration back the moment it ends.
    /// </summary>
    [Test]
    public void ALiveHostTokenAccumulatesNoRegistrationsAcrossInvocations()
    {
        var engine = Handler("globalThis.handler = () => new Response('ok');");

        // Never cancelled, and never disposed until the loop is over: exactly the shape that would let a
        // registration outlive its invocation if nothing released it.
        using var cts = new CancellationTokenSource();

        for (var i = 0; i < 25; i++)
        {
            var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);
            operation.IsCompleted.Should().BeTrue();
            using var response = operation.GetResult();
            Body(response).Should().Be("ok");

            engine._webApi!.HostAbortBridgeCount.Should().Be(0, "invocation {0} must have given its registration back", i);
        }
    }

    /// <summary>
    /// The same, for an invocation that fails rather than answering: the registration is about the invocation
    /// being over, not about it having succeeded.
    /// </summary>
    [Test]
    public void AFailingInvocationGivesItsRegistrationBackToo()
    {
        var engine = Handler("globalThis.handler = () => { throw new Error('nope'); };");

        using var cts = new CancellationTokenSource();

        var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);

        operation.IsFaulted.Should().BeTrue();
        engine._webApi!.HostAbortBridgeCount.Should().Be(0);
    }

    /// <summary>
    /// The awaitable shape owns no operation to hang the release off, so it releases in its own
    /// <c>finally</c> — and must, for exactly the reason the polled shape must.
    /// </summary>
    [Test]
    public async Task TheAwaitableShapeGivesItsRegistrationBackToo()
    {
        var engine = Handler("globalThis.handler = () => new Response('ok');");

        using var cts = new CancellationTokenSource();

        for (var i = 0; i < 5; i++)
        {
            using var response = await engine.WebApi.InvokeFetchHandlerAsync(Get(), cts.Token);
            Body(response).Should().Be("ok");

            engine._webApi!.HostAbortBridgeCount.Should().Be(0, "await {0} must have given its registration back", i);
        }

        // The same when the invocation fails rather than answering.
        engine.WebApi.SetFetchHandler(engine.Evaluate("() => { throw new Error('nope'); }"));
        Assert.ThrowsAsync<JavaScriptException>(() => engine.WebApi.InvokeFetchHandlerAsync(Get(), cts.Token));
        engine._webApi!.HostAbortBridgeCount.Should().Be(0);
    }

    /// <summary>
    /// Cancelling mid-flight through the awaitable shape: the same token ends the <c>await</c> and aborts the
    /// signal, and the two are not ordered against each other — so the only thing this can assert about the
    /// call is that it ends cancelled. What it does assert about the engine is the part that <b>is</b>
    /// deterministic, and that the release deliberately does not undo: an abort already on the event loop
    /// still lands on the next pump, which is what cancels the outbound work the abandoned handler started.
    /// </summary>
    [Test]
    public async Task AnAbortAlreadyEnqueuedStillLandsAfterTheAwaitGaveUp()
    {
        var engine = Handler("""
            globalThis.handler = request => new Promise(() => { globalThis.signal = request.signal; });
            """);

        using var cts = new CancellationTokenSource();

        // Nothing may touch the engine while the asynchronous host operation is in flight, so everything this
        // reads it for happens after the await has ended.
        var pending = engine.WebApi.InvokeFetchHandlerAsync(Get(), cts.Token);

        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => pending);

        // The registration went back when the call returned, however it returned.
        engine._webApi!.HostAbortBridgeCount.Should().Be(0);

        // ... and the abort itself was not swallowed with it. It may already have landed inside the await —
        // the two halves of the token race — but it must have landed by the time a pump has run.
        engine.Tasks.ProcessTasks();
        ((JsAbortSignal) engine.GetValue("signal")).Aborted.Should().BeTrue();
    }

    /// <summary>
    /// While an invocation is genuinely in flight the registration is held — which is what makes the counts
    /// above mean something rather than being satisfied by never registering at all.
    /// </summary>
    [Test]
    public void ARegistrationIsHeldWhileTheInvocationIsInFlight()
    {
        var engine = Handler("globalThis.handler = () => new Promise(() => {});");

        using var cts = new CancellationTokenSource();
        var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);

        operation.IsCompleted.Should().BeFalse();
        engine._webApi!.HostAbortBridgeCount.Should().Be(1);
    }

    /// <summary>
    /// A restore ends the evaluation cycle the invocation belonged to, and an abort enqueued before it is
    /// discarded at dequeue by the generation fence rather than aborting a signal whose cycle is over.
    /// </summary>
    [Test]
    public void ARestoreFencesOffAnAbortThatWasAlreadyEnqueued()
    {
        var engine = Handler("""
            globalThis.handler = request => new Promise(() => { globalThis.signal = request.signal; });
            """);
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        using var cts = new CancellationTokenSource();
        var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);
        var signal = (JsAbortSignal) engine.GetValue("signal");

        // Enqueued, then fenced: the job carries the cycle's generation and the restore moves the engine past
        // it.
        cts.Cancel();
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Tasks.ProcessTasks();

        signal.Aborted.Should().BeFalse();
        operation.IsFaulted.Should().BeTrue();

        // The restore released every host bridge with the rest of the cycle's state.
        engine._webApi!.HostAbortBridgeCount.Should().Be(0);
    }

    /// <summary>
    /// The single-argument overload is unchanged: it registers nothing, and its request's signal is the one
    /// that can never fire.
    /// </summary>
    [Test]
    public void TheOverloadWithoutATokenRegistersNothing()
    {
        var engine = Handler("""
            globalThis.handler = request => new Promise(() => { globalThis.signal = request.signal; });
            """);

        engine.WebApi.InvokeFetchHandler(Get());

        engine._webApi!.HostAbortBridgeCount.Should().Be(0);
        ((JsAbortSignal) engine.GetValue("signal")).Aborted.Should().BeFalse();

        // ... and so does an explicitly passed token that can never be cancelled.
        engine.WebApi.InvokeFetchHandler(Get(), CancellationToken.None);
        engine._webApi!.HostAbortBridgeCount.Should().Be(0);
    }

    /// <summary>
    /// Two invocations get two signals: the signal is per invocation, not per engine, so one request's abort
    /// cannot reach another request's handler on a pooled engine.
    /// </summary>
    [Test]
    public void EachInvocationGetsItsOwnSignal()
    {
        var engine = Handler("""
            globalThis.signals = [];
            globalThis.handler = request => new Promise(() => { globalThis.signals.push(request.signal); });
            """);

        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();

        engine.WebApi.InvokeFetchHandler(Get(), first.Token);
        engine.WebApi.InvokeFetchHandler(Get(), second.Token);

        engine.Evaluate("signals.length").AsNumber().Should().Be(2);
        engine.Evaluate("signals[0] === signals[1]").Should().Be(JsBoolean.False);

        first.Cancel();
        engine.Tasks.ProcessTasks();

        engine.Evaluate("[signals[0].aborted, signals[1].aborted].join(',')").Should().Be(new JsString("true,false"));
    }
}
#endif
