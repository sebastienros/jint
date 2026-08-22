#if NET8_0_OR_GREATER
#nullable enable

using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>EventSource</c> seen from outside the assembly: the policy a host writes, the thread its events arrive
/// on, and what a restore does to a connection that is still open.
/// </summary>
/// <remarks>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party — the stub
/// transport goes in through <c>Options.WebApi.Fetch.HttpClient</c>, the same door a host uses for a
/// <c>DelegatingHandler</c> or an <c>IHttpClientFactory</c> client, and it is the same group <c>fetch</c>
/// reads because server-sent events deliberately share it.
/// </para>
/// <para>
/// <b>The three tests that wait for a hanging handler's token run on
/// <see cref="DedicatedThread.RunAsync"/>.</b> A handler that hangs is parked in <c>Task.Delay</c>, so the
/// <c>catch</c> that sets <see cref="StubHandler.Cancelled"/> is a thread-pool continuation — and blocking an
/// xUnit pool worker to wait for one is the resource inversion described on
/// <see cref="DedicatedThread.RunAsync"/>, which is exactly the claim and exactly the treatment
/// sebastienros/jint#3207 gave the in-assembly sibling of these tests. Every other test here drives a handler
/// that answers synchronously, so its connection loop runs inline on the thread that started it and there is
/// nothing to wait for.
/// </para>
/// </remarks>
public class WebApiEventSourceTests
{
    private const string StreamUrl = "https://example.org/stream";

    /// <summary>
    /// How long a test will wait for a signal the transport raises. The claim is that the signal happens at
    /// all, never how quickly, so this is a ceiling only a genuine failure to propagate can reach — never an
    /// interval a loaded runner can lose (sebastienros/jint#3213).
    /// </summary>
    private static readonly TimeSpan TransportSignalCeiling = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A clock that only moves when a test moves it, so the reconnect delay is exact and instant.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Volatile.Read(ref _timestamp);

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(Volatile.Read(ref _timestamp));

