#if NET8_0_OR_GREATER
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The bridge between the WHATWG streams and <see cref="System.IO.Stream"/>, seen from outside the assembly:
/// <c>Engine.WebApi.CreateReadableStream</c>, <c>CreateWritableStream</c>,
/// <c>StartReadableStreamCopy</c> and <c>CopyReadableStreamAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything used here is reachable by a third party —
/// which is the point of putting the tests here rather than in <c>Jint.Tests</c>.
/// </para>
/// <para>
/// <b>Nothing here sleeps, races a clock, or asserts that time passed.</b> Every host stream is one whose
/// asynchronous methods complete synchronously (see <see cref="RecordingStream"/>), so the whole copy runs on
/// the engine's own turns and a bounded <c>ProcessTasks</c> loop is enough to drive it to completion. The one
/// test that deliberately goes off-thread, <see cref="ReadsAStreamWhoseReadsCompleteOnAnotherThread"/>, waits
/// for the outcome through the engine's own blocking drain rather than for an interval — under the wedge
/// ceiling <see cref="OffThreadStreamEngine"/> configures, which is a bound on a hang and never an assertion.
/// </para>
/// </remarks>
public class HostStreamBridgeTests
{
    private static Engine StreamEngine() => new(options => options.UseWebApis(WebApiFeatures.Streams));

    /// <summary>
    /// The same engine for the one test whose chunks arrive from another thread, with the promise budget the
    /// blocking drain runs under moved off the engine's ten-second default and onto
    /// <see cref="TestBudgets.WedgeCeiling"/>.
    /// </summary>
    /// <remarks>
    /// Nothing here asserts a duration — the assertion is the text that came out of the stream — so the
    /// budget is a wedge ceiling and widening it can hide nothing. What it removes is the thread pool from
    /// the set of things that decide the outcome: each chunk is delivered by a pool continuation, and on a
    /// saturated runner the whole copy has been seen failing as <c>PromiseRejectedException: Timeout of
    /// 00:00:10 reached</c> (#3358), which is a symptom of the machine rather than of the bridge.
    /// </remarks>
    private static Engine OffThreadStreamEngine() => new(options =>
    {
        options.UseWebApis(WebApiFeatures.Streams);
        options.Constraints.PromiseTimeout = TestBudgets.WedgeCeiling;
    });

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>
    /// A <see cref="MemoryStream"/> that records what the engine did to it, and — crucially — keeps the
    /// synchronous completion its base class gives the <see cref="Memory{T}"/> overloads.
    /// </summary>
    /// <remarks>
    /// The overrides are the <see cref="Memory{T}"/>-based ones because those are what the bridge calls;
    /// overriding <c>Read(byte[], int, int)</c> would record nothing, since <see cref="MemoryStream"/>
    /// implements the newer overloads without going through it.
    /// </remarks>
    private sealed class RecordingStream : MemoryStream
    {
        internal RecordingStream()
        {
        }

        internal RecordingStream(byte[] initial) : base(initial, writable: false)
        {
        }

        internal int Reads { get; private set; }

        internal int Writes { get; private set; }

        internal int Flushes { get; private set; }

