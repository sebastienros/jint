#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>pipeTo()</c> and <c>pipeThrough()</c> — https://streams.spec.whatwg.org/#readable-stream-pipe-to.
/// </summary>
/// <remarks>
/// The <c>AbortSignal</c> half needs the events feature for the <c>AbortController</c> global, so these
/// engines opt into both. The piping itself observes the signal through the internal abort-algorithm seam,
/// not through an <c>abort</c> event listener a script could remove.
/// </remarks>
public class StreamPipeTests
{
    private static Engine StreamEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams | WebApiFeatures.Events));
        engine.Execute("""
            var log = [];
            function sink(name) {
              return new WritableStream({
                write(chunk) { log.push(name + ':' + chunk); },
                close() { log.push(name + ':close'); },
                abort(reason) { log.push(name + ':abort:' + reason); }
              });
            }
            function source(chunks) {
              return new ReadableStream({
                start(c) { for (const chunk of chunks) { c.enqueue(chunk); } c.close(); },
                cancel(reason) { log.push('source:cancel:' + reason); }
              });
            }
            """);
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    [Test]
    public void PipesEveryChunkAndThenClosesTheDestination()
    {
        var engine = StreamEngine();
        engine.Execute("""
            source(['a', 'b']).pipeTo(sink('dest')).then(() => log.push('piped'));
            """);

        Log(engine).Should().Be("dest:a,dest:b,dest:close,piped");
    }

    [Test]
    public void LocksBothStreamsForTheDurationAndReleasesThemAtTheEnd()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var from = source(['a']);
            var to = sink('dest');
            var promise = from.pipeTo(to);
            log.push('locked:' + from.locked + ':' + to.locked);
            promise.then(() => log.push('released:' + from.locked + ':' + to.locked));
            """);

        Log(engine).Should().Be("locked:true:true,dest:a,dest:close,released:false:false");
    }

    [Test]
    public void RefusesALockedSourceOrDestination()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var from = source(['a']);
            var to = sink('dest');
            from.getReader();
            from.pipeTo(to).catch(e => log.push('source:' + e.name));
            var other = source(['b']);
            to.getWriter();
            other.pipeTo(to).catch(e => log.push('dest:' + e.name));
            other.pipeTo({}).catch(e => log.push('type:' + e.name));
            """);

        Log(engine).Should().Be("source:TypeError,dest:TypeError,type:TypeError");
    }

    [Test]
    public void PreventCloseLeavesTheDestinationOpen()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var to = sink('dest');
            source(['a']).pipeTo(to, { preventClose: true }).then(() => log.push('piped:' + to.locked));
            """);

        Log(engine).Should().Be("dest:a,piped:false");

        // Still writable afterwards.
        engine.Execute("to.getWriter().write('later');");
        Log(engine).Should().Be("dest:a,piped:false,dest:later");
    }

    [Test]
    public void AnErrorInTheSourceAbortsTheDestination()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var from = new ReadableStream({ start(c) { controller = c; c.enqueue('a'); } });
            from.pipeTo(sink('dest')).catch(e => log.push('piped:' + e.message));
            """);

        Log(engine).Should().Be("dest:a");

        engine.Execute("controller.error(new Error('source failed'));");
        Log(engine).Should().Be("dest:a,dest:abort:Error: source failed,piped:source failed");
    }

    [Test]
    public void PreventAbortLeavesTheDestinationAloneWhenTheSourceFails()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var to = sink('dest');
            var from = new ReadableStream({ start(c) { controller = c; } });
            from.pipeTo(to, { preventAbort: true }).catch(e => log.push('piped:' + e.message));
            controller.error(new Error('source failed'));
            """);

        Log(engine).Should().Be("piped:source failed");
        engine.Evaluate("to.locked").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void AnErrorInTheDestinationCancelsTheSource()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var to = new WritableStream({ write() { throw new Error('dest failed'); } });
            // Deliberately a source that has not closed: cancelling a stream that is already closed is a
            // no-op that never reaches the underlying source.
            var from = new ReadableStream({
              start(c) { c.enqueue('a'); },
              cancel(reason) { log.push('source:cancel:' + reason); }
            });
            from.pipeTo(to).catch(e => log.push('piped:' + e.message));
            """);

        Log(engine).Should().Be("source:cancel:Error: dest failed,piped:dest failed");
    }

    [Test]
    public void PreventCancelLeavesTheSourceAloneWhenTheDestinationFails()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var to = new WritableStream({ write() { throw new Error('dest failed'); } });
            source(['a']).pipeTo(to, { preventCancel: true }).catch(e => log.push('piped:' + e.message));
            """);

        Log(engine).Should().Be("piped:dest failed");
    }

    [Test]
    public void PipingToAnAlreadyClosingDestinationCancelsTheSource()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var to = sink('dest');
            var writer = to.getWriter();
            writer.close();
            writer.releaseLock();
            source(['a']).pipeTo(to).catch(e => log.push('piped:' + e.name));
            """);

        // The source is cancelled with the "destination closed" TypeError, and the destination's own close
        // — which was already queued — still runs.
        Log(engine).Should().Be("source:cancel:TypeError: The destination writable stream closed before all data could be piped to it,dest:close,piped:TypeError");
    }

    [Test]
    public void EnforcesTheDestinationsBackpressure()
    {
        // "While WritableStreamDefaultWriterGetDesiredSize(writer) is ≤ 0 or is null, the user agent must
        // not read from reader."
        var engine = StreamEngine();
        engine.Execute("""
            var release;
            var to = new WritableStream({
              write(chunk) { log.push('write:' + chunk); return new Promise(r => { release = r; }); }
            }, { highWaterMark: 1 });
            var pulls = 0;
            var from = new ReadableStream({
              pull(c) { pulls++; c.enqueue('chunk' + pulls); }
            }, { highWaterMark: 0 });
            from.pipeTo(to);
            """);

        // One chunk is read, handed to the sink, and fills the destination's queue; the pipe then waits for
        // the destination to want more.
        engine.Execute("log.push('pulls:' + pulls);");
        Log(engine).Should().Be("write:chunk1,pulls:1");

        engine.Execute("release();");
        engine.Execute("log.push('pulls:' + pulls);");
        Log(engine).Should().Be("write:chunk1,pulls:1,write:chunk2,pulls:2");
    }

    [Test]
    public void DoesNotWaitForAWriteBeforeReadingAgain()
    {
        // "An implementation that waits for each write to successfully complete before proceeding to the
        // next read/write operation violates this recommendation."
        var engine = StreamEngine();
        engine.Execute("""
            var pulls = 0;
            var from = new ReadableStream({ pull(c) { pulls++; c.enqueue('chunk' + pulls); } }, { highWaterMark: 0 });
            var to = new WritableStream({
              write(chunk) { log.push('write:' + chunk); return new Promise(() => {}); }
            }, { highWaterMark: 3 });
            from.pipeTo(to);
            """);

        // The first write never settles, and the pipe reads ahead anyway — three chunks, which is exactly
        // what the destination's queue had room for. An implementation that waited for each write would
        // have read one.
        engine.Execute("log.push('pulls:' + pulls);");
        Log(engine).Should().Be("write:chunk1,pulls:3");
    }

    [Test]
    public void AnAbortSignalStopsThePipeAndShutsBothStreamsDown()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller = new AbortController();
            var to = new WritableStream({
              write(chunk) { log.push('write:' + chunk); },
              abort(reason) { log.push('abort:' + reason.name); }
            });
            var from = new ReadableStream({
              start(c) { c.enqueue('a'); c.enqueue('b'); },
              cancel(reason) { log.push('cancel:' + reason.name); }
            });
            from.pipeTo(to, { signal: controller.signal }).catch(e => log.push('piped:' + e.name));
            """);

        Log(engine).Should().Be("write:a,write:b");

        engine.Execute("controller.abort();");

        // The destination is aborted and the source cancelled, both with the signal's reason, and the pipe's
        // own promise rejects with it too.
        Log(engine).Should().Be("write:a,write:b,abort:AbortError,cancel:AbortError,piped:AbortError");
    }

    [Test]
    public void AnAlreadyAbortedSignalStopsThePipeBeforeItReadsAnything()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var signal = AbortSignal.abort('nope');
            source(['a']).pipeTo(sink('dest'), { signal }).catch(e => log.push('piped:' + e));
            """);

        Log(engine).Should().Be("dest:abort:nope,source:cancel:nope,piped:nope");
    }

    [Test]
    public void HonoursPreventAbortAndPreventCancelWhenTheSignalFires()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller = new AbortController();
            var to = sink('dest');
            var from = source(['a']);
            from.pipeTo(to, { signal: controller.signal, preventAbort: true, preventCancel: true })
                .catch(e => log.push('piped:' + e.name));
            controller.abort();
            """);

        // Neither side is told anything; only the pipe's own promise reports the abort. The chunk that was
        // already being read when the signal fired still lands — the specification allows a pipe step that
        // has begun to finish, and only forbids starting new ones.
        Log(engine).Should().Be("dest:a,piped:AbortError");
    }

    [Test]
    public void RefusesANonAbortSignalOption()
    {
        var engine = StreamEngine();
        engine.Execute("""
            source(['a']).pipeTo(sink('dest'), { signal: 'nope' }).catch(e => log.push('piped:' + e.name));
            """);

        // A promise-returning operation reports an argument-conversion failure as a rejection.
        Log(engine).Should().Be("piped:TypeError");
    }

    [Test]
    public void PipeThroughReturnsTheReadableSideAndThrowsRatherThanRejecting()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({ transform(chunk, c) { c.enqueue(chunk.toUpperCase()); } });
            var out = source(['a', 'b']).pipeThrough(ts);
            log.push('same:' + (out === ts.readable));
            out.getReader().read().then(r => log.push('read:' + r.value));
            """);

        Log(engine).Should().Be("same:true,read:A");

        // Unlike pipeTo, pipeThrough returns a stream rather than a promise, so its failures throw.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("source(['x']).pipeThrough({})"))!
            .Error.Get("name").AsString().Should().Be("TypeError");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("source(['x']).pipeThrough({ readable: new ReadableStream(), writable: 5 })"))!
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Test]
    public void PipeThroughRefusesALockedPair()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream();
            ts.writable.getWriter();
            """);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("source(['a']).pipeThrough(ts)"))!
            .Error.Get("name").AsString().Should().Be("TypeError");

        engine.Execute("var from = source(['a']); from.getReader();");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("from.pipeThrough(new TransformStream())"))!
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Test]
    public void PipeToWaitsForOutstandingWritesBeforeShuttingDown()
    {
        // "Wait until every chunk that has been read has been written (i.e. the corresponding promises have
        // settled)" before performing the shutdown action.
        var engine = StreamEngine();
        engine.Execute("""
            var release;
            var controller;
            var to = new WritableStream({
              write(chunk) { log.push('write:' + chunk); return new Promise(r => { release = r; }); },
              abort(reason) { log.push('abort:' + reason.message); }
            }, { highWaterMark: 5 });
            var from = new ReadableStream({ start(c) { controller = c; c.enqueue('a'); } });
            from.pipeTo(to).catch(e => log.push('piped:' + e.message));
            """);

        Log(engine).Should().Be("write:a");

        engine.Execute("controller.error(new Error('boom'));");

        // The shutdown is pending on the write that is still in flight.
        Log(engine).Should().Be("write:a");

        engine.Execute("release();");
        Log(engine).Should().Be("write:a,abort:boom,piped:boom");
    }

    [Test]
    public void EnqueueingIntoAPipedSourceDoesNotReachTheSinkSynchronously()
    {
        // A read request's chunk steps run inside ReadableStreamFulfillReadRequest, i.e. on the stack of
        // the producer's own enqueue(). Starting the write there would run the destination's write
        // algorithm from that stack — and what licenses the pipe to schedule reads and writes however it
        // likes is precisely that "the exact manner in which this happens is not observable to author
        // code" (https://streams.spec.whatwg.org/#readable-stream-pipe-to).
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            new ReadableStream({ start(c) { controller = c; } }, { highWaterMark: 0 })
              .pipeTo(new WritableStream({ write(chunk) { log.push('write:' + chunk); } }));
            var synchronous;
            Promise.resolve().then(() => {
              controller.enqueue('a');
              synchronous = log.length > 0;
            });
            """);

        engine.Evaluate("synchronous").AsBoolean().Should().BeFalse();

        // Deferred, not dropped: the very next turn writes it.
        Log(engine).Should().Be("write:a");
    }

    [Test]
    public void TheDeferredWriteStillCountsAgainstBackpressureBeforeTheNextRead()
    {
        // The deferral moves the whole of the chunk steps, so the write is charged to the destination's
        // queue before the loop is let round again — otherwise the next step would consult the writer's
        // ready promise while the chunk it just read was still unaccounted for, and "backpressure must be
        // enforced" would be a lie. One chunk of head room means exactly one chunk in flight.
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var release = [];
            var to = new WritableStream({
              write(chunk) { log.push('write:' + chunk); return new Promise(r => release.push(r)); }
            }, { highWaterMark: 1 });
            new ReadableStream({ start(c) { controller = c; } }).pipeTo(to);
            """);

        engine.Execute("controller.enqueue('a'); controller.enqueue('b'); controller.enqueue('c');");

        // 'a' is in flight and 'b' has been taken into the queue; 'c' waits on the source.
        Log(engine).Should().Be("write:a");

        engine.Execute("release.shift()();");
        Log(engine).Should().Be("write:a,write:b");

        engine.Execute("release.shift()();");
        Log(engine).Should().Be("write:a,write:b,write:c");
    }

    [Test]
    public void ADeferredWriteIsStillWaitedForByAShutdownThatStartsInTheSameTurn()
    {
        // "Shutdown must stop activity … and must only perform writes of already-read chunks": a chunk
        // whose write has been deferred by the microtask above is already-read, so the shutdown action has
        // to wait for it. WaitForWritesToFinish notices the write that started while it was waiting.
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var release;
            var to = new WritableStream({
              write(chunk) { log.push('write:' + chunk); return new Promise(r => { release = r; }); },
              abort(reason) { log.push('abort:' + reason.message); }
            }, { highWaterMark: 5 });
            new ReadableStream({ start(c) { controller = c; } }).pipeTo(to).catch(e => log.push('piped:' + e.message));
            """);

        engine.Execute("controller.enqueue('a'); controller.error(new Error('boom'));");

        // The write started (one microtask later) and the abort is parked behind it.
        Log(engine).Should().Be("write:a");

        engine.Execute("release();");
        Log(engine).Should().Be("write:a,abort:boom,piped:boom");
    }

    [Test]
    public void AChunkThatArrivesAsThePipeFinalizesIsDroppedRatherThanWrittenThroughAReleasedWriter()
    {
        // The deferral opens a window the synchronous chunk steps did not have: the pipe can finalize —
        // releasing both locks — between the chunk arriving and the microtask that writes it. It takes a
        // shutdown that reaches Finalize *synchronously*, which is the no-action shutdown a preventCancel
        // pipe takes when the destination is already errored. Without the guard this dereferences a writer
        // with no stream; with it the chunk is dropped, which is right, because the destination it was read
        // for is gone.
        var engine = StreamEngine();
        engine.Execute("""
            var rsController, wsController;
            var rs = new ReadableStream({ start(c) { rsController = c; } });
            var ws = new WritableStream({
              start(c) { wsController = c; },
              write(chunk) { log.push('write:' + chunk); }
            });
            rs.pipeTo(ws, { preventCancel: true }).catch(e => log.push('piped:' + e.message));
            """);

        // Both in one turn: the destination's error queues the pipe's shutdown, and the enqueue then hands
        // the outstanding read request a chunk whose write is now a microtask behind that shutdown.
        engine.Execute("""
            wsController.error(new Error('boom'));
            rsController.enqueue('a');
            """);

        Log(engine).Should().Be("piped:boom");
        engine.Evaluate("rs.locked").AsBoolean().Should().BeFalse();
        engine.Evaluate("ws.locked").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void PipingKeepsWorkingThroughAChainOfTransforms()
    {
        // pipeThrough is pipeTo, so the deferral applies once per hop; the chain must still deliver every
        // chunk in order and close.
        var engine = StreamEngine();
        engine.Execute("""
            function mapper(fn) {
              return new TransformStream({ transform(chunk, c) { c.enqueue(fn(chunk)); } });
            }
            source(['a', 'b', 'c'])
              .pipeThrough(mapper(x => x.toUpperCase()))
              .pipeThrough(mapper(x => x + '!'))
              .pipeTo(sink('dest'))
              .then(() => log.push('piped'));
            """);

        Log(engine).Should().Be("dest:A!,dest:B!,dest:C!,dest:close,piped");
    }
}
#endif