        internal void Advance(long milliseconds) => Volatile.Write(ref _timestamp, Volatile.Read(ref _timestamp) + (milliseconds * TimeSpan.TicksPerMillisecond));
    }

    /// <summary>
    /// A handler that answers each attempt with what the test told it to, or hangs until its token is
    /// cancelled, and remembers which.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        internal List<string> Urls { get; } = new();

        internal bool Hang { get; init; }

        internal Func<int, HttpResponseMessage> Responder { get; init; } = static _ => Answer("data: ok\n\n");

        /// <summary>Set when the handler saw its cancellation token fire — the proof a close reached the socket.</summary>
        internal ManualResetEventSlim Cancelled { get; } = new(false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int attempt;
            lock (Urls)
            {
                Urls.Add(request.RequestUri!.ToString());
                attempt = Urls.Count - 1;
            }

            if (!Hang)
            {
                return Responder(attempt);
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

            return Responder(attempt);
        }

        internal int RequestCount
        {
            get
            {
                lock (Urls)
                {
                    return Urls.Count;
                }
            }
        }
    }

    private static HttpResponseMessage Answer(string body, string contentType = "text/event-stream", HttpStatusCode status = HttpStatusCode.OK)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        content.Headers.TryAddWithoutValidation("content-type", contentType);
        return new HttpResponseMessage(status) { Content = content };
    }

    private static (Engine Engine, ManualClock Clock, List<string> Records) SseEngine(
        HttpMessageHandler handler,
        Action<Options.FetchOptions>? configure = null,
        Action<Options>? extra = null)
    {
        var clock = new ManualClock();
        var records = new List<string>();

        var engine = new Engine(options =>
        {
            options.WebApi.Timers.TimeProvider = clock;
            options.UseEventSource(net =>
            {
                net.HttpClient = new HttpClient(handler);
                configure?.Invoke(net);
            });

            options.Configure(e => e.SetValue("record", new Action<string>(entry =>
            {
                lock (records)
                {
                    records.Add(entry);
                }
            })));

            extra?.Invoke(options);
        });

        return (engine, clock, records);
    }

    private static int Count(List<string> records)
    {
        lock (records)
        {
            return records.Count;
        }
    }

    private static string Joined(List<string> records)
    {
        lock (records)
        {
            return string.Join("|", records);
        }
    }

    /// <summary>
    /// Pumps the engine from the host's own thread, which is the only way anything an event source produces
    /// is delivered at all.
    /// </summary>
    /// <remarks>
    /// The bound is <see cref="TransportSignalCeiling"/> rather than an interval the engine is expected to
    /// beat: what is being waited for is a hand-over, so only a hand-over that never happens can reach it.
    /// </remarks>
    private static void Pump(Engine engine, Func<bool> until, string expectation)
    {
        var deadline = DateTime.UtcNow + TransportSignalCeiling;
        while (DateTime.UtcNow < deadline)
        {
            engine.Advanced.ProcessTasks();
            if (until())
            {
                return;
            }

            Thread.Sleep(2);
        }

        throw new TimeoutException($"Timed out waiting for {expectation}.");
    }

    private static void Idle(Engine engine)
    {
        for (var i = 0; i < 25; i++)
        {
            engine.Advanced.ProcessTasks();
            Thread.Sleep(2);
        }
    }

    /// <summary>
    /// Opens a stream and attaches the three handlers in <b>one</b> script, because <c>Execute</c> drains the
    /// event loop on its way out: a failure the constructor queued — a URL the policy refused, a slot the
    /// concurrency limit would not give — has already been delivered by the time a second <c>Execute</c>
    /// could attach a listener to hear it.
    /// </summary>
    private static void Start(Engine engine, string variable, string url)
    {
        engine.Execute($$"""
            var {{variable}} = new EventSource('{{url}}');
            {{variable}}.onopen = () => record('open');
            {{variable}}.onmessage = e => record('message:' + e.data);
            {{variable}}.onerror = () => record('error:' + {{variable}}.readyState);
            """);
    }

    [Fact]
    public void ADefaultEngineAndAUseWebApisEngineHaveNoEventSource()
    {
        new Engine().Evaluate("typeof EventSource").AsString().Should().Be("undefined");
        new Engine(options => options.UseWebApis()).Evaluate("typeof EventSource").AsString().Should().Be("undefined");
        new Engine(options => options.UseWebApis(WebApiFeatures.Default)).Evaluate("'EventSource' in globalThis").AsBoolean().Should().BeFalse();

        // Network access is granted one API at a time: fetch does not bring server-sent events with it.
        new Engine(options => options.UseFetch()).Evaluate("typeof EventSource").AsString().Should().Be("undefined");

        // ... and server-sent events do not bring fetch.
        var engine = new Engine(options => options.UseEventSource());
        engine.Evaluate("typeof EventSource").AsString().Should().Be("function");
        engine.Evaluate("typeof MessageEvent").AsString().Should().Be("function");
        engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");

        // The features it does bring are the ones its own surface is built from.
        engine.Evaluate("typeof EventTarget").AsString().Should().Be("function");
    }

    [Fact]
    public void RefusesASchemeTheHostDidNotAllow()
    {
        var handler = new StubHandler();
        var (engine, _, records) = SseEngine(handler, net => net.AllowedSchemes.Remove("http"));

        Start(engine, "es", "http://example.org/stream");
        Pump(engine, () => Count(records) > 0, "the error event");

        Joined(records).Should().Be("error:2");
        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public void RefusesAUrlTheHostFilterRejects()
    {
        var handler = new StubHandler();
        var (engine, _, records) = SseEngine(handler, net => net.UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase));

        Start(engine, "es", "https://169.254.169.254/latest/meta-data/");
        Pump(engine, () => Count(records) > 0, "the error event");

        // The failure carries no detail at all — the standard gives an event source's error event none — and
        // nothing reached a socket.
        Joined(records).Should().Be("error:2");
        handler.RequestCount.Should().Be(0);

        engine.Execute("var allowed = new EventSource('https://api.example.org/stream');");
        Pump(engine, () => handler.RequestCount == 1, "the allowed stream");
    }

    [Fact]
    public void ReRunsTheFilterOnEveryRedirectHop()
    {
        // The SSRF shape, and the reason Jint follows redirects itself: the first URL passes the filter and
        // the server answers with a redirect to one that does not.
        var handler = new StubHandler
        {
            Responder = _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Found);
                response.Headers.TryAddWithoutValidation("location", "http://169.254.169.254/latest/meta-data/");
                return response;
            },
        };

        var seen = new List<string>();
        var (engine, clock, records) = SseEngine(handler, net => net.UrlFilter = uri =>
        {
            lock (seen)
            {
                seen.Add(uri.ToString());
            }

            return uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
        });

        Start(engine, "es", "https://api.example.org/a");
        Pump(engine, () => Count(records) > 0, "the failure of the redirected connection");

        // CLOSED, not CONNECTING: a policy refusal is the host's own rule rather than a network error, and
        // retrying it would only run into the same rule again — so, like every other host limit, it fails the
        // connection for good instead of reconnecting.
        Joined(records).Should().Be("error:2");

        lock (seen)
        {
            seen.Should().Equal("https://api.example.org/a", "http://169.254.169.254/latest/meta-data/");
        }

        handler.Urls.Should().Equal("https://api.example.org/a");

        engine.Execute("es.close();");
        clock.Advance(60_000);
        Idle(engine);
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public Task CloseCancelsTheRequestInFlight() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler { Hang = true };
        var (engine, _, records) = SseEngine(handler);

        Start(engine, "es", StreamUrl);
        Pump(engine, () => handler.RequestCount == 1, "the request to reach the transport");

        engine.Execute("es.close();");

        // The abort reached the socket, not just the object.
        handler.Cancelled.Wait(TransportSignalCeiling).Should().BeTrue("closing an EventSource must cancel the request in flight, not merely mark the object CLOSED");

        engine.Evaluate("es.readyState").AsNumber().Should().Be(2);

        // And closing fires nothing: it is not a failure, so there is no error event.
        Idle(engine);
        Joined(records).Should().BeEmpty();
    });

    [Fact]
    public Task ARestoreCancelsTheConnectionAndDeliversNothingIntoTheRestoredEngine() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler { Hang = true };
        var (engine, _, records) = SseEngine(handler);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        Start(engine, "es", StreamUrl);
        Pump(engine, () => handler.RequestCount == 1, "the request to reach the transport");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The socket is let go at once ...
        handler.Cancelled.Wait(TransportSignalCeiling).Should().BeTrue("a restore must cancel the connection rather than leave the socket held");

        // ... and nothing from the ended cycle ever reaches the restored engine.
        Idle(engine);
        Count(records).Should().Be(0);

        // The engine is perfectly usable afterwards.
        engine.Evaluate("typeof EventSource").AsString().Should().Be("function");
        engine.Evaluate("typeof es").AsString().Should().Be("undefined");
    });

    [Fact]
    public void ARestoreAlsoTakesTheReconnectDelayWithIt()
    {
        // The stream ends by itself, which is a reestablish: an error event and a delay on the timer queue.
        var handler = new StubHandler { Responder = _ => Answer("retry: 10\ndata: one\n\n") };
        var (engine, clock, records) = SseEngine(handler);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        Start(engine, "es", StreamUrl);
        Pump(engine, () => Count(records) >= 3, "the open, message and error events");

        Joined(records).Should().Be("open|message:one|error:0");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // A timer registered by the ended cycle can never fire into the restored globals, so the reconnection
        // that was pending simply never happens.
        clock.Advance(60_000);
        Idle(engine);

        handler.RequestCount.Should().Be(1);
        Count(records).Should().Be(3);
    }

    [Fact]
    public Task AnEngineCancellationEndsTheConnectionSilently() => DedicatedThread.RunAsync(() =>
    {
        // A constraint that became an error event would let the script carry on — and reconnect — which is
        // precisely what the constraint exists to stop.
        var handler = new StubHandler { Hang = true };
        using var cancellation = new CancellationTokenSource();

        var (engine, clock, records) = SseEngine(handler, extra: options => options.CancellationToken(cancellation.Token));

        Start(engine, "es", StreamUrl);
        Pump(engine, () => handler.RequestCount == 1, "the request to reach the transport");

        cancellation.Cancel();
        handler.Cancelled.Wait(TransportSignalCeiling).Should().BeTrue("an engine cancellation must reach the connection in flight");

        clock.Advance(60_000);
        Idle(engine);

        Count(records).Should().Be(0);
        handler.RequestCount.Should().Be(1);
    });

    [Fact]
    public void BoundsHowManyStreamsOneEngineMayHaveOpen()
    {
        var handler = new StubHandler { Hang = true };
        var (engine, _, records) = SseEngine(handler, net => net.MaxConcurrentRequests = 1);

        engine.Execute($$"""
            var first = new EventSource('{{StreamUrl}}/1');
            var second = new EventSource('{{StreamUrl}}/2');
            second.onerror = () => record('error:' + second.readyState);
            """);
        Pump(engine, () => Count(records) > 0, "the second connection failing");

        Joined(records).Should().Be("error:2");
        engine.Evaluate("first.readyState").AsNumber().Should().Be(0);
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public void TheHostClientFactoryWinsAndSeesTheEngine()
    {
        var byFactory = new StubHandler();
        var byProperty = new StubHandler();
        var seen = new List<object?>();

        var (engine, _, records) = SseEngine(byProperty, net => net.HttpClientFactory = e =>
        {
            // Called on the engine thread, once per connection, so per-request host state is reachable.
            lock (seen)
            {
                seen.Add(e.Advanced.HostDefined);
            }

            return new HttpClient(byFactory);
        });

        engine.Advanced.HostDefined = "tenant-a";
        Start(engine, "es", StreamUrl);
        Pump(engine, () => Count(records) >= 2, "the open and message events");

        Joined(records).Should().StartWith("open|message:ok");
        byProperty.RequestCount.Should().Be(0);

        lock (seen)
        {
            // The stream ends by itself, so a reconnection is pending — the factory has been asked once, for
            // the one connection that has been made, and the clock has not moved.
            seen.Should().Equal("tenant-a");
        }
    }

    [Fact]
    public void SeveralEnginesFromOneOptionsInstanceCountTheirStreamsSeparately()
    {
        var handler = new StubHandler { Hang = true };
        var options = new Options().UseEventSource(net =>
        {
            net.HttpClient = new HttpClient(handler);
            net.MaxConcurrentRequests = 1;
        });

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("var es = new EventSource('" + StreamUrl + "/1');");
        second.Execute("var es = new EventSource('" + StreamUrl + "/2');");

        Pump(second, () => handler.RequestCount == 2, "both engines reaching the transport");

        // The second engine's stream was not refused by the first engine's slot being taken.
        second.Evaluate("es.readyState").AsNumber().Should().Be(0);
    }

    [Fact]
    public void TheGlobalsCarryTheAttributesWebIdlAsksFor()
    {
        var engine = new Engine(options => options.UseEventSource());

        foreach (var name in new[] { "EventSource", "MessageEvent" })
        {
            // https://webidl.spec.whatwg.org/#es-interfaces — an interface object is writable and
            // configurable but not enumerable.
            var descriptor = engine.Evaluate($"Object.getOwnPropertyDescriptor(globalThis, '{name}')").AsObject();
            descriptor.Get("writable").AsBoolean().Should().BeTrue();
            descriptor.Get("enumerable").AsBoolean().Should().BeFalse();
            descriptor.Get("configurable").AsBoolean().Should().BeTrue();

            // ... and never inside a shadow realm.
            engine.Evaluate($"new ShadowRealm().evaluate('typeof {name}')").AsString().Should().Be("undefined");
        }
    }

    [Fact]
    public void AHostGlobalOfTheSameNameWins()
    {
        var engine = new Engine(options => options
            .Configure(e => e.SetValue("EventSource", "host's own"))
            .UseEventSource());

        engine.Evaluate("EventSource").AsString().Should().Be("host's own");
    }

    [Fact]
    public void EveryListenerRunsOnTheHostsOwnThread()
    {
        // The shape a game loop or a message pump uses. The response is read on a thread pool thread, but
        // every listener runs on whichever thread pumped the engine — which is the whole point of the design,
        // and the reason a host can touch its own state from one without a lock.
        var handler = new StubHandler { Responder = _ => Answer("data: one\n\ndata: two\n\n") };
        var (engine, _, records) = SseEngine(handler);

        var threads = new List<int>();
        engine.SetValue("recordThread", new Action(() =>
        {
            lock (threads)
            {
                threads.Add(Environment.CurrentManagedThreadId);
            }
        }));

        engine.Execute($$"""
            var es = new EventSource('{{StreamUrl}}');
            es.onopen = () => { recordThread(); record('open'); };
            es.onmessage = e => { recordThread(); record('message:' + e.data); };
            """);

        Pump(engine, () => Count(records) >= 3, "the open event and two messages");
        Joined(records).Should().StartWith("open|message:one|message:two");

        lock (threads)
        {
            threads.Should().HaveCountGreaterThanOrEqualTo(3);
            threads.Should().AllSatisfy(id => id.Should().Be(Environment.CurrentManagedThreadId));
        }

        engine.Execute("es.close();");
    }
}
#endif