        internal bool Disposed { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reads++;
            return base.ReadAsync(buffer, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Writes++;
            return base.WriteAsync(buffer, cancellationToken);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Flushes++;
            return base.FlushAsync(cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>A stream whose reads fail, for the error path.</summary>
    private sealed class FailingReadStream : Stream
    {
        internal bool Disposed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(new IOException("the disk in question is on fire"));

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("the disk in question is on fire");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A stream whose reads genuinely leave the calling thread, so that the cross-thread chunk delivery is
    /// exercised rather than the synchronous window.
    /// </summary>
    private sealed class OffThreadStream : Stream
    {
        private readonly byte[] _data;
        private int _position;

        internal OffThreadStream(byte[] data)
        {
            _data = data;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _data.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Forces the continuation onto the thread pool, which is what puts the chunk delivery through the
            // event loop rather than through the synchronous window.
            await Task.Yield();

            var count = Math.Min(buffer.Length, _data.Length - _position);
            _data.AsSpan(_position, count).CopyTo(buffer.Span);
            _position += count;
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// A destination whose writes and flush genuinely leave the calling thread, so that the copy's own
    /// cross-thread path is exercised rather than its synchronous window.
    /// </summary>
    private sealed class OffThreadWriteStream : MemoryStream
    {
        internal bool Disposed { get; private set; }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            await base.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Gives the engine turns until the copy is done. Bounded rather than timed: every stream in this file
    /// completes its I/O synchronously, so each turn makes progress and the bound is only there so that a
    /// regression fails the test instead of hanging the suite.
    /// </summary>
    private static void Drive(Engine engine, HostStreamCopyOperation operation)
    {
        for (var i = 0; i < 10_000 && !operation.IsCompleted; i++)
        {
            engine.Tasks.ProcessTasks();
        }

        operation.IsCompleted.Should().BeTrue("the copy should have finished within 10000 engine turns");
    }

    [Test]
    public void ReadsAHostStreamAsAReadableStreamOfUint8Arrays()
    {
        var engine = StreamEngine();
        engine.SetValue("input", engine.WebApi.CreateReadableStream(new RecordingStream(Utf8("hello world"))));

        var text = engine.Evaluate("""
            (async () => {
              const reader = input.getReader();
              const parts = [];
              for (;;) {
                const { value, done } = await reader.read();
                if (done) break;
                parts.push(...value);
              }
              return String.fromCharCode(...parts);
            })()
            """).UnwrapIfPromise();

        text.AsString().Should().Be("hello world");
    }

    [Test]
    public void ChunksAreBoundedByTheChunkSize()
    {
        var engine = StreamEngine();
        var options = new HostReadableStreamOptions { ChunkSize = 4 };
        engine.SetValue("input", engine.WebApi.CreateReadableStream(new RecordingStream(Utf8("abcdefghij")), options));

        var sizes = engine.Evaluate("""
            (async () => {
              const reader = input.getReader();
              const sizes = [];
              for (;;) {
                const { value, done } = await reader.read();
                if (done) break;
                sizes.push(value.length);
              }
              return sizes.join(',');
            })()
            """).UnwrapIfPromise();

        sizes.AsString().Should().Be("4,4,2");
    }

    [Test]
    public void TheChunkIsAUint8ArrayAndAFreshOneEveryTime()
    {
        var engine = StreamEngine();
        engine.SetValue("input", engine.WebApi.CreateReadableStream(
            new RecordingStream(Utf8("abcd")),
            new HostReadableStreamOptions { ChunkSize = 2 }));

        var outcome = engine.Evaluate("""
            (async () => {
              const reader = input.getReader();
              const first = (await reader.read()).value;
              first[0] = 0;                              // the host's buffer must not be reachable through this
              const second = (await reader.read()).value;
              return (first instanceof Uint8Array) + ':' + (first === second) + ':' + second[0];
            })()
            """).UnwrapIfPromise();

        // 'c' is 99: the second chunk is unaffected by the write into the first, so the bridge is not handing
        // script a window onto its own reusable read buffer.
        outcome.AsString().Should().Be("true:false:99");
    }

    [Test]
    public void NothingIsReadUntilTheHighWaterMarkAsksForIt()
    {
        // The whole of backpressure, asserted structurally: with a high water mark of zero the queue never
        // wants a chunk, so not one byte leaves the host's stream until a reader asks. With the default of
        // one, exactly one chunk is read ahead.
        var engine = StreamEngine();
        var source = new RecordingStream(Utf8("abcdefgh"));

        engine.SetValue("input", engine.WebApi.CreateReadableStream(
            source,
            new HostReadableStreamOptions { ChunkSize = 2, HighWaterMark = 0 }));

        engine.Tasks.ProcessTasks();
        source.Reads.Should().Be(0);

        engine.Evaluate("input.getReader().read()").UnwrapIfPromise();
        source.Reads.Should().Be(1);

        var eager = new RecordingStream(Utf8("abcdefgh"));
        var other = StreamEngine();
        other.SetValue("input", other.WebApi.CreateReadableStream(eager, new HostReadableStreamOptions { ChunkSize = 2 }));

        other.Tasks.ProcessTasks();
        eager.Reads.Should().Be(1);
    }

    [Test]
    public void ReadingToTheEndReleasesTheHostStream()
    {
        var engine = StreamEngine();
        var source = new RecordingStream(Utf8("abc"));
        engine.SetValue("input", engine.WebApi.CreateReadableStream(source));

        engine.Evaluate("(async () => { const r = input.getReader(); while (!(await r.read()).done); })()").UnwrapIfPromise();

        source.Disposed.Should().BeTrue();
    }

    [Test]
    public void LeaveOpenKeepsTheHostStreamForTheHost()
    {
        var engine = StreamEngine();
        var source = new RecordingStream(Utf8("abc"));
        engine.SetValue("input", engine.WebApi.CreateReadableStream(source, new HostReadableStreamOptions { LeaveOpen = true }));

        engine.Evaluate("(async () => { const r = input.getReader(); while (!(await r.read()).done); })()").UnwrapIfPromise();

        source.Disposed.Should().BeFalse();
        source.CanRead.Should().BeTrue();
    }

    [Test]
    public void CancellingTheStreamReleasesTheHostStream()
    {
        var engine = StreamEngine();
        var source = new RecordingStream(Utf8("abcdefgh"));
        engine.SetValue("input", engine.WebApi.CreateReadableStream(source, new HostReadableStreamOptions { ChunkSize = 2 }));

        engine.Evaluate("input.cancel('bored')").UnwrapIfPromise();

        source.Disposed.Should().BeTrue();
    }

    [Test]
    public void AFailedReadErrorsTheStreamAndCarriesTheClrExceptionForTheHostAlone()
    {
        var engine = StreamEngine();
        var source = new FailingReadStream();
        engine.SetValue("input", engine.WebApi.CreateReadableStream(source));

        var rejection = Assert.Throws<PromiseRejectedException>(() => engine.Evaluate("input.getReader().read()").UnwrapIfPromise());

        var error = rejection.RejectedValue;
        error.Get("name").AsString().Should().Be("TypeError");

        // The message names the failure's shape but not its text: a path or a permission message would tell a
        // script things about the host it has no business learning.
        error.Get("message").AsString().Should().Be("The host stream failed while reading (IOException).");
        error.Get("message").AsString().Should().NotContain("on fire");

        // The exception itself rides the error value, where only the host can read it.
        JintException.TryGetClrException(rejection, out var clr).Should().BeTrue();
        clr!.Message.Should().Be("the disk in question is on fire");

        source.Disposed.Should().BeTrue();
    }

    [Test]
    public void ReadsAStreamWhoseReadsCompleteOnAnotherThread()
    {
        // The cross-thread half: every chunk arrives as a generation-stamped event-loop job rather than
        // through the synchronous window. UnwrapIfPromise is the engine's own blocking drain, which wakes on
        // the work-arrived signal rather than on a poll interval.
        var engine = OffThreadStreamEngine();
        engine.SetValue("input", engine.WebApi.CreateReadableStream(
            new OffThreadStream(Utf8("streamed off-thread")),
            new HostReadableStreamOptions { ChunkSize = 4 }));

        var text = engine.Evaluate("""
            (async () => {
              let out = '';
              for await (const chunk of input) out += String.fromCharCode(...chunk);
              return out;
            })()
            """).UnwrapIfPromise();

        text.AsString().Should().Be("streamed off-thread");
    }

    [Test]
    public void RestoringTheGlobalsReleasesEveryOpenBridge()
    {
        // The restore fence. The generation stamp already stops a chunk reaching the restored engine; what
        // this pins is the other half, that the host's file handle is closed rather than left to a finalizer.
        var engine = StreamEngine();
        var source = new RecordingStream(Utf8("abcdefgh"));
        var destination = new RecordingStream();

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.SetValue("input", engine.WebApi.CreateReadableStream(source, new HostReadableStreamOptions { HighWaterMark = 0 }));
        engine.SetValue("output", engine.WebApi.CreateWritableStream(destination));

        source.Disposed.Should().BeFalse();
        destination.Disposed.Should().BeFalse();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        source.Disposed.Should().BeTrue();
        destination.Disposed.Should().BeTrue();
    }

    [Test]
    public void WritesAWritableStreamToAHostStream()
    {
        var engine = StreamEngine();
        var destination = new RecordingStream();
        engine.SetValue("output", engine.WebApi.CreateWritableStream(destination));

        engine.Evaluate("""
            (async () => {
              const writer = output.getWriter();
              await writer.write(new Uint8Array([104, 105, 32]));   // "hi "
              await writer.write('there');                          // a string is UTF-8 encoded
              await writer.close();
            })()
            """).UnwrapIfPromise();

        // ToArray() still answers after a MemoryStream has been closed, which is what lets the default —
        // the engine owns and disposes the stream — be asserted at all.
        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("hi there");
        destination.Flushes.Should().BeGreaterThan(0);
        destination.Disposed.Should().BeTrue();
    }

    [Test]
    public void AChunkThatIsNotAByteSequenceIsRefusedRatherThanStringified()
    {
        var engine = StreamEngine();
        var destination = new RecordingStream();
        engine.SetValue("output", engine.WebApi.CreateWritableStream(destination));

        var outcome = engine.Evaluate("""
            (async () => {
              const writer = output.getWriter();
              try { await writer.write({ oops: true }); return 'no throw'; }
              catch (e) { return e.name + ':' + e.message; }
            })()
            """).UnwrapIfPromise();

        outcome.AsString().Should().StartWith("TypeError:A host stream can only be written a BufferSource, a Blob or a string");

        // Nothing was written, so an object mistaken for a buffer cannot land in the host's file as
        // "[object Object]".
        destination.ToArray().Should().BeEmpty();
    }

    [Test]
    public void TheWriterIsBackpressuredByTheHighWaterMark()
    {
        // desiredSize is the standard's own arithmetic — highWaterMark minus the queue — so this is an exact
        // structural statement about backpressure rather than a timing one.
        var engine = StreamEngine();
        engine.SetValue("output", engine.WebApi.CreateWritableStream(new RecordingStream()));

        var sizes = engine.Evaluate("""
            (() => {
              const writer = output.getWriter();
              const before = writer.desiredSize;
              writer.write(new Uint8Array([1]));
              writer.write(new Uint8Array([2]));
              return before + ',' + writer.desiredSize;
            })()
            """);

        sizes.AsString().Should().Be("1,-1");
    }

    [Test]
    public void AbortingTheWritableStreamReleasesTheHostStreamWithoutFlushing()
    {
        var engine = StreamEngine();
        var destination = new RecordingStream();
        engine.SetValue("output", engine.WebApi.CreateWritableStream(destination));

        engine.Evaluate("output.abort('nope')").UnwrapIfPromise();

        destination.Disposed.Should().BeTrue();
        destination.Flushes.Should().Be(0);
    }

    [Test]
    public void CopiesAScriptStreamThroughATransformIntoAHostStream()
    {
        // The recipe: a host stream in, a script TransformStream in the middle, a host stream out, and the
        // host driving every turn itself.
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams | WebApiFeatures.Encoding));
        var destination = new RecordingStream();

        engine.SetValue("input", engine.WebApi.CreateReadableStream(new RecordingStream(Utf8("shout this")), new HostReadableStreamOptions { ChunkSize = 3 }));

        var transformed = engine.Evaluate("""
            input.pipeThrough(new TransformStream({
              transform(chunk, controller) {
                const text = new TextDecoder().decode(chunk);
                controller.enqueue(new TextEncoder().encode(text.toUpperCase()));
              }
            }))
            """);

        var copy = engine.WebApi.StartReadableStreamCopy(transformed, destination);
        Drive(engine, copy);

        copy.IsFaulted.Should().BeFalse();
        copy.GetResult().Should().Be(10);
        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("SHOUT THIS");
        destination.Disposed.Should().BeTrue();
    }

    [Test]
    public void CopiesABlobsStreamIntoAHostStream()
    {
        // `Blob.stream()` is a ReadableStream the engine built itself, not one a script constructed, so this
        // is the copy taking a stream from somewhere other than its own tests.
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams | WebApiFeatures.Files));
        var destination = new RecordingStream();

        var body = engine.Evaluate("new Blob(['part one, ', 'part two']).stream()");

        var copy = engine.WebApi.StartReadableStreamCopy(body, destination);
        Drive(engine, copy);

        copy.IsFaulted.Should().BeFalse();
        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("part one, part two");
    }

    [Test]
    public void PipesAHostStreamThroughADecompressionStreamIntoAHostStream()
    {
        // Both halves of the bridge with an engine-supplied transform between them: a compressed host stream
        // in, `DecompressionStream` in the middle, a host stream out, and the host owning every turn.
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams | WebApiFeatures.Compression));
        var destination = new RecordingStream();

        var compressed = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(Utf8("the bytes that made the round trip"));
        }

        compressed.Position = 0;
        engine.SetValue("input", engine.WebApi.CreateReadableStream(compressed));

        var plain = engine.Evaluate("input.pipeThrough(new DecompressionStream('gzip'))");

        var copy = engine.WebApi.StartReadableStreamCopy(plain, destination);
        Drive(engine, copy);

        copy.IsFaulted.Should().BeFalse();
        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("the bytes that made the round trip");
    }

