#if NET8_0_OR_GREATER
#nullable enable

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
/// <b>No assertion in this file races a wall clock.</b> What is asserted is structural: how many times the
/// transport was read, in which order the chunks arrived, and what state a stream ended in. The two waits on
/// a <see cref="ManualResetEventSlim"/> are waits for an event that either happens or fails the test at a
/// deliberately generous bound, not measurements of how long anything took.
/// </remarks>
public class FetchStreamTests
{
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

    private static Engine WebEngine(HttpMessageHandler handler)
    {
        var engine = new Engine(options => options.UseFetch(fetch => fetch.HttpClient = new HttpClient(handler)));

        // ASCII-only helper, so these tests do not also need the Encoding feature.
        engine.Execute("function dec(u8) { return String.fromCharCode.apply(null, Array.from(u8)); }");
        return engine;
    }

    [Fact]
    public void TheBodyIsReadOneChunkPerReadAndNotBefore()
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

        engine.Evaluate("reader.read().then(x => x.done + ':' + dec(x.value))").UnwrapIfPromise().AsString().Should().Be("false:he");
        handler.Body.ReadCount.Should().Be(1);

        engine.Evaluate("reader.read().then(x => x.done + ':' + dec(x.value))").UnwrapIfPromise().AsString().Should().Be("false:llo");
        handler.Body.ReadCount.Should().Be(2);

        // The read that sees the end of the body closes the stream.
        engine.Evaluate("reader.read().then(x => x.done + ':' + (x.value === undefined))").UnwrapIfPromise().AsString().Should().Be("true:true");
        handler.Body.ReadCount.Should().Be(3);

        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ForAwaitOfDrainsAStreamingBody()
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
            })()").UnwrapIfPromise().AsString().Should().Be("a|b|c|");

        // One read per chunk plus the one that saw the end.
        handler.Body.ReadCount.Should().Be(4);
    }

    [Fact]
    public void TextOverAStreamingBodyEqualsTheBufferedAnswer()
    {
        var handler = new GatedHandler();
        handler.Body.Emit("{\"a\":");
        handler.Body.Emit("1}");
        handler.Body.Complete();

        var engine = WebEngine(handler);

        // The mixin's consumers run the standard's fully-read steps over the stream and reassemble the bytes,
        // so a body split across chunks is indistinguishable from one that arrived whole.
        var streamed = engine.Evaluate("fetch('https://example.org/').then(r => r.text())").UnwrapIfPromise().AsString();
        var buffered = engine.Evaluate("new Response('{\"a\":1}').text()").UnwrapIfPromise().AsString();

        streamed.Should().Be(buffered).And.Be("{\"a\":1}");
    }

    [Fact]
    public void JsonOverAStreamingBodyParsesAcrossChunkBoundaries()
    {
        var handler = new GatedHandler();
        handler.Body.Emit("{\"a\":");
        handler.Body.Emit("[1,2,");
        handler.Body.Emit("3]}");
        handler.Body.Complete();

        var engine = WebEngine(handler);

        engine.Evaluate("fetch('https://example.org/').then(r => r.json()).then(v => v.a.join('-'))")
            .UnwrapIfPromise().AsString().Should().Be("1-2-3");
    }

    [Fact]
    public void CloneTeesTheStreamAndBothHalvesAreReadable()
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

        engine.Evaluate("r.text()").UnwrapIfPromise().AsString().Should().Be("hello");
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeTrue();

        // The clone carries its own flag over its own branch, and the branch buffered everything the shared
        // reader pulled.
        engine.Evaluate("copy.bodyUsed").AsBoolean().Should().BeFalse();
        engine.Evaluate("copy.text()").UnwrapIfPromise().AsString().Should().Be("hello");

        // One reader over the original, so the transport is still read exactly once per chunk.
        handler.Body.ReadCount.Should().Be(3);
    }

    [Fact]
    public void CancellingTheBodyStreamCancelsTheTransport()
    {
        // Nothing is emitted, so the pump parks inside a read and the only way out is the token.
        var handler = new GatedHandler();
        var engine = WebEngine(handler);

        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Advanced.ProcessTasks();

        engine.Execute("var state = 'pending'; var reader = r.body.getReader(); reader.read().then(x => state = x.done ? 'done' : 'chunk', () => state = 'error');");
        engine.Advanced.ProcessTasks();

        handler.Body.ReadStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("the pull should have reached the transport");

        engine.Execute("reader.cancel('stop');");
        engine.Advanced.ProcessTasks();

        // The cancel reached the socket, not just the stream.
        handler.Body.Cancelled.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

        // Cancelling closes the stream, so the outstanding read resolves as done rather than hanging.
        engine.Evaluate("state").AsString().Should().Be("done");
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeTrue();
    }

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
    public void ARestoreCancelsATransportThatWasAlreadyReading()
    {
        var handler = new GatedHandler();
        var engine = WebEngine(handler);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Advanced.ProcessTasks();

        engine.Execute("var reader = r.body.getReader(); reader.read();");
        engine.Advanced.ProcessTasks();

        handler.Body.ReadStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        handler.Body.Cancelled.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
    }

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
}
#endif
