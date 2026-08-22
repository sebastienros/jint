#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>ReadableStream</c>, its default controller and its default reader, against the Streams Standard —
/// https://streams.spec.whatwg.org/#rs.
/// </summary>
/// <remarks>
/// <c>Engine.Execute</c> drains the event loop once the script has finished, so a test can write the whole
/// asynchronous story into a <c>log</c> array and then read it. Nothing here sleeps or starts a thread:
/// every promise these streams hand out is an ordinary engine promise settled by a job on that same queue.
/// </remarks>
public class ReadableStreamTests
{
    private static Engine StreamEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));
        engine.Execute("var log = [];");
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    private static JsValue Eval(string source) => StreamEngine().Evaluate(source);

    [Fact]
    public void ConstructsWithNoArgumentsAtAll()
    {
        Eval("new ReadableStream() instanceof ReadableStream").AsBoolean().Should().BeTrue();
        Eval("new ReadableStream(undefined, undefined).locked").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void RefusesANonObjectUnderlyingSource()
    {
        // `optional object underlyingSource` is not nullable, so an explicit null is a TypeError while an
        // omitted argument is simply missing — https://webidl.spec.whatwg.org/#es-object.
        var engine = StreamEngine();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new ReadableStream(null)"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new ReadableStream(5)"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void ConvertsTheStrategyBeforeTheUnderlyingSource()
    {
        // The strategy is a WebIDL dictionary and is converted at the IDL layer; the underlying source is
        // converted in the constructor's own prose. So the strategy's exception wins.
        var engine = StreamEngine();
        engine.Execute("var e1 = new Error('source'); var e2 = new Error('strategy');");

        var error = Assert.Throws<JavaScriptException>(() => engine.Evaluate(
            "new ReadableStream({ get start() { throw e1; } }, { get size() { throw e2; } })"));

        error.Error.Get("message").AsString().Should().Be("strategy");
    }

    [Fact]
    public void RefusesAnInvalidStreamType()
    {
        // "bytes" is the only value of the ReadableStreamType enumeration, and it builds a readable byte
        // stream (see ReadableByteStreamTests); anything else is the TypeError the enumeration conversion
        // raises — https://webidl.spec.whatwg.org/#es-enumeration.
        var engine = StreamEngine();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new ReadableStream({ type: 'nonsense' })"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        engine.Evaluate("new ReadableStream({ type: 'bytes' }) instanceof ReadableStream").AsBoolean().Should().BeTrue();

        // And a BYOB reader cannot be acquired from a stream that is not a byte stream.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new ReadableStream().getReader({ mode: 'byob' })"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void CallsStartSynchronouslyWithTheController()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var seen;
            var stream = new ReadableStream({ start(c) { seen = c; log.push('start'); } });
            log.push('after');
            """);

        Log(engine).Should().Be("start,after");
        engine.Evaluate("Object.getPrototypeOf(seen).constructor.name").AsString().Should().Be("ReadableStreamDefaultController");
    }

    [Fact]
    public void RethrowsAnExceptionFromStart()
    {
        // start()'s return type is `any`, so its exception is not converted into a rejection —
        // "Any thrown exceptions will be re-thrown by the ReadableStream() constructor."
        var engine = StreamEngine();
        var error = Assert.Throws<JavaScriptException>(() => engine.Evaluate(
            "new ReadableStream({ start() { throw new Error('boom'); } })"));

        error.Error.Get("message").AsString().Should().Be("boom");
    }

    [Fact]
    public void ErrorsTheStreamWhenStartRejects()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ start() { return Promise.reject(new Error('nope')); } });
            stream.getReader().read().then(() => log.push('resolved'), e => log.push('rejected:' + e.message));
            """);

        Log(engine).Should().Be("rejected:nope");
    }

    [Fact]
    public void ReadsTheChunksAnUnderlyingSourceEnqueued()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
              start(c) { c.enqueue('a'); c.enqueue('b'); c.close(); }
            });
            var reader = stream.getReader();
            (async () => {
              for (;;) {
                const { value, done } = await reader.read();
                if (done) { log.push('done'); return; }
                log.push(value);
              }
            })();
            """);

        Log(engine).Should().Be("a,b,done");
    }

    [Fact]
    public void ReadResultIsAPlainObjectWithValueAndDone()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var r = new ReadableStream({ start(c) { c.enqueue('x'); } }).getReader();
            var result;
            r.read().then(v => { result = v; });
            """);

        engine.Evaluate("Object.keys(result).join(',')").AsString().Should().Be("value,done");
        engine.Evaluate("result.value").AsString().Should().Be("x");
        engine.Evaluate("result.done").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.getPrototypeOf(result) === Object.prototype").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ClosingLeavesAlreadyEnqueuedChunksReadable()
    {
        // "Consumers will still be able to read any previously-enqueued chunks from the stream, but once
        // those are read, the stream will become closed."
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ start(c) { c.enqueue('a'); c.close(); } });
            log.push(stream.locked);
            var reader = stream.getReader();
            reader.read().then(r => log.push('1:' + r.value + ':' + r.done));
            reader.read().then(r => log.push('2:' + r.value + ':' + r.done));
            reader.closed.then(() => log.push('closed'));
            """);

        Log(engine).Should().Be("false,1:a:false,2:undefined:true,closed");
    }

    [Fact]
    public void EnqueueAndCloseThrowOnceCloseHasBeenRequested()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            new ReadableStream({ start(c) { controller = c; c.close(); } });
            try { controller.close(); } catch (e) { log.push('close:' + e.name); }
            try { controller.enqueue('x'); } catch (e) { log.push('enqueue:' + e.name); }
            """);

        Log(engine).Should().Be("close:TypeError,enqueue:TypeError");
    }

    [Fact]
    public void ErroringMakesEveryLaterInteractionFail()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ start(c) { c.error(new Error('bad')); } });
            var reader = stream.getReader();
            reader.read().then(() => log.push('resolved'), e => log.push('read:' + e.message));
            reader.closed.then(() => log.push('closed'), e => log.push('closed:' + e.message));
            """);

        Log(engine).Should().Be("read:bad,closed:bad");
    }

    [Fact]
    public void ErrorOnAnAlreadyErroredStreamIsANoOp()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new ReadableStream({ start(c) { controller = c; } });
            controller.error(new Error('first'));
            controller.error(new Error('second'));
            stream.getReader().closed.catch(e => log.push(e.message));
            """);

        Log(engine).Should().Be("first");
    }

    [Fact]
    public void DesiredSizeTracksTheQueueAgainstTheHighWaterMark()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new ReadableStream({ start(c) { controller = c; } }, { highWaterMark: 3 });
            log.push(controller.desiredSize);
            controller.enqueue('a');
            log.push(controller.desiredSize);
            controller.enqueue('b'); controller.enqueue('c'); controller.enqueue('d');
            log.push(controller.desiredSize);
            """);

        // The queue may overshoot the high water mark; the desired size simply goes negative.
        Log(engine).Should().Be("3,2,-1");
    }

    [Fact]
    public void DesiredSizeIsZeroWhenClosedAndNullWhenErrored()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var a, b;
            new ReadableStream({ start(c) { a = c; c.close(); } });
            new ReadableStream({ start(c) { b = c; c.error(new Error('x')); } });
            log.push(a.desiredSize);
            log.push(b.desiredSize);
            """);

        Log(engine).Should().Be("0,");
        engine.Evaluate("b.desiredSize").IsNull().Should().BeTrue();
    }

    [Fact]
    public void CallsPullOnceStartHasSettledAndTheQueueWantsMore()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
              start() { log.push('start'); },
              pull(c) { log.push('pull:' + c.desiredSize); c.enqueue('x'); }
            }, { highWaterMark: 2 });
            log.push('constructed');
            """);

        // pull() is not called until start() has completed — which is a microtask away even for a
        // synchronous start — and is then called repeatedly until the high water mark is reached.
        Log(engine).Should().Be("start,constructed,pull:2,pull:1");
    }

    [Fact]
    public void DoesNotCallPullRepeatedlyForANoOpPull()
    {
        // "it will only be called repeatedly if it enqueues at least one chunk … a no-op pull()
        // implementation will not be continually called."
        var engine = StreamEngine();
        engine.Execute("""
            new ReadableStream({ pull() { log.push('pull'); } }, { highWaterMark: 5 });
            """);

        Log(engine).Should().Be("pull");
    }

    [Fact]
    public void DoesNotCallPullAgainUntilItsPromiseFulfils()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var release;
            var stream = new ReadableStream({
              pull(c) {
                log.push('pull');
                c.enqueue('x');
                return new Promise(r => { release = r; });
              }
            }, { highWaterMark: 3 });

            // pull() has not run yet: it waits for start() to settle, which takes a microtask even when
            // start is absent. So the release has to be scheduled behind that.
            log.push('sync:' + (release === undefined));
            Promise.resolve().then(() => { log.push('releasing'); release(); });
            """);

        // One pull, then nothing at all until the promise it returned settles. The second pull replaces
        // `release` with its own resolver and nobody calls it, so there is no third — the high water mark of
        // 3 still wants a chunk, and the stream simply waits.
        Log(engine).Should().Be("sync:true,pull,releasing,pull");
    }

    [Fact]
    public void ErrorsTheStreamWhenPullRejects()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ pull() { return Promise.reject(new Error('pull failed')); } });
            stream.getReader().closed.catch(e => log.push(e.message));
            """);

        Log(engine).Should().Be("pull failed");
    }

    [Fact]
    public void APendingReadOutranksTheHighWaterMark()
    {
        // "If IsReadableStreamLocked(stream) is true and ReadableStreamGetNumReadRequests(stream) > 0,
        // return true" — a waiting consumer pulls even from a stream whose strategy would not.
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ pull(c) { log.push('pull'); c.enqueue('x'); } }, { highWaterMark: 0 });
            log.push('constructed');
            stream.getReader().read().then(r => log.push('read:' + r.value));
            """);

        Log(engine).Should().Be("constructed,pull,read:x");
    }

    [Fact]
    public void HandsAChunkStraightToAWaitingReaderWithoutMeasuringIt()
    {
        // A chunk delivered to a pending read never enters the queue, so the strategy's size() is not called
        // for it.
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new ReadableStream(
              { start(c) { controller = c; } },
              { highWaterMark: 10, size(chunk) { log.push('size:' + chunk); return 1; } });
            var reader = stream.getReader();
            reader.read().then(r => log.push('read:' + r.value));
            controller.enqueue('a');
            controller.enqueue('b');
            """);

        Log(engine).Should().Be("size:b,read:a");
    }

    [Fact]
    public void CallsTheStrategySizeOncePerQueuedChunk()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            new ReadableStream(
              { start(c) { controller = c; } },
              { highWaterMark: 10, size(chunk) { log.push('size:' + chunk); return chunk.length; } });
            controller.enqueue('a');
            controller.enqueue('bb');
            log.push(controller.desiredSize);
            """);

        Log(engine).Should().Be("size:a,size:bb,7");
    }

    [Fact]
    public void ErrorsTheStreamWhenTheStrategySizeThrows()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new ReadableStream(
              { start(c) { controller = c; } },
              { size() { throw new Error('size failed'); } });
            try { controller.enqueue('a'); } catch (e) { log.push('threw:' + e.message); }
            stream.getReader().closed.catch(e => log.push('closed:' + e.message));
            """);

        Log(engine).Should().Be("threw:size failed,closed:size failed");
    }

    [Fact]
    public void RejectsANegativeOrNaNHighWaterMark()
    {
        var engine = StreamEngine();

        foreach (var value in new[] { "-1", "NaN", "'foo'" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"new ReadableStream({{}}, {{ highWaterMark: {value} }})"))
                .Error.Get("name").AsString().Should().Be("RangeError");
        }

        // +∞ is explicitly allowed: it makes backpressure never apply.
        engine.Evaluate("new ReadableStream({}, { highWaterMark: Infinity }) instanceof ReadableStream").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void LocksTheStreamForOneReaderAtATime()
    {
        var engine = StreamEngine();
        engine.Execute("var stream = new ReadableStream(); var reader = stream.getReader();");

        engine.Evaluate("stream.locked").AsBoolean().Should().BeTrue();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("stream.getReader()"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        engine.Execute("reader.releaseLock();");
        engine.Evaluate("stream.locked").AsBoolean().Should().BeFalse();
        engine.Evaluate("stream.getReader() !== reader").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ReleasingTheLockRejectsOutstandingReadsAndKeepsTheChunks()
    {
        // "If the reader's lock is released while it still has pending read requests, then the promises
        // returned by the reader's read() method are immediately rejected with a TypeError. Any unread chunks
        // remain in the stream's internal queue and can be read later by acquiring a new reader."
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new ReadableStream({ start(c) { controller = c; } });
            var reader = stream.getReader();
            reader.read().then(() => log.push('resolved'), e => log.push('read:' + e.name));
            reader.closed.catch(e => log.push('closed:' + e.name));
            reader.releaseLock();
            controller.enqueue('kept');
            stream.getReader().read().then(r => log.push('again:' + r.value));
            """);

        // The closed promise is rejected before the read requests are, which is the order
        // ReadableStreamDefaultReaderRelease performs them in.
        Log(engine).Should().Be("closed:TypeError,read:TypeError,again:kept");
    }

    [Fact]
    public void AReleasedReaderRefusesEveryOperation()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var reader = new ReadableStream().getReader();
            reader.releaseLock();
            reader.read().catch(e => log.push('read:' + e.name));
            reader.cancel().catch(e => log.push('cancel:' + e.name));
            reader.closed.catch(e => log.push('closed:' + e.name));
            log.push('releaseLock:' + reader.releaseLock());
            """);

        Log(engine).Should().Be("releaseLock:undefined,read:TypeError,cancel:TypeError,closed:TypeError");
    }

    [Fact]
    public void CancelClosesTheStreamAndTellsTheUnderlyingSource()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
              start(c) { c.enqueue('a'); },
              cancel(reason) { log.push('cancel:' + reason); }
            });
            stream.cancel('because').then(v => log.push('fulfilled:' + v));
            stream.getReader().read().then(r => log.push('read:' + r.value + ':' + r.done));
            """);

        // The queue is discarded and the stream becomes closed, not errored. The cancel promise settles one
        // microtask behind the underlying source's own answer, because ReadableStreamCancel reacts to it to
        // discard its fulfillment value.
        Log(engine).Should().Be("cancel:because,read:undefined:true,fulfilled:undefined");
    }

    [Fact]
    public void CancelReportsAFailingUnderlyingSourceButStillCloses()
    {
        // "Even if the cancelation process fails, the stream will still close … The failure is only
        // communicated to the immediate caller."
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ cancel() { throw new Error('cancel failed'); } });
            stream.cancel().catch(e => log.push('cancel:' + e.message));
            stream.getReader().closed.then(() => log.push('closed'));
            """);

        Log(engine).Should().Be("closed,cancel:cancel failed");
    }

    [Fact]
    public void CancelOnALockedStreamRejectsWithoutCancelling()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ cancel() { log.push('should not run'); } });
            stream.getReader();
            stream.cancel().catch(e => log.push('cancel:' + e.name));
            """);

        Log(engine).Should().Be("cancel:TypeError");
    }

    [Fact]
    public void CancelIsAResolvedPromiseForAClosedStreamAndARejectionForAnErroredOne()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var closed = new ReadableStream({ start(c) { c.close(); } });
            var errored = new ReadableStream({ start(c) { c.error(new Error('e')); } });
            closed.cancel().then(() => log.push('closed:ok'));
            errored.cancel().catch(e => log.push('errored:' + e.message));
            """);

        Log(engine).Should().Be("closed:ok,errored:e");
    }

    [Fact]
    public void ReadFromInsidePullSeesTheChunkEnqueuedThere()
    {
        // Reentrancy: enqueueing from inside pull() while a read is outstanding is the ordinary case, and
        // the [[pulling]] flag keeps pull() from being re-entered while its promise is outstanding.
        var engine = StreamEngine();
        engine.Execute("""
            var count = 0;
            var stream = new ReadableStream({
              pull(c) {
                count++;
                log.push('pull' + count);
                c.enqueue(count);
                if (count === 3) { c.close(); }
              }
            }, { highWaterMark: 0 });
            var reader = stream.getReader();
            (async () => {
              for (;;) {
                const { value, done } = await reader.read();
                if (done) { log.push('done'); return; }
                log.push('got' + value);
              }
            })();
            """);

        Log(engine).Should().Be("pull1,got1,pull2,got2,pull3,got3,done");
    }

    [Fact]
    public void EveryInterfaceObjectIsANonEnumerableGlobal()
    {
        var engine = StreamEngine();

        // All thirteen: the five a script constructs by name, and the eight it only ever names for an
        // instanceof, a prototype patch or a feature detect.
        foreach (var name in new[]
                 {
                     "ReadableStream", "WritableStream", "TransformStream", "ByteLengthQueuingStrategy",
                     "CountQueuingStrategy", "ReadableStreamDefaultReader", "ReadableStreamDefaultController",
                     "WritableStreamDefaultWriter", "WritableStreamDefaultController",
                     "TransformStreamDefaultController", "ReadableByteStreamController", "ReadableStreamBYOBReader",
                     "ReadableStreamBYOBRequest",
                 })
        {
            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);
            descriptor.Writable.Should().BeTrue(name);
            descriptor.Configurable.Should().BeTrue(name);
            descriptor.Enumerable.Should().BeFalse(name);
        }

        // And the global names the very object an instance inherits from.
        engine.Evaluate("new ReadableStream().getReader() instanceof ReadableStreamDefaultReader")
            .AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(new ReadableStream().getReader()).constructor.name")
            .AsString().Should().Be("ReadableStreamDefaultReader");
    }

    [Fact]
    public void TheReaderInterfaceObjectIsConstructibleWithAStream()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ start(c) { c.enqueue('x'); } });
            var Ctor = Object.getPrototypeOf(stream.getReader()).constructor;
            """);

        // "This is equivalent to calling stream.getReader()", including the lock it takes.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Ctor(stream)"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        engine.Execute("var fresh = new ReadableStream({ start(c) { c.enqueue('y'); } });");
        engine.Evaluate("new Ctor(fresh) && fresh.locked").AsBoolean().Should().BeTrue();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Ctor({})"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void ControllerInterfaceObjectsAreNotConstructible()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var Ctor;
            new ReadableStream({ start(c) { Ctor = Object.getPrototypeOf(c).constructor; } });
            """);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Ctor()"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void MembersCarryTheWebIdlShape()
    {
        var engine = StreamEngine();

        engine.Evaluate("ReadableStream.length").AsNumber().Should().Be(0);
        engine.Evaluate("ReadableStream.name").AsString().Should().Be("ReadableStream");
        engine.Evaluate("ReadableStream.from.length").AsNumber().Should().Be(1);
        engine.Evaluate("ReadableStream.prototype.cancel.length").AsNumber().Should().Be(0);
        engine.Evaluate("ReadableStream.prototype.pipeTo.length").AsNumber().Should().Be(1);
        engine.Evaluate("ReadableStream.prototype.pipeThrough.length").AsNumber().Should().Be(1);
        engine.Evaluate("ReadableStream.prototype[Symbol.toStringTag]").AsString().Should().Be("ReadableStream");
        engine.Evaluate("Object.prototype.toString.call(new ReadableStream())").AsString().Should().Be("[object ReadableStream]");

        // `locked` is an accessor on the prototype, not an own property of the instance.
        engine.Evaluate("Object.getOwnPropertyNames(new ReadableStream()).length").AsNumber().Should().Be(0);
        engine.Evaluate("typeof Object.getOwnPropertyDescriptor(ReadableStream.prototype, 'locked').get").AsString().Should().Be("function");
    }

    [Fact]
    public void EveryMemberBrandChecksItsReceiver()
    {
        var engine = StreamEngine();

        // A promise-returning operation reports the failure as a rejection …
        engine.Execute("""
            ReadableStream.prototype.cancel.call({}).catch(e => log.push('cancel:' + e.name));
            ReadableStream.prototype.pipeTo.call({}, new WritableStream()).catch(e => log.push('pipeTo:' + e.name));
            """);
        Log(engine).Should().Be("cancel:TypeError,pipeTo:TypeError");

        // … while everything else throws.
        foreach (var member in new[] { "getReader", "tee", "values" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"ReadableStream.prototype.{member}.call({{}})"))
                .Error.Get("name").AsString().Should().Be("TypeError", member);
        }

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Object.getOwnPropertyDescriptor(ReadableStream.prototype, 'locked').get.call({})"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void FromAcceptsSyncAndAsyncIterables()
    {
        var engine = StreamEngine();
        engine.Execute("""
            async function drain(stream, label) {
              const reader = stream.getReader();
              for (;;) {
                const { value, done } = await reader.read();
                if (done) { log.push(label + ':done'); return; }
                log.push(label + ':' + value);
              }
            }
            (async () => {
              await drain(ReadableStream.from(['a', 'b']), 'array');
              await drain(ReadableStream.from(new Set(['c'])), 'set');
              await drain(ReadableStream.from((async function* () { yield 'd'; })()), 'gen');
              await drain(ReadableStream.from([Promise.resolve('e')]), 'promise');
            })();
            """);

        // A synchronous iterable is adapted through CreateAsyncFromSyncIterator, so a promise it yields is
        // awaited rather than handed on.
        Log(engine).Should().Be("array:a,array:b,array:done,set:c,set:done,gen:d,gen:done,promise:e,promise:done");
    }

    [Fact]
    public void FromRefusesEverythingThatIsNotAnObject()
    {
        var engine = StreamEngine();

        foreach (var value in new[] { "null", "undefined", "0", "true", "'ab'", "Symbol()", "{}", "{ [Symbol.iterator]: 42 }", "{ [Symbol.iterator]: () => 42 }" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"ReadableStream.from({value})"))
                .Error.Get("name").AsString().Should().Be("TypeError", value);
        }
    }

    [Fact]
    public void FromIgnoresTheSyncIteratorWhenAnAsyncOneExists()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var iterable = {
              [Symbol.iterator]() { log.push('sync'); },
              [Symbol.asyncIterator]() { log.push('async'); throw new Error('stop'); }
            };
            try { ReadableStream.from(iterable); } catch (e) { log.push('threw:' + e.message); }
            """);

        Log(engine).Should().Be("async,threw:stop");
    }

    [Fact]
    public void FromCallsReturnOnTheIteratorWhenTheStreamIsCancelled()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var iterable = {
              [Symbol.asyncIterator]() {
                return {
                  next() { return Promise.resolve({ value: 'a', done: false }); },
                  return(reason) { log.push('return:' + reason); return Promise.resolve({ done: true }); }
                };
              }
            };
            var stream = ReadableStream.from(iterable);
            stream.cancel('bye').then(() => log.push('cancelled'));
            """);

        Log(engine).Should().Be("return:bye,cancelled");
    }

    [Fact]
    public void FromOnlyAdvancesTheIteratorWhenAConsumerAsks()
    {
        // ReadableStreamFromIterable creates its stream with a high water mark of 0.
        var engine = StreamEngine();
        engine.Execute("""
            var n = 0;
            var iterable = { [Symbol.asyncIterator]() { return { next() { n++; return Promise.resolve({ value: n, done: false }); } }; } };
            var stream = ReadableStream.from(iterable);
            log.push('created:' + n);
            var reader = stream.getReader();
            reader.read().then(r => log.push('read:' + r.value + ':n=' + n));
            """);

        Log(engine).Should().Be("created:0,read:1:n=1");
    }
}
#endif