    [Test]
    public async Task CopyReadableStreamAsyncDrivesTheEngineWhileItWaits()
    {
        var engine = StreamEngine();
        var destination = new RecordingStream();
        var source = engine.Evaluate("new ReadableStream({ start(c) { c.enqueue(new Uint8Array([97, 98])); c.enqueue('c'); c.close(); } })");

        var written = await engine.WebApi.CopyReadableStreamAsync(source, destination);

        written.Should().Be(3);
        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("abc");
    }

    [Test]
    public async Task CopiesToADestinationWhoseWritesCompleteOnAnotherThread()
    {
        // The copy's own cross-thread half: each write settles on a thread-pool thread and comes back as a
        // generation-stamped event-loop job. CopyReadableStreamAsync is what drives those turns, so nothing
        // here waits for an interval.
        var engine = StreamEngine();
        var destination = new OffThreadWriteStream();
        var source = engine.Evaluate("""
            new ReadableStream({
              start(c) { c.enqueue('one '); c.enqueue('two '); c.enqueue('three'); c.close(); }
            })
            """);

        var written = await engine.WebApi.CopyReadableStreamAsync(source, destination);

        written.Should().Be(13);
        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("one two three");
        destination.Disposed.Should().BeTrue();
    }

    [Test]
    public void ACopyOfALockedStreamFailsTheOperationRatherThanTheStartCall()
    {
        var engine = StreamEngine();
        var source = engine.Evaluate("new ReadableStream()");
        engine.SetValue("source", source);
        engine.Evaluate("source.getReader()");

        var copy = engine.WebApi.StartReadableStreamCopy(source, new RecordingStream());

        copy.IsCompleted.Should().BeTrue();
        copy.IsFaulted.Should().BeTrue();
        copy.Error!.Get("name").AsString().Should().Be("TypeError");
        Assert.Throws<PromiseRejectedException>(() => copy.GetResult());
    }

