#if NET8_0_OR_GREATER
#nullable enable

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>fetch</c> as the Fetch Standard specifies it — https://fetch.spec.whatwg.org/#fetch-method — driven
/// against a stub <see cref="HttpMessageHandler"/> so that nothing here touches a network.
/// </summary>
/// <remarks>
/// Most of these tests never wait for anything: the handler answers synchronously, so the promise has settled
/// by the time the drain looks. The two that stream a request body and the two that read a response body do
/// wait, because both of those cross to a thread-pool worker — <c>FetchRequestBodyStream</c> and
/// <c>FetchBodyStream</c> are both channel-driven — and those hand their bodies to
/// <see cref="DedicatedThread.RunAsync"/> so that the wait is not itself holding the worker the transport
/// needs (sebastienros/jint#3213). Every wall-clock window in the class is
/// <see cref="TransportSignalCeiling"/>, a bound only a genuine failure to settle can reach, rather than an
/// interval a loaded runner can lose.
/// </remarks>
public class FetchTests
{
    /// <summary>
    /// How long the helpers below will wait for a fetch to settle. Not a measurement and not a budget: what is
    /// asserted is always what the fetch produced, never how long it took.
    /// </summary>
    private static readonly TimeSpan TransportSignalCeiling = TimeSpan.FromMinutes(2);

    /// <summary>
    /// What one request looked like when the transport sent it.
    /// </summary>
    /// <remarks>
    /// A snapshot rather than the <see cref="HttpRequestMessage"/> itself, because the transport disposes
    /// the message — and with it its content — as soon as the response is in hand. The headers are read
    /// through <c>NonValidated</c>, which answers the raw value rather than <see cref="HttpClient"/>'s
    /// re-serialization of its parsed form.
    /// </remarks>
    /// <param name="ContentLength">
    /// What the request's content answered for its own length, which is not the same question as whether a
    /// <c>Content-Length</c> header was in <paramref name="Headers"/>: this is the value
    /// <see cref="HttpClient"/> frames the request with — a number writes <c>Content-Length</c> and
    /// <see langword="null"/> writes <c>Transfer-Encoding: chunked</c> — and a message that never reaches a
    /// socket is the only place it can be read.
    /// </param>
    private sealed record RecordedRequest(string Method, string Url, Dictionary<string, string> Headers, string? Body, long? ContentLength)
    {
        internal string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// A handler that records what it was asked for and answers whatever the test told it to.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        internal List<RecordedRequest> Requests { get; } = new();

        internal Func<RecordedRequest, HttpResponseMessage>? Responder { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var recorded = await RecordAsync(request, cancellationToken).ConfigureAwait(false);
            Requests.Add(recorded);

            return Responder is { } responder
                ? responder(recorded)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        }

        /// <remarks>
        /// The body is read <b>asynchronously</b>. A streaming request body — the standard's
        /// <c>duplex: 'half'</c> — is produced by the engine thread as the transport drains it, so a
        /// synchronous read here would block this thread waiting on turns the engine has not run yet. That
        /// is why <c>FetchRequestBodyStream</c>'s content refuses synchronous serialization outright.
        /// </remarks>
        private static async Task<RecordedRequest> RecordAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Collect(headers, request.Headers);

            string? body = null;
            long? contentLength = null;
            if (request.Content is { } content)
            {
                Collect(headers, content.Headers);

                // After the collection, deliberately: reading the length is what computes it, and a content
                // that computed one here would look to every other test like a header the engine had set.
                contentLength = content.Headers.ContentLength;
                body = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            return new RecordedRequest(request.Method.Method, request.RequestUri!.ToString(), headers, body, contentLength);
        }

        private static void Collect(Dictionary<string, string> headers, System.Net.Http.Headers.HttpHeaders source)
        {
            foreach (var header in source.NonValidated)
            {
                headers[header.Key] = header.Value.ToString();
            }
        }
    }

    private static Engine WebEngine(HttpMessageHandler handler, Action<Options.FetchOptions>? configure = null)
    {
        return new Engine(options => options.UseFetch(fetch =>
        {
            fetch.HttpClient = new HttpClient(handler);
            configure?.Invoke(fetch);
        }));
    }

    private static JsValue Fetch(HttpMessageHandler handler, string source, Action<Options.FetchOptions>? configure = null)
    {
        return WebEngine(handler, configure).Evaluate(source).UnwrapIfPromise(TransportSignalCeiling);
    }

