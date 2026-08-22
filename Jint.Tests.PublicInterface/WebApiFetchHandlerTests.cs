#if NET8_0_OR_GREATER
#nullable enable

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Constraints;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi;
using Jint.WebApi.Fetch;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Fetch-handler hosting — <c>Engine.Advanced.SetFetchHandler</c> plus the two invoke shapes — seen from
/// outside the assembly, which is the only place it is meant to be used from.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so every type mentioned here is one a third-party host can
/// reach: <see cref="HttpRequestMessage"/> in, <see cref="HttpResponseMessage"/> out, and nothing in between
/// that is not already public.
/// </remarks>
public class WebApiFetchHandlerTests
{
    /// <summary>
    /// The features fetch-handler hosting needs, and deliberately not <see cref="WebApiFeatures.Fetch"/> —
    /// handling an inbound request is not a reason to grant the script outbound network access.
    /// </summary>
    private const WebApiFeatures ModelFeatures = WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files;

    private static Engine Host(WebApiFeatures extra = WebApiFeatures.None, Action<Options>? configure = null)
    {
        return new Engine(options =>
        {
            options.UseWebApis(ModelFeatures | extra);
            configure?.Invoke(options);
        });
    }

    /// <summary>Builds an engine, evaluates <paramref name="source"/> and registers its <c>handler</c> global.</summary>
    private static Engine Handler(string source, WebApiFeatures extra = WebApiFeatures.None, Action<Options>? configure = null)
    {
        var engine = Host(extra, configure);
        engine.Execute(source);
        engine.Advanced.SetFetchHandler(engine.GetValue("handler"));
        return engine;
    }

    private static HttpRequestMessage Get(string url = "https://example.org/hello?q=1") => new(HttpMethod.Get, url);

    /// <summary>
    /// The response body as text. <see cref="HttpContent.ReadAsStream()"/> rather than the asynchronous
    /// overload because every body here is a buffered <c>ByteArrayContent</c>, so there is nothing to wait
    /// for and nothing to deadlock on.
    /// </summary>
    private static string Text(HttpResponseMessage response)
    {
        using var stream = response.Content.ReadAsStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Gives the engine turns until the operation finishes, and fails rather than spinning forever if it
    /// never does.
    /// </summary>
    private static HttpResponseMessage Pump(Engine engine, FetchHandlerOperation operation)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!operation.IsCompleted)
        {
            engine.Advanced.ProcessTasks();
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

        return operation.GetResult();
    }

    [Fact]
    public void AnEngineWithoutTheObjectModelRefusesToRegisterAHandler()
    {
        var engine = new Engine();
        engine.Execute("function handle(request) { return null; }");

        var failure = Assert.Throws<InvalidOperationException>(() => engine.Advanced.SetFetchHandler(engine.GetValue("handle")));
        failure.Message.Should().Contain("WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files");
        failure.Message.Should().Contain("UseFetch");

        // Half the closure is not enough either.
        var partial = new Engine(options => options.UseWebApis(WebApiFeatures.Events | WebApiFeatures.Url));
        partial.Execute("function handle(request) { return null; }");
        Assert.Throws<InvalidOperationException>(() => partial.Advanced.SetFetchHandler(partial.GetValue("handle")));

        // ... and enabling fetch itself satisfies it, because that flag implies all three.
        var fetching = new Engine(options => options.UseFetch());
        fetching.Execute("function handle(request) { return null; }");
        fetching.Advanced.SetFetchHandler(fetching.GetValue("handle"));
        fetching.Advanced.HasFetchHandler.Should().BeTrue();
    }

    [Fact]
    public void RegisteringAHandlerInstallsTheObjectModelButNeverFetch()
    {
        var engine = Host();

        // Before: the three interface objects are not there, because no feature flag named them.
        engine.Evaluate("typeof Response").AsString().Should().Be("undefined");
        engine.Advanced.HasFetchHandler.Should().BeFalse();

        engine.Execute("function handle(request) { return new Response('x'); }");
        engine.Advanced.SetFetchHandler(engine.GetValue("handle"));

        engine.Evaluate("typeof Response").AsString().Should().Be("function");
        engine.Evaluate("typeof Request").AsString().Should().Be("function");
        engine.Evaluate("typeof Headers").AsString().Should().Be("function");
        engine.Advanced.HasFetchHandler.Should().BeTrue();

        // The whole point of the split: an inbound request is not a reason to grant outbound network access.
        engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");

        // WebIDL interface object attributes — https://webidl.spec.whatwg.org/#es-interfaces.
        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'Response')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeFalse();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AGlobalTheHostRegisteredItselfIsNotReplaced()
    {
        var engine = Host(configure: options => options.Configure(e => e.SetValue("Response", "mine")));
        engine.Execute("function handle(request) { return null; }");
        engine.Advanced.SetFetchHandler(engine.GetValue("handle"));

        engine.Evaluate("Response").AsString().Should().Be("mine");
    }

    [Fact]
    public void RoutesASynchronousHandlerAndAnswersWithItsResponse()
    {
        var engine = Handler("globalThis.handler = { fetch(request) { return new Response('hello ' + request.method, { status: 201, statusText: 'Created' }); } };");

        var operation = engine.Advanced.InvokeFetchHandler(Get());

        // A synchronous answer needs no pump at all.
        operation.IsCompleted.Should().BeTrue();
        operation.IsFaulted.Should().BeFalse();

        using var response = operation.GetResult();
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.ReasonPhrase.Should().Be("Created");
        Text(response).Should().Be("hello GET");
    }