    [Test]
    public void ACopyOfSomethingThatIsNotAReadableStreamIsAnArgumentException()
    {
        var engine = StreamEngine();

        Assert.Throws<ArgumentException>(() => engine.WebApi.StartReadableStreamCopy(JsValue.Undefined, new RecordingStream()));
        Assert.Throws<ArgumentException>(() => engine.WebApi.StartReadableStreamCopy(engine.Evaluate("({})"), new RecordingStream()));
        Assert.Throws<ArgumentNullException>(() => engine.WebApi.StartReadableStreamCopy(engine.Evaluate("new ReadableStream()"), null!));
    }

    [Test]
    public void ACopyThatFailsCancelsTheSourceUnlessToldNotTo()
    {
        var engine = StreamEngine();
        engine.Execute("var cancelled = [];");

        // A source that records its own cancel(), which is how the piping rules are observable from script.
        var source = engine.Evaluate("new ReadableStream({ start(c) { c.enqueue({}); }, cancel(r) { cancelled.push('yes'); } })");
        var copy = engine.WebApi.StartReadableStreamCopy(source, new RecordingStream());
        Drive(engine, copy);

        copy.IsFaulted.Should().BeTrue();
        engine.Evaluate("cancelled.join(',')").AsString().Should().Be("yes");

        engine.Execute("cancelled = [];");
        var kept = engine.Evaluate("new ReadableStream({ start(c) { c.enqueue({}); }, cancel(r) { cancelled.push('yes'); } })");
        var preventing = engine.WebApi.StartReadableStreamCopy(kept, new RecordingStream(), new HostStreamCopyOptions { PreventCancel = true });
        Drive(engine, preventing);

        preventing.IsFaulted.Should().BeTrue();
        engine.Evaluate("cancelled.join(',')").AsString().Should().Be("");
    }

