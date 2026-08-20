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

    [Fact]
    public void PipesEveryChunkAndThenClosesTheDestination()
    {
        var engine = StreamEngine();
        engine.Execute("""
            source(['a', 'b']).pipeTo(sink('dest')).then(() => log.push('piped'));
            """);

        Log(engine).Should().Be("dest:a,dest:b,dest:close,piped");
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void PreventCancelLeavesTheSourceAloneWhenTheDestinationFails()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var to = new WritableStream({ write() { throw new Error('dest failed'); } });
            source(['a']).pipeTo(to, { preventCancel: true }).catch(e => log.push('piped:' + e.message));
            """);

        Log(engine).Should().Be("piped:dest failed");
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void AnAlreadyAbortedSignalStopsThePipeBeforeItReadsAnything()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var signal = AbortSignal.abort('nope');
            source(['a']).pipeTo(sink('dest'), { signal }).catch(e => log.push('piped:' + e));
            """);

        Log(engine).Should().Be("dest:abort:nope,source:cancel:nope,piped:nope");
    }

    [Fact]
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

    [Fact]
    public void RefusesANonAbortSignalOption()
    {
        var engine = StreamEngine();
        engine.Execute("""
            source(['a']).pipeTo(sink('dest'), { signal: 'nope' }).catch(e => log.push('piped:' + e.name));
            """);

        // A promise-returning operation reports an argument-conversion failure as a rejection.
        Log(engine).Should().Be("piped:TypeError");
    }

    [Fact]
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
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("source(['x']).pipeThrough({})"))
            .Error.Get("name").AsString().Should().Be("TypeError");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("source(['x']).pipeThrough({ readable: new ReadableStream(), writable: 5 })"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void PipeThroughRefusesALockedPair()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream();
            ts.writable.getWriter();
            """);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("source(['a']).pipeThrough(ts)"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        engine.Execute("var from = source(['a']); from.getReader();");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("from.pipeThrough(new TransformStream())"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
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
}
#endif
