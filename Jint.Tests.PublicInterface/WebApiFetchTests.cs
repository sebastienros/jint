#if NET8_0_OR_GREATER
#nullable enable

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>fetch</c> seen from outside the assembly: the policy a host writes, the failures it produces and the
/// threads its promise settles on.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party — the
/// stub transport goes in through <c>Options.WebApi.Fetch.HttpClient</c>, which is the same door a host uses
/// for a <c>DelegatingHandler</c> or an <c>IHttpClientFactory</c> client.
/// </remarks>
public class WebApiFetchTests
{
    /// <summary>
    /// A handler that answers immediately or hangs until its token is cancelled, and remembers which.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        internal List<string> Urls { get; } = new();

        internal bool Hang { get; init; }

        internal Func<string, HttpResponseMessage> Responder { get; init; } =
            static _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };

        /// <summary>Set when the handler saw its cancellation token fire — the proof an abort reached the socket.</summary>
        internal ManualResetEventSlim Cancelled { get; } = new(false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Urls)
            {
                Urls.Add(request.RequestUri!.ToString());
            }

            if (!Hang)
            {
                return Responder(request.RequestUri!.ToString());
            }

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Cancelled.Set();
                throw;
            }

            return Responder(request.RequestUri!.ToString());
        }
    }

    private static Engine WebEngine(HttpMessageHandler handler, Action<Options.FetchOptions>? configure = null, Action<Options>? extra = null)
    {
        return new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.Timers).UseFetch(fetch =>
            {
                fetch.HttpClient = new HttpClient(handler);
                configure?.Invoke(fetch);
            });

            extra?.Invoke(options);
        });
    }

    [Fact]
    public void ADefaultEngineAndAUseWebApisEngineHaveNoFetch()
    {
        new Engine().Evaluate("typeof fetch").AsString().Should().Be("undefined");
        new Engine(options => options.UseWebApis()).Evaluate("typeof fetch").AsString().Should().Be("undefined");
        new Engine(options => options.UseWebApis(WebApiFeatures.Default)).Evaluate("'fetch' in globalThis").AsBoolean().Should().BeFalse();

        // UseFetch is the only call that grants it.
        new Engine(options => options.UseFetch()).Evaluate("typeof fetch").AsString().Should().Be("function");
    }

    [Fact]
    public void TheFetchGlobalCarriesTheAttributesWebIdlAsksFor()
    {
        var engine = new Engine(options => options.UseFetch());

        // https://webidl.spec.whatwg.org/#es-operations — a global operation is a writable, enumerable,
        // configurable data property, unlike an interface object.
        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'fetch')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();

        engine.Evaluate("fetch.length").AsNumber().Should().Be(1);
        engine.Evaluate("fetch.name").AsString().Should().Be("fetch");

        // ... and never inside a shadow realm.
        engine.Evaluate("new ShadowRealm().evaluate('typeof fetch')").AsString().Should().Be("undefined");
    }

    [Fact]
    public void RefusesASchemeTheHostDidNotAllow()
    {
        var handler = new StubHandler();
        var engine = WebEngine(handler, f => f.AllowedSchemes.Remove("http"));

        engine.Evaluate("fetch('http://example.org/').then(() => 'resolved', e => e.constructor.name + ': ' + e.message)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError: Failed to fetch");

        // Refused before a socket was opened.
        handler.Urls.Should().BeEmpty();

        // https is still allowed.
        engine.Evaluate("fetch('https://example.org/').then(r => r.status)").UnwrapIfPromise().AsNumber().Should().Be(200);
    }

    [Fact]
    public void RefusesAUrlTheHostFilterRejects()
    {
        var handler = new StubHandler();
        var engine = WebEngine(handler, f => f.UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase));

        engine.Evaluate("fetch('https://169.254.169.254/latest/meta-data/').then(() => 'resolved', e => e.message)")
            .UnwrapIfPromise().AsString().Should().Be("Failed to fetch");

        handler.Urls.Should().BeEmpty();

        engine.Evaluate("fetch('https://api.example.org/').then(r => r.status)").UnwrapIfPromise().AsNumber().Should().Be(200);
    }

    [Fact]
    public void ReRunsTheFilterOnEveryRedirectHop()
    {
        // The SSRF shape: the first URL passes the filter and the server answers with a redirect to a URL
        // that does not. An HttpClient following redirects itself would have reached it.
        var handler = new StubHandler
        {
            Responder = url =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Found);
                response.Headers.TryAddWithoutValidation("location", "http://169.254.169.254/latest/meta-data/");
                return response;
            },
        };

        var seen = new List<string>();
        var engine = WebEngine(handler, f => f.UrlFilter = uri =>
        {
            lock (seen)
            {
                seen.Add(uri.ToString());
            }

            return uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
        });

        engine.Evaluate("fetch('https://api.example.org/a').then(() => 'resolved', e => e.constructor.name + ': ' + e.message)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError: Failed to fetch");

        // The filter saw both hops, and only the first reached the transport.
        seen.Should().Equal("https://api.example.org/a", "http://169.254.169.254/latest/meta-data/");
        handler.Urls.Should().Equal("https://api.example.org/a");
    }

    [Fact]
    public void CapsTheResponseBodyWhenContentLengthDeclaresIt()
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 4096)) },
        };

        var engine = WebEngine(handler, f => f.MaxResponseBytes = 1024);

        engine.Evaluate("fetch('https://example.org/').then(() => 'resolved', e => e.constructor.name + '|' + e.message)")
            .UnwrapIfPromise().AsString().Should()
            .Be("TypeError|Failed to fetch: The response body exceeded the 1024 byte limit set by Options.WebApi.Fetch.MaxResponseBytes.");
    }

    [Fact]
    public void CapsTheResponseBodyWithoutAContentLengthWhileItStreams()
    {
        // A server that lies about the length, or uses chunked encoding, is caught by the running total
        // rather than by the declared one.
        //
        // Where that failure surfaces changed when response bodies became streams: the fetch promise
        // resolves as soon as the headers are in — which is what the standard prescribes and what a browser
        // does — so a cap that is only broken later can no longer reject it. It errors the body stream
        // instead, and every consumer of that body reports it. The connection is still dropped at the chunk
        // that crossed the line, so what the cap actually bounds is unchanged.
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(4096) },
        };

        var engine = WebEngine(handler, f => f.MaxResponseBytes = 1024);

        engine.Evaluate("fetch('https://example.org/').then(r => r.text()).then(() => 'resolved', e => e.constructor.name + '|' + e.message)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError|Failed to fetch: The response body exceeded the 1024 byte limit set by Options.WebApi.Fetch.MaxResponseBytes.");
    }

    [Fact]
    public void TheCapReachesAConsumerThatReadsTheStreamItself()
    {
        // The same failure through the other door: a script draining response.body chunk by chunk sees the
        // reader's promise reject rather than a body mixin method's.
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(4096) },
        };

        var engine = WebEngine(handler, f => f.MaxResponseBytes = 1024);

        engine.Evaluate(@"(async () => {
                const r = await fetch('https://example.org/');
                const reader = r.body.getReader();
                let seen = 0;
                try
                {
                    while (true) {
                        const { done, value } = await reader.read();
                        if (done) return 'closed after ' + seen;
                        seen += value.length;
                    }
                }
                catch (e) { return e.constructor.name + ' after ' + seen; }
            })()")
            // How many bytes arrived first depends on how the transport chunked them; that the read ends in
            // a TypeError rather than in a close is the pin.
            .UnwrapIfPromise().AsString().Should().StartWith("TypeError after ");
    }

    /// <summary>
    /// Content that streams its bytes and refuses to say how many there are, which is what a chunked
    /// response looks like.
    /// </summary>
    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly int _length;

        internal UnknownLengthContent(int length) => _length = length;

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            => stream.WriteAsync(new byte[_length], 0, _length);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    [Fact]
    public void ABodyUnderTheCapIsReadInFull()
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 1024)) },
        };

        var engine = WebEngine(handler, f => f.MaxResponseBytes = 1024);
        engine.Evaluate("fetch('https://example.org/').then(r => r.text()).then(t => t.length)")
            .UnwrapIfPromise().AsNumber().Should().Be(1024);
    }

    [Fact]
    public void AnAbortMidFlightRejectsWithTheReasonAndCancelsTheRequest()
    {
        var handler = new StubHandler { Hang = true };
        var engine = WebEngine(handler);

        var outcome = engine.Evaluate(@"(() => {
                const c = new AbortController();
                setTimeout(() => c.abort('stop'), 0);
                return fetch('https://example.org/', { signal: c.signal }).then(() => 'resolved', e => e);
            })()").UnwrapIfPromise();

        outcome.AsString().Should().Be("stop");

        // The abort reached the socket, not just the promise.
        handler.Cancelled.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
    }

    [Fact]
    public void ADeadlineRejectsWithATimeoutErrorDomException()
    {
        var handler = new StubHandler { Hang = true };
        var engine = WebEngine(handler, f => f.Timeout = TimeSpan.FromMilliseconds(50));

        engine.Evaluate("fetch('https://example.org/').then(() => 'resolved', e => e.name + '|' + (e instanceof Error))")
            .UnwrapIfPromise().AsString().Should().Be("TimeoutError|true");

        handler.Cancelled.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
    }

    [Fact]
    public void RefusesMoreConcurrentRequestsThanTheHostAllows()
    {
        var handler = new StubHandler { Hang = true };
        var engine = WebEngine(handler, f =>
        {
            f.MaxConcurrentRequests = 2;
            f.Timeout = TimeSpan.FromMilliseconds(50);
        });

        // Three at once: the third is refused rather than queued.
        engine.Execute("var outcome; fetch('https://example.org/1'); fetch('https://example.org/2'); fetch('https://example.org/3').then(() => outcome = 'resolved', e => outcome = e.message);");
        engine.Advanced.ProcessTasks();

        engine.Evaluate("outcome").AsString().Should()
            .Be("Failed to fetch: the engine already has 2 requests in flight, which is its Options.WebApi.Fetch.MaxConcurrentRequests limit.");

        lock (handler.Urls)
        {
            handler.Urls.Should().Equal("https://example.org/1", "https://example.org/2");
        }
    }

    [Fact]
    public void ASlotIsFreedWhenARequestSettles()
    {
        var handler = new StubHandler();
        var engine = WebEngine(handler, f => f.MaxConcurrentRequests = 1);

        for (var i = 0; i < 3; i++)
        {
            engine.Evaluate($"fetch('https://example.org/{i}').then(r => r.status)").UnwrapIfPromise().AsNumber().Should().Be(200);
        }

        handler.Urls.Should().HaveCount(3);
    }

    [Fact]
    public void ARestoreCancelsTheRequestAndTheOldPromiseNeverSettles()
    {
        var handler = new StubHandler { Hang = true };
        var settled = 0;

        var engine = WebEngine(handler, extra: options => options.Configure(e =>
            e.SetValue("record", new Action(() => Interlocked.Increment(ref settled)))));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Execute("fetch('https://example.org/').then(record, record);");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The socket is let go at once...
        handler.Cancelled.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

        // ... and nothing from the ended cycle ever settles into the restored engine.
        for (var i = 0; i < 20; i++)
        {
            engine.Advanced.ProcessTasks();
            Thread.Sleep(5);
        }

        Volatile.Read(ref settled).Should().Be(0);

        // The engine is perfectly usable afterwards.
        engine.Evaluate("typeof fetch").AsString().Should().Be("function");
    }

    [Fact]
    public void AnEngineCancellationSettlesNothingAtAll()
    {
        // A constraint that became a promise rejection would no longer bound anything: script would observe
        // an ordinary failed fetch and carry on.
        var handler = new StubHandler { Hang = true };
        using var cancellation = new CancellationTokenSource();
        var settled = 0;

        var engine = WebEngine(
            handler,
            extra: options => options
                .CancellationToken(cancellation.Token)
                .Configure(e => e.SetValue("record", new Action(() => Interlocked.Increment(ref settled)))));

        engine.Execute("fetch('https://example.org/').then(record, record);");

        cancellation.Cancel();
        handler.Cancelled.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

        for (var i = 0; i < 20; i++)
        {
            engine.Advanced.ProcessTasks();
            Thread.Sleep(5);
        }

        Volatile.Read(ref settled).Should().Be(0);
    }

    [Fact]
    public void CompletesUnderABlockingUnwrap()
    {
        var handler = new StubHandler();
        var engine = WebEngine(handler);

        engine.Evaluate("(async () => { const r = await fetch('https://example.org/'); return await r.text(); })()")
            .UnwrapIfPromise().AsString().Should().Be("ok");
    }

    [Fact]
    public async Task CompletesUnderEvaluateAsync()
    {
        var handler = new StubHandler();
        var engine = WebEngine(handler);

        var result = await engine.EvaluateAsync("(async () => { const r = await fetch('https://example.org/'); return await r.text(); })()");
        result.AsString().Should().Be("ok");
    }

    [Fact]
    public void CompletesUnderAHostProcessTasksLoop()
    {
        // The shape a game loop or a message pump uses: every turn provably runs on the host's own thread,
        // and nothing here ever blocks on the engine.
        var handler = new StubHandler();
        var engine = WebEngine(handler);

        engine.Execute("var text, done = false; fetch('https://example.org/').then(r => r.text()).then(t => { text = t; done = true; });");

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && !engine.Evaluate("done").AsBoolean())
        {
            engine.Advanced.ProcessTasks();
            Thread.Sleep(5);
        }

        engine.Evaluate("text").AsString().Should().Be("ok");
    }

    [Fact]
    public void TheHostReadsTheClrExceptionBehindAFailure()
    {
        // The script sees only "Failed to fetch"; the host gets the real cause off the error value.
        var handler = new ThrowingHandler();
        var engine = WebEngine(handler);

        engine.Execute("var error; fetch('https://example.org/').then(() => {}, e => error = e);");
        engine.Advanced.ProcessTasks();

        var error = engine.Evaluate("error");
        error.AsObject().Get("message").AsString().Should().Be("Failed to fetch");

        JintException.TryGetClrException(new JavaScriptException(error), out var clrException).Should().BeTrue();
        clrException!.ToString().Should().Contain("no such host is known");
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("no such host is known");
    }

    [Fact]
    public void TheHostClientFactoryWinsAndSeesTheEngine()
    {
        var byFactory = new StubHandler { Responder = _ => new HttpResponseMessage(HttpStatusCode.Accepted) };
        var byProperty = new StubHandler();
        var seen = new List<object?>();

        var engine = new Engine(options => options
            .UseFetch(fetch =>
            {
                fetch.HttpClient = new HttpClient(byProperty);
                fetch.HttpClientFactory = e =>
                {
                    // Called on the engine thread, so per-request host state is reachable.
                    seen.Add(e.Advanced.HostDefined);
                    return new HttpClient(byFactory);
                };
            }));

        engine.Advanced.HostDefined = "tenant-a";
        engine.Evaluate("fetch('https://example.org/').then(r => r.status)").UnwrapIfPromise().AsNumber().Should().Be(202);

        seen.Should().Equal("tenant-a");
        byProperty.Urls.Should().BeEmpty();
    }

    [Fact]
    public void SeveralEnginesFromOneOptionsInstanceCountTheirRequestsSeparately()
    {
        var handler = new StubHandler { Hang = true };
        var options = new Options().UseFetch(fetch =>
        {
            fetch.HttpClient = new HttpClient(handler);
            fetch.MaxConcurrentRequests = 1;
            fetch.Timeout = TimeSpan.FromMilliseconds(50);
        });

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("fetch('https://example.org/1');");
        second.Execute("var outcome; fetch('https://example.org/2').then(() => outcome = 'resolved', e => outcome = e.name);");
        second.Advanced.ProcessTasks();

        // The second engine's request was not refused by the first engine's slot being taken.
        second.Evaluate("typeof outcome").AsString().Should().Be("undefined");

        lock (handler.Urls)
        {
            handler.Urls.Should().HaveCount(2);
        }
    }
}
#endif