    [Test]
    public void ACopyOfAnErroredStreamFailsWithTheScriptsOwnError()
    {
        var engine = StreamEngine();
        var source = engine.Evaluate("new ReadableStream({ start(c) { c.error(new RangeError('nope')); } })");

        var copy = engine.WebApi.StartReadableStreamCopy(source, new RecordingStream());
        Drive(engine, copy);

        copy.IsFaulted.Should().BeTrue();
        copy.Error!.Get("name").AsString().Should().Be("RangeError");
        copy.Error!.Get("message").AsString().Should().Be("nope");
    }

    [Test]
    public void ACancelledCopyEndsWithAnAbortErrorAndReleasesTheDestination()
    {
        var engine = StreamEngine();
        var destination = new RecordingStream();

        using var cancellation = new CancellationTokenSource();
        var source = engine.Evaluate("new ReadableStream({ pull(c) { /* never produces a chunk */ } })");

        var copy = engine.WebApi.StartReadableStreamCopy(source, destination, options: null, cancellation.Token);
        engine.Tasks.ProcessTasks();
        copy.IsCompleted.Should().BeFalse();

        cancellation.Cancel();
        Drive(engine, copy);

        copy.IsFaulted.Should().BeTrue();
        copy.Error!.Get("name").AsString().Should().Be("AbortError");
        destination.Disposed.Should().BeTrue();
    }

