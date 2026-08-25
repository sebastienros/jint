#if NET8_0_OR_GREATER
#nullable enable

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Runtime;
using Jint.WebApi;
using Jint.WebApi.Fetch;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The host half of inbound cancellation: the token an ASP.NET Core host already holds —
/// <c>HttpContext.RequestAborted</c> — reaching the running handler as <c>request.signal</c>.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything asserted here is something a third-party host
/// can see for itself: the response it is served, the exception it is handed, and what a script can still
/// observe afterwards. The in-assembly half — the signal's CLR token, and the engine's registration list —
/// is pinned in <c>Jint.Tests.Runtime.WebApi.FetchHandlerAbortTests</c>.
/// </remarks>
public class WebApiFetchHandlerAbortTests
{
    private const WebApiFeatures ModelFeatures = WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files;

    private static Engine Handler(string source, WebApiFeatures extra = WebApiFeatures.None)
    {
        var engine = new Engine(options => options.UseWebApis(ModelFeatures | extra));
        engine.Execute(source);
        engine.WebApi.SetFetchHandler(engine.GetValue("handler"));
        return engine;
    }

    private static HttpRequestMessage Get() => new(HttpMethod.Get, "https://example.org/hello");

    private static string Text(HttpResponseMessage response)
    {
        using var stream = response.Content.ReadAsStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Gives the engine turns until the operation finishes, and fails rather than spinning forever.</summary>
    private static void Pump(Engine engine, FetchHandlerOperation operation)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!operation.IsCompleted)
        {
            engine.Tasks.ProcessTasks();
            if (operation.IsCompleted)
            {
                break;
            }

            if (stopwatch.Elapsed > TimeSpan.FromSeconds(10))
            {
                Assert.Fail("The fetch handler never completed.");
            }

            Thread.Sleep(1);
        }
    }

    /// <summary>
    /// The shape a host writes: the client goes away, the token fires, and the handler — which was waiting on
    /// its own <c>request.signal</c> — answers gracefully on the next turn the host pumps.
    /// </summary>
    [Fact]
    public void TheHostTokenReachesTheHandlerAsRequestSignal()
    {
        var engine = Handler("""
            globalThis.handler = request => new Promise(resolve => {
                request.signal.addEventListener('abort', () => {
                    resolve(new Response('abandoned: ' + request.signal.reason.name, { status: 499 }));
                });
            });
            """);

        using var cts = new CancellationTokenSource();
        var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);
        operation.IsCompleted.Should().BeFalse();

        cts.Cancel();

        // Nothing happened on the cancelling thread: aborting a signal dispatches a JavaScript event, and the
        // engine runs script only where the host pumps it.
        operation.IsCompleted.Should().BeFalse();

        Pump(engine, operation);

        using var response = operation.GetResult();
        ((int) response.StatusCode).Should().Be(499);

        // The standard's default reason, exactly as a script-side controller.abort() produces.
        Text(response).Should().Be("abandoned: AbortError");
    }

    /// <summary>
    /// The abort is <b>observational</b>: it tells the script, and settles nothing by itself. A handler that
    /// pays no attention and answers anyway is served, because the host that went on pumping is the one that
    /// decided to let it finish — stopping an invocation outright is the host's own lever, not the script's.
    /// </summary>
    [Fact]
    public void AHandlerThatIgnoresTheAbortStillAnswers()
    {
        var engine = Handler(
            """
            globalThis.handler = request => new Promise(resolve => {
                globalThis.signal = request.signal;
                setTimeout(() => resolve(new Response('served anyway')), 0);
            });
            """,
            WebApiFeatures.Timers);

        using var cts = new CancellationTokenSource();
        var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);

        cts.Cancel();
        Pump(engine, operation);

        operation.IsFaulted.Should().BeFalse();
        using var response = operation.GetResult();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Text(response).Should().Be("served anyway");

        // ... and the signal really did abort. The response was served in spite of it, not because it never
        // arrived.
        engine.Evaluate("signal.aborted").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// A handler that rejects on the abort — which is what an outbound <c>fetch</c> chained on
    /// <c>request.signal</c> does for it — fails the invocation through the ordinary failure contract, with
    /// no special case anywhere for cancellation.
    /// </summary>
    [Fact]
    public void AHandlerThatRejectsOnTheAbortFailsTheOperationTheOrdinaryWay()
    {
        var engine = Handler("""
            globalThis.handler = request => new Promise((resolve, reject) => {
                request.signal.addEventListener('abort', () => reject('gone: ' + request.signal.reason.name));
            });
            """);

        using var cts = new CancellationTokenSource();
        var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);

        cts.Cancel();
        Pump(engine, operation);

        operation.IsFaulted.Should().BeTrue();
        var failure = Assert.IsType<PromiseRejectedException>(operation.Error);
        failure.RejectedValue.AsString().Should().Be("gone: AbortError");

        // A host maps that to the wire itself, exactly as it maps every other handler failure — Jint never
        // turns one into a status code.
        Assert.IsAssignableFrom<JintException>(operation.Error);
    }

    /// <summary>
    /// A token that is already cancelled when the invocation starts gives the handler a request whose signal
    /// is aborted from its first statement, so it can refuse without doing any work — and it needs no pump,
    /// because building the request already runs on the engine's thread.
    /// </summary>
    [Fact]
    public void AnAlreadyCancelledTokenGivesTheHandlerAnAlreadyAbortedSignal()
    {
        var engine = Handler("""
            globalThis.handler = request => request.signal.aborted
                ? new Response(request.signal.reason.name, { status: 499 })
                : new Response('did work');
            """);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);

        operation.IsCompleted.Should().BeTrue();
        using var response = operation.GetResult();
        ((int) response.StatusCode).Should().Be(499);
        Text(response).Should().Be("AbortError");
    }

    /// <summary>
    /// The lifetime contract, from the only side a host can see it: once an invocation is over its
    /// registration is gone, so a token cancelled afterwards reaches nothing. A pooled engine serving request
    /// after request from one long-lived token therefore accumulates nothing.
    /// </summary>
    [Fact]
    public void AnInvocationThatCompletedNoLongerListensToTheHostToken()
    {
        var engine = Handler("""
            globalThis.seen = [];
            globalThis.handler = request => {
                globalThis.signal = request.signal;
                request.signal.addEventListener('abort', () => { globalThis.seen.push('late'); });
                return new Response('ok');
            };
            """);

        using var cts = new CancellationTokenSource();

        for (var i = 0; i < 10; i++)
        {
            var served = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);
            served.IsCompleted.Should().BeTrue();
            using var response = served.GetResult();
            Text(response).Should().Be("ok");
        }

        // The requests are all over. Cancelling now must reach none of their signals.
        cts.Cancel();
        engine.Tasks.ProcessTasks();

        engine.Evaluate("signal.aborted").AsBoolean().Should().BeFalse();
        engine.Evaluate("seen.length").AsNumber().Should().Be(0);
    }

    /// <summary>
    /// A restore ends the cycle the invocation belonged to. The operation reports itself abandoned, as it
    /// already did, and an abort enqueued before the restore lands on nothing — the fence discards it exactly
    /// as it discards the reaction that would have completed the operation.
    /// </summary>
    [Fact]
    public void ARestoreAbandonsTheInvocationAndNoLateAbortLands()
    {
        var engine = Handler("""
            globalThis.seen = [];
            globalThis.handler = request => new Promise(() => {
                globalThis.signal = request.signal;
                request.signal.addEventListener('abort', () => { globalThis.seen.push('late'); });
            });
            """);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        using var cts = new CancellationTokenSource();
        var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);

        // Held here because the restore takes the global away — the object survives, and whether it aborted
        // is exactly the question.
        var signal = engine.Evaluate("signal");

        cts.Cancel();
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        engine.Tasks.ProcessTasks();

        operation.IsFaulted.Should().BeTrue();
        Assert.IsType<InvalidOperationException>(operation.Error).Message.Should().Contain("abandoned");

        engine.SetValue("survivor", signal);
        engine.Evaluate("survivor.aborted").AsBoolean().Should().BeFalse();
    }

    /// <summary>
    /// The awaitable shape's existing token now also feeds the signal. An already-cancelled one is the
    /// deterministic half of that: the body read has nothing to read, the handler is called with an aborted
    /// signal, and its synchronous answer never reaches the await's cancellation check.
    /// </summary>
    [Fact]
    public async Task TheAwaitableShapeGivesAnAlreadyCancelledTokenTheSameAbortedSignal()
    {
        var engine = Handler("""
            globalThis.handler = request => new Response(
                request.signal.aborted ? request.signal.reason.name : 'live',
                { status: request.signal.aborted ? 499 : 200 });
            """);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var response = await engine.WebApi.InvokeFetchHandlerAsync(Get(), cts.Token);

        ((int) response.StatusCode).Should().Be(499);
        Text(response).Should().Be("AbortError");
    }

    /// <summary>
    /// And a live token that is never cancelled changes nothing about the awaitable shape, which is what the
    /// hosts already using it are entitled to.
    /// </summary>
    [Fact]
    public async Task TheAwaitableShapeIsUnchangedForATokenThatNeverFires()
    {
        var engine = Handler("globalThis.handler = request => new Response('aborted=' + request.signal.aborted);");

        using var cts = new CancellationTokenSource();

        using var response = await engine.WebApi.InvokeFetchHandlerAsync(Get(), cts.Token);

        Text(response).Should().Be("aborted=false");

        using var none = await engine.WebApi.InvokeFetchHandlerAsync(Get());
        Text(none).Should().Be("aborted=false");
    }

    /// <summary>
    /// The script-facing route reads the same truth: a Workers-shaped listener sees
    /// <c>event.request.signal</c> abort.
    /// </summary>
    [Fact]
    public void TheScriptRegisteredListenerRouteCarriesTheSignalToo()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.FetchEvents));
        engine.Execute("""
            addEventListener('fetch', event => {
                event.respondWith(new Promise(resolve => {
                    event.request.signal.addEventListener('abort', () => {
                        resolve(new Response('listener saw ' + event.request.signal.reason.name));
                    });
                }));
            });
            """);

        using var cts = new CancellationTokenSource();
        var operation = engine.WebApi.InvokeFetchHandler(Get(), cts.Token);

        cts.Cancel();
        Pump(engine, operation);

        using var response = operation.GetResult();
        Text(response).Should().Be("listener saw AbortError");
    }

    /// <summary>
    /// The single-argument overload is untouched: its request's signal is still the one that can never fire,
    /// so a host that never passes a token sees exactly the engine it saw before.
    /// </summary>
    [Fact]
    public void TheOverloadWithoutATokenStillHandsOverASignalThatNeverFires()
    {
        var engine = Handler("""
            globalThis.handler = request => new Response([
                request.signal instanceof AbortSignal,
                request.signal.aborted,
            ].join(','));
            """);

        var operation = engine.WebApi.InvokeFetchHandler(Get());
        using var response = operation.GetResult();
        Text(response).Should().Be("true,false");

        // The same for an explicitly passed token that can never be cancelled.
        var explicitNone = engine.WebApi.InvokeFetchHandler(Get(), CancellationToken.None);
        using var second = explicitNone.GetResult();
        Text(second).Should().Be("true,false");
    }

    /// <summary>
    /// The argument checks are the ones the other overload makes, in the same order: a host's own mistake is
    /// thrown from the call rather than delivered through the operation.
    /// </summary>
    [Fact]
    public void ArgumentFailuresAreStillTheHostsOwn()
    {
        var engine = Handler("globalThis.handler = () => new Response('x');");

        Assert.Throws<ArgumentNullException>(() => engine.WebApi.InvokeFetchHandler(null!, CancellationToken.None));

        var bare = new Engine(options => options.UseWebApis(ModelFeatures));
        Assert.Throws<InvalidOperationException>(() => bare.WebApi.InvokeFetchHandler(Get(), CancellationToken.None));
    }
}
#endif
