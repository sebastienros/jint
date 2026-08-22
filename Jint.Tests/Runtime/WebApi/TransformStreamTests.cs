#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>TransformStream</c> and its default controller — https://streams.spec.whatwg.org/#ts.
/// </summary>
public class TransformStreamTests
{
    private static Engine StreamEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));
        engine.Execute("""
            var log = [];
            async function drain(stream, label) {
              const reader = stream.getReader();
              for (;;) {
                let result;
                try { result = await reader.read(); }
                catch (e) { log.push(label + ':error:' + e.message); return; }
                if (result.done) { log.push(label + ':done'); return; }
                log.push(label + ':' + result.value);
              }
            }
            """);
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    [Fact]
    public void ExposesAReadableAndAWritableSide()
    {
        var engine = StreamEngine();
        engine.Execute("var ts = new TransformStream();");

        engine.Evaluate("ts.readable instanceof ReadableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("ts.writable instanceof WritableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("ts.readable === ts.readable && ts.writable === ts.writable").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(ts)").AsString().Should().Be("[object TransformStream]");
        engine.Evaluate("Object.getOwnPropertyNames(ts).length").AsNumber().Should().Be(0);
        engine.Evaluate("TransformStream.length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void WithNoTransformerItIsAnIdentityTransform()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream();
            drain(ts.readable, 'out');
            var writer = ts.writable.getWriter();
            writer.write('a');
            writer.write('b');
            writer.close();
            """);

        Log(engine).Should().Be("out:a,out:b,out:done");
    }

    [Fact]
    public void RunsTheTransformerForEveryChunk()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({
              transform(chunk, controller) { log.push('transform:' + chunk); controller.enqueue(chunk.toUpperCase()); }
            });
            drain(ts.readable, 'out');
            var writer = ts.writable.getWriter();
            writer.write('a');
            writer.write('b');
            writer.close();
            """);

        Log(engine).Should().Be("transform:a,out:A,transform:b,out:B,out:done");
    }

    [Fact]
    public void ATransformerMayProduceZeroOrManyChunksPerInput()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({
              transform(chunk, c) {
                if (chunk === 'skip') { return; }
                c.enqueue(chunk + '1');
                c.enqueue(chunk + '2');
              }
            });
            drain(ts.readable, 'out');
            var writer = ts.writable.getWriter();
            writer.write('skip');
            writer.write('x');
            writer.close();
            """);

        Log(engine).Should().Be("out:x1,out:x2,out:done");
    }

    [Fact]
    public void CallsStartWithTheControllerAndRethrowsItsException()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({ start(c) { log.push('start:' + (typeof c.enqueue)); c.enqueue('prefix'); } });
            drain(ts.readable, 'out');
            ts.writable.getWriter().close();
            """);

        Log(engine).Should().Be("start:function,out:prefix,out:done");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TransformStream({ start() { throw new Error('boom'); } })"))
            .Error.Get("message").AsString().Should().Be("boom");
    }

    [Fact]
    public void NeitherSideRunsBeforeStartHasSettled()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var release;
            var ts = new TransformStream({
              start() { return new Promise(r => { release = r; }); },
              transform(chunk, c) { log.push('transform:' + chunk); c.enqueue(chunk); }
            });
            drain(ts.readable, 'out');
            ts.writable.getWriter().write('a');
            log.push('written');
            """);

        Log(engine).Should().Be("written");

        engine.Execute("release();");
        Log(engine).Should().Be("written,transform:a,out:a");
    }

    [Fact]
    public void CallsFlushWhenTheWritableSideIsClosed()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({
              transform(chunk, c) { c.enqueue(chunk); },
              flush(c) { log.push('flush'); c.enqueue('tail'); }
            });
            drain(ts.readable, 'out');
            var writer = ts.writable.getWriter();
            writer.write('a');
            writer.close().then(() => log.push('closed'));
            """);

        Log(engine).Should().Be("out:a,flush,out:tail,out:done,closed");
    }

    [Fact]
    public void AFailingTransformErrorsBothSides()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({ transform() { throw new Error('transform failed'); } });
            drain(ts.readable, 'out');
            var writer = ts.writable.getWriter();
            writer.write('a').catch(e => log.push('write:' + e.message));
            writer.closed.catch(e => log.push('writable:' + e.message));
            """);

        Log(engine).Should().Be("out:error:transform failed,write:transform failed,writable:transform failed");
    }

    [Fact]
    public void AFailingFlushErrorsTheReadableSideAndTheClose()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({ flush() { throw new Error('flush failed'); } });
            drain(ts.readable, 'out');
            ts.writable.getWriter().close().catch(e => log.push('close:' + e.message));
            """);

        Log(engine).Should().Be("out:error:flush failed,close:flush failed");
    }

    [Fact]
    public void ControllerErrorErrorsBothSides()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var ts = new TransformStream({ start(c) { controller = c; } });
            drain(ts.readable, 'out');
            var writer = ts.writable.getWriter();
            writer.closed.catch(e => log.push('writable:' + e.message));
            controller.error(new Error('stopped'));
            """);

        Log(engine).Should().Be("out:error:stopped,writable:stopped");
    }

    [Fact]
    public void TerminateClosesTheReadableSideAndErrorsTheWritableOne()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({
              transform(chunk, c) { c.enqueue(chunk); if (chunk === 'stop') { c.terminate(); } }
            });
            drain(ts.readable, 'out');
            var writer = ts.writable.getWriter();
            writer.write('a');
            writer.write('stop');
            writer.write('after').catch(e => log.push('after:' + e.name));
            writer.closed.catch(e => log.push('writable:' + e.name));
            """);

        Log(engine).Should().Be("out:a,out:stop,out:done,after:TypeError,writable:TypeError");
    }

    [Fact]
    public void EnqueueAfterTheReadableSideIsGoneThrows()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var ts = new TransformStream({ start(c) { controller = c; } });
            ts.readable.cancel('done').then(() => {
              try { controller.enqueue('x'); } catch (e) { log.push('enqueue:' + e.name); }
            });
            """);

        Log(engine).Should().Be("enqueue:TypeError");
    }

    [Fact]
    public void CancellingTheReadableSideRunsTheTransformersCancel()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({ cancel(reason) { log.push('cancel:' + reason); } });
            var writer = ts.writable.getWriter();
            writer.closed.catch(e => log.push('writable:' + e));
            ts.readable.cancel('no more').then(() => log.push('cancelled'));
            """);

        // The cancel settles before the writable's closed promise rejects, not after: the writable is only
        // *erroring* when the reaction on the transformer's cancel() runs, and it finishes erroring when the
        // start promise's own reaction lands — two microtasks later, because "a promise resolved with" a
        // promise adopts it. That is what makes the cancel fulfil rather than reject.
        Log(engine).Should().Be("cancel:no more,cancelled,writable:no more");
    }

    [Fact]
    public void AbortingTheWritableSideRunsTheTransformersCancel()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({ cancel(reason) { log.push('cancel:' + reason); } });
            ts.readable.getReader().closed.catch(e => log.push('readable:' + e));
            ts.writable.abort('stop').then(() => log.push('aborted'));
            """);

        // The readable side is errored with the very reason the abort carried.
        Log(engine).Should().Be("cancel:stop,readable:stop,aborted");
    }

    [Fact]
    public void TheTransformersCancelRunsAtMostOnce()
    {
        // [[finishPromise]] is what makes cancel() and flush() mutually exclusive, and each of them run only
        // once however the two sides are shut down.
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({
              cancel(reason) { log.push('cancel:' + reason); },
              flush() { log.push('flush'); }
            });
            ts.readable.cancel('one').then(() => log.push('readable:ok'), e => log.push('readable:' + e));
            ts.writable.abort('two').then(() => log.push('writable:ok'), e => log.push('writable:' + e));
            """);

        // cancel() runs once, for the first shutdown, and flush() never — the second shutdown attaches to
        // the finish promise the first one created. Both callers see that promise fulfil: the writable side
        // is still "erroring" when TransformStreamDefaultSourceCancelAlgorithm's reaction looks at it, which
        // is the branch that resolves, and the abort request's own promise resolves with undefined because
        // the abort steps hand back the very finish promise the cancel had already created.
        Log(engine).Should().Be("cancel:one,readable:ok,writable:ok");
    }

    [Fact]
    public void AFailingCancelIsReportedToTheCaller()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({ cancel() { throw new Error('cancel failed'); } });
            drain(ts.readable, 'out');
            ts.writable.abort('stop').catch(e => log.push('abort:' + e.message));
            """);

        Log(engine).Should().Be("out:error:cancel failed,abort:cancel failed");
    }

    [Fact]
    public void BackpressureFromTheReadableSideThrottlesTheTransformer()
    {
        // The readable side's default high water mark is 0, so a transform runs only when a consumer asks
        // for a chunk. That is what carries backpressure across the stream.
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream({ transform(chunk, c) { log.push('transform:' + chunk); c.enqueue(chunk); } });
            var writer = ts.writable.getWriter();
            writer.write('a');
            writer.write('b');
            writer.write('c');
            """);

        // Nothing is transformed at all: the readable side's default high water mark is 0, so until a
        // consumer asks for a chunk the transformer is never invited to produce one.
        Log(engine).Should().Be("");

        // Each read releases the latch for exactly one more transform.
        engine.Execute("var reader = ts.readable.getReader(); reader.read().then(r => log.push('read:' + r.value));");
        Log(engine).Should().Be("transform:a,read:a");

        engine.Execute("reader.read().then(r => log.push('read:' + r.value));");
        Log(engine).Should().Be("transform:a,read:a,transform:b,read:b");
    }

    [Fact]
    public void HonoursExplicitHighWaterMarksOnBothSides()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream(
              { transform(chunk, c) { log.push('transform:' + chunk); c.enqueue(chunk); } },
              { highWaterMark: 5 },
              { highWaterMark: 2 });
            var writer = ts.writable.getWriter();
            log.push('desired:' + writer.desiredSize);
            writer.write('a'); writer.write('b'); writer.write('c');
            """);

        // The readable side buffers two chunks before it stops asking for more.
        Log(engine).Should().Be("desired:5,transform:a,transform:b");
    }

    [Fact]
    public void RefusesAReadableOrWritableType()
    {
        var engine = StreamEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TransformStream({ readableType: 'bytes' })"))
            .Error.Get("name").AsString().Should().Be("RangeError");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TransformStream({ writableType: 'bytes' })"))
            .Error.Get("name").AsString().Should().Be("RangeError");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TransformStream(null)"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void TheControllerReportsTheReadableSidesDesiredSize()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var ts = new TransformStream({ start(c) { controller = c; } }, undefined, { highWaterMark: 3 });
            log.push(controller.desiredSize);
            controller.enqueue('a');
            log.push(controller.desiredSize);
            """);

        Log(engine).Should().Be("3,2");
    }

    [Fact]
    public void TheControllerBrandChecksItsReceiverAndIsNotConstructible()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            new TransformStream({ start(c) { controller = c; } });
            var Ctor = Object.getPrototypeOf(controller).constructor;
            """);

        engine.Evaluate("Ctor.name").AsString().Should().Be("TransformStreamDefaultController");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Ctor()"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        foreach (var member in new[] { "enqueue.call({}, 'x')", "error.call({})", "terminate.call({})" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"Ctor.prototype.{member}"))
                .Error.Get("name").AsString().Should().Be("TypeError", member);
        }

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Object.getOwnPropertyDescriptor(Ctor.prototype, 'desiredSize').get.call({})"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void PipeThroughChainsTwoTransformStreams()
    {
        var engine = StreamEngine();
        engine.Execute("""
            function mapper(fn) {
              return new TransformStream({ transform(chunk, c) { c.enqueue(fn(chunk)); } });
            }
            var source = new ReadableStream({ start(c) { c.enqueue('a'); c.enqueue('b'); c.close(); } });
            var result = source.pipeThrough(mapper(x => x.toUpperCase())).pipeThrough(mapper(x => x + '!'));
            drain(result, 'out');
            """);

        Log(engine).Should().Be("out:A!,out:B!,out:done");
    }

    // ---------------------------------------------------------------------------------------------
    // readable.cancel() must fulfil while the writable side is still *erroring*.
    //
    // TransformStreamDefaultSourceCancelAlgorithm (https://streams.spec.whatwg.org/#transform-stream-
    // default-source-cancel) rejects its finish promise only "if writable.[[state]] is 'errored'", and
    // the three shapes below all fail the writable in the same turn as the cancel — early enough that
    // the writable controller has not started yet, so WritableStreamStartErroring leaves it "erroring"
    // and WritableStreamFinishErroring waits for the start promise's reaction. That reaction has to
    // land *after* the cancel's, which is only true when "a promise resolved with" builds a new promise
    // rather than handing back the transform's own start promise; see StreamPromises.ResolvedWith.

    [Fact]
    public void CancellingTheReadableSideFulfilsWhenTheControllerErrorsInTheSameTurn()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var ts = new TransformStream({ start(c) { controller = c; } });
            var cancelPromise = ts.readable.cancel(new Error('ignored'));
            controller.error(new Error('thrown'));
            cancelPromise.then(() => log.push('cancel:fulfilled'), e => log.push('cancel:rejected:' + e.message));
            ts.writable.getWriter().closed.then(
              () => log.push('closed:fulfilled'),
              e => log.push('closed:rejected:' + e.message));
            """);

        Log(engine).Should().Be("cancel:fulfilled,closed:rejected:thrown");
    }

    [Fact]
    public void CancellingTheReadableSideFulfilsWhenTheControllerTerminatesInTheSameTurn()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var ts = new TransformStream({ start(c) { controller = c; } });
            var cancelPromise = ts.readable.cancel({ name: 'cancelReason' });
            controller.terminate();
            cancelPromise.then(() => log.push('cancel:fulfilled'), e => log.push('cancel:rejected'));
            ts.writable.getWriter().closed.then(
              () => log.push('closed:fulfilled'),
              e => log.push('closed:rejected:' + e.name));
            """);

        Log(engine).Should().Be("cancel:fulfilled,closed:rejected:TypeError");
    }

    [Fact]
    public void AnAbortBeforeTheCancelSetsTheCloseReasonAndBothPromisesFulfil()
    {
        // The abort is queued while the transformer's start() microtask is still outstanding, so the
        // TransformStream has not been told about it yet when the cancel arrives.
        var engine = StreamEngine();
        engine.Execute("""
            var ts = new TransformStream();
            var writer = ts.writable.getWriter();
            var abortPromise = writer.abort(new Error('thrown'));
            var cancelPromise = ts.readable.cancel(new Error('cancel reason'));
            abortPromise.then(() => log.push('abort:fulfilled'), e => log.push('abort:rejected:' + e.message));
            cancelPromise.then(() => log.push('cancel:fulfilled'), e => log.push('cancel:rejected:' + e.message));
            writer.closed.then(() => log.push('closed:fulfilled'), e => log.push('closed:rejected:' + e.message));
            """);

        Log(engine).Should().Be("cancel:fulfilled,abort:fulfilled,closed:rejected:thrown");
    }

    [Fact]
    public void TheTransformsStartPromiseIsAdoptedRatherThanReusedByBothControllers()
    {
        // A transformer whose start() returns a promise gates both sides on it — and the writable side's
        // first write still waits for it, which is what the adoption above must not disturb.
        var engine = StreamEngine();
        engine.Execute("""
            var release;
            var ts = new TransformStream({
              start(c) { log.push('start'); return new Promise(r => { release = r; }); },
              transform(chunk, c) { log.push('transform:' + chunk); c.enqueue(chunk); }
            });
            drain(ts.readable, 'out');
            var writer = ts.writable.getWriter();
            writer.write('a').then(() => log.push('written'));
            log.push('sync-end');
            """);

        Log(engine).Should().Be("start,sync-end");

        engine.Execute("release();");
        Log(engine).Should().Be("start,sync-end,transform:a,out:a,written");
    }
}
#endif
