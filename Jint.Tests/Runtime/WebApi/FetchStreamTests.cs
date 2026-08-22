#if NET8_0_OR_GREATER
#nullable enable

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Jint.WebApi.Streams;
using SystemEncoding = System.Text.Encoding;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// A network response body as a live <c>ReadableStream</c> — https://fetch.spec.whatwg.org/#concept-response-body
/// — driven against a transport whose every read is gated by the test, so that nothing here depends on a
/// clock.
/// </summary>
/// <remarks>
/// <para>
/// <b>No assertion in this file races a wall clock.</b> What is asserted is structural: how many times the
/// transport was read, in which order the chunks arrived, and what state a stream ended in. The waits on a
/// <see cref="ManualResetEventSlim"/> are waits for an event that either happens or fails the test, not
/// measurements of how long anything took.
/// </para>
/// <para>
/// <b>Every test that reaches the transport runs on <see cref="DedicatedThread.RunAsync"/>.</b> A network
/// response body is pumped by a <c>Task.Run</c> loop inside <c>FetchBodyStream</c>, and every chunk it
/// delivers — and every cancellation it observes — is a thread-pool continuation. A test body that waited
/// for one while itself occupying an xUnit pool worker is the resource inversion described on
/// <see cref="DedicatedThread.RunAsync"/>: a saturated pool injects workers at roughly one per 500 ms, so a
/// ten-second budget stops being a check and becomes a race. That is what
/// <see cref="TheBodyIsReadOneChunkPerReadAndNotBefore"/> lost on the windows leg of a PR that touches
/// neither this class nor the engine (sebastienros/jint#3213), and it is the same mechanism
/// sebastienros/jint#3201 was diagnosed as. Off the pool, the wait costs the pool nothing.
/// </para>
/// </remarks>
public class FetchStreamTests
{
    /// <summary>
    /// How long a test will wait for a signal the transport raises: a chunk arriving, a read seeing its token
    /// fire, a request reaching the handler. The claim being made is always that the signal happens at all,
    /// never how quickly, so this is a ceiling only a genuine failure to deliver can reach — never a budget
    /// the transport has to beat. It is deliberately far past any plausible scheduling delay.
    /// </summary>
    private static readonly TimeSpan TransportSignalCeiling = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A response body stream whose reads are served from a channel the test fills, so a read only completes
    /// when the test has said what it should answer with — and blocks until then.
    /// </summary>
    private sealed class GatedBody : Stream
    {
        private readonly Channel<byte[]?> _chunks = Channel.CreateUnbounded<byte[]?>();

        private int _readCount;

        /// <summary>How many times the pump has asked the transport for bytes.</summary>
        internal int ReadCount => Volatile.Read(ref _readCount);

        /// <summary>Set the first time a read is entered, so a test can wait for the pump to be parked in one.</summary>
        internal ManualResetEventSlim ReadStarted { get; } = new(false);

        /// <summary>Set when a read saw its cancellation token fire — the proof a cancel reached the transport.</summary>
        internal ManualResetEventSlim Cancelled { get; } = new(false);

        internal void Emit(string text) => _chunks.Writer.TryWrite(SystemEncoding.UTF8.GetBytes(text));

        internal void Complete() => _chunks.Writer.TryWrite(null);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _readCount);
            ReadStarted.Set();

            byte[]? chunk;
            try
            {
                chunk = await _chunks.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Cancelled.Set();
                throw;
            }

            if (chunk is null)
            {
                return 0;
            }

            chunk.CopyTo(buffer);
            return chunk.Length;
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

    private sealed class GatedHandler : HttpMessageHandler
    {
        internal GatedBody Body { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // StreamContent hands the wrapped stream straight back from ReadAsStreamAsync, which is what
            // makes the gating observable: nothing buffers the body behind our backs.
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(Body) };
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// A handler that copies the <i>request</i> body into a recorder, one entry per write the transport
    /// makes, so a streaming upload is observable as it happens rather than only once it is complete.
    /// </summary>
    /// <remarks>
    /// <c>HttpContent.CopyToAsync</c> is what a real handler's send path does, and it goes straight to
    /// <c>SerializeToStreamAsync</c> — where <c>ReadAsStreamAsync</c> would buffer the whole body first and
    /// hide exactly what this is here to see.
    /// </remarks>
    private sealed class UploadHandler : HttpMessageHandler
    {
        private readonly RecordingStream _recorder = new();

