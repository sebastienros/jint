#if NET8_0_OR_GREATER
#nullable enable
#pragma warning disable JINT0002 // the fetch observer is a preview surface; a stream is one of the lanes it watches

using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Jint.WebApi.Fetch;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>EventSource</c> against the server-sent events section —
/// https://html.spec.whatwg.org/multipage/server-sent-events.html — driven by a stub
/// <see cref="HttpMessageHandler"/>, so nothing here touches a network.
/// </summary>
/// <remarks>
/// <para>
/// Every engine here runs on a <see cref="ManualClock"/>, so the reconnect delay — which rides the engine's
/// timer queue — only elapses when a test says so. A test that never advances it therefore never sees a
/// reconnection, however long it takes to run.
/// </para>
/// <para>
/// The events themselves arrive as event-loop jobs queued from a thread pool thread, so a test pumps until it
/// sees what it is waiting for rather than asserting straight after <c>Execute</c>. That is the same contract
/// a host has: nothing at all is delivered to an engine nobody pumps.
/// </para>
/// </remarks>
public class EventSourceTests
{
    private const string StreamUrl = "https://example.org/stream";

    /// <summary>
    /// How long a test will wait for a signal raised by a thread-pool continuation — a cancelled read
    /// resuming, a handler seeing its token fire. The claim being made is that the signal happens at all,
    /// never how quickly, so this is a ceiling only a genuine failure to propagate can reach. It is
    /// deliberately far past any plausible scheduling delay: a loaded runner injects pool workers at
    /// roughly one per 500 ms once saturated, which is what made a five-second window a flake
    /// (sebastienros/jint#3201) rather than a check. The test that waits on one also hands its body to
    /// <see cref="DedicatedThread.RunAsync"/>, so it is not itself holding a worker the continuation needs.
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
    /// A stream a test writes into and completes by hand, which is what an open connection looks like.
    /// </summary>
    private sealed class PushStream : Stream
    {
        private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();
        private byte[]? _current;
        private int _offset;

        internal void Push(string text) => _chunks.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

        internal void Push(byte[] bytes) => _chunks.Writer.TryWrite(bytes);

        /// <summary>Ends the response body, which is what makes the next read answer zero.</summary>
        internal void Complete() => _chunks.Writer.TryComplete();

        /// <summary>
        /// Set when a read was cancelled — the proof that ending the connection reached the transport rather
        /// than only the <c>EventSource</c> object.
        /// </summary>
        internal ManualResetEventSlim ReadCancelled { get; } = new(false);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (_current is null || _offset == _current.Length)
            {
                _current = null;
                _offset = 0;

                bool available;
                try
                {
                    available = await _chunks.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    ReadCancelled.Set();
                    throw;
                }

                if (!available)
                {
                    return 0;
                }

                _chunks.Reader.TryRead(out _current);
            }

            var count = Math.Min(buffer.Length, _current.Length - _offset);
            _current.AsSpan(_offset, count).CopyTo(buffer.Span);
            _offset += count;
            return count;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// A handler that answers each attempt with whatever the test told it to, and remembers what it was asked.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly List<CancellationTokenRegistration> _registrations = new();

        internal List<RecordedRequest> Requests { get; } = new();

        internal Func<int, HttpResponseMessage> Responder { get; set; } = static _ => Answer("");

        /// <summary>Set when the handler saw its cancellation token fire — the proof a close reached the socket.</summary>
        internal ManualResetEventSlim Cancelled { get; } = new(false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int attempt;
            lock (Requests)
            {
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in request.Headers.NonValidated)
                {
                    headers[header.Key] = header.Value.ToString();
                }

                Requests.Add(new RecordedRequest(request.Method.Method, request.RequestUri!.ToString(), headers));
                attempt = Requests.Count - 1;
            }

