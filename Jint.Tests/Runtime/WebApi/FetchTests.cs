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
public class FetchTests
{
    /// <summary>
    /// What one request looked like when the transport sent it.
    /// </summary>
    /// <remarks>
    /// A snapshot rather than the <see cref="HttpRequestMessage"/> itself, because the transport disposes
    /// the message — and with it its content — as soon as the response is in hand. The headers are read
    /// through <c>NonValidated</c>, which answers the raw value rather than <see cref="HttpClient"/>'s
    /// re-serialization of its parsed form.
    /// </remarks>
    private sealed record RecordedRequest(string Method, string Url, Dictionary<string, string> Headers, string? Body)
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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var recorded = Record(request);
            Requests.Add(recorded);

            var response = Responder is { } responder
                ? responder(recorded)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };

            return Task.FromResult(response);
        }

        private static RecordedRequest Record(HttpRequestMessage request)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Collect(headers, request.Headers);

            string? body = null;
            if (request.Content is { } content)
            {
                Collect(headers, content.Headers);
                using var reader = new StreamReader(content.ReadAsStream());
                body = reader.ReadToEnd();
            }

            return new RecordedRequest(request.Method.Method, request.RequestUri!.ToString(), headers, body);
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
        return WebEngine(handler, configure).Evaluate(source).UnwrapIfPromise();
    }

    [Fact]
    public void ResolvesWithAResponseCarryingTheStatusAndBody()
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
            .UnwrapIfPromise().AsString().Should().Be("201:true:basic:https://example.org/a:false");

        engine.Evaluate("fetch('https://example.org/a').then(r => r.text())").UnwrapIfPromise().AsString().Should().Be("hello");
    }

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

    [Fact]
    public void AStreamRequestBodyIsReadInFullBeforeAnythingIsSent()
    {
        // Streaming uploads (the standard's `duplex: "half"`) are out of scope: the stream is drained first
        // and the request carries the bytes it produced.
        var handler = new StubHandler();

        Fetch(handler, @"fetch('https://example.org/a', {
            method: 'POST',
            body: new ReadableStream({
                start(c) { c.enqueue(new Uint8Array([104])); c.enqueue(new Uint8Array([105])); c.close(); },
            }),
        })");

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be("POST");
        request.Body.Should().Be("hi");

        // https://fetch.spec.whatwg.org/#concept-bodyinit-extract — the ReadableStream arm implies no type.
        request.Header("content-type").Should().BeNull();
    }

    [Fact]
    public void AStreamRequestBodyThatErrorsRejectsBeforeASocketIsOpened()
    {
        var handler = new StubHandler();

        Fetch(handler, @"fetch('https://example.org/a', {
            method: 'POST',
            body: new ReadableStream({ start(c) { c.error(new TypeError('boom')); } }),
        }).then(() => 'resolved', e => e.constructor.name + ': ' + e.message)")
            .AsString().Should().Be("TypeError: boom");

        handler.Requests.Should().BeEmpty();
    }

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
        engine.Advanced.ProcessTasks();

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
            .UnwrapIfPromise().AsString().Should().Be("https://example.org/b:true:200");

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
        engine.Advanced.ProcessTasks();

        engine.Evaluate("r.status").AsNumber().Should().Be(204);
        engine.Evaluate("r.text()").UnwrapIfPromise().AsString().Should().Be("");

        // A null body is never disturbed, so it reads as often as the script likes.
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeFalse();
        engine.Evaluate("r.text()").UnwrapIfPromise().AsString().Should().Be("");
    }

    [Fact]
    public void TheResponseBodyObeysTheUsualConsumeRules()
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"a\":1}") },
        };

        var engine = WebEngine(handler);
        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Advanced.ProcessTasks();

        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeFalse();

        // A network response streams: body is the live ReadableStream, not null.
        engine.Evaluate("Object.prototype.toString.call(r.body)").AsString().Should().Be("[object ReadableStream]");

        // clone() tees, so each object gets its own branch — https://fetch.spec.whatwg.org/#concept-body-clone.
        engine.Execute("var copy = r.clone();");
        engine.Evaluate("r.body === copy.body").AsBoolean().Should().BeFalse();

        engine.Evaluate("r.json()").UnwrapIfPromise().AsObject().Get("a").AsNumber().Should().Be(1);
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeTrue();
        engine.Evaluate("r.text().then(() => 'resolved', e => e.constructor.name)").UnwrapIfPromise().AsString().Should().Be("TypeError");

        // The clone carries its own flag over the same bytes.
        engine.Evaluate("copy.bodyUsed").AsBoolean().Should().BeFalse();
        engine.Evaluate("copy.text()").UnwrapIfPromise().AsString().Should().Be("{\"a\":1}");
    }

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