        /// <summary>Set as soon as the transport has the request, before a byte of body is asked for.</summary>
        internal ManualResetEventSlim RequestStarted { get; } = new(false);

        /// <summary>Set when the upload saw its cancellation token fire — the proof an abort reached it.</summary>
        internal ManualResetEventSlim Cancelled { get; } = new(false);

        internal IReadOnlyList<string> Chunks => _recorder.Chunks;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestStarted.Set();

            try
            {
                await request.Content!.CopyToAsync(_recorder, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Cancelled.Set();
                throw;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        }

        private sealed class RecordingStream : Stream
        {
            private readonly List<string> _chunks = new();

            internal IReadOnlyList<string> Chunks
            {
                get
                {
                    lock (_chunks)
                    {
                        return _chunks.ToArray();
                    }
                }
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                Write(buffer.Span);
                return default;
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                var text = SystemEncoding.UTF8.GetString(buffer);
                lock (_chunks)
                {
                    _chunks.Add(text);
                }
            }

            public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Runs event-loop turns until <paramref name="condition"/> holds, or fails the test at
    /// <see cref="TransportSignalCeiling"/>. The bound is not a measurement: what is being waited for is a
    /// hand-over between the engine's thread and the transport's, which either happens or means the test has
    /// found a bug.
    /// </summary>
    private static void PumpUntil(Engine engine, Func<bool> condition, string what)
    {
        var elapsed = Stopwatch.StartNew();

        while (!condition())
        {
            if (elapsed.Elapsed > TransportSignalCeiling)
            {
                Assert.Fail("Timed out waiting for " + what);
            }

            engine.Advanced.ProcessTasks();
            Thread.Sleep(1);
        }
    }

    private static Engine WebEngine(HttpMessageHandler handler)
    {
        var engine = new Engine(options => options.UseFetch(fetch => fetch.HttpClient = new HttpClient(handler)));

        // ASCII-only helper, so these tests do not also need the Encoding feature.
        engine.Execute("function dec(u8) { return String.fromCharCode.apply(null, Array.from(u8)); }");
        return engine;
    }

    [Fact]
    public Task TheBodyIsReadOneChunkPerReadAndNotBefore() => DedicatedThread.RunAsync(() =>
    {
        var handler = new GatedHandler();
        handler.Body.Emit("he");
        handler.Body.Emit("llo");
        handler.Body.Complete();

        var engine = WebEngine(handler);
        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Advanced.ProcessTasks();

        engine.Evaluate("r.status").AsNumber().Should().Be(200);

        // Backpressure: the stream's high water mark is zero, so the promise resolving with the response has
        // touched the socket for its headers and for nothing else. Pumping cannot change that — only a
        // consumer can.
        engine.Advanced.ProcessTasks();
        handler.Body.ReadCount.Should().Be(0);

        // Nor does taking a reader, which locks the stream without asking it for anything.
        engine.Execute("var reader = r.body.getReader();");
        handler.Body.ReadCount.Should().Be(0);

        engine.Evaluate("reader.read().then(x => x.done + ':' + dec(x.value))").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("false:he");
        handler.Body.ReadCount.Should().Be(1);

        engine.Evaluate("reader.read().then(x => x.done + ':' + dec(x.value))").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("false:llo");
        handler.Body.ReadCount.Should().Be(2);

        // The read that sees the end of the body closes the stream.
        engine.Evaluate("reader.read().then(x => x.done + ':' + (x.value === undefined))").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("true:true");
        handler.Body.ReadCount.Should().Be(3);

        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeTrue();
    });

    [Fact]
    public Task ForAwaitOfDrainsAStreamingBody() => DedicatedThread.RunAsync(() =>
    {
        var handler = new GatedHandler();
        handler.Body.Emit("a");
        handler.Body.Emit("b");
        handler.Body.Emit("c");
        handler.Body.Complete();

        var engine = WebEngine(handler);

        engine.Evaluate(@"(async () => {
                const r = await fetch('https://example.org/');
                let out = '';
                for await (const chunk of r.body) { out += dec(chunk) + '|'; }
                return out;
            })()").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("a|b|c|");

        // One read per chunk plus the one that saw the end.
        handler.Body.ReadCount.Should().Be(4);
    });

    [Fact]
    public Task TextOverAStreamingBodyEqualsTheBufferedAnswer() => DedicatedThread.RunAsync(() =>
    {
        var handler = new GatedHandler();
        handler.Body.Emit("{\"a\":");
        handler.Body.Emit("1}");
        handler.Body.Complete();

        var engine = WebEngine(handler);

        // The mixin's consumers run the standard's fully-read steps over the stream and reassemble the bytes,
        // so a body split across chunks is indistinguishable from one that arrived whole.
        var streamed = engine.Evaluate("fetch('https://example.org/').then(r => r.text())").UnwrapIfPromise(TransportSignalCeiling).AsString();
        var buffered = engine.Evaluate("new Response('{\"a\":1}').text()").UnwrapIfPromise().AsString();

        streamed.Should().Be(buffered).And.Be("{\"a\":1}");
    });

    [Fact]
    public Task JsonOverAStreamingBodyParsesAcrossChunkBoundaries() => DedicatedThread.RunAsync(() =>
    {
        var handler = new GatedHandler();
        handler.Body.Emit("{\"a\":");
        handler.Body.Emit("[1,2,");
        handler.Body.Emit("3]}");
        handler.Body.Complete();

        var engine = WebEngine(handler);

        engine.Evaluate("fetch('https://example.org/').then(r => r.json()).then(v => v.a.join('-'))")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("1-2-3");
    });

    [Fact]
    public Task CloneTeesTheStreamAndBothHalvesAreReadable() => DedicatedThread.RunAsync(() =>
    {
        var handler = new GatedHandler();
        handler.Body.Emit("hel");
        handler.Body.Emit("lo");
        handler.Body.Complete();

        var engine = WebEngine(handler);
        engine.Execute("var r; fetch('https://example.org/').then(x => x && (r = x));");
        engine.Advanced.ProcessTasks();

        // https://fetch.spec.whatwg.org/#concept-body-clone — the original keeps the first branch, the clone
        // gets the second, and neither is the stream that existed before.
        engine.Execute("var before = r.body; var copy = r.clone();");
        engine.Evaluate("r.body === before").AsBoolean().Should().BeFalse();
        engine.Evaluate("copy.body === r.body").AsBoolean().Should().BeFalse();

        engine.Evaluate("r.text()").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("hello");
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeTrue();

        // The clone carries its own flag over its own branch, and the branch buffered everything the shared
        // reader pulled.
        engine.Evaluate("copy.bodyUsed").AsBoolean().Should().BeFalse();
        engine.Evaluate("copy.text()").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("hello");

        // One reader over the original, so the transport is still read exactly once per chunk.
        handler.Body.ReadCount.Should().Be(3);
    });

    [Fact]
    public Task CancellingTheBodyStreamCancelsTheTransport() => DedicatedThread.RunAsync(() =>
    {
        // Nothing is emitted, so the pump parks inside a read and the only way out is the token.
        var handler = new GatedHandler();
        var engine = WebEngine(handler);

        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Advanced.ProcessTasks();

        engine.Execute("var state = 'pending'; var reader = r.body.getReader(); reader.read().then(x => state = x.done ? 'done' : 'chunk', () => state = 'error');");
        engine.Advanced.ProcessTasks();

        handler.Body.ReadStarted.Wait(TransportSignalCeiling).Should().BeTrue("the pull should have reached the transport");

        engine.Execute("reader.cancel('stop');");
        engine.Advanced.ProcessTasks();

        // The cancel reached the socket, not just the stream.
        handler.Body.Cancelled.Wait(TransportSignalCeiling).Should().BeTrue(
            "cancelling the body must cancel the read in flight, not merely close the stream");

        // Cancelling closes the stream, so the outstanding read resolves as done rather than hanging.
        engine.Evaluate("state").AsString().Should().Be("done");
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeTrue();
    });

    /// <remarks>
    /// One of the two tests in this class that stays on the xUnit worker, because nothing here waits for the
    /// transport: no consumer ever pulls, so the read loop never leaves its demand wait, and every assertion
    /// below — including the two negative ones — is decided on the engine thread by the time
    /// <c>RestoreGlobalSnapshot</c> returns.
    /// </remarks>
    [Fact]
    public void ARestoreErrorsALiveBodyStreamAndCancelsTheTransport()
    {
        var handler = new GatedHandler();
        var engine = WebEngine(handler);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Advanced.ProcessTasks();

        // Held on the host side, because the restore is about to revert the global that names it.
        var body = engine.Evaluate("r.body");
        var stream = (JsReadableStream) body;
        stream.State.Should().Be(ReadableStreamState.Readable);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The stream reports failure rather than silently never producing another byte ...
        stream.State.Should().Be(ReadableStreamState.Errored);
        stream.StoredError.AsObject().Get("message").AsString().Should().Contain("globals were restored");

        // ... and the connection is let go at once.
        handler.Body.ReadStarted.IsSet.Should().BeFalse("nothing ever asked the transport for bytes");
        engine.Evaluate("typeof r").AsString().Should().Be("undefined");

        // The engine is perfectly usable afterwards.
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Fact]
    public Task ARestoreCancelsATransportThatWasAlreadyReading() => DedicatedThread.RunAsync(() =>
    {
        var handler = new GatedHandler();
        var engine = WebEngine(handler);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Advanced.ProcessTasks();

        engine.Execute("var reader = r.body.getReader(); reader.read();");
        engine.Advanced.ProcessTasks();

        handler.Body.ReadStarted.Wait(TransportSignalCeiling).Should().BeTrue(
            "the read should have reached the transport");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        handler.Body.Cancelled.Wait(TransportSignalCeiling).Should().BeTrue(
            "a restore must cancel the read in flight rather than leave the connection held");
    });

    /// <remarks>
    /// The other one, and for a stronger reason: a null body status has no <c>FetchBodyStream</c> at all, so
    /// there is no transport loop to wait for and <c>r.text()</c> settles on the engine's own queue.
    /// </remarks>
    [Fact]
    public void ANullBodyStatusStillHasNoStreamAtAll()
    {
        var handler = new NoContentHandler();
        var engine = WebEngine(handler);

        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Advanced.ProcessTasks();

        // https://fetch.spec.whatwg.org/#null-body-status — null, not an empty stream, and reading it never
        // disturbs anything.
        engine.Evaluate("r.body").Should().Be(Native.JsValue.Null);
        engine.Evaluate("r.text()").UnwrapIfPromise().AsString().Should().Be("");
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeFalse();
    }

    private sealed class NoContentHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }

