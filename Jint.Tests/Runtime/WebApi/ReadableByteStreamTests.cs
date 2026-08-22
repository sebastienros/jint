#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// Readable byte streams — <c>new ReadableStream({ type: "bytes" })</c>,
/// <c>ReadableByteStreamController</c>, <c>ReadableStreamBYOBRequest</c> and BYOB reading — against the
/// Streams Standard, https://streams.spec.whatwg.org/#rbs-controller-class.
/// </summary>
/// <remarks>
/// The thing every one of these tests is really about is buffer ownership: a byte stream transfers the
/// <c>ArrayBuffer</c> behind every view that crosses it, so the side that handed a view over is left with a
/// detached one and the side that received it owns the memory. Several tests assert the detachment
/// directly, because it is the observable half of the zero-copy design.
/// </remarks>
public class ReadableByteStreamTests
{
    private static Engine StreamEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));
        engine.Execute("var log = [];");
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    [Fact]
    public void ConstructsAByteStream()
    {
        var engine = StreamEngine();
        engine.Execute("var stream = new ReadableStream({ type: 'bytes' });");

        engine.Evaluate("stream instanceof ReadableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("stream.locked").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void GivesTheUnderlyingSourceAByteController()
    {
        var engine = StreamEngine();
        engine.Execute("var seen; new ReadableStream({ type: 'bytes', start(c) { seen = c; } });");

        engine.Evaluate("Object.getPrototypeOf(seen).constructor.name").AsString().Should().Be("ReadableByteStreamController");
        engine.Evaluate("seen[Symbol.toStringTag]").AsString().Should().Be("ReadableByteStreamController");

        // A byte stream's default high water mark is 0, not 1: it pulls only once a consumer asks.
        engine.Evaluate("seen.desiredSize").AsNumber().Should().Be(0);
        engine.Evaluate("seen.byobRequest").Should().Be(JsValue.Null);
    }

    [Fact]
    public void RefusesAQueuingStrategyWithASizeFunction()
    {
        // "If strategy["size"] exists, throw a RangeError exception" — a byte stream's chunk size is its
        // byte length, so a size() would have nothing to mean.
        var engine = StreamEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new ReadableStream({ type: 'bytes' }, { size: () => 1 })"))
            .Error.Get("name").AsString().Should().Be("RangeError");

        // A high water mark on its own is fine, and is in bytes.
        engine.Execute("var c; new ReadableStream({ type: 'bytes', start(controller) { c = controller; } }, { highWaterMark: 64 });");
        engine.Evaluate("c.desiredSize").AsNumber().Should().Be(64);
    }

    [Fact]
    public void ReadsAnEnqueuedChunkWithADefaultReader()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
                type: 'bytes',
                start(c) { c.enqueue(new Uint8Array([1, 2, 3])); c.close(); }
            });
            var reader = stream.getReader();
            reader.read().then(r => log.push(r.done + ':' + r.value.constructor.name + ':' + Array.from(r.value)));
            reader.read().then(r => log.push(r.done + ':' + r.value));
            """);

        // A default read of a byte stream always yields a Uint8Array, whatever view was enqueued.
        Log(engine).Should().Be("false:Uint8Array:1,2,3,true:undefined");
    }

    [Fact]
    public void TransfersTheBufferOfAnEnqueuedChunk()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var source = new Uint8Array([7, 8]);
            var c;
            var stream = new ReadableStream({ type: 'bytes', start(controller) { c = controller; } });
            c.enqueue(source);
            """);

        // The chunk's buffer belongs to the stream now, so the caller's view is detached.
        engine.Evaluate("source.byteLength").AsNumber().Should().Be(0);
        engine.Evaluate("source.buffer.byteLength").AsNumber().Should().Be(0);
    }

    [Fact]
    public void RefusesAnEmptyOrNonViewChunk()
    {
        var engine = StreamEngine();
        engine.Execute("var c; new ReadableStream({ type: 'bytes', start(controller) { c = controller; } });");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("c.enqueue('nope')"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("c.enqueue(new Uint8Array(0))"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        // A view over a detached buffer has a byte length of 0 and is refused by the same check.
        engine.Execute("var detached = new Uint8Array([1]); c.enqueue(detached);");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("c.enqueue(detached)"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void AcquiresABYOBReader()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ type: 'bytes' });
            var reader = stream.getReader({ mode: 'byob' });
            """);

        engine.Evaluate("Object.getPrototypeOf(reader).constructor.name").AsString().Should().Be("ReadableStreamBYOBReader");
        engine.Evaluate("stream.locked").AsBoolean().Should().BeTrue();

        // The constructor is equivalent to getReader({ mode: 'byob' }), and refuses a locked stream.
        var byobReaderConstructor = "Object.getPrototypeOf(reader).constructor";
        Assert.Throws<JavaScriptException>(() => engine.Evaluate($"new ({byobReaderConstructor})(stream)"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        engine.Execute("reader.releaseLock();");
        engine.Evaluate($"new ({byobReaderConstructor})(stream) instanceof Object").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void RefusesABYOBReaderForANonByteStream()
    {
        var engine = StreamEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new ReadableStream().getReader({ mode: 'byob' })"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        // And the stream is not left locked by the attempt.
        engine.Evaluate("(() => { const s = new ReadableStream(); try { s.getReader({ mode: 'byob' }); } catch (e) {} return s.locked; })()")
            .AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void FillsABYOBReadFromTheQueue()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
                type: 'bytes',
                start(c) { c.enqueue(new Uint8Array([1, 2, 3, 4])); }
            });
            var reader = stream.getReader({ mode: 'byob' });
            var view = new Uint8Array(2);
            reader.read(view).then(r => log.push(r.done + ':' + Array.from(r.value) + ':' + r.value.byteOffset));
            reader.read(new Uint8Array(4)).then(r => log.push(r.done + ':' + Array.from(r.value)));
            """);

        // The second read is served from what is left of the queued chunk: a BYOB read fulfils as soon as
        // its minimum fill (one element, by default) is met, not once the whole view is full.
        Log(engine).Should().Be("false:1,2:0,false:3,4");
    }

    [Fact]
    public void TransfersTheViewPassedToABYOBRead()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ type: 'bytes', start(c) { c.enqueue(new Uint8Array([9])); } });
            var reader = stream.getReader({ mode: 'byob' });
            var view = new Uint8Array(1);
            var buffer = view.buffer;
            var returned;
            reader.read(view).then(r => { returned = r.value; });
            """);

        // The caller's view and its buffer are detached; the value handed back is a new view of the same
        // type onto the same memory, which is what makes a BYOB read loop recycle one allocation.
        engine.Evaluate("view.byteLength").AsNumber().Should().Be(0);
        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
        engine.Evaluate("returned.constructor.name").AsString().Should().Be("Uint8Array");
        engine.Evaluate("Array.from(returned).join()").AsString().Should().Be("9");
        engine.Evaluate("returned.buffer === buffer").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void KeepsTheViewTypeOfABYOBRead()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var c;
            var stream = new ReadableStream({ type: 'bytes', start(controller) { c = controller; } });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint16Array(2)).then(r => log.push(r.value.constructor.name + ':' + r.value.length + ':' + Array.from(r.value)));
            c.enqueue(new Uint8Array([1, 0, 2, 0]));
            """);

        // The view constructor comes from the typed array constructors table, and the element size is what
        // makes four bytes two Uint16 elements.
        Log(engine).Should().Be("Uint16Array:2:1,2");
    }

    [Fact]
    public void ReadsIntoADataView()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var c;
            var stream = new ReadableStream({ type: 'bytes', start(controller) { c = controller; } });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new DataView(new ArrayBuffer(2))).then(r => log.push(r.value.constructor.name + ':' + r.value.getUint8(0) + ',' + r.value.getUint8(1)));
            c.enqueue(new Uint8Array([5, 6]));
            """);

        Log(engine).Should().Be("DataView:5,6");
    }

    [Fact]
    public void HonoursTheMinimumFill()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var c;
            var stream = new ReadableStream({ type: 'bytes', start(controller) { c = controller; } });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(4), { min: 3 }).then(r => log.push('read:' + Array.from(r.value)));
            c.enqueue(new Uint8Array([1]));
            log.push('one');
            c.enqueue(new Uint8Array([2]));
            log.push('two');
            c.enqueue(new Uint8Array([3]));
            log.push('three');
            """);

        // The read only fulfils once three bytes have arrived, and it takes a microtask to do so.
        Log(engine).Should().Be("one,two,three,read:1,2,3");
    }

    [Fact]
    public void ValidatesTheArgumentsOfABYOBRead()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ type: 'bytes' });
            var reader = stream.getReader({ mode: 'byob' });
            function reason(source) { return eval(source).then(() => 'resolved', e => e.name); }
            """);

        // Everything about read() rejects rather than throws: the operation returns a promise type.
        engine.Execute("""
            reason("reader.read('nope')").then(n => log.push('nonView:' + n));
            reason("reader.read(new Uint8Array(0))").then(n => log.push('empty:' + n));
            reason("reader.read(new Uint8Array(2), { min: 0 })").then(n => log.push('minZero:' + n));
            reason("reader.read(new Uint8Array(2), { min: 3 })").then(n => log.push('minTooBig:' + n));
            """);

        engine.Execute("reader.releaseLock(); reason('reader.read(new Uint8Array(2))').then(n => log.push('released:' + n));");

        Log(engine).Should().Be("nonView:TypeError,empty:TypeError,minZero:TypeError,minTooBig:RangeError,released:TypeError");
    }

    [Fact]
    public void OffersABYOBRequestToTheUnderlyingSource()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
                type: 'bytes',
                pull(c) {
                    log.push('pull:' + c.byobRequest.view.byteLength);
                    c.byobRequest.view[0] = 42;
                    c.byobRequest.respond(1);
                }
            });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(8)).then(r => log.push('read:' + r.value.byteLength + ':' + r.value[0]));
            """);

        // The request's view is a window onto the caller's own buffer: the source writes straight into it.
        Log(engine).Should().Be("pull:8,read:1:42");
    }

    [Fact]
    public void InvalidatesTheBYOBRequestOnceRespondedTo()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var kept;
            var stream = new ReadableStream({
                type: 'bytes',
                pull(c) { kept = c.byobRequest; kept.view[0] = 1; kept.respond(1); }
            });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(1)).then(() => log.push('read'));
            """);

        Log(engine).Should().Be("read");
        engine.Evaluate("kept.view").Should().Be(JsValue.Null);
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("kept.respond(1)"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void ValidatesRespond()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var request;
            var stream = new ReadableStream({ type: 'bytes', pull(c) { request = c.byobRequest; } });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(2));
            """);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("request.respond(3)"))
            .Error.Get("name").AsString().Should().Be("RangeError");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("request.respond(0)"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("request.respond(-1)"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void RespondsWithANewView()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var replacement;
            var stream = new ReadableStream({
                type: 'bytes',
                pull(c) {
                    const request = c.byobRequest;
                    replacement = new Uint8Array(request.view.buffer, request.view.byteOffset, 2);
                    replacement[0] = 3;
                    replacement[1] = 4;
                    c.byobRequest.respondWithNewView(replacement);
                }
            });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(4)).then(r => log.push('read:' + Array.from(r.value)));
            """);

        Log(engine).Should().Be("read:3,4");

        // The source's own view is transferred away as the chunk is handed over, so it cannot go on writing
        // into memory the consumer now owns.
        engine.Evaluate("replacement.byteLength").AsNumber().Should().Be(0);
    }

    [Fact]
    public void ValidatesRespondWithANewView()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var request;
            var stream = new ReadableStream({ type: 'bytes', pull(c) { request = c.byobRequest; } });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(4));
            """);

        // The new view has to describe the very region the request named: the same start, a buffer of the
        // same capacity, and no more bytes than were asked for. Note what is *not* checked — the buffer
        // need not be the request's own, since it is transferred in either way.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("request.respondWithNewView(new Uint8Array(8))"))
            .Error.Get("name").AsString().Should().Be("RangeError");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate(
            "request.respondWithNewView(new Uint8Array(request.view.buffer, request.view.byteOffset + 1, 1))"))
            .Error.Get("name").AsString().Should().Be("RangeError");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("request.respondWithNewView('nope')"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void AutomaticallyAllocatesABufferForADefaultRead()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
                type: 'bytes',
                autoAllocateChunkSize: 16,
                pull(c) {
                    log.push('request:' + c.byobRequest.view.byteLength);
                    c.byobRequest.view.set([1, 2]);
                    c.byobRequest.respond(2);
                }
            });
            stream.getReader().read().then(r => log.push('read:' + r.value.byteLength + ':' + Array.from(r.value)));
            """);

        // autoAllocateChunkSize is what lets a source written for BYOB serve an ordinary read() as well.
        Log(engine).Should().Be("request:16,read:2:1,2");
    }

    [Fact]
    public void LetsTheSourceEnqueueInsteadOfRespondingToAnAutomaticAllocation()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var kept;
            var stream = new ReadableStream({
                type: 'bytes',
                autoAllocateChunkSize: 8,
                pull(c) { kept = c.byobRequest; c.enqueue(new Uint8Array([9, 9])); }
            });
            stream.getReader().read().then(r => log.push('read:' + Array.from(r.value)));
            """);

        // The allocated buffer is simply dropped: an enqueue past a pending pull-into invalidates its BYOB
        // request first, so the source cannot respond into a buffer whose bytes nobody is waiting for.
        Log(engine).Should().Be("read:9,9");
        engine.Evaluate("kept.view").Should().Be(JsValue.Null);
    }

    [Fact]
    public void RefusesAZeroAutoAllocateChunkSize()
    {
        var engine = StreamEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new ReadableStream({ type: 'bytes', autoAllocateChunkSize: 0 })"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        // The member is an [EnforceRange] unsigned long long, so it is converted — and rejected — even for a
        // stream that is not a byte stream.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new ReadableStream({ autoAllocateChunkSize: -1 })"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void HandsBackTheMemoryOfAClosedStream()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ type: 'bytes', start(c) { c.close(); } });
            var reader = stream.getReader({ mode: 'byob' });
            var view = new Uint8Array(4);
            reader.read(view).then(r => log.push(r.done + ':' + r.value.constructor.name + ':' + r.value.byteLength + ':' + r.value.buffer.byteLength));
            """);

        // "byobReader.read(chunk) will fulfill with { value: newViewOnSameMemory, done: true } for closed
        // streams" — the memory comes back, as an empty view onto a buffer of the original size.
        Log(engine).Should().Be("true:Uint8Array:0:4");
    }

    [Fact]
    public void DiscardsTheMemoryOfACancelledStream()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ type: 'bytes' });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(4)).then(r => log.push(r.done + ':' + r.value));
            reader.cancel('bored');
            """);

        // "If the stream is canceled, the backing memory is discarded and byobReader.read(chunk) fulfills
        // with the more traditional { value: undefined, done: true } instead."
        Log(engine).Should().Be("true:undefined");
    }

    [Fact]
    public void RefusesToCloseWithAPartialElement()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var c;
            var stream = new ReadableStream({ type: 'bytes', start(controller) { c = controller; } });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint16Array(2)).then(() => log.push('resolved'), e => log.push('rejected:' + e.name));
            c.byobRequest.view[0] = 1;
            c.byobRequest.respond(1);
            """);

        // One byte of a two-byte element has been written, so there is nothing that can be handed back.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("c.close()"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        engine.Execute("");
        Log(engine).Should().Be("rejected:TypeError");
    }

    [Fact]
    public void EndsAPartlyFilledReadWithAZeroByteResponse()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var pulls = 0;
            var stream = new ReadableStream({
                type: 'bytes',
                pull(c) {
                    if (pulls === 0) { c.byobRequest.view[0] = 1; c.byobRequest.respond(1); }
                    else { c.close(); c.byobRequest.respond(0); }
                    pulls++;
                }
            });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(3), { min: 3 }).then(r => log.push(r.done + ':' + Array.from(r.value)));
            reader.closed.then(() => log.push('closed'));
            """);

        // Closing does not settle a pending BYOB read on its own — ReadableStreamClose touches only a
        // default reader's read requests. The source ends it by responding with zero bytes, which commits
        // whatever was filled: "the promise is fulfilled with the remaining elements in the stream, which
        // might be fewer than the initially requested amount".
        // The closed promise settles first: close() resolves it, and only the respond() after it commits
        // the descriptor that fulfils the read.
        Log(engine).Should().Be("closed,true:1");
        engine.Evaluate("pulls").AsNumber().Should().Be(2);
    }

    [Fact]
    public void RefusesAReadThatCanNeverBeFilled()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
                type: 'bytes',
                start(c) { c.enqueue(new Uint8Array([1, 2, 3])); c.close(); }
            });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint16Array(2), { min: 2 }).then(() => log.push('resolved'), e => log.push('read:' + e.name));
            reader.closed.then(() => log.push('closed'), e => log.push('closed:' + e.name));
            """);

        // Three bytes cannot fill two Uint16 elements and the stream has been closed, so no more are
        // coming: the read is refused and the stream errors rather than hanging.
        Log(engine).Should().Be("read:TypeError,closed:TypeError");
    }

    [Fact]
    public void ErrorsAPendingBYOBReadWhenTheStreamErrors()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var c;
            var stream = new ReadableStream({ type: 'bytes', start(controller) { c = controller; } });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(2)).then(() => log.push('resolved'), e => log.push('rejected:' + e.message));
            reader.closed.catch(e => log.push('closed:' + e.message));
            c.error(new Error('boom'));
            """);

        // The closed promise is rejected before the pending read is: ReadableStreamError settles the
        // reader's closedness first and only then errors its read-into requests.
        Log(engine).Should().Be("closed:boom,rejected:boom");
    }

    [Fact]
    public void RejectsPendingBYOBReadsWhenTheLockIsReleased()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var c;
            var stream = new ReadableStream({ type: 'bytes', start(controller) { c = controller; } });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(2)).then(() => log.push('resolved'), e => log.push('rejected:' + e.name));
            reader.releaseLock();
            log.push('released:' + stream.locked);
            """);

        Log(engine).Should().Be("released:false,rejected:TypeError");
    }

    [Fact]
    public void KeepsTheBytesWrittenIntoAnAbandonedBuffer()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var c;
            var stream = new ReadableStream({ type: 'bytes', start(controller) { c = controller; } });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(4)).catch(() => log.push('abandoned'));
            reader.releaseLock();

            // The underlying source is still filling the buffer it was given, and may still respond into it.
            c.byobRequest.view[0] = 11;
            c.byobRequest.view[1] = 12;
            c.byobRequest.respond(2);

            stream.getReader().read().then(r => log.push('recovered:' + Array.from(r.value)));
            """);

        // [[ReleaseSteps]] downgrades the descriptor to reader type "none" rather than dropping it, so the
        // bytes the source wrote end up in the stream's queue instead of being lost.
        Log(engine).Should().Be("abandoned,recovered:11,12");
    }

    [Fact]
    public void ServesAWaitingDefaultReadDirectlyFromAnEnqueue()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var c;
            var stream = new ReadableStream({ type: 'bytes', start(controller) { c = controller; } });
            var reader = stream.getReader();
            reader.read().then(r => log.push('read:' + Array.from(r.value)));
            c.enqueue(new Uint8Array([1, 2]));
            log.push('enqueued:' + c.desiredSize);
            """);

        // The waiting read takes the chunk directly, so nothing is left in the queue.
        Log(engine).Should().Be("enqueued:0,read:1,2");
    }

    [Fact]
    public void SplitsOneEnqueueAcrossSeveralBYOBReads()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var c;
            var stream = new ReadableStream({ type: 'bytes', start(controller) { c = controller; } });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(2)).then(r => log.push('a:' + Array.from(r.value)));
            reader.read(new Uint8Array(2)).then(r => log.push('b:' + Array.from(r.value)));
            c.enqueue(new Uint8Array([1, 2, 3, 4]));
            """);

        Log(engine).Should().Be("a:1,2,b:3,4");
    }

    [Fact]
    public void TeesAByteStreamIntoTwoByteStreams()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
                type: 'bytes',
                start(c) { c.enqueue(new Uint8Array([1, 2])); c.close(); }
            });
            var [a, b] = stream.tee();
            log.push('byob:' + (typeof a.getReader({ mode: 'byob' }).read === 'function'));
            b.getReader().read().then(r => log.push('b:' + Array.from(r.value)));
            """);

        // Both branches are byte streams, so both can be read BYOB — and the second branch's chunk is a
        // copy, since a byte stream transfers the buffer of everything enqueued into it.
        Log(engine).Should().Be("byob:true,b:1,2");
    }

    [Fact]
    public void TeeReadsTheOriginalThroughABranchesOwnBuffer()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
                type: 'bytes',
                pull(c) { c.byobRequest.view.set([1, 2]); c.byobRequest.respond(2); },
                autoAllocateChunkSize: 8
            });
            var [a, b] = stream.tee();
            a.getReader({ mode: 'byob' }).read(new Uint8Array(2)).then(r => log.push('a:' + Array.from(r.value)));
            b.getReader().read().then(r => log.push('b:' + Array.from(r.value)));
            """);

        Log(engine).Should().Be("a:1,2,b:1,2");
    }

    [Fact]
    public void PipesAndIteratesAByteStream()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
                type: 'bytes',
                start(c) { c.enqueue(new Uint8Array([1])); c.enqueue(new Uint8Array([2])); c.close(); }
            });
            (async () => { for await (const chunk of stream) { log.push('chunk:' + Array.from(chunk)); } log.push('done'); })();
            """);

        Log(engine).Should().Be("chunk:1,chunk:2,done");

        var piped = StreamEngine();
        piped.Execute("""
            var stream = new ReadableStream({
                type: 'bytes',
                start(c) { c.enqueue(new Uint8Array([3, 4])); c.close(); }
            });
            var sink = new WritableStream({ write(chunk) { log.push('write:' + Array.from(chunk)); } });
            stream.pipeTo(sink).then(() => log.push('piped'));
            """);

        Log(piped).Should().Be("write:3,4,piped");
    }

    [Fact]
    public void NamesTheThreeByteStreamInterfaceObjectsOnTheGlobal()
    {
        var engine = StreamEngine();

        // Globals like every other Streams interface, and each is what its instances inherit from.
        engine.Evaluate("typeof ReadableByteStreamController").AsString().Should().Be("function");
        engine.Evaluate("typeof ReadableStreamBYOBReader").AsString().Should().Be("function");
        engine.Evaluate("typeof ReadableStreamBYOBRequest").AsString().Should().Be("function");

        engine.Execute("""
            var request;
            var stream = new ReadableStream({ type: 'bytes', pull(c) { request = c.byobRequest; } });
            stream.getReader({ mode: 'byob' }).read(new Uint8Array(1));
            """);

        engine.Evaluate("request instanceof ReadableStreamBYOBRequest").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(request) === ReadableStreamBYOBRequest.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("request[Symbol.toStringTag]").AsString().Should().Be("ReadableStreamBYOBRequest");
    }

    [Fact]
    public void RefusesToConstructTheControllerAndTheRequest()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller, request;
            var stream = new ReadableStream({ type: 'bytes', pull(c) { controller = c; request = c.byobRequest; } });
            stream.getReader({ mode: 'byob' }).read(new Uint8Array(1));
            """);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new (Object.getPrototypeOf(controller).constructor)()"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new (Object.getPrototypeOf(request).constructor)()"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void RefusesAForeignReceiver()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller, request;
            var stream = new ReadableStream({ type: 'bytes', pull(c) { controller = c; request = c.byobRequest; } });
            var reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(1));
            """);

        // pull() runs on a microtask, so the controller and its BYOB request only exist once the first
        // script has been executed and the event loop drained.
        engine.Execute("""
            var controllerProto = Object.getPrototypeOf(controller);
            var requestProto = Object.getPrototypeOf(request);
            var readerProto = Object.getPrototypeOf(reader);
            """);

        foreach (var source in new[]
                 {
                     "Object.getOwnPropertyDescriptor(controllerProto, 'desiredSize').get.call({})",
                     "controllerProto.enqueue.call({}, new Uint8Array(1))",
                     "Object.getOwnPropertyDescriptor(requestProto, 'view').get.call({})",
                     "requestProto.respond.call({}, 1)",
                     "readerProto.releaseLock.call({})",
                 })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate(source))
                .Error.Get("name").AsString().Should().Be("TypeError", source);
        }

        // The two promise-returning members reject instead of throwing.
        engine.Execute("""
            readerProto.read.call({}, new Uint8Array(1)).catch(e => log.push('read:' + e.name));
            Object.getOwnPropertyDescriptor(readerProto, 'closed').get.call({}).catch(e => log.push('closed:' + e.name));
            """);

        Log(engine).Should().Be("read:TypeError,closed:TypeError");
    }

    /// <summary>
    /// The source every composition test below reads from: a byte stream that hands out the chunks it was
    /// given and then closes.
    /// </summary>
    private const string ByteStreamSource = """
        function byteStream(chunks) {
            let i = 0;
            return new ReadableStream({
                type: 'bytes',
                pull(c) {
                    if (i < chunks.length) { c.enqueue(new Uint8Array(chunks[i++])); } else { c.close(); }
                }
            });
        }
        """;

    /// <summary>
    /// A byte stream is a readable stream like any other on the producing side, so it pipes through the
    /// transform streams other standards define. Those were written against the default machinery and know
    /// nothing about byte streams; that they need to know nothing is the point of extending the default
    /// controller rather than forking it.
    /// </summary>
    [Fact]
    public void PipesThroughATransformStreamDefinedByAnotherStandard()
    {
        var engine = new Engine(options => options.UseWebApis(
            WebApiFeatures.Streams | WebApiFeatures.Encoding | WebApiFeatures.Compression));
        engine.Execute(ByteStreamSource);

        // The bytes of "hi!", split across two chunks, decoded by a TextDecoderStream — which emits one
        // string per input chunk, so the whole stream has to be drained to see all of it.
        engine.Evaluate("""
            (async () => {
                let out = '';
                for await (const s of byteStream([[104, 105], [33]]).pipeThrough(new TextDecoderStream())) { out += s; }
                return out;
            })()
            """)
            .UnwrapIfPromise().AsString().Should().Be("hi!");

        // And through a pair that has to see every byte: gzip and back.
        engine.Evaluate("""
            byteStream([[1, 2, 3, 4, 5]])
                .pipeThrough(new CompressionStream('gzip'))
                .pipeThrough(new DecompressionStream('gzip'))
                .getReader().read().then(r => Array.from(r.value).join(','))
            """)
            .UnwrapIfPromise().AsString().Should().Be("1,2,3,4,5");

        // A script's own TransformStream sees ordinary Uint8Array chunks too.
        engine.Evaluate("""
            byteStream([[10, 20, 30]])
                .pipeThrough(new TransformStream({ transform(c, ctrl) { ctrl.enqueue(c.length); } }))
                .getReader().read().then(r => r.value)
            """)
            .UnwrapIfPromise().AsNumber().Should().Be(3);
    }

    /// <summary>
    /// A byte stream is accepted as a <c>BodyInit</c>, which reads it with a <i>default</i> reader — the one
    /// path through a byte controller that an ordinary consumer takes without knowing it is one.
    /// </summary>
    [Fact]
    public void IsAcceptedAsAFetchBody()
    {
        var engine = new Engine(options => options.UseFetch());
        engine.Execute(ByteStreamSource);

        engine.Evaluate("new Response(byteStream([[65, 66], [67]])).text()")
            .UnwrapIfPromise().AsString().Should().Be("ABC");

        engine.Evaluate("new Request('https://example.org/', { method: 'POST', body: byteStream([[68]]), duplex: 'half' }).text()")
            .UnwrapIfPromise().AsString().Should().Be("D");
    }

    /// <summary>
    /// Every stream the <i>engine</i> hands out over a byte sequence it holds is a byte stream:
    /// <c>Response.body</c> and <c>Request.body</c> (https://fetch.spec.whatwg.org/#concept-body, whose
    /// stream is "set up with byte reading support") and <c>Blob.stream()</c>
    /// (https://w3c.github.io/FileAPI/#blob-get-stream). So BYOB reading works on all three.
    /// </summary>
    /// <remarks>
    /// This test replaces the boundary pin that asserted the opposite. It is the observable half of the
    /// upgrade: nothing about the default-reader path changed, and the next test says so.
    /// </remarks>
    [Fact]
    public void ServesBYOBOnTheBodiesTheEngineHandsOut()
    {
        var engine = new Engine(options => options.UseFetch());

        foreach (var source in BodySources)
        {
            engine.Execute($"var reader = ({source}).getReader({{ mode: 'byob' }});");
            engine.Evaluate("Object.getPrototypeOf(reader).constructor.name").AsString()
                .Should().Be("ReadableStreamBYOBReader", source);

            // The caller's buffer is filled in place, and the view handed back is a view onto the transfer
            // of that very buffer — which is the whole point of reading BYOB.
            engine.Evaluate("""
                reader.read(new Uint8Array(8)).then(r =>
                    r.done + ':' + r.value.constructor.name + ':' + r.value.byteLength + ':' +
                    String.fromCharCode.apply(null, Array.from(r.value)))
                """)
                .UnwrapIfPromise().AsString().Should().Be("false:Uint8Array:2:xy", source);

            // A second read sees the end of the body, and gets its buffer back rather than losing it.
            engine.Evaluate("reader.read(new Uint8Array(8)).then(r => r.done + ':' + r.value.byteLength)")
                .UnwrapIfPromise().AsString().Should().Be("true:0", source);
        }
    }

    /// <summary>
    /// The regression half of the upgrade above: a default reader on those same bodies still answers with
    /// one <c>Uint8Array</c> chunk carrying the whole body, and every <c>Body</c>-mixin consumer still works.
    /// </summary>
    [Fact]
    public void StillServesADefaultReaderOnTheBodiesTheEngineHandsOut()
    {
        var engine = new Engine(options => options.UseFetch());

        foreach (var source in BodySources)
        {
            engine.Evaluate($"({source}).getReader().read().then(r => r.done + ':' + r.value.constructor.name + ':' + Array.from(r.value))")
                .UnwrapIfPromise().AsString().Should().Be("false:Uint8Array:120,121", source);
        }

        engine.Evaluate("new Response('xy').text()").UnwrapIfPromise().AsString().Should().Be("xy");
        engine.Evaluate("new Blob(['xy']).text()").UnwrapIfPromise().AsString().Should().Be("xy");
        engine.Evaluate("new Response('xy').arrayBuffer().then(b => b.byteLength)").UnwrapIfPromise().AsNumber().Should().Be(2);
    }

    /// <summary>
    /// <c>tee()</c> on a body picks the byte-stream algorithm now, so both branches are byte streams and each
    /// can be read BYOB — https://streams.spec.whatwg.org/#readable-stream-tee dispatches on the controller.
    /// </summary>
    [Fact]
    public void TeesABodyIntoTwoByteStreams()
    {
        var engine = new Engine(options => options.UseFetch());
        engine.Execute("var branches = new Response('xy').body.tee();");

        engine.Evaluate("branches[0].getReader({ mode: 'byob' }).read(new Uint8Array(4)).then(r => Array.from(r.value).join(','))")
            .UnwrapIfPromise().AsString().Should().Be("120,121");

        // The second branch is independent, and reads the same bytes — through a default reader, which a
        // byte tee serves by swapping the reader over the original.
        engine.Evaluate("branches[1].getReader().read().then(r => Array.from(r.value).join(','))")
            .UnwrapIfPromise().AsString().Should().Be("120,121");
    }

    /// <summary>
    /// <c>clone()</c> is the <c>Body</c> mixin's own tee — https://fetch.spec.whatwg.org/#concept-body-clone
    /// — so a cloned body whose stream had already been materialized is a byte stream on both sides.
    /// </summary>
    [Fact]
    public void ClonesAMaterializedBodyIntoTwoByteStreams()
    {
        var engine = new Engine(options => options.UseFetch());

        // Reading `body` first is what forces the clone down the tee path rather than the shared-source one.
        engine.Execute("var original = new Response('xy'); original.body; var copy = original.clone();");

        foreach (var name in new[] { "original", "copy" })
        {
            engine.Evaluate($"{name}.body.getReader({{ mode: 'byob' }}).read(new Uint8Array(4)).then(r => Array.from(r.value).join(','))")
                .UnwrapIfPromise().AsString().Should().Be("120,121", name);
        }
    }

    /// <summary>
    /// The bodies the engine hands out have no <c>autoAllocateChunkSize</c>, so a BYOB read is served by the
    /// bytes the source enqueues rather than through a <c>byobRequest</c> — but the buffer the caller
    /// supplied is still transferred away from it, which is the ownership rule every byte stream keeps.
    /// </summary>
    [Fact]
    public void TransfersTheCallersBufferOnABodyRead()
    {
        var engine = new Engine(options => options.UseFetch());
        engine.Execute("var view = new Uint8Array(8); var buffer = view.buffer;");

        engine.Evaluate("new Blob(['xy']).stream().getReader({ mode: 'byob' }).read(view).then(r => r.value.byteLength)")
            .UnwrapIfPromise().AsNumber().Should().Be(2);

        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
        engine.Evaluate("view.byteLength").AsNumber().Should().Be(0);
    }

    /// <summary>
    /// The three streams the engine builds over bytes it already holds — a buffered <c>Response</c> body, a
    /// buffered <c>Request</c> body and a <c>Blob</c>'s stream. All three carry the two bytes of "xy".
    /// </summary>
    private static readonly string[] BodySources =
    [
        "new Response('xy').body",
        "new Request('https://example.org/', { method: 'POST', body: 'xy' }).body",
        "new Blob(['xy']).stream()",
    ];
}
#endif
