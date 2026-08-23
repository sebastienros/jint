#if NET8_0_OR_GREATER
#nullable enable

using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// A <c>Headers</c> object's guard, https://fetch.spec.whatwg.org/#concept-headers-guard: which of the
/// standard's five values every construction site carries, and what each one lets a script do.
/// </summary>
/// <remarks>
/// <para>
/// The matrix is here in one file rather than spread over <c>HeadersTests</c>, <c>RequestTests</c>,
/// <c>ResponseTests</c>, <c>FetchTests</c> and <c>CacheTests</c> because a guard is only meaningful as a
/// whole: an implementation that refused every mutation would pass half of these, and one that refused none
/// would pass the other half. Every site is therefore pinned from both directions — the mutation that must
/// throw, and the mutation that must still succeed.
/// </para>
/// <para>
/// Only <c>immutable</c> changes behaviour in Jint. <c>request</c>, <c>request-no-cors</c> and
/// <c>response</c> exist in the standard to enforce the forbidden-header-name lists, which Jint deliberately
/// does not enforce — see <c>HeadersGuard</c>'s own documentation — and <c>request-no-cors</c> cannot arise
/// at all, because <c>RequestInit</c>'s <c>mode</c> member is not implemented.
/// </para>
/// </remarks>
public class HeadersGuardTests
{
    /// <summary>
    /// How long a fetch may take to settle against the stub below. Not a measurement and not a budget: what is
    /// asserted is always what the fetch produced, never how long it took.
    /// </summary>
    private static readonly TimeSpan TransportSignalCeiling = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A transport that answers every request from memory, so nothing here touches a network.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        internal Func<HttpResponseMessage>? Responder { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = Responder?.Invoke() ?? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok"),
            };