    [Fact]
    public Task ResolvesWithAResponseCarryingTheStatusAndBody() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("hello"),
            },
        };

        var engine = WebEngine(handler);
        engine.Evaluate("fetch('https://example.org/a').then(r => r.status + ':' + r.ok + ':' + r.type + ':' + r.url + ':' + r.redirected)")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("201:true:basic:https://example.org/a:false");

        engine.Evaluate("fetch('https://example.org/a').then(r => r.text())").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("hello");
    });

    [Fact]
    public void SendsTheMethodHeadersAndBodyTheRequestDescribes()
    {
        var handler = new StubHandler();

        Fetch(handler, "fetch('https://example.org/a', { method: 'post', body: 'hi', headers: { 'x-a': '1' } })");

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be("POST");
        request.Url.Should().Be("https://example.org/a");
        request.Header("x-a").Should().Be("1");

        // The body's implied Content-Type is a content header, so it has to land on the content rather than
        // being dropped.
        request.Header("content-type").Should().Be("text/plain;charset=UTF-8");
        request.Body.Should().Be("hi");
    }

    /// <summary>
    /// A <c>ReadableStream</c> body is streamed to the wire — the standard's <c>duplex: 'half'</c> — and
    /// arrives as the bytes the stream produced, chunked because nothing can compute its length in advance.
    /// </summary>
    [Fact]
    public Task AStreamRequestBodyReachesTheTransport() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler();

        Fetch(handler, @"fetch('https://example.org/a', {
            method: 'POST',
            duplex: 'half',
            body: new ReadableStream({
                start(c) { c.enqueue(new Uint8Array([104])); c.enqueue(new Uint8Array([105])); c.close(); },
            }),
        })");

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be("POST");
        request.Body.Should().Be("hi");

        // No Content-Length: the length is not known when the headers go out, so the body is chunked.
        request.Header("content-length").Should().BeNull();

        // https://fetch.spec.whatwg.org/#concept-bodyinit-extract — the ReadableStream arm implies no type.
        request.Header("content-type").Should().BeNull();
    });

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request step 41: a <c>ReadableStream</c> body without
    /// <c>duplex</c> is a <c>TypeError</c>, which <c>fetch</c> turns into a rejection rather than a throw
    /// because the whole of its synchronous half does.
    /// </summary>
    [Fact]
    public void AStreamRequestBodyWithoutDuplexRejects()
    {
        var handler = new StubHandler();

        Fetch(handler, @"fetch('https://example.org/a', {
            method: 'POST',
            body: new ReadableStream({ start(c) { c.close(); } }),
        }).then(() => 'resolved', e => e.constructor.name)")
            .AsString().Should().Be("TypeError");

        handler.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// A request body stream that errors fails the fetch. The standard's <c>processBodyError</c> steps
    /// (https://fetch.spec.whatwg.org/#concept-http-network-fetch) <i>terminate</i> the fetch controller,
    /// so what the promise rejects with is the one network-error <c>TypeError</c> — not the stream's own
    /// error, which a buffered upload would have propagated verbatim.
    /// </summary>
    [Fact]
    public Task AStreamRequestBodyThatErrorsFailsTheFetch() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler();

        Fetch(handler, @"fetch('https://example.org/a', {
            method: 'POST',
            duplex: 'half',
            body: new ReadableStream({ start(c) { c.error(new TypeError('boom')); } }),
        }).then(() => 'resolved', e => e.constructor.name + ': ' + e.message)")
            .AsString().Should().Be("TypeError: Failed to fetch");
    });

    /// <summary>
    /// https://fetch.spec.whatwg.org/#http-redirect-fetch step 12: "If internalResponse's status is not 303,
    /// request's body is non-null, and request's body's source is null, then return a network error." The
    /// bytes have gone down the first hop's socket and cannot go down a second one.
    /// </summary>
    [Fact]
    public Task AStreamRequestBodyCannotSurviveABodyPreservingRedirect() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler
        {
            Responder = recorded =>
            {
                if (recorded.Url.EndsWith("/a", StringComparison.Ordinal))
                {
                    var redirect = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
                    redirect.Headers.Add("location", "https://example.org/b");
                    return redirect;
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
            },
        };

        Fetch(handler, @"fetch('https://example.org/a', {
            method: 'POST',
            duplex: 'half',
            body: new ReadableStream({ start(c) { c.enqueue(new Uint8Array([104])); c.close(); } }),
        }).then(() => 'resolved', e => e.constructor.name + ': ' + e.message)")
            .AsString().Should().Be("TypeError: Failed to fetch");

        // The first hop was sent; the second never was.
        handler.Requests.Should().ContainSingle().Which.Url.Should().Be("https://example.org/a");
    });

    /// <summary>
    /// A 303 is the exemption the step above carves out: it drops the body along with the method, so there
    /// is nothing left to re-send and the redirect is followed as it would be for any other body.
    /// </summary>
    [Fact]
    public Task AStreamRequestBodyFollowsA303ThatDropsIt() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler
        {
            Responder = recorded =>
            {
                if (recorded.Url.EndsWith("/a", StringComparison.Ordinal))
                {
                    var redirect = new HttpResponseMessage(HttpStatusCode.SeeOther);
                    redirect.Headers.Add("location", "https://example.org/b");
                    return redirect;
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
            },
        };

        Fetch(handler, @"fetch('https://example.org/a', {
            method: 'POST',
            duplex: 'half',
            body: new ReadableStream({ start(c) { c.enqueue(new Uint8Array([104])); c.close(); } }),
        }).then(r => r.status + ':' + r.redirected)")
            .AsString().Should().Be("200:true");

        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].Method.Should().Be("GET");
        handler.Requests[1].Body.Should().BeNull();
    });

    [Fact]
    public void MapsEveryResponseHeaderIncludingRepeatedOnes()
    {
        var handler = new StubHandler
        {
            Responder = _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("x") };
                response.Headers.Add("set-cookie", "a=1");
                response.Headers.Add("set-cookie", "b=2");
                response.Headers.Add("x-multi", "one");
                response.Headers.Add("x-multi", "two");
                return response;
            },
        };

        var engine = WebEngine(handler);
        engine.Execute("var h; fetch('https://example.org/').then(r => h = r.headers);");
        engine.Tasks.ProcessTasks();

        // getSetCookie keeps the values apart, which is the whole reason it exists.
        engine.Evaluate("h.getSetCookie().join('|')").AsString().Should().Be("a=1|b=2");

        // Everything else combines, exactly as get does for a header list built by hand.
        engine.Evaluate("h.get('x-multi')").AsString().Should().Be("one, two");
        engine.Evaluate("h.get('content-type')").AsString().Should().StartWith("text/plain");
    }

    [Fact]
    public void RejectsRatherThanThrowsForAnUnparsableUrl()
    {
        var handler = new StubHandler();

        // https://fetch.spec.whatwg.org/#dom-global-fetch step 2 — the Request constructor's TypeError
        // becomes a rejection, so a fetch chain never needs a try.
        Fetch(handler, "fetch('nonsense').then(() => 'resolved', e => e.constructor.name)")
            .AsString().Should().Be("TypeError");

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public void RejectsForAPreAbortedSignalWithItsOwnReason()
    {
        var handler = new StubHandler();

        // https://fetch.spec.whatwg.org/#abort-fetch — the reason is the signal's, not a fresh error.
        Fetch(handler, "(() => { const c = new AbortController(); c.abort('gone'); return fetch('https://example.org/', { signal: c.signal }).then(() => 'resolved', e => e); })()")
            .AsString().Should().Be("gone");

        // A bare abort() defaults the reason to an AbortError DOMException.
        Fetch(handler, "(() => { const c = new AbortController(); c.abort(); return fetch('https://example.org/', { signal: c.signal }).then(() => 'resolved', e => e.name); })()")
            .AsString().Should().Be("AbortError");

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public void FollowsARedirectAndReportsTheFinalUrl()
    {
        var handler = Redirecting("https://example.org/b");

        var engine = WebEngine(handler);
        engine.Evaluate("fetch('https://example.org/a').then(r => r.url + ':' + r.redirected + ':' + r.status)")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("https://example.org/b:true:200");

        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].Url.Should().Be("https://example.org/b");
    }

    [Fact]
    public void ResolvesARelativeLocationAgainstTheUrlThatSentIt()
    {
        var handler = Redirecting("/b?q");

        Fetch(handler, "fetch('https://example.org/deep/a').then(r => r.url)")
            .AsString().Should().Be("https://example.org/b?q");
    }

    [Fact]
    public void RewritesPostToGetOnA303AndDropsTheBody()
    {
        // https://fetch.spec.whatwg.org/#http-redirect-fetch step 11.
        var handler = Redirecting("https://example.org/b", HttpStatusCode.SeeOther);

        Fetch(handler, "fetch('https://example.org/a', { method: 'POST', body: 'hi' }).then(r => r.status)");

        handler.Requests[0].Method.Should().Be("POST");
        handler.Requests[1].Method.Should().Be("GET");
        handler.Requests[1].Body.Should().BeNull();

        // The body headers go with the body.
        handler.Requests[1].Header("content-type").Should().BeNull();
    }

    [Fact]
    public void RewritesPostToGetOnA302ButNotOnA307()
    {
        var to302 = Redirecting("https://example.org/b", HttpStatusCode.Found);
        Fetch(to302, "fetch('https://example.org/a', { method: 'POST', body: 'hi' }).then(r => r.status)");
        to302.Requests[1].Method.Should().Be("GET");

        // 307 and 308 are the two that keep the method and the body.
        var to307 = Redirecting("https://example.org/b", HttpStatusCode.TemporaryRedirect);
        Fetch(to307, "fetch('https://example.org/a', { method: 'POST', body: 'hi' }).then(r => r.status)");
        to307.Requests[1].Method.Should().Be("POST");
        to307.Requests[1].Body.Should().Be("hi");
    }

    [Fact]
    public void StripsCredentialHeadersWhenARedirectCrossesOrigin()
    {
        var handler = Redirecting("https://other.example/b");

        Fetch(handler, "fetch('https://example.org/a', { headers: { authorization: 'Bearer secret', cookie: 'sid=1', 'x-keep': 'yes' } }).then(r => r.status)");

        handler.Requests[1].Header("authorization").Should().BeNull();
        handler.Requests[1].Header("cookie").Should().BeNull();
        handler.Requests[1].Header("x-keep").Should().Be("yes");
    }

    [Fact]
    public void KeepsCredentialHeadersOnASameOriginRedirect()
    {
        var handler = Redirecting("https://example.org/b");

        Fetch(handler, "fetch('https://example.org/a', { headers: { authorization: 'Bearer secret' } }).then(r => r.status)");

        handler.Requests[1].Header("authorization").Should().Be("Bearer secret");
    }

    [Fact]
    public void HonoursTheThreeRedirectModes()
    {
        // 'error' refuses outright.
        Fetch(Redirecting("https://example.org/b"), "fetch('https://example.org/a', { redirect: 'error' }).then(() => 'resolved', e => e.constructor.name + ': ' + e.message)")
            .AsString().Should().Be("TypeError: Failed to fetch");

        // 'manual' hands the redirect response itself to the script — Node's reading, not a browser's
        // opaque-redirect filtered response.
        var manual = Redirecting("https://example.org/b");
        Fetch(manual, "fetch('https://example.org/a', { redirect: 'manual' }).then(r => r.status + ':' + r.redirected + ':' + r.headers.get('location'))")
            .AsString().Should().Be("301:false:https://example.org/b");
        manual.Requests.Should().ContainSingle();
    }

    [Fact]
    public void ARedirectWithoutALocationIsAnOrdinaryResponse()
    {
        // "If locationURL is null, then return actualResponse."
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Content = new StringContent("body") },
        };

        Fetch(handler, "fetch('https://example.org/a').then(r => r.status + ':' + r.redirected)")
            .AsString().Should().Be("301:false");
    }

    [Fact]
    public void RefusesMoreRedirectsThanTheLimitAllows()
    {
        var handler = new StubHandler
        {
            Responder = _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Found);
                response.Headers.TryAddWithoutValidation("location", "https://example.org/" + Guid.NewGuid().ToString("N"));
                return response;
            },
        };

        Fetch(handler, "fetch('https://example.org/a').then(() => 'resolved', e => e.constructor.name + '|' + e.message)", f => f.MaxRedirects = 3)
            .AsString().Should().Be("TypeError|Failed to fetch: The request to 'https://example.org/a' exceeded the limit of 3 redirects.");

        // Four requests: the first plus the three the limit allowed.
        handler.Requests.Should().HaveCount(4);
    }

    [Fact]
    public void ANullBodyStatusCarriesNoBody()
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.NoContent),
        };

        var engine = WebEngine(handler);
        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Tasks.ProcessTasks();

        engine.Evaluate("r.status").AsNumber().Should().Be(204);
        engine.Evaluate("r.text()").UnwrapIfPromise().AsString().Should().Be("");

        // A null body is never disturbed, so it reads as often as the script likes.
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeFalse();
        engine.Evaluate("r.text()").UnwrapIfPromise().AsString().Should().Be("");
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-fetch step 12 — the <c>Accept</c> a request that named none of
    /// its own is given, and <c>*/*</c> is the value for every destination but the handful a browser knows.
    /// </summary>
    [Fact]
    public void SendsTheDefaultAcceptAndLeavesAScriptSetOneAlone()
    {
        var handler = new StubHandler();
        Fetch(handler, "fetch('https://example.org/a').then(r => r.status)");
        handler.Requests.Should().ContainSingle().Which.Header("accept").Should().Be("*/*");

        // Step 13, the Accept-Language beside it, applies only "if request's client is non-null" and reports
        // a user's language preferences. There is neither here, so nothing is sent.
        handler.Requests[0].Header("accept-language").Should().BeNull();

        var named = new StubHandler();
        Fetch(named, "fetch('https://example.org/a', { headers: { accept: 'custom/*' } }).then(r => r.status)");
        named.Requests.Should().ContainSingle().Which.Header("accept").Should().Be("custom/*");
    }

    [Fact]
    public void TheDefaultAcceptIsNotVisibleOnTheRequestTheScriptHolds()
    {
        // The step appends to the header list fetch is working with, which is a copy: a browser sends
        // Accept: */* while the script's own Request still answers null for it.
        Fetch(new StubHandler(), "(() => { const r = new Request('https://example.org/a'); return fetch(r).then(() => String(r.headers.get('accept'))); })()")
            .AsString().Should().Be("null");
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-header-list is one list, so a content header a script set
    /// reaches the wire whether or not the request has a body — which is what the BCL's split of the same
    /// list into request headers and content headers stood in the way of.
    /// </summary>
    [Fact]
    public void CarriesContentHeadersOnARequestThatHasNoBody()
    {
        var handler = new StubHandler();

        Fetch(handler, @"fetch('https://example.org/a', {
            method: 'GET',
            headers: { 'Content-Encoding': 'Identity', 'Content-Language': 'en-US', 'Content-Location': 'foo' },
        }).then(r => r.status)");

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Header("content-encoding").Should().Be("Identity");
        request.Header("content-language").Should().Be("en-US");
        request.Header("content-location").Should().Be("foo");

        // https://fetch.spec.whatwg.org/#concept-http-network-or-cache-fetch step 8 appends Content-Length
        // only for a body, or for a bodiless POST or PUT — so the content this GET had to be given to carry
        // those three headers must not announce a length of its own, and it sends nothing.
        request.ContentLength.Should().BeNull();
        request.Body.Should().BeEmpty();
    }

    [Fact]
    public void ABodilessPostStillAnnouncesTheZeroLengthTheStandardGivesIt()
    {
        var handler = new StubHandler();

        Fetch(handler, "fetch('https://example.org/a', { method: 'POST', headers: { 'content-type': 'application/json' } }).then(r => r.status)");

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Header("content-type").Should().Be("application/json");

        // "If httpRequest's body is null and httpRequest's method is `POST` or `PUT`, then set
        // contentLengthHeaderValue to `0`" — which is also what a bodiless POST sent before it had anywhere
        // to put a Content-Type.
        request.ContentLength.Should().Be(0);
    }

    [Fact]
    public void ARequestWithNoBodyDoesNotCarryAScriptSetContentLength()
    {
        var handler = new StubHandler();

        // Content-Length is what a request body is framed with, and this request has none. Browsers refuse
        // the name outright as a forbidden request-header; Jint declines to enforce that list (see
        // HeadersGuard) and drops the value at the wire instead, exactly as it did before the header list
        // had any content of its own to be carried on.
        Fetch(handler, "fetch('https://example.org/a', { headers: { 'Content-Language': 'en-US', 'Content-Length': '99' } }).then(r => r.status)");

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Header("content-language").Should().Be("en-US");
        request.Header("content-length").Should().BeNull();
        request.ContentLength.Should().BeNull();
    }

    [Fact]
    public void AResponseToHeadCarriesNoBody()
    {
        // https://fetch.spec.whatwg.org/#concept-main-fetch step 22: "If response is not a network error and
        // either request's method is `HEAD` or `CONNECT`, or internalResponse's status is a null body status,
        // set internalResponse's body to null and disregard any enqueuing toward it (if any)." A server that
        // answers HEAD with bytes is what the step exists to standardise the handling of.
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("hello-world") },
        };

        var engine = WebEngine(handler);
        engine.Execute("var r; fetch('https://example.org/', { method: 'HEAD' }).then(x => r = x);");
        engine.Tasks.ProcessTasks();

        engine.Evaluate("r.status").AsNumber().Should().Be(200);
        engine.Evaluate("r.body === null").AsBoolean().Should().BeTrue();
        engine.Evaluate("r.text()").UnwrapIfPromise().AsString().Should().Be("");

        // Nothing was disturbed, so it reads as often as the script likes — the same property a 204 has.
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeFalse();
        engine.Evaluate("r.text()").UnwrapIfPromise().AsString().Should().Be("");

        // The headers stay as they came: a HEAD answers the length of the representation it describes, and
        // that it describes bytes it does not send is the whole point of asking with HEAD.
        engine.Evaluate("r.headers.get('content-length')").AsString().Should().Be("11");
    }

    [Fact]
    public void AHeadOfSomethingLargerThanTheCapIsNotRefused()
    {
        // The cap bounds what a response spends, and a HEAD spends nothing: refusing one for the length it
        // reports would fail the one request that is asking precisely so as not to transfer it.
        var handler = new StubHandler
        {
            Responder = _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
                response.Content.Headers.ContentLength = 5_000_000;
                return response;
            },
        };

        var engine = WebEngine(handler, fetch => fetch.MaxResponseBytes = 1024);
        engine.Execute("var r; fetch('https://example.org/', { method: 'HEAD' }).then(x => r = x, e => r = e);");
        engine.Tasks.ProcessTasks();

        engine.Evaluate("r.status").AsNumber().Should().Be(200);
        engine.Evaluate("r.headers.get('content-length')").AsString().Should().Be("5000000");

        // The same length on a GET is still refused before the promise settles.
        Fetch(handler, "fetch('https://example.org/').then(() => 'resolved', e => e.constructor.name)", fetch => fetch.MaxResponseBytes = 1024)
            .AsString().Should().Be("TypeError");
    }

    [Fact]
    public Task TheResponseBodyObeysTheUsualConsumeRules() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"a\":1}") },
        };

        var engine = WebEngine(handler);
        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Tasks.ProcessTasks();

        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeFalse();

        // A network response streams: body is the live ReadableStream, not null.
        engine.Evaluate("Object.prototype.toString.call(r.body)").AsString().Should().Be("[object ReadableStream]");

        // clone() tees, so each object gets its own branch — https://fetch.spec.whatwg.org/#concept-body-clone.
        engine.Execute("var copy = r.clone();");
        engine.Evaluate("r.body === copy.body").AsBoolean().Should().BeFalse();

        engine.Evaluate("r.json()").UnwrapIfPromise(TransportSignalCeiling).AsObject().Get("a").AsNumber().Should().Be(1);
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeTrue();
        engine.Evaluate("r.text().then(() => 'resolved', e => e.constructor.name)").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("TypeError");

        // The clone carries its own flag over the same bytes.
        engine.Evaluate("copy.bodyUsed").AsBoolean().Should().BeFalse();
        engine.Evaluate("copy.text()").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("{\"a\":1}");
    });

    [Fact]
    public void ANetworkFailureRejectsWithAnUninformativeTypeError()
    {
        var handler = new ThrowingHandler();

        // A message naming the DNS failure or the refused connection would let a script map the host's
        // internal network by probing it; the CLR exception rides the error value instead, for the host.
        Fetch(handler, "fetch('https://example.org/').then(() => 'resolved', e => e.constructor.name + ': ' + e.message)")
            .AsString().Should().Be("TypeError: Failed to fetch");
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("no such host is known");
    }

    private static StubHandler Redirecting(string location, HttpStatusCode status = HttpStatusCode.MovedPermanently)
    {
        var handler = new StubHandler();
        handler.Responder = _ =>
        {
            if (handler.Requests.Count > 1)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("done") };
            }

            var response = new HttpResponseMessage(status);
            response.Headers.TryAddWithoutValidation("location", location);
            return response;
        };

        return handler;
    }
}
#endif
