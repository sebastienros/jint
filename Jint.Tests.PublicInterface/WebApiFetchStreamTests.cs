#if NET8_0_OR_GREATER
#nullable enable

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Jint;
using SystemEncoding = System.Text.Encoding;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A streaming <c>fetch</c> body seen from outside the assembly: what a host's transport is asked for, when,
/// and what stops it.
/// </summary>
/// <remarks>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything here goes in through
/// <c>Options.WebApi.Fetch.HttpClient</c> — the same door a host uses for a <c>DelegatingHandler</c> or an
/// <c>IHttpClientFactory</c> client — and comes back out through script.
/// </para>
/// <para>
/// <b>Nothing here races a wall clock.</b> The transport's reads are gated by the test, so what is asserted
/// is how many of them happened and in which order; the waits on a <see cref="ManualResetEventSlim"/> are
/// waits for an event at a deliberately generous bound, not measurements.
/// </para>
/// <para>
/// <b>Every test that reaches the transport runs on <see cref="DedicatedThread.RunAsync"/>.</b> A network
/// response body is pumped by a <c>Task.Run</c> loop inside <c>FetchBodyStream</c>, so each chunk and each
/// cancellation arrives on a thread-pool worker; a body that blocked a pool worker to wait for one
/// would be the resource inversion described on <see cref="DedicatedThread.RunAsync"/>, and the bound would
/// stop being a check and start being a race (sebastienros/jint#3213).
/// </para>
/// </remarks>
public class WebApiFetchStreamTests
{
    /// <summary>
    /// How long a test will wait for the transport. A ceiling only a genuine failure to deliver can reach,
    /// never a budget the pool has to beat: what is asserted is that the read, or the cancel, happens at all.
    /// </summary>
    private static readonly TimeSpan TransportSignalCeiling = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A response body whose reads are served from a channel the test fills, so a read blocks until the test
    /// has said what it should answer with.
    /// </summary>
    private sealed class GatedBody : Stream
    {
        private readonly Channel<byte[]?> _chunks = Channel.CreateUnbounded<byte[]?>();

        private int _readCount;

        internal int ReadCount => Volatile.Read(ref _readCount);