            return Task.FromResult(response);
        }
    }

    private static Engine WebEngine() => new(options => options.UseFetch());

    private static Engine FetchEngine(HttpMessageHandler handler)
        => new(options => options.UseFetch(fetch => fetch.HttpClient = new HttpClient(handler)));

    /// <summary>Runs a fetch-driven script and waits for the promise it answers with.</summary>
    private static string FetchResult(HttpMessageHandler handler, string source)
        => FetchEngine(handler).Evaluate(source).UnwrapIfPromise(TransportSignalCeiling).AsString();

    /// <summary>
    /// The three mutating operations, each reporting what it did. Every guarded site is held to the same
    /// script, so a site that refuses one operation and not another cannot pass.
    /// </summary>
    private const string TryMutate = """
        function tryMutate(headers) {
            const results = [];
            for (const attempt of [
                () => headers.append('x-guard', 'appended'),
                () => headers.set('x-guard', 'set'),
                () => headers.delete('x-existing'),
            ]) {
                try {
                    attempt();
                    results.push('ok');
                } catch (e) {
                    results.push(e.constructor.name + '/' + (e.message.indexOf('immutable') >= 0));
                }
            }
            return results.join(',');
        }
        """;

    [Fact]
    public void ABareHeadersObjectIsMutable()
    {
        // "The new Headers(init) constructor steps are: Set this's guard to 'none'."
        // https://fetch.spec.whatwg.org/#dom-headers
        var engine = WebEngine();
        engine.Execute(TryMutate);
        engine.Execute("var h = new Headers({ 'x-existing': '1' });");

        engine.Evaluate("tryMutate(h)").AsString().Should().Be("ok,ok,ok");
        engine.Evaluate("h.get('x-guard')").AsString().Should().Be("set");
        engine.Evaluate("h.has('x-existing')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ARequestsHeadersAreMutable()
    {
        // "Set this's headers to a new Headers object … whose … guard is 'request'."
        // https://fetch.spec.whatwg.org/#dom-request
        var engine = WebEngine();
        engine.Execute(TryMutate);
        engine.Execute("var r = new Request('https://example.org/', { headers: { 'x-existing': '1' } });");

        engine.Evaluate("tryMutate(r.headers)").AsString().Should().Be("ok,ok,ok");
        engine.Evaluate("r.headers.get('x-guard')").AsString().Should().Be("set");

        // A Request built from another Request copies the header list, and its own guard is "request" again.
        engine.Execute("var copy = new Request(r);");
        engine.Evaluate("tryMutate(copy.headers)").AsString().Should().Be("ok,ok,ok");

        // clone() carries this's headers's guard, which for an ordinary Request is "request".
        // https://fetch.spec.whatwg.org/#dom-request-clone
        engine.Evaluate("tryMutate(r.clone().headers)").AsString().Should().Be("ok,ok,ok");
    }

    [Fact]
    public void AResponsesHeadersAreMutable()
    {
        // "Set this's headers to a new Headers object … whose … guard is 'response'."
        // https://fetch.spec.whatwg.org/#dom-response, and the same for Response.json:
        // https://fetch.spec.whatwg.org/#dom-response-json creates its object with "response".
        var engine = WebEngine();
        engine.Execute(TryMutate);
        engine.Execute("var r = new Response('body', { headers: { 'x-existing': '1' } });");

        engine.Evaluate("tryMutate(r.headers)").AsString().Should().Be("ok,ok,ok");
        engine.Evaluate("r.headers.get('x-guard')").AsString().Should().Be("set");

        engine.Execute("var j = Response.json({ a: 1 });");
        engine.Evaluate("tryMutate(j.headers)").AsString().Should().Be("ok,ok,ok");

        // https://fetch.spec.whatwg.org/#dom-response-clone — the clone carries the same guard.
        engine.Evaluate("tryMutate(new Response('body').clone().headers)").AsString().Should().Be("ok,ok,ok");
    }

    [Fact]
    public void TheTwoImmutableStaticsRefuseEveryMutation()
    {
        // https://fetch.spec.whatwg.org/#dom-response-error and
        // https://fetch.spec.whatwg.org/#dom-response-redirect both create their Response object with the
        // "immutable" guard.
        var engine = WebEngine();
        engine.Execute(TryMutate);

        engine.Evaluate("tryMutate(Response.error().headers)").AsString()
            .Should().Be("TypeError/true,TypeError/true,TypeError/true");

        engine.Evaluate("tryMutate(Response.redirect('https://example.org/a').headers)").AsString()
            .Should().Be("TypeError/true,TypeError/true,TypeError/true");

        // The guard travels through clone(), so the copy cannot be edited either.
        engine.Evaluate("tryMutate(Response.error().clone().headers)").AsString()
            .Should().Be("TypeError/true,TypeError/true,TypeError/true");
    }

    [Fact]
    public void AFetchedResponsesHeadersAreImmutable()
    {
        // "Set responseObject to the result of creating a Response object, given response, 'immutable', and
        // relevantRealm" — https://fetch.spec.whatwg.org/#dom-global-fetch. What the server said is not the
        // script's to rewrite. sebastienros/jint#3281.
        var handler = new StubHandler
        {
            Responder = () =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
                response.Headers.Add("x-existing", "1");
                return response;
            },
        };

        FetchResult(handler, TryMutate + """
            fetch('https://example.org/a').then(r => tryMutate(r.headers));
            """).Should().Be("TypeError/true,TypeError/true,TypeError/true");

        // And nothing stuck: the refused mutations left the list exactly as the transport built it.
        FetchResult(handler, """
            fetch('https://example.org/a').then(r => {
                try { r.headers.append('name', 'value'); } catch (e) { }
                try { r.headers.delete('x-existing'); } catch (e) { }
                return r.headers.get('name') + '|' + r.headers.get('x-existing');
            });
            """).Should().Be("null|1");
    }

    [Fact]
    public void AFetchedResponsesCloneIsImmutableToo()
    {
        // https://fetch.spec.whatwg.org/#dom-response-clone hands the clone "this's headers's guard", so the
        // clone of an immutable response is immutable — cloning is not a way around the guard.
        var handler = new StubHandler();

        FetchResult(handler, TryMutate + """
            fetch('https://example.org/a').then(r => tryMutate(r.clone().headers));
            """).Should().Be("TypeError/true,TypeError/true,TypeError/true");
    }

    [Fact]
    public void AFetchedResponsesHeadersCanStillBeReadEveryWay()
    {
        // Nothing about reading is guarded: get, has, getSetCookie, iteration and forEach are all unaffected
        // by "immutable", which only ever appears in the three mutating algorithms.
        var handler = new StubHandler
        {
            Responder = () =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
                response.Headers.Add("x-multi", "one");
                response.Headers.Add("x-multi", "two");
                response.Headers.Add("set-cookie", "a=1");
                response.Headers.Add("set-cookie", "b=2");
                return response;
            },
        };

        FetchResult(handler, """
            fetch('https://example.org/a').then(r => {
                const h = r.headers;
                const seen = [];
                h.forEach((value, name) => seen.push(name));
                return [
                    h.get('x-multi'),
                    h.has('x-multi'),
                    h.getSetCookie().join('|'),
                    [...h.keys()].join('|'),
                    [...h].length === seen.length,
                ].join(';');
            });
            """).Should().Be("one, two;true;a=1|b=2;content-length|content-type|set-cookie|set-cookie|x-multi;true");
    }

    [Fact]
    public void AFetchedResponsesHeadersCanBeCopiedIntoAMutableOne()
    {
        // The escape hatch, and the one an embedder rewrites to: a guard belongs to the Headers *object*, so
        // filling a new one from an immutable one — https://fetch.spec.whatwg.org/#concept-headers-fill —
        // copies the headers and not the guard. This is the browser-blessed way to add a header to a
        // response that came off the wire, and the migration for anything that used to mutate in place.
        var handler = new StubHandler
        {
            Responder = () =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
                response.Headers.Add("x-existing", "1");
                return response;
            },
        };

        FetchResult(handler, """
            fetch('https://example.org/a').then(async r => {
                const copy = new Headers(r.headers);
                copy.set('x-added', '2');

                const rebuilt = new Response(await r.text(), { status: r.status, headers: copy });
                rebuilt.headers.set('x-later', '3');

                return [
                    rebuilt.headers.get('x-existing'),
                    rebuilt.headers.get('x-added'),
                    rebuilt.headers.get('x-later'),
                    r.headers.get('x-added') === null,
                ].join('|');
            });
            """).Should().Be("1|2|3|true");
    }

    [Fact]
    public void TheGuardIsCheckedAfterTheNameAndValueAre()
    {
        // "To validate a header (name, value) for a Headers object headers: 1. If name is not a header name
        // or value is not a header value, then throw a TypeError. 2. If headers's guard is 'immutable', then
        // throw a TypeError." — https://fetch.spec.whatwg.org/#headers-validate. Both are TypeErrors, so the
        // order is observable only through the message, but it is observable, and the same order applies to
        // delete, which validates (name, ``) before it looks at the guard.
        var engine = WebEngine();
        engine.Execute("var h = Response.error().headers;");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("h.append('bad name', 'v')"))
            .Message.Should().Contain("Invalid name");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("h.set('bad name', 'v')"))
            .Message.Should().Contain("Invalid name");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("h.delete('bad name')"))
            .Message.Should().Contain("Invalid name");

        // A valid name reaches the guard check instead.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("h.append('good-name', 'v')"))
            .Message.Should().Contain("immutable");

        // And an invalid *value* is refused before the guard too — the same step 1.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("h.append('good-name', 'a\\nb')"))
            .Message.Should().Contain("Invalid value");
    }

    [Fact]
    public void ACachedRequestAndResponseAreBothImmutable()
    {
        // "Add a new Response object associated with response and a new Headers object whose guard is
        // 'immutable'", and the same wording for the Request objects keys() answers with —
        // https://w3c.github.io/ServiceWorker/#cache-matchall and #cache-keys. A cached object a script could
        // edit would look like editing the cache, which it is not.
        var engine = new Engine(options => options.UseCacheApi());
        engine.Execute(TryMutate);

        engine.Evaluate("""
            (async () => {
                const cache = await caches.open('v1');
                await cache.put(
                    new Request('https://example.org/a', { headers: { 'x-existing': '1' } }),
                    new Response('hello', { headers: { 'x-existing': '1' } }));

                const response = await cache.match('https://example.org/a');
                const keys = await cache.keys();
                return tryMutate(response.headers) + ';' + tryMutate(keys[0].headers);
            })()
            """).UnwrapIfPromise().AsString()
            .Should().Be("TypeError/true,TypeError/true,TypeError/true;TypeError/true,TypeError/true,TypeError/true");
    }

    [Fact]
    public void AFetchHandlersRequestStaysMutable()
    {
        // A deliberate divergence, recorded rather than silent. The Service Worker Standard's Handle Fetch
        // creates the FetchEvent's Request "given request, a new Headers object's guard which is 'immutable'"
        // — https://w3c.github.io/ServiceWorker/#on-fetch-request-algorithm — but Jint's inbound Request is
        // built once for both routes, and the other route is Options.WebApi.Fetch's plain handler callback,
        // which no algorithm governs: there the script *is* the endpoint rather than an interceptor watching
        // a request the user agent is already making. Making the two disagree would be worse than either
        // answer, so both carry "request". See FetchHandlerHosting.CreateRequest.
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files));
        engine.Execute(TryMutate + """
            globalThis.handler = {
                fetch(request) {
                    return new Response(tryMutate(request.headers));
                }
            };
            """);
        engine.Advanced.SetFetchHandler(engine.GetValue("handler"));

        var message = new HttpRequestMessage(HttpMethod.Get, "https://example.org/");
        message.Headers.Add("x-existing", "1");

        var operation = engine.Advanced.InvokeFetchHandler(message);
        for (var i = 0; i < 100 && !operation.IsCompleted; i++)
        {
            engine.Advanced.ProcessTasks();
        }

        using var response = operation.GetResult();
        using var stream = response.Content.ReadAsStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        reader.ReadToEnd().Should().Be("ok,ok,ok");
    }
}
#endif