            _registrations.Add(cancellationToken.Register(() => Cancelled.Set()));
            return Task.FromResult(Responder(attempt));
        }
    }

    private sealed record RecordedRequest(string Method, string Url, Dictionary<string, string> Headers)
    {
        internal string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
    }

    private static HttpResponseMessage Answer(string body, string contentType = "text/event-stream", HttpStatusCode status = HttpStatusCode.OK)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        content.Headers.TryAddWithoutValidation("content-type", contentType);
        return new HttpResponseMessage(status) { Content = content };
    }

    private static HttpResponseMessage Answer(PushStream stream, string contentType = "text/event-stream")
    {
        var content = new StreamContent(stream);
        content.Headers.TryAddWithoutValidation("content-type", contentType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static (Engine Engine, ManualClock Clock) SseEngine(HttpMessageHandler handler, Action<Options.FetchOptions>? configure = null)
    {
        var clock = new ManualClock();
        var engine = new Engine(options =>
        {
            options.WebApi.Timers.TimeProvider = clock;
            options.UseEventSource(net =>
            {
                net.HttpClient = new HttpClient(handler);
                configure?.Invoke(net);
            });
        });

        // The separator is a tilde rather than a colon because an event's data may be empty, or may itself
        // contain a colon, and an ambiguous log makes for an assertion that cannot fail honestly.
        engine.Execute("""
            var log = [];
            function watch(source) {
                source.onopen = () => log.push('open~' + source.readyState);
                source.onmessage = e => log.push('message~' + e.type + '~' + e.data + '~' + e.lastEventId + '~' + e.origin);
                source.onerror = () => log.push('error~' + source.readyState);
                return source;
            }
            """);

        return (engine, clock);
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join('|')").AsString();

    /// <summary>
    /// The first <paramref name="entries"/> of the log. A stream that has ended queues an <c>error</c> of its
    /// own, and whether that has arrived yet is a race; what each test is about is what came before it.
    /// </summary>
    private static string Log(Engine engine, int entries) => engine.Evaluate($"log.slice(0, {entries}).join('|')").AsString();

    /// <summary>
    /// Pumps the engine until <paramref name="until"/> holds, which is what a host's own loop does. Nothing
    /// an event source produces is delivered any other way.
    /// </summary>
    /// <remarks>
    /// The bound is <see cref="TransportSignalCeiling"/> for the reason stated on it: what this turns the
    /// engine over waiting for is a job queued from the transport's own thread-pool continuation, so a
    /// fifteen-second window was the same fixed clock over the same hand-over that
    /// <see cref="CloseStopsEverythingAndDispatchesNothingFurther"/>'s wait already gave up
    /// (sebastienros/jint#3213). Only a hand-over that never happens can reach a ceiling this far out.
    /// </remarks>
    private static void Pump(Engine engine, Func<bool> until, string expectation)
    {
        var deadline = DateTime.UtcNow + TransportSignalCeiling;
        while (DateTime.UtcNow < deadline)
        {
            engine.Tasks.ProcessTasks();
            if (until())
            {
                return;
            }

            Thread.Sleep(2);
        }

        throw new TimeoutException($"Timed out waiting for {expectation}. The log holds: {Log(engine)}");
    }

    private static void PumpUntilLogHas(Engine engine, int entries, string expectation)
        => Pump(engine, () => engine.Evaluate("log.length").AsNumber() >= entries, expectation);

    /// <summary>
    /// A <see cref="FetchObserver"/> that writes down what it was told, in the order it was told.
    /// </summary>
    /// <remarks>
    /// Every callback arrives on a transport thread, so the list is guarded and a test waits for a count
    /// rather than assuming the hand-over has happened; <see cref="Take"/> answers a prefix so that a
    /// terminal call a test is not about — the failure a closed stream reports — cannot race into an
    /// assertion.
    /// </remarks>
    private sealed class RecordingObserver : FetchObserver
    {
        private readonly List<string> _events = new();

        public override ValueTask<FetchInterception?> OnRequestAsync(ObservedFetchRequest request, CancellationToken cancellationToken)
        {
            Add($"request {request.Id} {request.Method} {request.Url} redirects={request.RedirectCount} initiator={request.Initiator}");
            return new ValueTask<FetchInterception?>((FetchInterception?) null);
        }

        public override void OnResponse(ObservedFetchResponse response)
            => Add($"response {response.Id} {response.Status} redirect={response.IsRedirect}");

        public override void OnData(FetchRequestId id, ReadOnlySpan<byte> chunk)
            => Add($"data {id} {chunk.Length}");

        public override void OnCompleted(FetchRequestId id, long bodyLength)
            => Add($"completed {id} {bodyLength}");

        public override void OnFailed(FetchRequestId id, string reason, Exception? exception)
            => Add($"failed {id} {reason}");

        /// <summary>How many notifications have arrived so far.</summary>
        internal int Count
        {
            get
            {
                lock (_events)
                {
                    return _events.Count;
                }
            }
        }

        /// <summary>Waits for <paramref name="count"/> notifications and answers exactly those.</summary>
        internal IReadOnlyList<string> Take(int count)
        {
            var deadline = DateTime.UtcNow + TransportSignalCeiling;
            while (DateTime.UtcNow < deadline)
            {
                lock (_events)
                {
                    if (_events.Count >= count)
                    {
                        return _events.GetRange(0, count);
                    }
                }

                Thread.Sleep(2);
            }

            lock (_events)
            {
                Assert.Fail($"the observer should have been told {count} things; it was told: {string.Join(" | ", _events)}");
                return _events.ToArray();
            }
        }

        private void Add(string entry)
        {
            lock (_events)
            {
                _events.Add(entry);
            }
        }
    }

    /// <summary>
    /// An observer sees a stream the way it sees a fetch: the request, the response, a chunk at a time as
    /// the bytes arrive, and one terminal call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The initiator is <c>FetchInitiator.EventSource</c>, because a host reporting this to a tool has to
    /// name the interface that asked — the Chrome DevTools Protocol has a resource type of its own for a
    /// stream.
    /// </para>
    /// <para>
    /// <b>The chunks are what make this whole rather than partial.</b> An <c>EventSource</c> reads its own
    /// body, so nothing in the transport can hand them over; without the call in the read loop an observer
    /// would be told a stream was asked for and answered and never that a byte of it arrived, which is why
    /// this lane was left unobserved until there was somewhere to put them
    /// ([#3621](https://github.com/sebastienros/jint/issues/3621)).
    /// </para>
    /// </remarks>
    [Test]
    public void AnObserverSeesTheRequestTheResponseAndEveryChunk()
    {
        var stream = new PushStream();
        var handler = new StubHandler { Responder = _ => Answer(stream) };
        var observer = new RecordingObserver();
        var (engine, _) = SseEngine(handler, net => net.Observer = observer);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");

        stream.Push("data: one\n\n");
        PumpUntilLogHas(engine, 2, "the open event and the first message");

        stream.Push("data: two\n\n");
        PumpUntilLogHas(engine, 3, "the second message");

        var events = observer.Take(4);
        var id = events[0].Split(' ')[1];

        events.Should().Equal(
            $"request {id} GET {StreamUrl} redirects=0 initiator=EventSource",
            $"response {id} 200 redirect=False",
            $"data {id} 11",
            $"data {id} 11");

        engine.Execute("es.close();");
        stream.Complete();
    }

    /// <summary>
    /// A stream the server ends completes, and the reconnect that follows is a request of its own with its
    /// own identifier — which is what it is on the wire, and what a network panel shows.
    /// </summary>
    [Test]
    public void EachConnectionIsItsOwnObservedRequestAndTheEndCompletesIt()
    {
        var second = new PushStream();
        var handler = new StubHandler
        {
            Responder = attempt => attempt == 0 ? Answer("data: one\n\n") : Answer(second),
        };

        var observer = new RecordingObserver();
        var (engine, clock) = SseEngine(handler, net => net.Observer = observer);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 3, "open, the message, and the error the ended stream queues");

        // The reconnect is a timer entry on the engine's own queue, so it takes the clock and a pump.
        clock.Advance(5000);
        Pump(engine, () => handler.Requests.Count == 2, "the reconnect");

        var events = observer.Take(6);
        var first = events[0].Split(' ')[1];
        var next = events[4].Split(' ')[1];

        next.Should().NotBe(first, "a reconnect is a second request, not a second hop of the first");
        events.Should().Equal(
            $"request {first} GET {StreamUrl} redirects=0 initiator=EventSource",
            $"response {first} 200 redirect=False",
            $"data {first} 11",
            $"completed {first} 11",
            $"request {next} GET {StreamUrl} redirects=0 initiator=EventSource",
            $"response {next} 200 redirect=False");

        engine.Execute("es.close();");
        second.Complete();
    }

    /// <summary>
    /// A response the standard fails the connection over reaches the observer as the response it was and
    /// then as a failure, so the terminal call is never left unmade.
    /// </summary>
    [Test]
    public void AResponseTheStandardRefusesIsReportedAsAFailure()
    {
        var handler = new StubHandler { Responder = _ => Answer("", "text/plain") };
        var observer = new RecordingObserver();
        var (engine, _) = SseEngine(handler, net => net.Observer = observer);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 1, "the error event");

        var events = observer.Take(3);
        var id = events[0].Split(' ')[1];

        events.Should().Equal(
            $"request {id} GET {StreamUrl} redirects=0 initiator=EventSource",
            $"response {id} 200 redirect=False",
            $"failed {id} The response is not a text/event-stream.");
    }

    [Test]
    public void DispatchesAMessageEventForEachBlankLineTerminatedBlock()
    {
        // The standard's own example: "the event's data attribute would contain the string YHOO\n+2\n10".
        var handler = new StubHandler { Responder = _ => Answer("data: YHOO\ndata: +2\ndata: 10\n\n") };
        var (engine, _) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 2, "the open and message events");

        Log(engine, 2).Should().Be("open~1|message~message~YHOO\n+2\n10~~https://example.org");

        // The request the standard describes: a GET announcing what it wants and refusing a cached answer.
        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be("GET");
        request.Header("accept").Should().Be("text/event-stream");
        request.Header("cache-control").Should().Be("no-cache");
        request.Header("last-event-id").Should().BeNull();
    }

    [Test]
    public void AnEventFieldRenamesTheEventAndAnIdFieldOutlivesIt()
    {
        // The standard's second worked example, minus the comment: an id sticks until the server changes it,
        // and an "id" with no value resets it.
        var handler = new StubHandler
        {
            Responder = _ => Answer(": keep-alive\n\ndata: first\nid: 1\n\nevent: ping\ndata: second\n\ndata: third\nid\n\n"),
        };

        var (engine, _) = SseEngine(handler);

        engine.Execute($"""
            var es = watch(new EventSource('{StreamUrl}'));
            es.addEventListener('ping', e => log.push('ping~' + e.data + '~' + e.lastEventId));
            """);

        PumpUntilLogHas(engine, 4, "the open event and three dispatches");

        // The renamed event reaches addEventListener and not onmessage, which is what makes a custom type
        // worth sending; the id set by the first block is still on the second and third.
        Log(engine, 4).Should().Be(
            "open~1|message~message~first~1~https://example.org|ping~second~1|message~message~third~~https://example.org");
    }

    [Test]
    public void ACommentOnlyStreamDispatchesNothingAndKeepsTheConnectionOpen()
    {
        var stream = new PushStream();
        var handler = new StubHandler { Responder = _ => Answer(stream) };
        var (engine, _) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");

        // A comment is the keep-alive of the protocol: it is a line, so it proves the connection is alive,
        // and it produces no event.
        stream.Push(":\n: keep-alive\n\n");
        PumpUntilLogHas(engine, 1, "the open event");

        for (var i = 0; i < 20; i++)
        {
            engine.Tasks.ProcessTasks();
            Thread.Sleep(2);
        }

        Log(engine).Should().Be("open~1");
        engine.Evaluate("es.readyState").AsNumber().Should().Be(1);

        engine.Execute("es.close();");
        stream.Complete();
    }

    [Test]
    public void ParsesTheFieldGrammarTheStandardSpellsOut()
    {
        // "The following stream fires two events" — the first with the empty string, the middle with a single
        // newline, and the last block discarded because no blank line follows it.
        var handler = new StubHandler { Responder = _ => Answer("data\n\ndata\ndata\n\ndata:\n") };
        var (engine, _) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 3, "the open event and two dispatches");

        Log(engine, 3).Should().Be("open~1|message~message~~~https://example.org|message~message~\n~~https://example.org");
    }

    [Test]
    public void RemovesExactlyOneSpaceAfterTheColon()
    {
        // "The following stream fires two identical events … because the space after the colon is ignored."
        var handler = new StubHandler { Responder = _ => Answer("data:test\n\ndata: test\n\ndata:  test\n\n") };
        var (engine, _) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 4, "three dispatches");

        engine.Evaluate("log.slice(1, 4).map(x => x.split('~')[2]).join('|')").AsString().Should().Be("test|test| test");
    }

    [Test]
    public void HandlesEveryLineEndingAndAChunkBoundaryInsideOne()
    {
        var stream = new PushStream();
        var handler = new StubHandler { Responder = _ => Answer(stream) };
        var (engine, _) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 1, "the open event");

        // A CRLF split across two reads: the CR ends the line and the LF that opens the next chunk must not
        // end an empty one, or the event would be dispatched a field early.
        stream.Push("data: crlf\r");
        PumpUntilLogHas(engine, 1, "no dispatch yet");
        stream.Push("\ndata: more\r\n\r\n");

        // A lone CR and a lone LF are line endings of their own.
        stream.Push("data: cr\r\rdata: lf\n\n");
        PumpUntilLogHas(engine, 4, "three dispatches");

        Log(engine, 4).Should().Be(
            "open~1|message~message~crlf\nmore~~https://example.org|message~message~cr~~https://example.org|message~message~lf~~https://example.org");

        engine.Execute("es.close();");
        stream.Complete();
    }

    [Test]
    public void StripsOneLeadingByteOrderMark()
    {
        // "The UTF-8 decode algorithm strips one leading UTF-8 Byte Order Mark (BOM), if any" — a BOM left in
        // place would make the first field name "﻿data", which is not a field name at all.
        var body = new List<byte> { 0xEF, 0xBB, 0xBF };
        body.AddRange(Encoding.UTF8.GetBytes("data: hello\n\n"));

        var handler = new StubHandler
        {
            Responder = _ =>
            {
                var content = new ByteArrayContent(body.ToArray());
                content.Headers.TryAddWithoutValidation("content-type", "text/event-stream");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            },
        };

        var (engine, _) = SseEngine(handler);
        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 2, "the message event");

        Log(engine, 2).Should().Be("open~1|message~message~hello~~https://example.org");
    }

    [Test]
    public void DecodesUtf8AcrossAChunkBoundary()
    {
        var stream = new PushStream();
        var handler = new StubHandler { Responder = _ => Answer(stream) };
        var (engine, _) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 1, "the open event");

        // The two halves of one UTF-8 sequence, in two reads.
        var bytes = Encoding.UTF8.GetBytes("data: héllo\n\n");
        stream.Push(bytes[..7]);
        stream.Push(bytes[7..]);

        PumpUntilLogHas(engine, 2, "the message event");
        Log(engine, 2).Should().Be("open~1|message~message~héllo~~https://example.org");

        engine.Execute("es.close();");
        stream.Complete();
    }

    [Test]
    public void ReadyStateFollowsTheConnectionAndTheConstantsAreOnBothObjects()
    {
        var stream = new PushStream();
        var handler = new StubHandler { Responder = _ => Answer(stream) };
        var (engine, _) = SseEngine(handler);

        // "When the object is created, its readyState must be set to CONNECTING (0)." Read inside the script,
        // because Execute drains the event loop on its way out and the connection may have opened by then.
        engine.Evaluate($"(() => {{ var s = new EventSource('{StreamUrl}'); var state = s.readyState; s.close(); return state; }})()")
            .AsNumber().Should().Be(0);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        engine.Evaluate("es.url").AsString().Should().Be(StreamUrl);
        engine.Evaluate("es.withCredentials").AsBoolean().Should().BeFalse();

        PumpUntilLogHas(engine, 1, "the open event");
        engine.Evaluate("es.readyState").AsNumber().Should().Be(1);

        engine.Execute("es.close();");
        engine.Evaluate("es.readyState").AsNumber().Should().Be(2);

        // https://webidl.spec.whatwg.org/#es-constants — on the interface object and on the prototype.
        engine.Evaluate("[EventSource.CONNECTING, EventSource.OPEN, EventSource.CLOSED].join(',')").AsString().Should().Be("0,1,2");
        engine.Evaluate("[EventSource.prototype.CONNECTING, EventSource.prototype.OPEN, EventSource.prototype.CLOSED].join(',')").AsString().Should().Be("0,1,2");

        // … in the order the IDL declares them, which that section defines them in and which a record
        // conversion over the interface object reads.
        engine.Evaluate("Object.keys(EventSource).join(',')").AsString().Should().Be("CONNECTING,OPEN,CLOSED");

        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(EventSource, 'OPEN')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeFalse();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeFalse();

        stream.Complete();
    }

    [Test]
    public Task CloseStopsEverythingAndDispatchesNothingFurther() => DedicatedThread.RunAsync(() =>
    {
        var stream = new PushStream();
        var handler = new StubHandler { Responder = _ => Answer(stream) };
        var (engine, _) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 1, "the open event");

        // Pushed before the close and delivered after it: step 8 of the dispatch steps only dispatches "if
        // the readyState attribute is set to a value other than CLOSED".
        stream.Push("data: dropped\n\n");
        engine.Execute("es.close();");

        for (var i = 0; i < 20; i++)
        {
            engine.Tasks.ProcessTasks();
            Thread.Sleep(2);
        }

        Log(engine).Should().Be("open~1");
        engine.Evaluate("es.readyState").AsNumber().Should().Be(2);

        // The transport was told, not just the object: the read in flight was cancelled.
        stream.ReadCancelled.Wait(TransportSignalCeiling).Should().BeTrue(
            "closing an EventSource must cancel the read in flight, not merely mark the object CLOSED");

        stream.Complete();
    });

    [Test]
    public void CloseFromInsideAListenerStopsTheRestOfTheSameChunk()
    {
        // Both events arrive in one read and are delivered by one job, so the readyState is what stops the
        // second — step 8 of the dispatch steps is a task per event, and each of them asks again.
        var handler = new StubHandler { Responder = _ => Answer("data: one\n\ndata: two\n\n") };
        var (engine, _) = SseEngine(handler);

        engine.Execute($$"""
            var es = watch(new EventSource('{{StreamUrl}}'));
            es.addEventListener('message', e => { if (e.data === 'one') { es.close(); } });
            """);

        PumpUntilLogHas(engine, 2, "the open and first message events");

        for (var i = 0; i < 20; i++)
        {
            engine.Tasks.ProcessTasks();
            Thread.Sleep(2);
        }

        Log(engine).Should().Be("open~1|message~message~one~~https://example.org");
        engine.Evaluate("es.readyState").AsNumber().Should().Be(2);
    }

    [Test]
    public void AWrongContentTypeFailsTheConnectionForGood()
    {
        // "if res's status is not 200, or if res's Content-Type is not text/event-stream, then fail the
        // connection" — and "once the user agent has failed the connection, it does not attempt to reconnect".
        var handler = new StubHandler { Responder = _ => Answer("data: nope\n\n", "text/plain") };
        var (engine, clock) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 1, "the error event");

        Log(engine).Should().Be("error~2");
        engine.Evaluate("es.readyState").AsNumber().Should().Be(2);

        clock.Advance(60_000);
        engine.Tasks.ProcessTasks();
        handler.Requests.Should().ContainSingle();
    }

    [Test]
    public void AContentTypeWithParametersIsStillAnEventStream()
    {
        // The check is on the MIME type essence, so a charset parameter changes nothing.
        var handler = new StubHandler { Responder = _ => Answer("data: ok\n\n", "TEXT/Event-Stream; charset=utf-8") };
        var (engine, _) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 2, "the message event");

        engine.Evaluate("log[1]").AsString().Should().Be("message~message~ok~~https://example.org");
    }

    [Test]
    public void ANon200StatusFailsTheConnectionForGood()
    {
        var handler = new StubHandler { Responder = _ => Answer("", "text/event-stream", HttpStatusCode.NoContent) };
        var (engine, clock) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 1, "the error event");

        Log(engine).Should().Be("error~2");

        clock.Advance(60_000);
        engine.Tasks.ProcessTasks();
        handler.Requests.Should().ContainSingle();
    }

    [Test]
    public void AUrlThePolicyRefusesFailsTheConnectionWithoutOpeningASocket()
    {
        var handler = new StubHandler();
        var (engine, _) = SseEngine(handler, net => net.UrlFilter = uri => !uri.Host.StartsWith("169.254.", StringComparison.Ordinal));

        engine.Execute("var es = watch(new EventSource('https://169.254.169.254/latest/meta-data/'));");

        // The failure is a queued task, not a throw: the constructor answered with an object, exactly as the
        // standard's step 16 says, and the object then failed.
        PumpUntilLogHas(engine, 1, "the error event");
        Log(engine).Should().Be("error~2");
        handler.Requests.Should().BeEmpty();
    }

    [Test]
    public void ASchemeTheHostDidNotAllowFailsTheConnection()
    {
        var handler = new StubHandler();
        var (engine, _) = SseEngine(handler, net => net.AllowedSchemes.Remove("http"));

        engine.Execute("var es = watch(new EventSource('http://example.org/stream'));");
        PumpUntilLogHas(engine, 1, "the error event");

        Log(engine).Should().Be("error~2");
        handler.Requests.Should().BeEmpty();
    }

    [Test]
    public void ReconnectsAfterTheStreamEndsUsingTheAnnouncedRetryAndTheLastEventId()
    {
        var handler = new StubHandler
        {
            Responder = attempt => attempt == 0
                ? Answer("retry: 250\ndata: one\nid: 42\n\n")
                : Answer("data: two\n\n"),
        };

        var (engine, clock) = SseEngine(handler);
        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");

        // The first stream ends by itself, which is a reestablish: error, CONNECTING, and a delay.
        PumpUntilLogHas(engine, 3, "the open, message and error events");
        Log(engine).Should().Be("open~1|message~message~one~42~https://example.org|error~0");
        engine.Evaluate("es.readyState").AsNumber().Should().Be(0);

        // Nothing has been retried yet: the delay is on the timer queue, and this clock has not moved.
        for (var i = 0; i < 10; i++)
        {
            engine.Tasks.ProcessTasks();
            Thread.Sleep(2);
        }

        handler.Requests.Should().ContainSingle();

        // Just short of the announced 250ms, then past it.
        clock.Advance(200);
        engine.Tasks.ProcessTasks();
        handler.Requests.Should().ContainSingle();

        clock.Advance(60);
        PumpUntilLogHas(engine, 5, "the second connection's open and message events");

        Log(engine, 5).Should().Be(
            "open~1|message~message~one~42~https://example.org|error~0|open~1|message~message~two~42~https://example.org");

        // Step 5 of reestablishing the connection: the last event ID string rides the retry.
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].Header("last-event-id").Should().Be("42");
    }

    [Test]
    public void AReconnectionRunsTheUrlPolicyAgain()
    {
        var handler = new StubHandler { Responder = _ => Answer("retry: 10\ndata: one\n\n") };
        var allow = 1;

        var (engine, clock) = SseEngine(handler, net => net.UrlFilter = _ => Volatile.Read(ref allow) != 0);
        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");

        PumpUntilLogHas(engine, 3, "the first connection ending");

        // Revoking the destination between two attempts really does stop the next one.
        Volatile.Write(ref allow, 0);
        clock.Advance(50);
        PumpUntilLogHas(engine, 4, "the failure of the retry");

        Log(engine).Should().Be("open~1|message~message~one~~https://example.org|error~0|error~2");
        engine.Evaluate("es.readyState").AsNumber().Should().Be(2);
        handler.Requests.Should().ContainSingle();
    }

    [Test]
    public void CloseDuringTheReconnectDelayStopsTheRetry()
    {
        var handler = new StubHandler { Responder = _ => Answer("retry: 10\ndata: one\n\n") };
        var (engine, clock) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 3, "the first connection ending");

        engine.Execute("es.close();");
        clock.Advance(1000);

        for (var i = 0; i < 20; i++)
        {
            engine.Tasks.ProcessTasks();
            Thread.Sleep(2);
        }

        // "If the EventSource object's readyState attribute is not set to CONNECTING, then return."
        handler.Requests.Should().ContainSingle();
        engine.Evaluate("es.readyState").AsNumber().Should().Be(2);
    }

    [Test]
    public void AnEventLargerThanTheCapFailsTheConnection()
    {
        // MaxResponseBytes cannot bound a stream, so it bounds one event — which is what has to be held in
        // memory. Like every host limit, exceeding it does not reconnect.
        var handler = new StubHandler { Responder = _ => Answer("data: " + new string('x', 200) + "\n\n") };
        var (engine, clock) = SseEngine(handler, net => net.MaxResponseBytes = 64);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 2, "the open and error events");

        Log(engine).Should().Be("open~1|error~2");

        clock.Advance(60_000);
        engine.Tasks.ProcessTasks();
        handler.Requests.Should().ContainSingle();
    }

    [Test]
    public void AnEventAtTheCapStillArrives()
    {
        // Fifty x's, plus the "data: " the line itself costs, is what fits under a cap of sixty-four: the cap
        // is on what the parser holds, which is the data buffer plus the line it is reading.
        var handler = new StubHandler { Responder = _ => Answer("data: " + new string('x', 50) + "\n\n") };
        var (engine, _) = SseEngine(handler, net => net.MaxResponseBytes = 64);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 2, "the message event");

        engine.Evaluate("log[1].split('~')[2].length").AsNumber().Should().Be(50);
    }

    [Test]
    public void RefusesMoreStreamsThanTheHostAllows()
    {
        var stream = new PushStream();
        var handler = new StubHandler { Responder = _ => Answer(stream) };
        var (engine, _) = SseEngine(handler, net => net.MaxConcurrentRequests = 1);

        engine.Execute($"var first = watch(new EventSource('{StreamUrl}')); var second = watch(new EventSource('{StreamUrl}'));");
        PumpUntilLogHas(engine, 2, "the first connection opening and the second failing");

        engine.Evaluate("second.readyState").AsNumber().Should().Be(2);
        engine.Evaluate("first.readyState").AsNumber().Should().Be(1);
        handler.Requests.Should().ContainSingle();

        // The slot comes back when the stream that held it closes.
        engine.Execute("first.close();");
        engine.Execute($"var third = watch(new EventSource('{StreamUrl}'));");
        Pump(engine, () => engine.Evaluate("third.readyState").AsNumber() == 1, "the third connection opening");

        engine.Execute("third.close();");
        stream.Complete();
    }

    // ---------------------------------------------------------------- data: URLs

    /// <summary>
    /// One event, <c>data: hi</c>, as the issue that asked for this lane spelled it (sebastienros/jint#4283).
    /// </summary>
    private const string HelloDataUrl = "data:text/event-stream,data:%20hi%0A%0A";

    /// <summary>
    /// What one pass of <see cref="HelloDataUrl"/> logs: the announcement, the event, and the end of the body —
    /// which is not a network error, so it reestablishes the connection rather than failing it. The origin is
    /// <c>null</c>, because a <c>data:</c> URL's origin is opaque.
    /// </summary>
    private const string HelloPass = "open~1|message~message~hi~~null|error~0";

    /// <summary>
    /// Pumps an engine whose only work is a <c>data:</c> URL's. Every job that lane queues is queued from the
    /// engine thread itself, so a fixed number of turns drains it; nothing here waits on another thread.
    /// </summary>
    private static void Drain(Engine engine)
    {
        for (var i = 0; i < 10; i++)
        {
            engine.Tasks.ProcessTasks();
        }
    }

    /// <summary>
    /// Main fetch sends a <c>data:</c> URL to scheme fetch's <c>data</c> arm whatever the request's mode, so an
    /// <c>EventSource</c> gets a 200 whose <c>Content-Type</c> is the URL's own: announced, parsed line by line,
    /// and — because the end of a body that was not a network error reestablishes the connection — followed by
    /// <c>error</c> with <c>CONNECTING</c>. No request reaches the host's client, and the constructor returns
    /// before any of it, as constructor step 16 does for every URL.
    /// </summary>
    [Test]
    public void ADataUrlIsAnnouncedParsedAndReestablishedWithoutATransport()
    {
        var handler = new StubHandler();
        var (engine, _) = SseEngine(handler);

        engine.Evaluate($"(() => {{ var s = watch(new EventSource('{HelloDataUrl}')); var seen = s.readyState + ':' + log.length; s.close(); return seen; }})()")
            .AsString().Should().Be("0:0");
        Drain(engine);
        Log(engine).Should().BeEmpty("a source closed before its first task ran dispatches nothing");

        engine.Execute($"var es = watch(new EventSource('{HelloDataUrl}'));");
        PumpUntilLogHas(engine, 3, "the open, message and error events");

        Log(engine).Should().Be(HelloPass);
        engine.Evaluate("es.readyState").AsNumber().Should().Be(0);
        engine.Evaluate("es.url").AsString().Should().Be(HelloDataUrl);
        handler.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// The end of the body reestablishes the connection, so the same URL is fetched again every reconnection
    /// time — and the replay is a task on the engine's timer queue, the default three seconds of it until a
    /// <c>retry</c> field says otherwise.
    /// </summary>
    [Test]
    public void TheReplayWaitsTheDefaultReconnectionTimeOnTheTimerQueue()
    {
        var handler = new StubHandler();
        var (engine, clock) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{HelloDataUrl}'));");
        PumpUntilLogHas(engine, 3, "the first pass");

        // The only thing scheduled is the reconnect delay, and it is a timer: an engine nobody pumps replays
        // nothing, and this clock has not moved.
        engine.Tasks.TimeUntilNextScheduledWork.Should().Be(TimeSpan.FromMilliseconds(3000));

        clock.Advance(2999);
        Drain(engine);
        Log(engine).Should().Be(HelloPass);

        clock.Advance(1);
        PumpUntilLogHas(engine, 6, "the replay");
        Log(engine).Should().Be(HelloPass + "|" + HelloPass);
        handler.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// A <c>retry</c> field sets the reconnection time the replay waits, and an <c>id</c> field is the last
    /// event ID every event of the next pass starts from.
    /// </summary>
    [Test]
    public void TheReplayHonoursRetryAndCarriesTheLastEventId()
    {
        var handler = new StubHandler();
        var (engine, clock) = SseEngine(handler);

        engine.Execute("var es = watch(new EventSource('data:text/event-stream,retry:%20250%0Aid:%207%0Adata:%20tick%0A%0Adata:%20tock%0A%0A'));");
        PumpUntilLogHas(engine, 4, "the first pass");

        const string Pass = "open~1|message~message~tick~7~null|message~message~tock~7~null|error~0";
        Log(engine).Should().Be(Pass);

        clock.Advance(200);
        Drain(engine);
        Log(engine).Should().Be(Pass);

        clock.Advance(60);
        PumpUntilLogHas(engine, 8, "the replay after the announced 250ms");
        Log(engine).Should().Be(Pass + "|" + Pass);
    }

    /// <summary>
    /// <c>close()</c> during the reconnect delay cancels the replay — "if the EventSource object's readyState
    /// attribute is not set to CONNECTING, then return" — and leaves the engine nothing scheduled.
    /// </summary>
    [Test]
    public void CloseDuringTheReplayDelayCancelsTheReplay()
    {
        var handler = new StubHandler();
        var (engine, clock) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{HelloDataUrl}'));");
        PumpUntilLogHas(engine, 3, "the first pass");
        engine.Tasks.TimeUntilNextScheduledWork.Should().NotBeNull();

        engine.Execute("es.close();");
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();

        clock.Advance(60_000);
        Drain(engine);

        Log(engine).Should().Be(HelloPass);
        engine.Evaluate("es.readyState").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// A listener that closes the source stops the rest of the pass — the events after it and the end of the
    /// body that would have reestablished the connection — and nothing is left queued or scheduled.
    /// </summary>
    [TestCase("open", "open~1")]
    [TestCase("message", "open~1|message~message~one~~null")]
    public void CloseFromAListenerEndsThePass(string type, string expected)
    {
        var handler = new StubHandler();
        var (engine, clock) = SseEngine(handler);

        engine.Execute($$"""
            var es = watch(new EventSource('data:text/event-stream,data:%20one%0A%0Adata:%20two%0A%0A'));
            es.addEventListener('{{type}}', () => es.close());
            """);
        Drain(engine);

        Log(engine).Should().Be(expected);
        engine.Evaluate("es.readyState").AsNumber().Should().Be(2);
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();

        clock.Advance(60_000);
        Drain(engine);
        Log(engine).Should().Be(expected);
    }

    /// <summary>
    /// "If res's status is not 200, or if res's Content-Type is not text/event-stream, then fail the
    /// connection" — the arm's 200 carries the URL's own MIME type, so any other one fails, for good. A
    /// <c>data:</c> URL naming none is <c>text/plain;charset=US-ASCII</c>.
    /// </summary>
    [TestCase("data:,data:%20hi%0A%0A")]
    [TestCase("data:text/plain,data:%20hi%0A%0A")]
    [TestCase("data:application/json;base64,ZGF0YTogaGkKCg==")]
    [TestCase("data:text/event-stream/x,data:%20hi%0A%0A")]
    public void ADataUrlOfAnotherMimeTypeFailsTheConnectionForGood(string url)
    {
        var handler = new StubHandler();
        var (engine, clock) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{url}'));");
        PumpUntilLogHas(engine, 1, "the error event");

        Log(engine).Should().Be("error~2");
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();

        clock.Advance(60_000);
        Drain(engine);
        Log(engine).Should().Be("error~2");
        handler.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// The check is on the MIME type essence, so a parameter — or a case the URL parser keeps — changes nothing.
    /// </summary>
    [TestCase("data:TEXT/Event-Stream;charset=utf-8,data:%20ok%0A%0A")]
    [TestCase("data:text/event-stream;,data:%20ok%0A%0A")]
    public void AMimeTypeWithParametersIsStillAnEventStream(string url)
    {
        var (engine, _) = SseEngine(new StubHandler());

        engine.Execute($"var es = watch(new EventSource('{url}'));");
        PumpUntilLogHas(engine, 2, "the message event");

        engine.Evaluate("log[1]").AsString().Should().Be("message~message~ok~~null");
        engine.Execute("es.close();");
    }

    /// <summary>
    /// A base64 payload is decoded by the <c>data:</c> URL processor before the event stream parser sees a
    /// byte, and the stream is then UTF-8 whatever the URL's charset says.
    /// </summary>
    [TestCase("data: hi\n\n", "hi")]
    [TestCase("data: héllo\r\ndata: wörld\r\n\r\n", "héllo\nwörld")]
    public void ABase64DataUrlIsDecodedBeforeItIsParsed(string stream, string data)
    {
        var (engine, _) = SseEngine(new StubHandler());
        var url = "data:text/event-stream;charset=windows-1252;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(stream));

        engine.Execute($"var es = watch(new EventSource('{url}'));");
        PumpUntilLogHas(engine, 3, "the first pass");

        Log(engine).Should().Be($"open~1|message~message~{data}~~null|error~0");
        engine.Execute("es.close();");
    }

    /// <summary>
    /// A URL the processor refuses is a network error that every later attempt would meet again, which is the
    /// case step 15.ii lets a user agent fail rather than reestablish: "unless the user agent knows that to be
    /// futile".
    /// </summary>
    [TestCase("data:text/event-stream")]
    [TestCase("data:text/event-stream;base64,%%%")]
    public void AnUnprocessableDataUrlFailsTheConnectionForGood(string url)
    {
        var (engine, clock) = SseEngine(new StubHandler());

        engine.Execute($"var es = watch(new EventSource('{url}'));");
        PumpUntilLogHas(engine, 1, "the error event");

        Log(engine).Should().Be("error~2");
        clock.Advance(60_000);
        Drain(engine);
        Log(engine).Should().Be("error~2");
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    /// <summary>
    /// <c>MaxResponseBytes</c> bounds the decoded body, so a script cannot escape the host's size ceiling by
    /// inlining a stream: over it, the connection fails before it is announced and is not retried. The bound is
    /// on what the payload decodes to, not on its length — a base64 payload of sixteen characters is ten bytes.
    /// </summary>
    [TestCase("data:text/event-stream,data:%20hi%0A%0A", "open~1|message~message~hi~~null|error~0")]
    [TestCase("data:text/event-stream;base64,ZGF0YTogaGkKCg==", "open~1|message~message~hi~~null|error~0")]
    [TestCase("data:text/event-stream,data:%20hi!%0A%0A", "error~2")]
    public void MaxResponseBytesBoundsTheDecodedBody(string url, string expected)
    {
        var (engine, clock) = SseEngine(new StubHandler(), net => net.MaxResponseBytes = 10);

        engine.Execute($"var es = watch(new EventSource('{url}'));");
        PumpUntilLogHas(engine, expected.Split('|').Length, "the first pass");

        Log(engine).Should().Be(expected);
        engine.Execute("es.close();");
        clock.Advance(60_000);
        Drain(engine);
        Log(engine).Should().Be(expected);
    }

    /// <summary>
    /// No request happens, so an observer hears nothing — not of the pass, not of a replay, not of a failure.
    /// The control proves the observer is wired: a network URL the policy refuses is reported to it.
    /// </summary>
    [Test]
    public void AnObserverIsToldNothingOfADataUrl()
    {
        var observer = new RecordingObserver();
        var (engine, clock) = SseEngine(new StubHandler(), net =>
        {
            net.Observer = observer;
            net.MaxResponseBytes = 16;
            net.UrlFilter = uri => uri.Scheme != "https";
        });

        engine.Execute($$"""
            var es = watch(new EventSource('{{HelloDataUrl}}'));
            var refused = new EventSource('data:text/plain,data:%20hi%0A%0A');
            var tooLarge = new EventSource('data:text/event-stream,data:%20more%20than%20sixteen%0A%0A');
            """);
        PumpUntilLogHas(engine, 3, "the first pass");

        clock.Advance(3000);
        PumpUntilLogHas(engine, 6, "the replay");
        engine.Execute("es.close();");
        observer.Count.Should().Be(0);

        engine.Execute($"new EventSource('{StreamUrl}');");
        observer.Take(1).Single().Should().StartWith("failed ").And.EndWith($" The fetch policy refused '{StreamUrl}'.");
    }

    /// <summary>
    /// The bytes are in the URL, so the destination policy has nothing to decide: a scheme list without
    /// <c>data</c>, a filter that refuses everything and a concurrency cap of zero all leave the stream
    /// working — the filter is never even asked, on the first pass or on a replay.
    /// </summary>
    [Test]
    public void TheSchemeListTheFilterAndTheConcurrencyCapDoNotApplyToADataUrl()
    {
        var handler = new StubHandler();
        var filterCalls = 0;
        var (engine, clock) = SseEngine(handler, net =>
        {
            net.AllowedSchemes.Clear();
            net.AllowedSchemes.Add("https");
            net.UrlFilter = _ =>
            {
                Interlocked.Increment(ref filterCalls);
                return false;
            };
            net.MaxConcurrentRequests = 0;
        });

        engine.Execute($"var es = watch(new EventSource('{HelloDataUrl}'));");
        PumpUntilLogHas(engine, 3, "the first pass");

        clock.Advance(3000);
        PumpUntilLogHas(engine, 6, "the replay");

        Log(engine).Should().Be(HelloPass + "|" + HelloPass);
        Volatile.Read(ref filterCalls).Should().Be(0);
        handler.Requests.Should().BeEmpty();
        engine.Execute("es.close();");
    }

    /// <summary>
    /// Listing <c>data</c> among the allowed schemes used to hand the URL to the host's client; the engine
    /// answers it either way, and the client is never asked.
    /// </summary>
    [Test]
    public void ADataUrlOnTheSchemeListStillNeverReachesTheTransport()
    {
        var handler = new StubHandler { Responder = _ => Answer("data: from the network\n\n") };
        var (engine, _) = SseEngine(handler, net => net.AllowedSchemes.Add("data"));

        engine.Execute($"var es = watch(new EventSource('{HelloDataUrl}'));");
        PumpUntilLogHas(engine, 3, "the first pass");

        Log(engine).Should().Be(HelloPass);
        handler.Requests.Should().BeEmpty();
        engine.Execute("es.close();");
    }

    /// <summary>
    /// An engine whose listeners report to a host-side list rather than to a script array, for the tests whose
    /// engine can no longer be asked to evaluate anything — one that has been cancelled, or restored.
    /// </summary>
    private static (Engine Engine, ManualClock Clock, List<string> Records) RecordingDataUrlEngine(CancellationToken cancellationToken = default)
    {
        var clock = new ManualClock();
        var records = new List<string>();
        var engine = new Engine(options =>
        {
            options.WebApi.Timers.TimeProvider = clock;
            if (cancellationToken.CanBeCanceled)
            {
                options.ObserveCancellation(cancellationToken);
            }

            options.UseEventSource();
        });

        engine.SetValue("record", new Action<string>(records.Add));
        return (engine, clock, records);
    }

    private const string RecordedHello = $$"""
        var es = new EventSource('{{HelloDataUrl}}');
        es.onopen = () => record('open');
        es.onmessage = e => record(e.data);
        es.onerror = () => record('error' + es.readyState);
        """;

    /// <summary>
    /// The engine's cancellation ends a <c>data:</c> URL's replay as it ends a network connection: silently,
    /// with no event a listener could carry on from — which here would also be a listener throwing out of the
    /// pump every reconnection time — and with nothing left scheduled.
    /// </summary>
    [Test]
    public void AnEngineCancellationEndsTheReplaySilently()
    {
        using var cancellation = new CancellationTokenSource();
        var (engine, clock, records) = RecordingDataUrlEngine(cancellation.Token);

        engine.Execute(RecordedHello);
        Drain(engine);
        string.Join("|", records).Should().Be("open|hi|error0");

        cancellation.Cancel();
        clock.Advance(60_000);
        Drain(engine);

        string.Join("|", records).Should().Be("open|hi|error0");
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    /// <summary>
    /// A restore clears the timer queue the replay waits on, so the pass a cycle started never replays into the
    /// restored globals.
    /// </summary>
    [Test]
    public void ARestoreTakesTheDataUrlReplayWithIt()
    {
        var (engine, clock, records) = RecordingDataUrlEngine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute(RecordedHello);
        Drain(engine);
        string.Join("|", records).Should().Be("open|hi|error0");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        clock.Advance(60_000);
        Drain(engine);

        string.Join("|", records).Should().Be("open|hi|error0");
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
        engine.Evaluate("typeof es").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// The replay holds nothing outside the engine — no thread, no operating-system timer — so an engine left
    /// in a reconnect delay is collectable once the host lets go of it.
    /// </summary>
    [Test]
    public void AnEngineInADataUrlsReplayDelayIsCollectable()
    {
        const int count = 4;
        var references = new List<WeakReference>(count);
        for (var i = 0; i < count; i++)
        {
            references.Add(OpenAndForget());
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        references.Count(static reference => reference.IsAlive).Should().Be(0,
            "a data: URL's replay must be held by the engine's own timer queue and nothing else");

        // NoInlining so the engine cannot stay rooted in this frame across the collection.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static WeakReference OpenAndForget()
        {
            var (engine, _) = SseEngine(new StubHandler());
            engine.Execute($"var es = watch(new EventSource('{HelloDataUrl}'));");
            Drain(engine);
            Log(engine).Should().Be(HelloPass);
            engine.Tasks.TimeUntilNextScheduledWork.Should().NotBeNull();
            return new WeakReference(engine);
        }
    }

    [Test]
    public void AnUnparsableUrlIsASyntaxErrorDomException()
    {
        // Constructor step 4: "if urlRecord is failure, then throw a SyntaxError DOMException" — which is not
        // the TypeError the fetch interfaces raise for the same mistake.
        var handler = new StubHandler();
        var (engine, _) = SseEngine(handler);

        engine.Evaluate("(() => { try { new EventSource('nonsense'); } catch (e) { return e.name + '|' + (e instanceof DOMException); } })()")
            .AsString().Should().Be("SyntaxError|true");

        engine.Evaluate("(() => { try { new EventSource(); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");

        handler.Requests.Should().BeEmpty();
    }

    [Test]
    public void ARelativeUrlIsResolvedAgainstTheBaseUrl()
    {
        // Constructor step 3: "let urlRecord be the result of encoding-parsing a URL given url, relative to
        // settings". Options.WebApi.Fetch.BaseUrl is what stands in for the settings object here, and it is
        // what Request and WebSocket already resolve against - so all three interfaces read one relative URL
        // the same way, and a document embedding this engine can write the URL it would write in a browser.
        var handler = new StubHandler();
        var (engine, _) = SseEngine(handler, net => net.BaseUrl = new Uri("https://example.org/pages/one.html"));

        engine.Execute("var source = new EventSource('../stream');");
        Pump(engine, () => handler.Requests.Count > 0, "the connection to be attempted");

        handler.Requests[0].Url.Should().Be(StreamUrl);
        engine.Execute("source.close();");
    }

    [Test]
    public void AnEngineWithNoBaseUrlStillRequiresAnAbsoluteUrl()
    {
        // The other half of the same rule: with nothing to resolve against, a relative URL is a failure and
        // step 4's SyntaxError is what a script gets - not a request to a host nobody named.
        var handler = new StubHandler();
        var (engine, _) = SseEngine(handler);

        engine.Evaluate("(() => { try { new EventSource('../stream'); } catch (e) { return e.name; } })()")
            .AsString().Should().Be("SyntaxError");

        handler.Requests.Should().BeEmpty();
    }

    [Test]
    public void WithCredentialsIsRememberedAndChangesNothing()
    {
        var handler = new StubHandler { Responder = _ => Answer("data: ok\n\n") };
        var (engine, _) = SseEngine(handler);

        engine.Execute($"var es = watch(new EventSource('{StreamUrl}', {{ withCredentials: true }}));");
        PumpUntilLogHas(engine, 2, "the message event");

        engine.Evaluate("es.withCredentials").AsBoolean().Should().BeTrue();

        // There is no cookie jar and no credential store for it to select, so the request is the same one.
        handler.Requests[0].Header("cookie").Should().BeNull();
        handler.Requests[0].Header("authorization").Should().BeNull();
    }

    [Test]
    public void AnEventSourceIsAnEventTargetAndItsEventsAreEvents()
    {
        var handler = new StubHandler { Responder = _ => Answer("data: ok\n\n") };
        var (engine, _) = SseEngine(handler);

        engine.Execute($"""
            var es = new EventSource('{StreamUrl}');
            var seen;
            es.addEventListener('message', e => seen = e);
            """);

        Pump(engine, () => !engine.Evaluate("seen").IsUndefined(), "the message event");

        engine.Evaluate("es instanceof EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("seen instanceof MessageEvent").AsBoolean().Should().BeTrue();
        engine.Evaluate("seen instanceof Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("seen.isTrusted").AsBoolean().Should().BeTrue();
        engine.Evaluate("seen.target === es").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(seen)").AsString().Should().Be("[object MessageEvent]");
        engine.Evaluate("seen.source").Should().Be(Jint.Native.JsValue.Null);
        engine.Evaluate("Array.isArray(seen.ports) && seen.ports.length === 0 && Object.isFrozen(seen.ports)").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void MessageEventIsConstructibleWithTheMembersItsDictionaryDeclares()
    {
        var handler = new StubHandler();
        var (engine, _) = SseEngine(handler);

        engine.Evaluate("new MessageEvent('x').data").Should().Be(Jint.Native.JsValue.Null);
        engine.Evaluate("new MessageEvent('x').origin").AsString().Should().BeEmpty();
        engine.Evaluate("new MessageEvent('x').lastEventId").AsString().Should().BeEmpty();
        engine.Evaluate("new MessageEvent('x', { data: 1, origin: 'o', lastEventId: '7' }).data + ':' + new MessageEvent('x', { origin: 'o' }).origin")
            .AsString().Should().Be("1:o");
        engine.Evaluate("new MessageEvent('x').isTrusted").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.getPrototypeOf(MessageEvent) === Event").AsBoolean().Should().BeTrue();

        // Nothing in this engine is a MessagePort, so anything in either member is a TypeError.
        engine.Evaluate("(() => { try { new MessageEvent('x', { source: {} }); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");
        engine.Evaluate("(() => { try { new MessageEvent('x', { ports: [{}] }); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");
        engine.Evaluate("new MessageEvent('x', { ports: [] }).ports.length").AsNumber().Should().Be(0);
    }

    [Test]
    public void InstallsTheInterfaceObjectsBehindItsOwnFlagAndNowhereElse()
    {
        foreach (var name in new[] { "EventSource", "MessageEvent" })
        {
            new Engine().Evaluate($"typeof {name}").AsString().Should().Be("undefined");

            // EventSource is not in the default set and is not brought along by fetch either: two separate
            // grants. MessageEvent is different — the messaging feature is part of Default and installs the
            // same intrinsic — so only the network grant claims hold for it.
            if (name == "EventSource")
            {
                new Engine(options => options.UseWebApis()).Evaluate($"typeof {name}").AsString().Should().Be("undefined");
            }

            new Engine(options => options.UseFetch()).Evaluate($"typeof {name}").AsString().Should().Be("undefined");

            var engine = new Engine(options => options.UseEventSource());
            engine.Evaluate($"typeof {name}").AsString().Should().Be("function");

            // An interface object: writable and configurable, but not enumerable.
            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);
            descriptor.Enumerable.Should().BeFalse();
            descriptor.Writable.Should().BeTrue();
            descriptor.Configurable.Should().BeTrue();

            // ... and never inside a shadow realm.
            engine.Evaluate($"new ShadowRealm().evaluate('typeof {name}')").AsString().Should().Be("undefined");
        }
    }

    [Test]
    public void BringsTheEventsFeatureAndNotTheFetchOne()
    {
        // The closure is computed at install, so it catches a host that assigned Features directly.
        var options = new Options();
        options.WebApi.Features = WebApiFeatures.EventSource;

        var engine = new Engine(options);
        engine.Evaluate("typeof EventTarget").AsString().Should().Be("function");
        engine.Evaluate("typeof AbortController").AsString().Should().Be("function");
        engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");

        // ... and the option value still reads back exactly what the host asked for.
        options.WebApi.Features.Should().Be(WebApiFeatures.EventSource);
    }

    [Test]
    public void TheInterfaceObjectRefusesToBeCalledWithoutNew()
    {
        var engine = new Engine(options => options.UseEventSource());

        engine.Evaluate("(() => { try { EventSource('https://example.org/'); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");

        engine.Evaluate("EventSource.length").AsNumber().Should().Be(1);
        engine.Evaluate("EventSource.name").AsString().Should().Be("EventSource");
        engine.Evaluate("Object.getPrototypeOf(EventSource) === EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(EventSource.prototype) === EventTarget.prototype").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void TheAccessorsRefuseAForeignReceiver()
    {
        var engine = new Engine(options => options.UseEventSource());

        foreach (var member in new[] { "url", "readyState", "withCredentials", "onmessage" })
        {
            engine.Evaluate($"(() => {{ try {{ Object.getOwnPropertyDescriptor(EventSource.prototype, '{member}').get.call({{}}); }} catch (e) {{ return e.constructor.name; }} }})()")
                .AsString().Should().Be("TypeError");
        }

        engine.Evaluate("(() => { try { EventSource.prototype.close.call({}); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");
    }

    /// <summary>
    /// What an event handler IDL attribute is —
    /// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes — read on an
    /// <c>EventSource</c>: the handler is <b>one entry of the object's own listener list</b> that keeps the
    /// position it first took, a non-object clears it (<c>[LegacyTreatNonObjectAsNull]</c>), and an object
    /// that is not callable is stored and read back but never invoked.
    /// </summary>
    [Test]
    public void AHandlerAttributeKeepsItsPositionAndTakesANonObjectAsNull()
    {
        var stream = new PushStream();
        var handler = new StubHandler { Responder = _ => Answer(stream) };
        var (engine, _) = SseEngine(handler);

        engine.Execute($"""
            var es = new EventSource('{StreamUrl}');
            es.onmessage = () => log.push('handler');
            es.addEventListener('message', () => log.push('listener'));
            es.onmessage = () => log.push('replaced');
            """);

        stream.Push("data: one\n\n");
        PumpUntilLogHas(engine, 2, "both message handlers");

        // Reassigning replaced the callback in place, so the handler still runs before a listener added after
        // it — a remove-and-add would have put it last.
        Log(engine, 2).Should().Be("replaced|listener");

        engine.Execute("es.onmessage = 42;");
        engine.Evaluate("es.onmessage").IsNull().Should().BeTrue();

        stream.Push("data: two\n\n");
        PumpUntilLogHas(engine, 3, "the listener alone");
        Log(engine, 3).Should().Be("replaced|listener|listener");

        // An object that is not callable is kept and read back, and the dispatch simply passes it over.
        engine.Execute("var bag = {}; es.onmessage = bag;");
        engine.Evaluate("es.onmessage === bag").AsBoolean().Should().BeTrue();

        stream.Push("data: three\n\n");
        PumpUntilLogHas(engine, 4, "the listener alone again");
        Log(engine, 4).Should().Be("replaced|listener|listener|listener");

        stream.Complete();
    }
}
#endif