        internal ManualResetEventSlim ReadStarted { get; } = new(false);

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
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(Body) });
    }

    private static Engine WebEngine(HttpMessageHandler handler)
    {
        var engine = new Engine(options => options.UseFetch(fetch => fetch.HttpClient = new HttpClient(handler)));
        engine.Execute("function dec(u8) { return String.fromCharCode.apply(null, Array.from(u8)); }");
        return engine;
    }

    [Test]
    public void EnablingFetchAlsoGivesTheStreamInterfaces()
    {
        // response.body is a ReadableStream, so the fetch flag brings the Streams feature with it — the same
        // reason it brings Blob and AbortSignal.
        var engine = new Engine(options => options.UseFetch());

        engine.Evaluate("typeof ReadableStream").AsString().Should().Be("function");
        engine.Evaluate("typeof TransformStream").AsString().Should().Be("function");

        // ... and the host's own reading of Features is untouched by the closure.
        var options = new Options().UseFetch();
        options.WebApi.Features.Should().Be(WebApiFeatures.Fetch);
    }

    [Test]
    public Task AHostScriptDrainsTheResponseBodyChunkByChunk() => DedicatedThread.RunAsync(() =>
    {
        var handler = new GatedHandler();
        handler.Body.Emit("first ");
        handler.Body.Emit("second");
        handler.Body.Complete();

        var engine = WebEngine(handler);

        engine.Evaluate(@"(async () => {
                const r = await fetch('https://example.org/');
                const reader = r.body.getReader();
                const seen = [];
                while (true) {
                    const { done, value } = await reader.read();
                    if (done) break;
                    seen.push(dec(value));
                }
                return seen.join('|');
            })()").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("first |second");

        // One read per chunk, plus the one that saw the end of the body.
        handler.Body.ReadCount.Should().Be(3);
    });

    [Test]
    public Task TheTransportIsOnlyReadWhenTheScriptAsks() => DedicatedThread.RunAsync(() =>
    {
        // Backpressure, from the host's side: a script that takes the response and never reads its body
        // leaves the transport untouched however hard the engine is pumped.
        var handler = new GatedHandler();
        handler.Body.Emit("ignored");
        handler.Body.Complete();

        var engine = WebEngine(handler);
        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Tasks.ProcessTasks();

        engine.Evaluate("r.status").AsNumber().Should().Be(200);

        for (var i = 0; i < 10; i++)
        {
            engine.Tasks.ProcessTasks();
        }

        // Deterministic against the shipped implementation rather than a race: with a high water mark of
        // zero, no pull is ever issued until a consumer asks, so nothing can release the transport loop
        // however long or hard the engine is pumped.
        handler.Body.ReadStarted.IsSet.Should().BeFalse();
        handler.Body.ReadCount.Should().Be(0);

        // The first read is what reaches the socket.
        engine.Evaluate("r.body.getReader().read().then(x => dec(x.value))").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("ignored");
        handler.Body.ReadCount.Should().Be(1);
    });

    [Test]
    public Task CancellingTheBodyLetsGoOfTheConnection() => DedicatedThread.RunAsync(() =>
    {
        // Nothing is emitted, so the transport is parked in a read and only its token can end it.
        var handler = new GatedHandler();
        var engine = WebEngine(handler);

        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Tasks.ProcessTasks();

        engine.Execute("var reader = r.body.getReader(); reader.read();");
        engine.Tasks.ProcessTasks();

        handler.Body.ReadStarted.Wait(TransportSignalCeiling).Should().BeTrue("the read should have reached the transport");

        engine.Execute("reader.cancel();");
        engine.Tasks.ProcessTasks();

        handler.Body.Cancelled.Wait(TransportSignalCeiling).Should().BeTrue("cancelling the body must cancel the read in flight, not merely close the stream");
    });

    [Test]
    public Task ARestoreLetsGoOfAStreamingBodyMidFlight() => DedicatedThread.RunAsync(() =>
    {
        var handler = new GatedHandler();
        var engine = WebEngine(handler);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Execute("var r; fetch('https://example.org/').then(x => r = x);");
        engine.Tasks.ProcessTasks();

        engine.Execute("var reader = r.body.getReader(); reader.read();");
        engine.Tasks.ProcessTasks();

        handler.Body.ReadStarted.Wait(TransportSignalCeiling).Should().BeTrue("the read should have reached the transport");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The socket goes with the cycle that opened it: a body still arriving is not something the restored
        // engine can finish reading, so it is cancelled rather than left holding a connection.
        handler.Body.Cancelled.Wait(TransportSignalCeiling).Should().BeTrue("a restore must cancel the read in flight rather than leave the connection held");

        engine.Evaluate("typeof r").AsString().Should().Be("undefined");
        engine.Evaluate("typeof fetch").AsString().Should().Be("function");
    });

    [Test]
    public Task AResponseBodyCanBePipedThroughATransformStream() => DedicatedThread.RunAsync(() =>
    {
        // The two features meeting: a streaming body driving a TransformStream, all on the engine's own job
        // queue and with nothing but the host's transport off it.
        var handler = new GatedHandler();
        handler.Body.Emit("ab");
        handler.Body.Emit("cd");
        handler.Body.Complete();

        var engine = WebEngine(handler);

        engine.Evaluate(@"(async () => {
                const r = await fetch('https://example.org/');
                const upper = new TransformStream({
                    transform(chunk, controller) { controller.enqueue(dec(chunk).toUpperCase()); },
                });

                let out = '';
                for await (const piece of r.body.pipeThrough(upper)) { out += piece; }
                return out;
            })()").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("ABCD");
    });
}
#endif