    [Test]
    public void ACopyCancelledBeforeItStartsNeverLocksTheSource()
    {
        var engine = StreamEngine();
        var destination = new RecordingStream();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var source = engine.Evaluate("new ReadableStream()");
        engine.SetValue("source", source);

        var copy = engine.WebApi.StartReadableStreamCopy(source, destination, options: null, cancellation.Token);

        copy.IsFaulted.Should().BeTrue();
        copy.Error!.Get("name").AsString().Should().Be("AbortError");
        destination.Disposed.Should().BeTrue();
        engine.Evaluate("source.locked").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void ACopyAbandonedByARestoreReportsItselfCompletedRatherThanPollingForever()
    {
        var engine = StreamEngine();
        var destination = new RecordingStream();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var source = engine.Evaluate("new ReadableStream({ pull(c) { } })");
        var copy = engine.WebApi.StartReadableStreamCopy(source, destination);
        copy.IsCompleted.Should().BeFalse();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        copy.IsCompleted.Should().BeTrue();
        copy.IsFaulted.Should().BeTrue();
        copy.Error!.Get("message").AsString().Should().Contain("abandoned");
        destination.Disposed.Should().BeTrue();
    }

    [Test]
    public void TheOptionsRefuseValuesThatWouldMeanNothing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HostReadableStreamOptions { ChunkSize = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new HostReadableStreamOptions { HighWaterMark = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new HostWritableStreamOptions { HighWaterMark = double.NaN });
    }

    [Test]
    public void ANonReadableOrNonWritableStreamIsRefusedAtTheBoundary()
    {
        var engine = StreamEngine();

        Assert.Throws<ArgumentNullException>(() => engine.WebApi.CreateReadableStream(null!));
        Assert.Throws<ArgumentNullException>(() => engine.WebApi.CreateWritableStream(null!));

        var readOnly = new RecordingStream(Utf8("abc"));
        Assert.Throws<ArgumentException>(() => engine.WebApi.CreateWritableStream(readOnly));
    }

    [Test]
    public void AStreamCreatedWithoutTheFeatureFlagStillWorksButIsNotInstanceOfAnything()
    {
        // The bridge installs no global and needs none: what the Streams flag buys a script is the
        // constructor's name, not the object's behaviour.
        var engine = new Engine();
        engine.SetValue("input", engine.WebApi.CreateReadableStream(new RecordingStream(Utf8("ok"))));

        engine.Evaluate("typeof ReadableStream").AsString().Should().Be("undefined");
        engine.Evaluate("(async () => String.fromCharCode(...(await input.getReader().read()).value))()")
            .UnwrapIfPromise().AsString().Should().Be("ok");
    }
}
#endif