    /// <summary>
    /// The upload half of the same design: a <c>ReadableStream</c> request body sent with
    /// <c>duplex: 'half'</c> reaches the transport <b>as the script produces it</b>, not after the whole
    /// stream has been drained into memory.
    /// <para>
    /// https://fetch.spec.whatwg.org/#dom-requestinit-duplex
    /// </para>
    /// </summary>
    [Fact]
    public Task AStreamingRequestBodyReachesTheTransportAsTheScriptProducesIt() => DedicatedThread.RunAsync(() =>
    {
        var handler = new UploadHandler();
        var engine = WebEngine(handler);

        engine.Execute("""
            var t = new TransformStream();
            var writer = t.writable.getWriter();
            var state = 'pending';
            fetch('https://example.org/', { method: 'POST', duplex: 'half', body: t.readable })
                .then(r => state = 'ok:' + r.status, e => state = 'failed:' + e.message);
            """);

        // The request is already on its way, with nothing written into it: a buffered upload would not have
        // opened it at all until the stream had closed.
        PumpUntil(engine, () => handler.RequestStarted.IsSet, "the request to reach the transport");
        engine.Advanced.ProcessTasks();
        handler.Chunks.Should().BeEmpty();
        engine.Evaluate("state").AsString().Should().Be("pending");

        engine.Execute("writer.write(new Uint8Array([104, 105]));");
        PumpUntil(engine, () => handler.Chunks.Count == 1, "the first chunk to reach the transport");
        handler.Chunks[0].Should().Be("hi");

        // The fetch is still open: the body is not finished, so neither is the request.
        engine.Evaluate("state").AsString().Should().Be("pending");

        engine.Execute("writer.write(new Uint8Array([33])); writer.close();");
        PumpUntil(engine, () => !string.Equals(engine.Evaluate("state").AsString(), "pending", StringComparison.Ordinal), "the fetch to settle");

        engine.Evaluate("state").AsString().Should().Be("ok:200");

        // Two writes, in order, each carrying exactly what the script enqueued.
        string.Concat(handler.Chunks).Should().Be("hi!");
        handler.Chunks.Should().HaveCount(2);

        // The body's stream is disturbed and locked, as a body consumed by a fetch always is.
        engine.Evaluate("t.readable.locked").AsBoolean().Should().BeTrue();
    });

