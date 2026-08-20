#if NET8_0_OR_GREATER
#nullable enable

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using Jint.Runtime;
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
}
#endif