    [Fact]
    public void TheRequestTheScriptSeesCarriesTheUrlMethodHeadersAndBody()
    {
        var engine = Handler("""
            globalThis.handler = {
                fetch(request) {
                    return request.text().then(body => new Response(JSON.stringify({
                        url: request.url,
                        method: request.method,
                        accept: request.headers.get('accept'),
                        contentType: request.headers.get('content-type'),
                        isRequest: request instanceof Request,
                        hasSignal: !request.signal.aborted,
                        body,
                    })));
                }
            };
            """);

        var message = new HttpRequestMessage(HttpMethod.Post, "https://example.org/a/../b?q=1#frag")
        {
            Content = new StringContent("{\"n\":1}", Encoding.UTF8, "application/json"),
        };
        message.Headers.Add("Accept", "text/plain");

        var payload = Pump(engine, engine.Advanced.InvokeFetchHandler(message));
        var json = Text(payload);

        // The URL is the WHATWG serialization of the absolute URI, so it is normalized.
        json.Should().Contain("\"url\":\"https://example.org/b?q=1#frag\"");
        json.Should().Contain("\"method\":\"POST\"");
        json.Should().Contain("\"accept\":\"text/plain\"");

        // A content header is part of the one header list the Fetch Standard has, so it has to be visible on
        // request.headers even though System.Net.Http keeps it on the content.
        json.Should().Contain("\"contentType\":\"application/json; charset=utf-8\"");
        json.Should().Contain("\"isRequest\":true");
        json.Should().Contain("\"hasSignal\":true");
        json.Should().Contain("\"body\":\"{\\\"n\\\":1}\"");

        payload.Dispose();
    }

    [Fact]
    public void TheMethodIsNormalizedTheWayTheRequestConstructorNormalizesIt()
    {
        var engine = Handler("globalThis.handler = request => new Response(request.method);");

        // https://fetch.spec.whatwg.org/#concept-method-normalize — the six standard methods only.
        using var normalized = engine.Advanced.InvokeFetchHandler(new HttpRequestMessage(new HttpMethod("post"), "https://example.org/")).GetResult();
        Text(normalized).Should().Be("POST");

        using var untouched = engine.Advanced.InvokeFetchHandler(new HttpRequestMessage(new HttpMethod("patch"), "https://example.org/")).GetResult();
        Text(untouched).Should().Be("patch");
    }

    [Fact]
    public void MultiValuedHeadersSurviveInBothDirections()
    {
        var engine = Handler("""
            globalThis.handler = {
                fetch(request) {
                    const headers = new Headers();
                    headers.append('set-cookie', 'a=1; Path=/');
                    headers.append('set-cookie', 'b=2; Path=/');
                    headers.append('x-seen', request.headers.getSetCookie().join('|'));
                    return new Response(null, { status: 204, headers });
                }
            };
            """);

        var message = Get();
        message.Headers.Add("Set-Cookie", "in1=1");
        message.Headers.Add("Set-Cookie", "in2=2");

        using var response = engine.Advanced.InvokeFetchHandler(message).GetResult();

        // Inbound: two headers of the same name are two entries, not one comma-joined value — which is the
        // whole reason getSetCookie() exists.
        response.Headers.GetValues("x-seen").Single().Should().Be("in1=1|in2=2");

        // Outbound: likewise, because a Set-Cookie value may itself contain a comma.
        response.Headers.GetValues("Set-Cookie").Should().Equal("a=1; Path=/", "b=2; Path=/");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A response with no body and no content header carries no content of its own, which the framework
        // reads back as an empty one rather than as null — so a host copying it never has to special-case it.
        Text(response).Should().BeEmpty();
    }

    [Fact]
    public void AContentHeaderLandsOnTheContentAndAFramingHeaderIsTheHostsOwn()
    {
        var engine = Handler("""
            globalThis.handler = () => new Response('hi', {
                headers: {
                    'content-type': 'text/plain;charset=utf-8',
                    'content-length': '999',
                    'cache-control': 'no-store',
                },
            });
            """);

        using var response = engine.Advanced.InvokeFetchHandler(Get()).GetResult();

        // System.Net.Http splits headers in two and refuses a content header on the response collection; the
        // Fetch Standard has one list, so the conversion has to route each name to the half that accepts it.
        response.Content.Headers.ContentType!.ToString().Should().Be("text/plain; charset=utf-8");
        response.Headers.GetValues("cache-control").Single().Should().Be("no-store");
        response.Headers.Any(header => string.Equals(header.Key, "content-type", StringComparison.OrdinalIgnoreCase)).Should().BeFalse();

        // Content-Length is the host's stack's to compute: a script's claim about a body it is not sending is
        // a response-splitting primitive, so it is dropped and the content answers for itself.
        response.Content.Headers.ContentLength.Should().Be(2);
    }

    [Fact]
    public void AContentHeaderOnABodilessResponseStillReachesTheWire()
    {
        var engine = Handler("globalThis.handler = () => new Response(null, { status: 200, headers: { 'content-type': 'application/json' } });");

        using var response = engine.Advanced.InvokeFetchHandler(Get()).GetResult();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        Text(response).Should().BeEmpty();
    }

    [Fact]
    public void ThePromiseHandlerCompletesOnAPumpedTurn()
    {
        var engine = Handler("globalThis.handler = { async fetch(request) { return new Response('async'); } };");

        var operation = engine.Advanced.InvokeFetchHandler(Get());

        // An async function's answer is a promise, and a promise needs a turn.
        operation.IsCompleted.Should().BeFalse();
        operation.Response.Should().BeNull();

        using var response = Pump(engine, operation);
        Text(response).Should().Be("async");
    }

    [Fact]
    public async Task ThePromiseHandlerCompletesThroughTheAwaitableVariant()
    {
        var engine = Handler("globalThis.handler = { async fetch(request) { return new Response('awaited ' + request.url); } };");

        using var response = await engine.Advanced.InvokeFetchHandlerAsync(Get("https://example.org/x"));
        (await response.Content.ReadAsStringAsync()).Should().Be("awaited https://example.org/x");
    }

