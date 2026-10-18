#if NET8_0_OR_GREATER
#nullable enable

#pragma warning disable JINT0002 // the fetch observer is a preview surface

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.WebApi.Fetch;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>data</c> arm of scheme fetch — https://fetch.spec.whatwg.org/#scheme-fetch — as <c>fetch</c> and
/// <c>XMLHttpRequest</c> reach it.
/// </summary>
/// <remarks>
/// <para>
/// Every engine here has a host <see cref="HttpClient"/> whose handler records what reaches it, and the
/// assertion beside every answer is that nothing did: a <c>data:</c> URL is answered by the engine itself, so a
/// request that got as far as a transport would be the defect. The arm settles its <c>fetch</c> promise
/// synchronously and queues one task for an asynchronous <c>XMLHttpRequest</c>, so nothing here waits on a
/// thread-pool completion; <see cref="Ceiling"/> is a bound only a failure to settle at all can reach.
/// </para>
/// <para>
/// The processor's own steps — where the base64 marker ends, the MIME type defaults, the failures, and the
/// ceiling measured before decoding — are <see cref="DataUrlTests"/>'.
/// </para>
/// </remarks>
public class DataUrlFetchTests
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A transport that records every request it is handed. A <c>data:</c> URL must never be one of them.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        internal ConcurrentQueue<string> Requests { get; } = new();

        internal Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request.RequestUri!.OriginalString);

            return Task.FromResult(Responder?.Invoke(request)
                ?? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("from the network") });
        }
    }

    /// <summary>Counts every notification and every ask an observer can receive.</summary>
    private sealed class CountingObserver : FetchObserver
    {
        private int _calls;

        internal int Calls => Volatile.Read(ref _calls);

        private void Count() => Interlocked.Increment(ref _calls);

        public override ValueTask<FetchInterception?> OnRequestAsync(ObservedFetchRequest request, CancellationToken cancellationToken)
        {
            Count();
            return base.OnRequestAsync(request, cancellationToken);
        }

        public override void OnResponse(ObservedFetchResponse response) => Count();

        public override ValueTask<FetchResponseInterception?> OnInterceptedResponseAsync(FetchResponseInterceptionContext context, CancellationToken cancellationToken)
        {
            Count();
            return base.OnInterceptedResponseAsync(context, cancellationToken);
        }

        public override ValueTask<FetchAuthDecision?> OnAuthRequiredAsync(ObservedFetchAuthChallenge challenge, CancellationToken cancellationToken)
        {
            Count();
            return base.OnAuthRequiredAsync(challenge, cancellationToken);
        }

        public override void OnData(FetchRequestId id, ReadOnlySpan<byte> chunk) => Count();

        public override void OnCompleted(FetchRequestId id, long bodyLength) => Count();

        public override void OnFailed(FetchRequestId id, string reason, Exception? exception) => Count();
    }

    private static Engine FetchEngine(RecordingHandler handler, Action<Options.FetchOptions>? configure = null)
        => new(options => options.UseFetch(fetch =>
        {
            fetch.HttpClient = new HttpClient(handler);
            configure?.Invoke(fetch);
        }));

    private static string Run(Engine engine, string script) => engine.Evaluate(script).UnwrapIfPromise(Ceiling).AsString();

    /// <summary>What a fetch settled with, as one string: the rejection's text, or what <paramref name="onResolved"/> made of the response.</summary>
    private static string Outcome(Engine engine, string fetchCall, string onResolved = "r => r.text()")
        => Run(engine, $"{fetchCall}.then({onResolved}).then(v => String(v), e => String(e))");

    // ---------------------------------------------------------------- fetch: the response

    /// <summary>
    /// Steps 3 and 4 of the arm: 200, <c>OK</c>, one header — the serialized MIME type, and no
    /// <c>Content-Length</c> — and the decoded bytes. Main fetch makes it a basic response, and nothing
    /// redirected, so its URL is the request's.
    /// </summary>
    [Test]
    public void ABase64UrlIsAnsweredWithA200OkCarryingOnlyItsContentType()
    {
        var handler = new RecordingHandler();
        var engine = FetchEngine(handler);

        Run(engine, """
            (async () => {
              const r = await fetch('data:text/plain;base64,SGVsbG8=');
              return [r.status, r.statusText, r.ok, r.type, r.redirected, r.url, JSON.stringify([...r.headers]), await r.text()].join('|');
            })()
            """).Should().Be("""200|OK|true|basic|false|data:text/plain;base64,SGVsbG8=|[["content-type","text/plain"]]|Hello""");

        handler.Requests.Should().BeEmpty();
    }

    [Test]
    public void APercentEncodedUrlKeepsItsMimeTypeAndItsParameters()
    {
        var engine = FetchEngine(new RecordingHandler());

        Outcome(engine, "fetch('data:text/html;charset=utf-8,%3Cp%3Ecaf%C3%A9%3C/p%3E')",
                "async r => r.headers.get('content-type') + '|' + await r.text()")
            .Should().Be("text/html;charset=utf-8|<p>café</p>");
    }

    /// <summary>Step 14: no MIME type at all is <c>text/plain;charset=US-ASCII</c>.</summary>
    [Test]
    public void AUrlNamingNoMimeTypeIsUsAsciiText()
    {
        var engine = FetchEngine(new RecordingHandler());

        Outcome(engine, "fetch('data:,a%20b')", "async r => r.headers.get('content-type') + '|' + await r.text()")
            .Should().Be("text/plain;charset=US-ASCII|a b");
    }

    /// <summary>
    /// The fragment is no part of the body — the processor serializes without it — and no part of the
    /// response's URL either, which <c>Response.url</c> serializes without one.
    /// </summary>
    [Test]
    public void TheFragmentIsNeitherTheBodyNorPartOfTheResponseUrl()
    {
        var engine = FetchEngine(new RecordingHandler());

        Outcome(engine, "fetch('data:,before#after')", "async r => r.url + '|' + await r.text()")
            .Should().Be("data:,before|before");
    }

    /// <summary>
    /// Step 2: the processor's failure is a network error — the same uninformative <c>TypeError</c> any other
    /// network error is.
    /// </summary>
    [TestCase("data:no-comma-at-all")]
    [TestCase("data:;base64,a")]
    [TestCase("data:text/plain;base64,%%%")]
    public void AUrlTheProcessorRefusesIsANetworkError(string url)
    {
        var handler = new RecordingHandler();
        var engine = FetchEngine(handler);

        Outcome(engine, $"fetch('{url}')").Should().Be("TypeError: Failed to fetch");
        handler.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// Unlike the <c>blob</c> arm, which answers <c>GET</c> alone, the <c>data</c> arm names no method:
    /// <c>fetch/api/basic/scheme-data.any.js</c> POSTs and expects the body. The request's own body goes
    /// nowhere, because there is nowhere for it to go.
    /// </summary>
    [TestCase("POST")]
    [TestCase("PUT")]
    [TestCase("DELETE")]
    public void AnyMethodIsAnsweredAndTheRequestBodyGoesNowhere(string method)
    {
        var handler = new RecordingHandler();
        var engine = FetchEngine(handler);

        var body = method == "DELETE" ? "" : ", body: 'not sent anywhere'";
        Outcome(engine, $"fetch('data:,hello', {{ method: '{method}'{body} }})", "async r => r.status + '|' + await r.text()")
            .Should().Be("200|hello");

        handler.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// Main fetch gives a <c>HEAD</c> no body, and a response built in memory never reaches the transport
    /// that applies that rule — so the arm applies it: the headers, and a null body rather than an empty one.
    /// </summary>
    [Test]
    public void AHeadGetsTheContentTypeAndNoBodyAtAll()
    {
        var engine = FetchEngine(new RecordingHandler());

        Outcome(engine, "fetch('data:,hello', { method: 'HEAD' })",
                "async r => [r.status, r.body === null, r.headers.get('content-type'), JSON.stringify(await r.text())].join('|')")
            .Should().Be("200|true|text/plain;charset=US-ASCII|\"\"");
    }

    // ---------------------------------------------------------------- fetch: the size ceiling

    /// <summary>
    /// <c>Options.WebApi.Fetch.MaxResponseBytes</c> bounds the <i>decoded</i> body, inclusive, whatever the
    /// encoding — and over it the failure carries the transport's own message, which
    /// <c>FetchOperation.NetworkError</c> lets through because it names the host's limit and nothing else.
    /// </summary>
    [TestCase("data:,hello", "hello")]
    [TestCase("data:,%68%65%6C%6C%6F", "hello")]
    [TestCase("data:;base64,aGVsbG8=", "hello")]
    [TestCase("data:;base64,aGVs%20bG8", "hello")]
    [TestCase("data:,hello!", null)]
    [TestCase("data:,%68%65%6C%6C%6F%21", null)]
    [TestCase("data:;base64,aGVsbG8h", null)]
    public void MaxResponseBytesBoundsTheDecodedBodyAtTheBoundary(string url, string? expected)
    {
        var engine = FetchEngine(new RecordingHandler(), fetch => fetch.MaxResponseBytes = 5);

        Outcome(engine, $"fetch('{url}')").Should().Be(expected
            ?? "TypeError: Failed to fetch: The response body exceeded the 5 byte limit set by Options.WebApi.Fetch.MaxResponseBytes.");
    }

    /// <summary>A <c>HEAD</c> reads no body, so it is never refused for one's length.</summary>
    [Test]
    public void AHeadIsNeverRefusedForItsLength()
    {
        var engine = FetchEngine(new RecordingHandler(), fetch => fetch.MaxResponseBytes = 5);

        Outcome(engine, "fetch('data:,far more than five bytes', { method: 'HEAD' })", "r => r.status").Should().Be("200");
    }

    // ---------------------------------------------------------------- fetch: what does not apply

    /// <summary>
    /// fetch()'s step for an already-aborted signal precedes main fetch and therefore scheme fetch, so it
    /// wins over the arm — over a URL the arm would answer and over one it would refuse alike.
    /// </summary>
    [TestCase("data:,hello")]
    [TestCase("data:no-comma-at-all")]
    public void AnAlreadyAbortedSignalRejectsWithItsReasonBeforeTheArm(string url)
    {
        var observer = new CountingObserver();
        var engine = FetchEngine(new RecordingHandler(), fetch => fetch.Observer = observer);

        Run(engine, $$"""
            (() => {
              const controller = new AbortController();
              controller.abort('stopped');
              return fetch('{{url}}', { signal: controller.signal }).then(() => 'resolved', e => String(e));
            })()
            """).Should().Be("stopped");

        observer.Calls.Should().Be(0, "a data: URL is no request for an observer to be told about, not even as a refusal");
    }

    /// <summary>
    /// The bytes are in the URL, so the destination policy has nothing to decide: a scheme list without
    /// <c>data</c>, a filter that refuses everything and a concurrency cap of zero all leave it working —
    /// the filter is never even asked — while the same engine still refuses a network URL.
    /// </summary>
    [Test]
    public void TheSchemeListTheUrlFilterAndTheConcurrencyCapDoNotApply()
    {
        var handler = new RecordingHandler();
        var filterCalls = 0;
        var engine = FetchEngine(handler, fetch =>
        {
            fetch.AllowedSchemes.Clear();
            fetch.AllowedSchemes.Add("https");
            fetch.UrlFilter = _ =>
            {
                Interlocked.Increment(ref filterCalls);
                return false;
            };
            fetch.MaxConcurrentRequests = 0;
        });

        Outcome(engine, "fetch('data:,hello')").Should().Be("hello");
        filterCalls.Should().Be(0);

        // The control: the same engine, a URL that would reach the transport.
        Outcome(engine, "fetch('https://example.org/')").Should().Be("TypeError: Failed to fetch");
        filterCalls.Should().Be(1);
        handler.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// No request happens, so an observer hears nothing — not of an answer, not of the processor's refusal,
    /// not of a body over the ceiling. The control proves the observer is wired: a refused network URL is
    /// reported to it.
    /// </summary>
    [Test]
    public void AnObserverIsToldNothing()
    {
        var observer = new CountingObserver();
        var engine = FetchEngine(new RecordingHandler(), fetch =>
        {
            fetch.Observer = observer;
            fetch.MaxResponseBytes = 5;
            fetch.UrlFilter = uri => uri.Scheme != "https";
        });

        Outcome(engine, "fetch('data:,hello')").Should().Be("hello");
        Outcome(engine, "fetch('data:no-comma-at-all')").Should().Be("TypeError: Failed to fetch");
        Outcome(engine, "fetch('data:,more than five')").Should().StartWith("TypeError: Failed to fetch: The response body exceeded");
        observer.Calls.Should().Be(0);

        Outcome(engine, "fetch('https://example.org/')").Should().Be("TypeError: Failed to fetch");
        observer.Calls.Should().Be(1, "a refusal before the transport is reported as OnFailed alone");
    }

    // ---------------------------------------------------------------- fetch: redirects

    /// <summary>
    /// The arm answers the request's own URL and never a <c>Location</c>: HTTP-redirect fetch step 6 makes a
    /// redirect to any scheme that is not HTTP(S) a network error, which is what
    /// <c>fetch/api/redirect/redirect-to-dataurl.any.js</c> expects in every mode. It holds with <c>data</c>
    /// on the scheme list as well — the redirect is refused rather than handed to the host's client.
    /// </summary>
    [TestCase(false, "data:,hello")]
    [TestCase(true, "data:,hello")]
    [TestCase(true, "blob:null/0b1c4b1e-5f0e-4b8a-9d3e-6f1d2c3b4a59")]
    public void ARedirectToALocalSchemeIsANetworkError(bool schemeListed, string location)
    {
        var handler = new RecordingHandler
        {
            Responder = request => request.RequestUri!.Scheme == "https"
                ? Redirect(location)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("from the network") },
        };

        var engine = FetchEngine(handler, fetch =>
        {
            if (schemeListed)
            {
                fetch.AllowedSchemes.Add("data");
                fetch.AllowedSchemes.Add("blob");
            }
        });

        Outcome(engine, "fetch('https://example.org/redirect')").Should().Be("TypeError: Failed to fetch");
        handler.Requests.Should().Equal("https://example.org/redirect");

        // A manual redirect is not followed at all, so it hands the script the 302 as it always did.
        Outcome(engine, "fetch('https://example.org/redirect', { redirect: 'manual' })", "r => r.status + '|' + r.headers.get('location')")
            .Should().Be("302|" + location);

        static HttpResponseMessage Redirect(string location)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.TryAddWithoutValidation("Location", location);
            return response;
        }
    }

    // ---------------------------------------------------------------- XMLHttpRequest

    /// <summary>
    /// The interface alone, no network grant: a <c>data:</c> URL is answered anyway, exactly as a blob URL is.
    /// </summary>
    private static Engine XhrEngine(Action<Options.FetchOptions>? configure = null)
        => new(options => options.UseXmlHttpRequest(fetch => configure?.Invoke(fetch)));

    private static void PumpUntilDone(Engine engine)
    {
        var deadline = DateTime.UtcNow + Ceiling;
        while (engine.Evaluate("xhr.readyState").AsNumber() != 4)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The request never reached DONE. The log holds: " + engine.Evaluate("JSON.stringify(log)").AsString());
            }

            engine.Tasks.ProcessTasks();
            Thread.Sleep(1);
        }
    }

    private const string Logging = """
        var log = [];
        var xhr = new XMLHttpRequest();
        xhr.onreadystatechange = () => log.push('rs' + xhr.readyState);
        for (const t of ['loadstart', 'progress', 'load', 'error', 'loadend']) {
          xhr.addEventListener(t, e => log.push(t + ':' + e.loaded + '/' + e.total + '/' + e.lengthComputable));
        }
        """;

    /// <summary>
    /// The whole of an asynchronous request: every event, in order, and every attribute the response is read
    /// through. The response names no <c>Content-Length</c>, so the progress events report a total of 0 and
    /// are not length-computable — https://xhr.spec.whatwg.org/#dom-xmlhttprequest-send extracts the length
    /// from the header list.
    /// </summary>
    [Test]
    public void AnAsynchronousRequestIsAnsweredWithoutANetworkGrant()
    {
        var engine = XhrEngine();

        engine.Execute(Logging + """
            xhr.open('GET', 'data:text/plain;charset=utf-8,caf%C3%A9#fragment');
            xhr.send();
            """);

        PumpUntilDone(engine);

        engine.Evaluate("log.join()").AsString().Should().Be(
            "rs1,loadstart:0/0/false,rs2,rs3,progress:5/0/false,progress:5/0/false,rs4,load:5/0/false,loadend:5/0/false");

        engine.Evaluate("""
            [xhr.status, xhr.statusText, xhr.responseURL, JSON.stringify(xhr.getAllResponseHeaders()),
             xhr.getResponseHeader('content-length'), xhr.responseText].join('|')
            """).AsString().Should().Be("""200|OK|data:text/plain;charset=utf-8,caf%C3%A9|"content-type: text/plain;charset=utf-8\r\n"||café""");
    }

    /// <summary>
    /// The same bytes through each response type, the JSON one with the BOM and the duplicate key
    /// <c>xhr/json.any.js</c> uses to tell the right parser from the wrong one.
    /// </summary>
    [TestCase("text", "data:,hello", "xhr.response")]
    [TestCase("json", "data:,\\uFEFF{ \"b\": 1, \"a\": 2, \"b\": 3 }", "Object.keys(xhr.response).join() + '|' + xhr.response.b")]
    [TestCase("arraybuffer", "data:;base64,AAEC/w==", "Array.from(new Uint8Array(xhr.response)).join()")]
    [TestCase("blob", "data:image/png;base64,AAEC", "xhr.response.type + '|' + xhr.response.size")]
    public void EveryResponseTypeReadsTheDecodedBytes(string responseType, string url, string read)
    {
        var expected = responseType switch
        {
            "text" => "hello",
            "json" => "b,a|3",
            "arraybuffer" => "0,1,2,255",
            _ => "image/png|3",
        };

        var engine = XhrEngine();

        engine.Execute(Logging + $"""
            xhr.responseType = '{responseType}';
            xhr.open('GET', '{url}');
            xhr.send();
            """);

        PumpUntilDone(engine);
        engine.Evaluate(read).AsString().Should().Be(expected);
    }

    /// <summary>
    /// A synchronous request is answered before <c>send()</c> returns, with no task and no wait, and fires
    /// what the synchronous flag fires: no <c>HEADERS_RECEIVED</c>, no <c>LOADING</c>, no progress.
    /// </summary>
    [Test]
    public void ASynchronousRequestIsAnsweredBeforeSendReturns()
    {
        var engine = XhrEngine();

        engine.Execute(Logging + """
            xhr.open('GET', 'data:,hello', false);
            xhr.send();
            """);

        engine.Evaluate("[xhr.readyState, xhr.status, xhr.responseText, log.join()].join('|')").AsString()
            .Should().Be("4|200|hello|rs1,rs4,load:5/0/false,loadend:5/0/false");
    }

    [TestCase("HEAD", "200||text/plain;charset=US-ASCII")]
    [TestCase("POST", "200|hello|text/plain;charset=US-ASCII")]
    public void AnyMethodIsAnsweredAndAHeadHasNoBody(string method, string expected)
    {
        var engine = XhrEngine();

        engine.Execute(Logging + $"""
            xhr.open('{method}', 'data:,hello');
            xhr.send('not sent anywhere');
            """);

        PumpUntilDone(engine);
        engine.Evaluate("[xhr.status, xhr.responseText, xhr.getResponseHeader('Content-Type')].join('|')").AsString().Should().Be(expected);
    }

    /// <summary>
    /// A URL the processor refuses and a body over <c>MaxResponseBytes</c> are both a network error: the
    /// <c>error</c> event for an asynchronous request, a <c>NetworkError</c> thrown out of <c>send()</c> for a
    /// synchronous one.
    /// </summary>
    [TestCase("data:no-comma-at-all")]
    [TestCase("data:,more than five")]
    public void ARefusalIsANetworkError(string url)
    {
        var engine = XhrEngine(fetch => fetch.MaxResponseBytes = 5);

        engine.Execute(Logging + $"""
            xhr.open('GET', '{url}');
            xhr.send();
            """);

        PumpUntilDone(engine);
        engine.Evaluate("[xhr.status, log.join()].join('|')").AsString()
            .Should().Be("0|rs1,loadstart:0/0/false,rs4,error:0/0/false,loadend:0/0/false");

        engine.Evaluate($$"""
            (() => {
              const sync = new XMLHttpRequest();
              sync.open('GET', '{{url}}', false);
              try { sync.send(); } catch (e) { return e.name + ':' + (e instanceof DOMException); }
              return 'no throw';
            })()
            """).AsString().Should().Be("NetworkError:true");
    }

    /// <summary>
    /// The interface reads its policy from <c>Options.WebApi.Fetch</c>, and none of it applies here: a scheme
    /// list without <c>data</c>, a filter that refuses everything, a cap of zero and an observer all leave
    /// the request answered and the observer uninformed.
    /// </summary>
    [Test]
    public void ThePolicyAndTheObserverDoNotApply()
    {
        var observer = new CountingObserver();
        var filterCalls = 0;
        var engine = XhrEngine(fetch =>
        {
            fetch.AllowedSchemes.Clear();
            fetch.UrlFilter = _ =>
            {
                Interlocked.Increment(ref filterCalls);
                return false;
            };
            fetch.MaxConcurrentRequests = 0;
            fetch.Observer = observer;
        });

        engine.Execute(Logging + """
            xhr.open('GET', 'data:,hello');
            xhr.send();
            """);

        PumpUntilDone(engine);
        engine.Evaluate("xhr.status + '|' + xhr.responseText").AsString().Should().Be("200|hello");
        filterCalls.Should().Be(0);
        observer.Calls.Should().Be(0);
    }
}
#endif
