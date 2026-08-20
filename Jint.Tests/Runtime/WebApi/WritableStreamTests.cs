#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>WritableStream</c>, its default controller and its default writer, against the Streams Standard —
/// https://streams.spec.whatwg.org/#ws.
/// </summary>
public class WritableStreamTests
{
    private static Engine StreamEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));
        engine.Execute("var log = [];");
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    [Fact]
    public void ConstructsWithNoArgumentsAtAll()
    {
        var engine = StreamEngine();
        engine.Evaluate("new WritableStream() instanceof WritableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("new WritableStream().locked").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void RefusesANonObjectSinkAndAnySinkType()
    {
        var engine = StreamEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new WritableStream(null)"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        // The `type` member is reserved for a future byte-oriented writable stream; any value is a RangeError.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new WritableStream({ type: 'bytes' })"))
            .Error.Get("name").AsString().Should().Be("RangeError");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new WritableStream({ type: 'anything' })"))
            .Error.Get("name").AsString().Should().Be("RangeError");
    }

    [Fact]
    public void CallsStartSynchronouslyAndRethrowsItsException()
    {
        var engine = StreamEngine();
        engine.Execute("new WritableStream({ start(c) { log.push('start:' + (typeof c.error)); } }); log.push('after');");
        Log(engine).Should().Be("start:function,after");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new WritableStream({ start() { throw new Error('boom'); } })"))
            .Error.Get("message").AsString().Should().Be("boom");
    }

    [Fact]
    public void WritesReachTheSinkInOrderOneAtATime()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var release;
            var stream = new WritableStream({
              write(chunk) {
                log.push('write:' + chunk);
                return new Promise(r => { release = () => { log.push('done:' + chunk); r(); }; });
              }
            });
            var writer = stream.getWriter();
            writer.write('a').then(() => log.push('a-settled'));
            writer.write('b').then(() => log.push('b-settled'));
            log.push('queued');
            """);

        // Nothing reaches the sink synchronously: the queue only advances once start() has settled, which
        // is a microtask away even for an absent start. Then only the first write is handed over; the
        // second waits behind it.
        Log(engine).Should().Be("queued,write:a");

        engine.Execute("release();");
        Log(engine).Should().Be("queued,write:a,done:a,write:b,a-settled");

        engine.Execute("release();");
        Log(engine).Should().Be("queued,write:a,done:a,write:b,a-settled,done:b,b-settled");
    }

    [Fact]
    public void CloseWaitsForTheQueuedWritesAndThenCallsTheSink()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream({
              write(chunk) { log.push('write:' + chunk); },
              close() { log.push('close'); }
            });
            var writer = stream.getWriter();
            writer.write('a');
            writer.write('b');
            writer.close().then(() => log.push('closed'));
            writer.closed.then(() => log.push('writer-closed'));
            """);

        Log(engine).Should().Be("write:a,write:b,close,closed,writer-closed");
    }

    [Fact]
    public void WritingAfterCloseHasBeenRequestedFails()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var writer = new WritableStream().getWriter();
            writer.close();
            writer.write('late').catch(e => log.push('write:' + e.name));
            writer.close().catch(e => log.push('close:' + e.name));
            """);

        Log(engine).Should().Be("write:TypeError,close:TypeError");
    }

    [Fact]
    public void AFailingWriteErrorsTheStream()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream({ write() { return Promise.reject(new Error('write failed')); } });
            var writer = stream.getWriter();
            writer.write('a').catch(e => log.push('write:' + e.message));
            writer.closed.catch(e => log.push('closed:' + e.message));
            writer.ready.catch(e => log.push('ready:' + e.message));
            """);

        // The in-flight write's own promise is rejected first, then the writer's ready — which erroring
        // rejects on the way in — and the writer's closed last, once the stream has finished erroring.
        Log(engine).Should().Be("write:write failed,ready:write failed,closed:write failed");
    }

    [Fact]
    public void ControllerErrorMakesEveryLaterWriteFail()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new WritableStream({ start(c) { controller = c; } });
            var writer = stream.getWriter();
            controller.error(new Error('stopped'));
            writer.write('a').catch(e => log.push('write:' + e.message));
            writer.closed.catch(e => log.push('closed:' + e.message));
            """);

        Log(engine).Should().Be("write:stopped,closed:stopped");
    }

    [Fact]
    public void ControllerErrorIsANoOpOnceTheStreamHasStoppedBeingWritable()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new WritableStream({ start(c) { controller = c; } });
            controller.error(new Error('first'));
            controller.error(new Error('second'));
            stream.getWriter().closed.catch(e => log.push(e.message));
            """);

        Log(engine).Should().Be("first");
    }

    [Fact]
    public void ReadyTracksBackpressure()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var release;
            var stream = new WritableStream({
              write() { return new Promise(r => { release = r; }); }
            }, { highWaterMark: 1 });
            var writer = stream.getWriter();
            log.push('desired:' + writer.desiredSize);
            writer.ready.then(() => log.push('ready-1'));
            writer.write('a');
            log.push('desired:' + writer.desiredSize);
            var second = writer.ready;
            second.then(() => log.push('ready-2'));
            """);

        // The first ready was already fulfilled; the write pushed the queue to the high water mark, so the
        // getter now answers a fresh, pending promise.
        Log(engine).Should().Be("desired:1,desired:0,ready-1");

        engine.Execute("release();");
        Log(engine).Should().Be("desired:1,desired:0,ready-1,ready-2");
    }

    [Fact]
    public void AWriterAcquiredUnderBackpressureStartsNotReady()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream({ write() { return new Promise(() => {}); } }, { highWaterMark: 0 });
            var writer = stream.getWriter();
            log.push('desired:' + writer.desiredSize);
            writer.ready.then(() => log.push('ready'));
            """);

        // A high water mark of 0 means backpressure from the very first moment.
        Log(engine).Should().Be("desired:0");
    }

    [Fact]
    public void DesiredSizeCountsTheQueueAgainstTheHighWaterMark()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream({ write() { return new Promise(() => {}); } }, { highWaterMark: 3 });
            var writer = stream.getWriter();
            writer.write('a');
            log.push(writer.desiredSize);
            writer.write('b');
            log.push(writer.desiredSize);
            """);

        // The first chunk is handed to the sink but stays queued until the sink's promise settles.
        Log(engine).Should().Be("2,1");
    }

    [Fact]
    public void DesiredSizeIsZeroWhenClosedAndNullWhenErrored()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var a = new WritableStream().getWriter();
            a.close();
            var controller;
            var b = new WritableStream({ start(c) { controller = c; } }).getWriter();
            controller.error(new Error('x'));
            """);

        engine.Evaluate("a.desiredSize").AsNumber().Should().Be(0);
        engine.Evaluate("b.desiredSize").IsNull().Should().BeTrue();
    }

    [Fact]
    public void DesiredSizeThrowsForAReleasedWriter()
    {
        // The attribute is an `unrestricted double?`, not a promise type, so this one throws where the rest
        // of the writer's members reject.
        var engine = StreamEngine();
        engine.Execute("var writer = new WritableStream().getWriter(); writer.releaseLock();");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("writer.desiredSize"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void CallsTheStrategySizeOncePerChunk()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream(
              { write() { return new Promise(() => {}); } },
              { highWaterMark: 10, size(chunk) { log.push('size:' + chunk); return chunk.length; } });
            var writer = stream.getWriter();
            writer.write('a');
            writer.write('bbb');
            log.push('desired:' + writer.desiredSize);
            """);

        Log(engine).Should().Be("size:a,size:bbb,desired:6");
    }

    [Fact]
    public void AThrowingStrategySizeErrorsTheStream()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream({}, { size() { throw new Error('size failed'); } });
            var writer = stream.getWriter();
            writer.write('a').catch(e => log.push('write:' + e.message));
            writer.closed.catch(e => log.push('closed:' + e.message));
            """);

        Log(engine).Should().Be("write:size failed,closed:size failed");
    }

    [Fact]
    public void AbortErrorsTheStreamAndTellsTheSink()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream({ abort(reason) { log.push('abort:' + reason); } });
            var writer = stream.getWriter();
            writer.abort('stop').then(() => log.push('aborted'));
            writer.closed.catch(e => log.push('closed:' + e));
            writer.write('late').catch(e => log.push('write:' + e));
            """);

        Log(engine).Should().Be("abort:stop,write:stop,aborted,closed:stop");
    }

    [Fact]
    public void AbortReportsAFailingSink()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream({ abort() { throw new Error('abort failed'); } });
            stream.abort('stop').catch(e => log.push('abort:' + e.message));
            """);

        Log(engine).Should().Be("abort:abort failed");
    }

    [Fact]
    public void AbortIsANoOpOnAClosedStream()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream({ abort() { log.push('should not run'); }, close() { log.push('close'); } });
            var writer = stream.getWriter();
            writer.close().then(() => {
              writer.releaseLock();
              stream.abort('late').then(() => log.push('abort-resolved'));
            });
            """);

        Log(engine).Should().Be("close,abort-resolved");
    }

    [Fact]
    public void AbortingWhileAWriteIsInFlightWaitsForIt()
    {
        // "erroring" is the state between noticing the failure and the in-flight sink operation finishing:
        // abort() never interrupts a write the sink has already been handed.
        var engine = StreamEngine();
        engine.Execute("""
            var release, writeReached;
            var reached = new Promise(r => { writeReached = r; });
            var stream = new WritableStream({
              write() { log.push('write'); writeReached(); return new Promise(r => { release = r; }); },
              abort(reason) { log.push('abort:' + reason); }
            });
            var writer = stream.getWriter();
            writer.write('a').then(() => log.push('write-settled'), e => log.push('write-rejected:' + e));
            reached.then(() => {
              writer.abort('stop').then(() => log.push('aborted'));
              log.push('requested');
            });
            """);

        // The abort has been requested but the sink has not been asked yet.
        Log(engine).Should().Be("write,requested");

        engine.Execute("release();");

        // The write the sink had already accepted still succeeds; only then is abort() called.
        Log(engine).Should().Be("write,requested,abort:stop,write-settled,aborted");
    }

    [Fact]
    public void TheControllerSignalIsAbortedBeforeTheSinkIsAsked()
    {
        // The controller's AbortSignal exists so a sink can abandon a long write immediately, without
        // waiting to be asked to abort.
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream({
              start(c) {
                log.push('aborted:' + c.signal.aborted);
                c.signal.addEventListener('abort', () => log.push('signal:' + c.signal.reason));
              },
              abort(reason) { log.push('abort:' + reason); }
            });
            stream.abort('stop').then(() => log.push('done'));
            """);

        Log(engine).Should().Be("aborted:false,signal:stop,abort:stop,done");
    }

    [Fact]
    public void TheControllerSignalDefaultsToAnAbortErrorWhenNoReasonIsGiven()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream({
              start(c) { c.signal.addEventListener('abort', () => log.push(c.signal.reason.name + ':' + (c.signal.reason instanceof DOMException))); }
            });
            stream.abort();
            """);

        Log(engine).Should().Be("AbortError:true");
    }

    [Fact]
    public void LocksTheStreamForOneWriterAtATime()
    {
        var engine = StreamEngine();
        engine.Execute("var stream = new WritableStream(); var writer = stream.getWriter();");

        engine.Evaluate("stream.locked").AsBoolean().Should().BeTrue();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("stream.getWriter()"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        engine.Execute("writer.releaseLock();");
        engine.Evaluate("stream.locked").AsBoolean().Should().BeFalse();
        engine.Evaluate("stream.getWriter() !== writer").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ALockedStreamRefusesAbortAndClose()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream();
            stream.getWriter();
            stream.abort().catch(e => log.push('abort:' + e.name));
            stream.close().catch(e => log.push('close:' + e.name));
            """);

        Log(engine).Should().Be("abort:TypeError,close:TypeError");
    }

    [Fact]
    public void AReleasedWriterRefusesEveryOperation()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var writer = new WritableStream().getWriter();
            writer.releaseLock();
            writer.write('a').catch(e => log.push('write:' + e.name));
            writer.close().catch(e => log.push('close:' + e.name));
            writer.abort().catch(e => log.push('abort:' + e.name));
            writer.ready.catch(e => log.push('ready:' + e.name));
            writer.closed.catch(e => log.push('closed:' + e.name));
            """);

        Log(engine).Should().Be("write:TypeError,close:TypeError,abort:TypeError,ready:TypeError,closed:TypeError");
    }

    [Fact]
    public void ReleasingTheLockLeavesOutstandingWritesAlone()
    {
        // "the lock can still be released even if some ongoing writes have not yet finished … the lock
        // instead simply prevents other producers from writing in an interleaved manner."
        var engine = StreamEngine();
        engine.Execute("""
            var release, writeReached;
            var reached = new Promise(r => { writeReached = r; });
            var stream = new WritableStream({ write() { writeReached(); return new Promise(r => { release = r; }); } });
            var writer = stream.getWriter();
            writer.write('a').then(() => log.push('write-settled'));
            reached.then(() => {
              writer.releaseLock();
              log.push('released:' + stream.locked);
              release();
            });
            """);

        Log(engine).Should().Be("released:false,write-settled");
    }

    [Fact]
    public void MembersCarryTheWebIdlShape()
    {
        var engine = StreamEngine();

        engine.Evaluate("WritableStream.length").AsNumber().Should().Be(0);
        engine.Evaluate("WritableStream.prototype.abort.length").AsNumber().Should().Be(0);
        engine.Evaluate("WritableStream.prototype.close.length").AsNumber().Should().Be(0);
        engine.Evaluate("WritableStream.prototype.getWriter.length").AsNumber().Should().Be(0);
        engine.Evaluate("Object.prototype.toString.call(new WritableStream())").AsString().Should().Be("[object WritableStream]");
        engine.Evaluate("Object.getOwnPropertyNames(new WritableStream()).length").AsNumber().Should().Be(0);

        engine.Execute("var writer = new WritableStream().getWriter();");
        engine.Evaluate("Object.prototype.toString.call(writer)").AsString().Should().Be("[object WritableStreamDefaultWriter]");
        engine.Evaluate("Object.getPrototypeOf(writer).constructor.name").AsString().Should().Be("WritableStreamDefaultWriter");
        engine.Evaluate("Object.getPrototypeOf(writer).constructor.length").AsNumber().Should().Be(1);
    }

    [Fact]
    public void TheWriterInterfaceObjectIsConstructibleWithAStream()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new WritableStream();
            var Ctor = Object.getPrototypeOf(stream.getWriter()).constructor;
            var fresh = new WritableStream();
            """);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Ctor(stream)"))
            .Error.Get("name").AsString().Should().Be("TypeError");
        engine.Evaluate("new Ctor(fresh) && fresh.locked").AsBoolean().Should().BeTrue();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Ctor({})"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void EveryMemberBrandChecksItsReceiver()
    {
        var engine = StreamEngine();

        engine.Execute("""
            WritableStream.prototype.abort.call({}).catch(e => log.push('abort:' + e.name));
            WritableStream.prototype.close.call({}).catch(e => log.push('close:' + e.name));
            var writerProto = Object.getPrototypeOf(new WritableStream().getWriter());
            writerProto.write.call({}, 'x').catch(e => log.push('write:' + e.name));
            Object.getOwnPropertyDescriptor(writerProto, 'ready').get.call({}).catch(e => log.push('ready:' + e.name));
            """);

        Log(engine).Should().Be("abort:TypeError,close:TypeError,write:TypeError,ready:TypeError");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("WritableStream.prototype.getWriter.call({})"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }
}
#endif