    [Fact]
    public async Task TheAwaitableVariantReadsARequestBodyWithoutABufferedContent()
    {
        var engine = Handler("globalThis.handler = { async fetch(request) { return new Response(await request.text()); } };");

        var message = new HttpRequestMessage(HttpMethod.Put, "https://example.org/")
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("streamed"))),
        };

        using var response = await engine.Advanced.InvokeFetchHandlerAsync(message);
        (await response.Content.ReadAsStringAsync()).Should().Be("streamed");
    }

    [Fact]
    public void ATimerInsideTheHandlerRunsWhileTheHostPumps()
    {
        var engine = Handler(
            "globalThis.handler = () => new Promise(resolve => setTimeout(() => resolve(new Response('late')), 5));",
            WebApiFeatures.Timers);

        var operation = engine.Advanced.InvokeFetchHandler(Get());
        operation.IsCompleted.Should().BeFalse();

        using var response = Pump(engine, operation);
        Text(response).Should().Be("late");
    }

    [Fact]
    public void AHandlerThatThrowsFaultsTheOperationWithTheJavaScriptException()
    {
        var engine = Handler("globalThis.handler = () => { throw new TypeError('boom'); };");

        var operation = engine.Advanced.InvokeFetchHandler(Get());

        operation.IsCompleted.Should().BeTrue();
        operation.IsFaulted.Should().BeTrue();
        operation.Response.Should().BeNull();

        // Never a synthesized 500: what a failure means on the wire is the host's decision, so the failure
        // arrives as the exception it was.
        var failure = Assert.IsType<JavaScriptException>(operation.Error);
        failure.Error.Get("name").AsString().Should().Be("TypeError");
        failure.Message.Should().Be("boom");

        Assert.Throws<JavaScriptException>(() => operation.GetResult());
    }

    [Fact]
    public void AnAsyncHandlerThatThrowsFaultsWithTheRejection()
    {
        var engine = Handler("globalThis.handler = { async fetch() { throw new Error('later'); } };");

        var operation = engine.Advanced.InvokeFetchHandler(Get());
        var stopwatch = Stopwatch.StartNew();
        while (!operation.IsCompleted && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
        {
            engine.Advanced.ProcessTasks();
        }

        operation.IsFaulted.Should().BeTrue();

        // A throw inside an async function is a rejection, and a rejection reaching a host is a
        // PromiseRejectedException everywhere else in Jint too.
        var failure = Assert.IsType<PromiseRejectedException>(operation.Error);
        failure.RejectedValue.AsObject().Get("message").AsString().Should().Be("later");
    }

    [Fact]
    public async Task TheAwaitableVariantThrowsWhatTheHandlerFailedWith()
    {
        var throwing = Handler("globalThis.handler = () => { throw new TypeError('sync boom'); };");
        var syncFailure = await Assert.ThrowsAsync<JavaScriptException>(() => throwing.Advanced.InvokeFetchHandlerAsync(Get()));
        syncFailure.Message.Should().Be("sync boom");

        var rejecting = Handler("globalThis.handler = { async fetch() { throw new Error('async boom'); } };");
        var asyncFailure = await Assert.ThrowsAsync<PromiseRejectedException>(() => rejecting.Advanced.InvokeFetchHandlerAsync(Get()));
        asyncFailure.RejectedValue.AsObject().Get("message").AsString().Should().Be("async boom");
    }

    [Fact]
    public void AnExecutionConstraintThatFiresFaultsTheOperation()
    {
        var engine = Handler(
            "globalThis.handler = () => { let n = 0; for (let i = 0; i < 10000; i++) { n += i; } return new Response(String(n)); };",
            configure: options => options.MaxStatements(50));

        var operation = engine.Advanced.InvokeFetchHandler(Get());

        operation.IsCompleted.Should().BeTrue();
        operation.IsFaulted.Should().BeTrue();
        Assert.IsType<StatementsCountOverflowException>(operation.Error);

        // The budget is per entry into the engine, so the engine is still usable afterwards — a handler that
        // stays inside it answers normally.
        engine.Execute("globalThis.handler = () => new Response('cheap');");
        engine.Advanced.SetFetchHandler(engine.GetValue("handler"));
        using var response = engine.Advanced.InvokeFetchHandler(Get()).GetResult();
        Text(response).Should().Be("cheap");
    }

    [Fact]
    public void TheWorkersModuleConventionIsRegisteredFromAModuleNamespace()
    {
        var engine = Host();
        engine.Modules.Add("worker", "export default { fetch(request) { return new Response('from ' + request.url); } };");

        // The module namespace goes straight in: SetFetchHandler unwraps `default` and then finds `fetch`.
        engine.Advanced.SetFetchHandler(engine.Modules.Import("worker"));

        using var response = engine.Advanced.InvokeFetchHandler(Get("https://example.org/m")).GetResult();
        Text(response).Should().Be("from https://example.org/m");
    }

    [Fact]
    public void TheHandlerObjectIsTheReceiverSoItsSiblingsAreReachable()
    {
        var engine = Handler("""
            globalThis.handler = {
                greeting: 'hi',
                fetch(request) { return new Response(this.greeting); },
            };
            """);

        using var response = engine.Advanced.InvokeFetchHandler(Get()).GetResult();
        Text(response).Should().Be("hi");
    }

    [Fact]
    public void APlainFunctionIsAcceptedAndSeesNoReceiver()
    {
        var engine = Handler("globalThis.handler = function (request) { return new Response(this === globalThis ? 'global' : 'other'); };");

        using var response = engine.Advanced.InvokeFetchHandler(Get()).GetResult();

        // The handler is called with `this` undefined, which a sloppy-mode function turns into the global
        // object exactly as a bare call anywhere else does; nothing was invented to stand in for a receiver.
        Text(response).Should().Be("global");
    }

    [Fact]
    public void SomethingThatIsNotAHandlerIsRefusedAtRegistration()
    {
        var engine = Host();
        engine.Execute("globalThis.notAHandler = { fetch: 42 };");

        // Refused where the host made the mistake, not on the first request in production.
        var failure = Assert.Throws<ArgumentException>(() => engine.Advanced.SetFetchHandler(engine.GetValue("notAHandler")));
        failure.Message.Should().Contain("export default { fetch(request)");

        Assert.Throws<ArgumentException>(() => engine.Advanced.SetFetchHandler(JsNumber.Create(1)));
        engine.Advanced.HasFetchHandler.Should().BeFalse();
    }

    [Fact]
    public void TheHandlerCanBeReplacedAndCleared()
    {
        var engine = Handler("globalThis.handler = () => new Response('first');");

        engine.Execute("globalThis.second = () => new Response('second');");
        engine.Advanced.SetFetchHandler(engine.GetValue("second"));
        using (var response = engine.Advanced.InvokeFetchHandler(Get()).GetResult())
        {
            Text(response).Should().Be("second");
        }

        engine.Advanced.SetFetchHandler(null);
        engine.Advanced.HasFetchHandler.Should().BeFalse();

        var failure = Assert.Throws<InvalidOperationException>(() => engine.Advanced.InvokeFetchHandler(Get()));
        failure.Message.Should().Contain("SetFetchHandler");
    }

    [Fact]
    public void AHandlerThatAnswersSomethingOtherThanAResponseFails()
    {
        var direct = Handler("globalThis.handler = () => 'just a string';");
        var operation = direct.Advanced.InvokeFetchHandler(Get());
        operation.IsFaulted.Should().BeTrue();
        Assert.IsType<InvalidOperationException>(operation.Error).Message.Should().Contain("must answer with a Response");

        // ... and the same when it arrives a turn later, where escaping would erupt out of the host's pump.
        var deferred = Handler("globalThis.handler = { async fetch() { return { status: 200 }; } };");
        var deferredOperation = deferred.Advanced.InvokeFetchHandler(Get());
        deferred.Advanced.ProcessTasks();
        deferredOperation.IsFaulted.Should().BeTrue();
        Assert.IsType<InvalidOperationException>(deferredOperation.Error);
    }

    [Fact]
    public void ANetworkErrorResponseIsNotSendable()
    {
        var engine = Handler("globalThis.handler = () => Response.error();");

        var operation = engine.Advanced.InvokeFetchHandler(Get());
        operation.IsFaulted.Should().BeTrue();
        Assert.IsType<InvalidOperationException>(operation.Error).Message.Should().Contain("Response.error()");
    }

    [Fact]
    public void TheRequestUriMustBeAbsolute()
    {
        var engine = Handler("globalThis.handler = () => new Response('x');");

        var relative = new HttpRequestMessage(HttpMethod.Get, new Uri("/hello", UriKind.Relative));
        var operation = engine.Advanced.InvokeFetchHandler(relative);

        operation.IsFaulted.Should().BeTrue();
        Assert.IsType<InvalidOperationException>(operation.Error).Message.Should().Contain("relative");
    }

    [Fact]
    public void ArgumentFailuresAreTheHostsOwn()
    {
        var engine = Handler("globalThis.handler = () => new Response('x');");

        Assert.Throws<ArgumentNullException>(() => engine.Advanced.InvokeFetchHandler(null!));

        var pending = Handler("globalThis.handler = { async fetch() { return new Response('x'); } };");
        var operation = pending.Advanced.InvokeFetchHandler(Get());
        Assert.Throws<InvalidOperationException>(() => operation.GetResult())
            .Message.Should().Contain("ProcessTasks");
    }

    [Fact]
    public void SequentialInvocationsReuseTheEngine()
    {
        var engine = Handler("""
            globalThis.seen = [];
            globalThis.handler = {
                fetch(request) {
                    globalThis.seen.push(request.url);
                    return new Response(String(globalThis.seen.length));
                }
            };
            """);

        for (var i = 1; i <= 3; i++)
        {
            using var response = engine.Advanced.InvokeFetchHandler(Get($"https://example.org/{i}")).GetResult();
            Text(response).Should().Be(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // Each invocation got its own Request; the engine — and everything the script left on it — is shared,
        // which is exactly the pooled-engine shape a host wants.
        engine.Evaluate("globalThis.seen.join(',')").AsString()
            .Should().Be("https://example.org/1,https://example.org/2,https://example.org/3");
    }

    [Fact]
    public void APooledEngineRestoresItsGlobalsBetweenRequests()
    {
        var engine = Handler("""
            globalThis.handler = {
                fetch(request) {
                    globalThis.leaked = (globalThis.leaked || 0) + 1;
                    return new Response(String(globalThis.leaked));
                }
            };
            """);

        // Captured after the registration, which is what installs Request/Response/Headers: a snapshot taken
        // before it would restore a global object that has no Response for the handler to answer with.
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        for (var i = 0; i < 3; i++)
        {
            engine.Advanced.RestoreGlobalSnapshot(snapshot);

            using var response = engine.Advanced.InvokeFetchHandler(Get()).GetResult();

            // Every request starts from the captured globals, so the previous one's counter is not there.
            Text(response).Should().Be("1");
        }

        // The handler is host state and survives the restore, so it never needs re-registering.
        engine.Advanced.HasFetchHandler.Should().BeTrue();
    }

    [Fact]
    public void AnInvocationTheEngineFencedOffCompletesAsFaulted()
    {
        var engine = Handler("globalThis.handler = { async fetch() { return new Response('never'); } };");
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var operation = engine.Advanced.InvokeFetchHandler(Get());
        operation.IsCompleted.Should().BeFalse();

        // The restore ends the evaluation cycle, so the reaction that would complete this operation is
        // discarded at dequeue rather than run. A host polling IsCompleted must not poll forever.
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        operation.IsCompleted.Should().BeTrue();
        operation.IsFaulted.Should().BeTrue();
        Assert.IsType<InvalidOperationException>(operation.Error).Message.Should().Contain("abandoned");

        // The handler itself is host state and survives, like Engine.Advanced.HostDefined.
        engine.Advanced.HasFetchHandler.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------------------
    // The second registration form: WebApiFeatures.FetchEvents and addEventListener('fetch', …).
    // ---------------------------------------------------------------------------------------------------

    private sealed class RecordingSink : DiagnosticsSink
    {
        internal List<DiagnosticEvent> Reports { get; } = new();

        public override void Report(DiagnosticEvent report) => Reports.Add(report);
    }

    /// <summary>
    /// Builds an engine with the fetch-events feature and evaluates <paramref name="source"/>, which is
    /// expected to register its own listener. Nothing is registered with <see cref="Engine.AdvancedOperations.SetFetchHandler"/>.
    /// </summary>
    /// <param name="prepare">
    /// Runs against the built engine <em>before</em> <paramref name="source"/> does, which is where a global
    /// the listeners call has to be installed.
    /// </param>
    private static Engine Listener(string source, Action<Options>? configure = null, Action<Engine>? prepare = null)
    {
        var engine = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.FetchEvents);
            configure?.Invoke(options);
        });

        prepare?.Invoke(engine);
        engine.Execute(source);
        return engine;
    }

    [Fact]
    public void AScriptRegisteredListenerServesTheRequestThroughThePolledShape()
    {
        var engine = Listener("addEventListener('fetch', event => event.respondWith(new Response('from ' + event.request.url, { status: 201 })));");

        // Nothing was registered with SetFetchHandler, and the host asks for the request exactly as before.
        engine.Advanced.HasFetchHandler.Should().BeFalse();

        using var response = Pump(engine, engine.Advanced.InvokeFetchHandler(Get("https://example.org/w")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        Text(response).Should().Be("from https://example.org/w");
    }

    [Fact]
    public async Task AScriptRegisteredListenerServesTheRequestThroughTheAwaitableShape()
    {
        var engine = Listener("""
            addEventListener('fetch', event => {
                event.respondWith(event.request.text().then(body => new Response('echo:' + body)));
            });
            """);

        var message = new HttpRequestMessage(HttpMethod.Post, "https://example.org/")
        {
            Content = new StringContent("payload", Encoding.UTF8, "text/plain"),
        };

        using var response = await engine.Advanced.InvokeFetchHandlerAsync(message);
        (await response.Content.ReadAsStringAsync()).Should().Be("echo:payload");
    }

    [Fact]
    public void AnExplicitHandlerAlwaysWinsOverTheScriptsListeners()
    {
        var engine = Listener("""
            globalThis.log = [];
            addEventListener('fetch', event => { globalThis.log.push('listener'); event.respondWith(new Response('listener')); });
            globalThis.handler = () => { globalThis.log.push('handler'); return new Response('handler'); };
            """);

        engine.Advanced.SetFetchHandler(engine.GetValue("handler"));

        using (var response = engine.Advanced.InvokeFetchHandler(Get()).GetResult())
        {
            // A script that adds a listener must not be able to take the route away from the host.
            Text(response).Should().Be("handler");
        }

        engine.Evaluate("globalThis.log.join(',')").AsString().Should().Be("handler");

        // Clearing the handler is how a host hands the route over, deliberately.
        engine.Advanced.SetFetchHandler(null);
        using var second = Pump(engine, engine.Advanced.InvokeFetchHandler(Get()));
        Text(second).Should().Be("listener");
    }

    [Fact]
    public void WithoutTheFeatureFlagAListenerIsNotConsulted()
    {
        // Everything the listener form needs except the flag itself: the global addEventListener is there, so
        // registering succeeds and simply routes nothing.
        var engine = new Engine(options => options.UseWebApis(ModelFeatures | WebApiFeatures.GlobalEvents));
        engine.Execute("addEventListener('fetch', event => event.respondWith(new Response('x')));");

        engine.Evaluate("typeof FetchEvent").AsString().Should().Be("undefined");

        var failure = Assert.Throws<InvalidOperationException>(() => engine.Advanced.InvokeFetchHandler(Get()));
        failure.Message.Should().Contain("SetFetchHandler");
        failure.Message.Should().Contain("WebApiFeatures.FetchEvents");
    }

    [Fact]
    public void NoListenerRespondingFailsTheOperation()
    {
        var engine = Listener("addEventListener('fetch', () => { /* looks at the request and shrugs */ });");

        var operation = engine.Advanced.InvokeFetchHandler(Get());

        // There is no network for an unanswered request to fall through to, so this is a failure like any
        // other — and it arrives through the operation rather than being thrown at the start call.
        operation.IsCompleted.Should().BeTrue();
        operation.IsFaulted.Should().BeTrue();
        Assert.IsType<InvalidOperationException>(operation.Error).Message.Should().Contain("respondWith");
    }

    [Fact]
    public async Task NoListenerRespondingThrowsFromTheAwaitableShape()
    {
        var engine = Listener("addEventListener('fetch', () => { });");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.Advanced.InvokeFetchHandlerAsync(Get()));
        failure.Message.Should().Contain("respondWith");
    }

    [Fact]
    public void AListenerThatThrowsFaultsTheOperationWhenNoSinkIsSet()
    {
        var engine = Listener("addEventListener('fetch', () => { throw new TypeError('listener boom'); });");

        var operation = engine.Advanced.InvokeFetchHandler(Get());

        // With no diagnostics sink an exception escaping a listener propagates — the EventTarget contract,
        // unchanged — and the invoke turns it into the operation's failure, exactly as it does for a handler
        // that threw.
        operation.IsFaulted.Should().BeTrue();
        var failure = Assert.IsType<JavaScriptException>(operation.Error);
        failure.Error.Get("name").AsString().Should().Be("TypeError");
        failure.Message.Should().Be("listener boom");
    }

    [Fact]
    public void AListenerThatThrowsIsReportedAndALaterOneStillAnswers()
    {
        var sink = new RecordingSink();
        var engine = Listener(
            """
            addEventListener('fetch', () => { throw new TypeError('first fails'); });
            addEventListener('fetch', event => event.respondWith(new Response('second answers')));
            """,
            options => options.UseWebApis(webApi => webApi.Diagnostics.Sink = sink));

        using var response = Pump(engine, engine.Advanced.InvokeFetchHandler(Get()));

        // A sink is what turns "report the exception and carry on" from a lie into the specified behaviour, so
        // the request is still served.
        Text(response).Should().Be("second answers");
        sink.Reports.Should().Contain(report => report.Kind == DiagnosticEventKind.UncaughtCallbackError);
    }

    [Fact]
    public void AConstraintThatFiresDuringTheDispatchFaultsTheOperation()
    {
        var engine = Listener(
            "addEventListener('fetch', event => { let n = 0; for (let i = 0; i < 10000; i++) { n += i; } event.respondWith(new Response(String(n))); });",
            options => options.MaxStatements(50));

        var operation = engine.Advanced.InvokeFetchHandler(Get());

        // A constraint is a JintException that is not a JavaScriptException, so it erupts past a sink and past
        // the dispatch — and the invoke turns it into the operation's failure rather than leaving the host
        // polling a promise that can never settle.
        operation.IsCompleted.Should().BeTrue();
        operation.IsFaulted.Should().BeTrue();
        Assert.IsType<StatementsCountOverflowException>(operation.Error);
    }

    // ---- the dispatch is one constraint run, however many listeners it invokes ----
    //
    // The test above proves only that *a* constraint fires and that the failure reaches the host; it would
    // pass just as well if every listener were bracketed separately, because a single listener cannot tell
    // the difference. What follows is the part that can: two and three listeners, and the question of whose
    // allowance the second one spends.

    /// <summary>
    /// A host-written constraint that records when the engine arms and disarms it, so a test can see the
    /// dispatch the way <c>Engine.ResetConstraints</c> sees it.
    /// </summary>
    /// <remarks>
    /// It is a pin for every constraint kind and not only for itself. The engine resets constraints in one
    /// loop over all of them, and each built-in's per-run state is established by nothing but its own
    /// <see cref="Constraint.Reset"/> — <c>MaxStatementsConstraint</c> zeroes its counter there and
    /// <c>TimeConstraint</c> captures its deadline there. So the cadence this observes is exactly the cadence
    /// at which a statement allowance is refilled and a wall-clock deadline re-armed.
    /// <para>
    /// That indirection is what the <i>timeout</i> half of the property has to be pinned through.
    /// <c>TimeConstraint</c> reads <c>Stopwatch.GetTimestamp()</c> directly and has no <c>TimeProvider</c>
    /// seam (sebastienros/jint#3232), so a test that watched a deadline span two listeners could only do it
    /// by sleeping through a fraction of a real interval — a wall-clock race, and the flake family #3221 is
    /// about. Counting resets says the same thing exactly.
    /// </para>
    /// </remarks>
    private sealed class BudgetProbeConstraint : Constraint
    {
        private readonly List<string> _log;
        private int _statements;

        internal BudgetProbeConstraint(List<string> log)
        {
            _log = log;
        }

        /// <summary>Statements charged between the two most recent resets, i.e. during the last run.</summary>
        internal int StatementsInLastRun { get; private set; }

        public override void Check() => _statements++;

        public override void Reset()
        {
            StatementsInLastRun = _statements;
            _statements = 0;
            _log.Add("reset");
        }
    }

    /// <summary>A listener that costs a measurable number of statements and answers nothing.</summary>
    private const string CountingListener =
        "addEventListener('fetch', () => { var n = 0; for (var i = 0; i < 200; i++) { n += i; } });";

    /// <summary>The same workload, plus the <c>respondWith</c> that ends the dispatch successfully.</summary>
    private const string CountingResponder =
        "addEventListener('fetch', event => { var n = 0; for (var i = 0; i < 200; i++) { n += i; } event.respondWith(new Response('ok')); });";

    /// <summary>
    /// What one dispatch of <paramref name="source"/> costs in statements, measured rather than assumed:
    /// what a listener costs is an engine detail, and pinning the property is not a reason to pin the number.
    /// </summary>
    private static int StatementsOneDispatchCosts(string source, string expected = "ok")
    {
        var probe = new BudgetProbeConstraint([]);
        var engine = Listener(source, options => options.Constraint(probe));

        using var response = Pump(engine, engine.Advanced.InvokeFetchHandler(Get()));
        Text(response).Should().Be(expected);

        // The reset on the way out of the dispatch is what published the count, and nothing the host does
        // afterwards — pumping included — resets a constraint again.
        return probe.StatementsInLastRun;
    }

    [Fact]
    public void TheWholeDispatchIsOneConstraintRunWhateverTheListenerCount()
    {
        var log = new List<string>();

        var engine = Listener(
            """
            addEventListener('fetch', () => { mark('first'); });
            addEventListener('fetch', () => { mark('second'); });
            addEventListener('fetch', event => { mark('third'); event.respondWith(new Response('ok')); });
            """,
            options => options.Constraint(new BudgetProbeConstraint(log)),
            prepare: e => e.SetValue("mark", new Action<string>(log.Add)));

        // Evaluating the source above was a host entry of its own and armed the constraints twice; the
        // dispatch is what this is about.
        log.Clear();

        using var response = Pump(engine, engine.Advanced.InvokeFetchHandler(Get()));
        Text(response).Should().Be("ok");

        // One arming before the dispatch and one after it, with all three listeners in between: the engine is
        // entered once for the dispatch, not once per listener. A per-listener bracket would show up here as
        // a "reset" pair around every mark.
        log.Should().Equal("reset", "first", "second", "third", "reset");
    }

    [Fact]
    public void EveryListenerInTheDispatchDrawsOnOneStatementAllowance()
    {
        // Half again as much as one listener's whole dispatch costs. The two listeners here carry the same
        // workload, so that is comfortably more than either of them needs on its own and comfortably less than
        // the two of them need together — and both margins scale with the workload rather than with whatever
        // the engine's fixed per-dispatch overhead happens to be today.
        var alone = StatementsOneDispatchCosts(CountingResponder);
        var allowance = alone + alone / 2;

        var single = Listener(CountingResponder, options => options.MaxStatements(allowance));
        using var served = Pump(single, single.Advanced.InvokeFetchHandler(Get()));
        Text(served).Should().Be("ok");

        // The same allowance, one more listener: the second listener runs out of what the first one spent.
        // Bracketing each listener separately would hand the second a fresh `allowance` — more than enough —
        // and this request would be served too.
        var pair = Listener(CountingListener + "\n" + CountingResponder, options => options.MaxStatements(allowance));
        var operation = pair.Advanced.InvokeFetchHandler(Get());

        operation.IsCompleted.Should().BeTrue();
        operation.IsFaulted.Should().BeTrue();
        Assert.IsType<StatementsCountOverflowException>(operation.Error);

        // Said the other way round, and without a limit involved at all: one dispatch is charged for every
        // listener it invokes, so adding one costs statements rather than starting a fresh count.
        StatementsOneDispatchCosts(CountingListener + "\n" + CountingResponder).Should().BeGreaterThan(alone);
    }

    public static bool MemoryAccountingAvailable => MemoryLimitConstraint.Accuracy != MemoryLimitAccuracy.Unavailable;

    /// <summary>A listener that allocates enough for the difference between one budget and two to be plain.</summary>
    private const string AllocatingListener =
        "addEventListener('fetch', () => { var a = []; for (var i = 0; i < 20000; i++) { a.push({ x: i }); } note(); });";

    private const string AllocatingResponder =
        "addEventListener('fetch', event => { var a = []; for (var i = 0; i < 20000; i++) { a.push({ x: i }); } note(); event.respondWith(new Response('ok')); });";

    [Fact(Skip = "Managed allocation accounting is unavailable on this runtime.", SkipUnless = nameof(MemoryAccountingAvailable))]
    public void EveryListenerInTheDispatchDrawsOnOneAllocationBudget()
    {
        var observed = new List<long>();

        var metered = Listener(
            AllocatingListener + "\n" + AllocatingResponder,
            options => options.LimitMemory(1L << 40),
            prepare: e =>
            {
                var meter = e.Constraints.Find<MemoryLimitConstraint>()!;
                e.SetValue("note", new Action(() => observed.Add(meter.AllocatedBytes)));
            });

        using var response = Pump(metered, metered.Advanced.InvokeFetchHandler(Get()));
        Text(response).Should().Be("ok");

        // The second listener's meter reads its own allocations on top of the first listener's, because the
        // accounting segment the dispatch opened is still the one it is allocating in. With a bracket per
        // listener the second reading would be a fresh count of roughly the first's size.
        observed.Should().HaveCount(2);
        observed[0].Should().BeGreaterThan(0);
        observed[1].Should().BeGreaterThan(observed[0] + observed[0] / 2);

        // And the same shape as the statement allowance above: a limit half way between what one listener
        // allocates and what two allocate serves one request and fails the other.
        var limit = (observed[0] + observed[1]) / 2;

        var single = Listener(AllocatingResponder, options => options.LimitMemory(limit), prepare: NoNote);
        using var served = Pump(single, single.Advanced.InvokeFetchHandler(Get()));
        Text(served).Should().Be("ok");

        var pair = Listener(AllocatingListener + "\n" + AllocatingResponder, options => options.LimitMemory(limit), prepare: NoNote);
        var operation = pair.Advanced.InvokeFetchHandler(Get());

        operation.IsCompleted.Should().BeTrue();
        operation.IsFaulted.Should().BeTrue();
        Assert.IsType<MemoryLimitExceededException>(operation.Error);
    }

    /// <summary>Satisfies the <c>note()</c> call in the allocating listeners for a run that is not metering.</summary>
    private static void NoNote(Engine engine) => engine.SetValue("note", new Action(() => { }));

    // ---- and what the bracket does not cover ----

    /// <summary>A listener whose synchronous half and deferred half cost the same, so the two are comparable.</summary>
    private const string CountingDeferredResponder =
        "addEventListener('fetch', event => { var n = 0; for (var i = 0; i < 200; i++) { n += i; } "
        + "event.respondWith(Promise.resolve().then(() => { var m = 0; for (var j = 0; j < 200; j++) { m += j; } return new Response('late'); })); });";

    [Fact]
    public void TheTurnsPumpedAfterTheDispatchGetAFreshStatementAllowance()
    {
        // The dispatch is synchronous and so is the bracket around it. A listener that answers with a promise
        // has already returned by the time the promise's reaction runs, so the reaction belongs to whatever
        // entry runs it — here Advanced.ProcessTasks, which arms nothing and is not a bracket.
        var synchronousHalf = StatementsOneDispatchCosts(CountingDeferredResponder, "late");

        // Half again as much as the dispatch spent. What is left of it after the dispatch is far less than the
        // reaction needs, so the request can only be served if the reaction is charged against a refilled
        // allowance rather than against the dispatch's remainder — which is exactly what the reset on the way
        // out of the bracket leaves behind.
        var allowance = synchronousHalf + synchronousHalf / 2;

        var engine = Listener(CountingDeferredResponder, options => options.MaxStatements(allowance));
        using var response = Pump(engine, engine.Advanced.InvokeFetchHandler(Get()));

        Text(response).Should().Be("late");
    }

    [Fact(Skip = "Managed allocation accounting is unavailable on this runtime.", SkipUnless = nameof(MemoryAccountingAvailable))]
    public void TheTurnsPumpedAfterTheDispatchStayInsideTheDispatchsAllocationBudget()
    {
        // Allocation is the one budget that does follow the answer out of the dispatch: a promise reaction
        // captures the operation's accounting state when it is registered and resumes it when it runs, so the
        // bytes the reaction allocates are charged to the request that is still outstanding. The statement
        // allowance above and the wall-clock deadline do not work that way, which is why this is its own test
        // rather than another assertion in that one.
        var deferred =
            "addEventListener('fetch', event => { var a = []; for (var i = 0; i < 20000; i++) { a.push({ x: i }); } note(); "
            + "event.respondWith(Promise.resolve().then(() => { var b = []; for (var j = 0; j < 20000; j++) { b.push({ y: j }); } return new Response('late'); })); });";

        var synchronousHalf = 0L;
        var metered = Listener(
            deferred,
            options => options.LimitMemory(1L << 40),
            prepare: e =>
            {
                var meter = e.Constraints.Find<MemoryLimitConstraint>()!;
                e.SetValue("note", new Action(() => synchronousHalf = meter.AllocatedBytes));
            });

        using var response = Pump(metered, metered.Advanced.InvokeFetchHandler(Get()));
        Text(response).Should().Be("late");
        synchronousHalf.Should().BeGreaterThan(0);

        // Half again as much as the synchronous half allocated: enough for it, and — only because the
        // reaction continues the same accounting — not enough for both halves together. A reaction given a
        // budget of its own would allocate the same bytes again from zero and this request would be served.
        var limit = synchronousHalf + synchronousHalf / 2;

        var engine = Listener(deferred, options => options.LimitMemory(limit), prepare: NoNote);
        var operation = engine.Advanced.InvokeFetchHandler(Get());

        // The dispatch itself fits.
        operation.IsFaulted.Should().BeFalse();

        // The reaction does not, and where it fails is where the host is standing: Advanced.ProcessTasks is
        // not a constraint bracket, so the failure erupts from the pump. A host that answers requests out of a
        // script's promises has to guard its own pump, not only the invocation.
        Assert.Throws<MemoryLimitExceededException>(() =>
        {
            for (var i = 0; i < 100 && !operation.IsCompleted; i++)
            {
                engine.Advanced.ProcessTasks();
            }
        });
    }

    [Fact]
    public void AListenerThatAnswersAndThenThrowsServesItsResponseWhenASinkIsSet()
    {
        var sink = new RecordingSink();
        var engine = Listener(
            "addEventListener('fetch', event => { event.respondWith(new Response('answered')); throw new Error('after answering'); });",
            options => options.UseWebApis(webApi => webApi.Diagnostics.Sink = sink));

        using var response = Pump(engine, engine.Advanced.InvokeFetchHandler(Get()));

        // respondWith() already committed the answer, and reporting the throw is what lets the dispatch finish
        // — so the request is served and the failure is still visible. This is what a browser does.
        Text(response).Should().Be("answered");
        sink.Reports.Should().Contain(report => report.Kind == DiagnosticEventKind.UncaughtCallbackError);
    }

    [Fact]
    public void AListenerThatAnswersAndThenThrowsFailsTheOperationWithNoSink()
    {
        var engine = Listener("addEventListener('fetch', event => { event.respondWith(new Response('answered')); throw new Error('after answering'); });");

        var operation = engine.Advanced.InvokeFetchHandler(Get());

        // The one case where the two configurations disagree about a request that was answered. With nowhere
        // to report to, preferring the response would lose the exception entirely — so the exception wins and
        // the host sees the failure, which is the same bargain every other engine-invoked callback makes.
        operation.IsFaulted.Should().BeTrue();
        Assert.IsType<JavaScriptException>(operation.Error).Message.Should().Be("after answering");
    }

    [Fact]
    public void TheDispatchIsAHostEntryAndArmsTheConstraintsAfresh()
    {
        // The loop is there so the listener runs past the amortized check cadence: a wall-clock constraint is
        // amortizable, so a three-statement listener would never reach a check at all and the test would pass
        // whatever the dispatch did.
        var engine = Listener(
            "addEventListener('fetch', event => { let n = 0; for (let i = 0; i < 500; i++) { n += i; } event.respondWith(new Response('answered')); });",
            options => options.TimeoutInterval(TimeSpan.FromMilliseconds(200)));

        // Time the host spent between requests is the host's, not the script's. Entering the engine is what
        // arms the wall-clock budget — a TimeConstraint otherwise measures from the moment the previous entry
        // returned — so a request arriving long afterwards still gets its whole allowance. The dispatch is
        // bracketed in exactly what Engine.Call brackets the registered-handler route in, which is what makes
        // that true here as well.
        Thread.Sleep(400);

        using var response = Pump(engine, engine.Advanced.InvokeFetchHandler(Get()));
        Text(response).Should().Be("answered");
    }

    [Fact]
    public void AListenerInvocationTheEngineFencedOffCompletesAsFaulted()
    {
        var engine = Listener("addEventListener('fetch', event => event.respondWith(new Promise(() => { })));");
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var operation = engine.Advanced.InvokeFetchHandler(Get());
        operation.IsCompleted.Should().BeFalse();

        // The operation records the cycle it was started in before the dispatch runs, so a restore abandons it
        // just as it abandons one started by a registered handler. A host polling IsCompleted must not poll
        // forever.
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        operation.IsCompleted.Should().BeTrue();
        operation.IsFaulted.Should().BeTrue();
        Assert.IsType<InvalidOperationException>(operation.Error).Message.Should().Contain("abandoned");
    }

    [Fact]
    public void TheFeatureInstallsTheObjectModelBeforeAnyScriptRuns()
    {
        // Unlike the SetFetchHandler door, whose install happens when the host registers, this one is part of
        // building the engine — so a module may construct a Response at top level.
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.FetchEvents));
        engine.Modules.Add("worker", "const canned = new Response('top level'); addEventListener('fetch', e => e.respondWith(canned.clone()));");
        engine.Modules.Import("worker");

        using var response = Pump(engine, engine.Advanced.InvokeFetchHandler(Get()));
        Text(response).Should().Be("top level");

        // And still no outbound network.
        engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");
    }
}
#endif