    /// <summary>
    /// A streaming upload is cross-cycle state like any other in-flight request, so
    /// <c>RestoreGlobalSnapshot</c> ends it: the transport's token fires and the engine half stops reading
    /// the script's stream. Without that, a request nobody can finish would hold the socket until the
    /// deadline.
    /// </summary>
    [Fact]
    public Task ARestoreEndsAStreamingRequestBody() => DedicatedThread.RunAsync(() =>
    {
        var handler = new UploadHandler();
        var engine = WebEngine(handler);
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("""
            var t = new TransformStream();
            var writer = t.writable.getWriter();
            fetch('https://example.org/', { method: 'POST', duplex: 'half', body: t.readable });
            """);

        PumpUntil(engine, () => handler.RequestStarted.IsSet, "the request to reach the transport");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        handler.Cancelled.Wait(TransportSignalCeiling).Should().BeTrue(
            "a restore must end the upload in flight, not leave the request holding the socket");
    });

    /// <summary>
    /// A network response body is a byte stream — https://fetch.spec.whatwg.org/#concept-body — so it can be
    /// read BYOB straight off the socket, into a buffer the consumer recycles. The chunk boundary stays the
    /// transport's: one BYOB read takes one transport read, however much room the caller offered.
    /// </summary>
    [Fact]
    public Task ANetworkResponseBodyCanBeReadByob() => DedicatedThread.RunAsync(() =>
    {
        var handler = new GatedHandler();
        handler.Body.Emit("he");
        handler.Body.Emit("llo");
        handler.Body.Complete();

        var engine = WebEngine(handler);
        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Advanced.ProcessTasks();

        // Taking a BYOB reader is as free as taking a default one: nothing is read until read() asks.
        engine.Execute("var reader = r.body.getReader({ mode: 'byob' });");
        handler.Body.ReadCount.Should().Be(0);

        engine.Evaluate("reader.read(new Uint8Array(8)).then(x => x.done + ':' + dec(x.value))")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("false:he");
        handler.Body.ReadCount.Should().Be(1);

        engine.Evaluate("reader.read(new Uint8Array(8)).then(x => x.done + ':' + dec(x.value))")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("false:llo");
        handler.Body.ReadCount.Should().Be(2);

        engine.Evaluate("reader.read(new Uint8Array(8)).then(x => x.done + ':' + x.value.byteLength)")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("true:0");
        handler.Body.ReadCount.Should().Be(3);
    });
}
#endif
