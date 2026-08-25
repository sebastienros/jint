#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;
using Jint.WebApi.Streams;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// Transferring a <c>ReadableStream</c>, a <c>WritableStream</c> or a <c>TransformStream</c>, against
/// https://streams.spec.whatwg.org/#transferrable-streams and the three interfaces' transfer steps.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is one engine transferring to itself through <c>structuredClone</c>, which is what the
/// composition of the two phases gives — the transfer steps need a target <i>realm</i>, not a target agent.
/// The cross-<i>engine</i> form is the same machinery with two pumps and lives in
/// <c>Jint.Tests.PublicInterface.WebApiTransferableStreamTests</c>.
/// </para>
/// <para>
/// <c>Engine.Execute</c> drains the event loop once the script has finished, so a test writes the whole
/// asynchronous story into a <c>log</c> array and then reads it. A chunk crossing a transferred stream is
/// several event-loop tasks — a <c>pull</c> one way, a <c>chunk</c> the other, each delivered as a port
/// message — and the drain runs all of them.
/// </para>
/// </remarks>
public class TransferableStreamTests
{
    private const WebApiFeatures TransferableStreamFeatures = WebApiFeatures.Streams | WebApiFeatures.StructuredClone;

    private static Engine StreamEngine(WebApiFeatures features = TransferableStreamFeatures)
    {
        var engine = new Engine(options => options.UseWebApis(features));
        engine.Execute("var log = [];");
        engine.Execute("function err(f) { try { f(); return 'no error'; } catch (e) { return e.name; } }");
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    private static string Err(Engine engine, string body) => engine.Evaluate("err(function() { " + body + " })").AsString();

    /// <summary>Reads everything a readable stream will ever produce into <c>log</c>.</summary>
    private const string DrainHelper = """
        function drain(stream, tag) {
          return (async function () {
            var reader = stream.getReader();
            try {
              for (;;) {
                var r = await reader.read();
                if (r.done) { log.push(tag + ':done'); return; }
                log.push(tag + ':' + r.value);
              }
            } catch (e) {
              log.push(tag + '!' + (e && e.name));
            }
          })();
        }
        """;

    // ---------------------------------------------------------------- a ReadableStream

    [Fact]
    public void ATransferredReadableStreamDeliversItsChunksToTheClone()
    {
        var engine = StreamEngine();
        engine.Execute(DrainHelper);

        engine.Execute("""
            var rs = new ReadableStream({ start(c) { c.enqueue('a'); c.enqueue('b'); c.close(); } });
            var moved = structuredClone(rs, { transfer: [rs] });
            drain(moved, 'got');
            """);

        Log(engine).Should().Be("got:a,got:b,got:done");
    }

    [Fact]
    public void TheCloneIsARealReadableStreamOfThisRealm()
    {
        var engine = StreamEngine();

        engine.Execute("""
            var rs = new ReadableStream();
            var moved = structuredClone(rs, { transfer: [rs] });
            """);

        engine.Evaluate("moved instanceof ReadableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(moved) === ReadableStream.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("moved === rs").AsBoolean().Should().BeFalse();
        engine.Evaluate("moved.locked").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void TheOriginalIsLockedAndDisturbedAfterwards()
    {
        var engine = StreamEngine();

        engine.Execute("""
            var rs = new ReadableStream({ start(c) { c.enqueue('a'); } });
            var moved = structuredClone(rs, { transfer: [rs] });
            """);

        // "The original will become locked and no longer directly usable" — the pipe the transfer steps
        // started holds a reader on it.
        engine.Evaluate("rs.locked").AsBoolean().Should().BeTrue();
        Err(engine, "rs.getReader();").Should().Be("TypeError");
        Err(engine, "rs.tee();").Should().Be("TypeError");

        // And disturbed, because ReadableStreamPipeTo reads from it. Nothing script-facing exposes the slot;
        // it exists for the specifications built on top of this one, so it is asserted from the inside.
        ((JsReadableStream) engine.Evaluate("rs").AsObject()).Disturbed.Should().BeTrue();
    }

    [Fact]
    public void ALockedReadableStreamIsADataCloneError()
    {
        var engine = StreamEngine();
        engine.Execute("var rs = new ReadableStream(); var reader = rs.getReader();");

        Err(engine, "structuredClone(rs, { transfer: [rs] });").Should().Be("DataCloneError");

        // The refusal happens before anything is moved: the stream is still exactly what it was.
        engine.Evaluate("rs.locked").AsBoolean().Should().BeTrue();
        ((JsReadableStream) engine.Evaluate("rs").AsObject()).Detached.Should().BeFalse();
    }

    [Fact]
    public void TransferringTheSameStreamTwiceIsADataCloneError()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var rs = new ReadableStream({ start(c) { c.close(); } });
            var moved = structuredClone(rs, { transfer: [rs] });
            """);

        // The pipe has finished and released its reader by now, so the lock no longer refuses this — which is
        // exactly what [[Detached]] is for.
        engine.Evaluate("rs.locked").AsBoolean().Should().BeFalse();
        Err(engine, "structuredClone(rs, { transfer: [rs] });").Should().Be("DataCloneError");
    }

    [Fact]
    public void AStreamInTheMessageButNotInTheTransferListIsADataCloneError()
    {
        var engine = StreamEngine();

        // Transferable and not serializable, exactly as a MessagePort is.
        Err(engine, "structuredClone(new ReadableStream());").Should().Be("DataCloneError");
        Err(engine, "structuredClone(new WritableStream());").Should().Be("DataCloneError");
        Err(engine, "structuredClone(new TransformStream());").Should().Be("DataCloneError");
        Err(engine, "structuredClone({ inner: new ReadableStream() });").Should().Be("DataCloneError");
    }

    [Fact]
    public void ADuplicateInTheTransferListIsADataCloneError()
    {
        var engine = StreamEngine();
        engine.Execute("var rs = new ReadableStream();");

        Err(engine, "structuredClone(rs, { transfer: [rs, rs] });").Should().Be("DataCloneError");
    }

    [Fact]
    public void AStreamReachedFromTheMessageResolvesToTheTransferredOne()
    {
        var engine = StreamEngine();
        engine.Execute(DrainHelper);

        engine.Execute("""
            var rs = new ReadableStream({ start(c) { c.enqueue('x'); c.close(); } });
            var out = structuredClone({ first: rs, second: rs }, { transfer: [rs] });
            log.push('same=' + (out.first === out.second));
            log.push('isRS=' + (out.first instanceof ReadableStream));
            drain(out.first, 'got');
            """);

        Log(engine).Should().Be("same=true,isRS=true,got:x,got:done");
    }

    // ---------------------------------------------------------------- close and error propagation

    [Fact]
    public void ClosingTheSourceClosesTheTransferredStream()
    {
        var engine = StreamEngine();
        engine.Execute(DrainHelper);

        engine.Execute("""
            var controller;
            var rs = new ReadableStream({ start(c) { controller = c; } });
            var moved = structuredClone(rs, { transfer: [rs] });
            drain(moved, 'got');
            """);

        Log(engine).Should().Be("");

        engine.Execute("controller.enqueue('one');");
        Log(engine).Should().Be("got:one");

        engine.Execute("controller.close();");
        Log(engine).Should().Be("got:one,got:done");
    }

    [Fact]
    public void ACloseStillOnTheQueueIsNotMistakenForALostChannel()
    {
        var engine = StreamEngine();

        // Two reads outstanding at once is what puts the receiving side's `pull` on the very stack that
        // dispatches a chunk: ReadableStreamDefaultControllerEnqueue fulfils the first read request and then
        // calls CallPullIfNeeded, which sees the second one still waiting. At that instant the sender has
        // already posted `close` AND disentangled — the close algorithm does both, in that order — so the
        // channel's far side reads as closed while the message that closes this stream cleanly is still
        // sitting on its queue. Asking only "has the far side gone" would throw that message away and error a
        // stream that is about to finish; JsMessagePort.IsChannelExhausted asks about the queue too.
        engine.Execute("""
            var rs = new ReadableStream({ start(c) { c.enqueue('only'); c.close(); } });
            var moved = structuredClone(rs, { transfer: [rs] });
            var reader = moved.getReader();
            reader.read().then(function (r) { log.push('1:' + r.value); }, function (e) { log.push('1!' + e.name); });
            reader.read().then(function (r) { log.push('2:' + r.value + ':' + r.done); }, function (e) { log.push('2!' + e.name); });
            """);

        Log(engine).Should().Be("1:only,2:undefined:true");
    }

    [Fact]
    public void ErroringTheSourceErrorsTheTransferredStreamWithTheSameReason()
    {
        var engine = StreamEngine();
        engine.Execute(DrainHelper);

        engine.Execute("""
            var controller;
            var rs = new ReadableStream({ start(c) { controller = c; } });
            var moved = structuredClone(rs, { transfer: [rs] });
            moved.getReader().read().then(
              function () { log.push('resolved'); },
              function (e) { log.push('rejected:' + e.name + ':' + e.message); });
            """);

        engine.Execute("controller.error(new RangeError('from the source'));");

        // The reason is structured-cloned across, so it is an error of the receiving realm carrying the same
        // name and message rather than the very object the source errored with.
        Log(engine).Should().Be("rejected:RangeError:from the source");
    }

    [Fact]
    public void CancellingTheTransferredStreamCancelsTheOriginalSource()
    {
        var engine = StreamEngine();

        engine.Execute("""
            var rs = new ReadableStream({
              start(c) { c.enqueue('a'); },
              cancel(reason) { log.push('source cancelled: ' + reason.message); }
            });
            var moved = structuredClone(rs, { transfer: [rs] });
            """);

        engine.Execute("moved.cancel(new Error('no longer wanted'));");

        // Backward through the channel as an `error` message, then backward through the pipe as a cancel.
        Log(engine).Should().Be("source cancelled: no longer wanted");
    }

    [Fact]
    public void AChunkThatCannotBeClonedFailsTheTransferredStream()
    {
        var engine = StreamEngine();
        engine.Execute(DrainHelper);

        engine.Execute("""
            var controller;
            var rs = new ReadableStream({ start(c) { controller = c; } });
            var moved = structuredClone(rs, { transfer: [rs] });
            drain(moved, 'got');
            """);

        engine.Execute("controller.enqueue(function () {});");

        // PackAndPostMessageHandlingError sends the DataCloneError to the readable side and rejects the write,
        // which errors the writable and — through the pipe — cancels the source.
        Log(engine).Should().Be("got!DataCloneError");
    }

    // ---------------------------------------------------------------- backpressure

    [Fact]
    public void TheSenderDoesNotRunAheadOfTheReceiver()
    {
        var engine = StreamEngine();

        // The source records every pull it is asked for. The cross-realm writable's high water mark is 1 and
        // the readable side's is 0, so nothing crosses until the receiving side actually reads. The supply is
        // capped rather than unbounded so that a loss of backpressure is a failed assertion here and not a
        // test that never returns.
        engine.Execute("""
            var pulls = 0;
            var next = 0;
            var rs = new ReadableStream({
              pull(c) { pulls++; if (next >= 200) { c.close(); return; } c.enqueue('c' + (next++)); }
            });
            var moved = structuredClone(rs, { transfer: [rs] });
            var reader = moved.getReader();
            """);

        // The pipe primed itself with one chunk in flight and one in the writable's queue, and then stopped.
        var afterTransfer = engine.Evaluate("pulls").AsNumber();
        afterTransfer.Should().BeLessThanOrEqualTo(3);

        engine.Execute("reader.read().then(function (r) { log.push(r.value); });");
        Log(engine).Should().Be("c0");

        // One read releases a bounded amount of further work rather than draining the source.
        engine.Evaluate("pulls").AsNumber().Should().BeLessThanOrEqualTo(afterTransfer + 2);

        engine.Execute("reader.read().then(function (r) { log.push(r.value); });");
        Log(engine).Should().Be("c0,c1");
    }

    // ---------------------------------------------------------------- a WritableStream

    [Fact]
    public void ATransferredWritableStreamDeliversWritesToTheOriginalSink()
    {
        var engine = StreamEngine();

        engine.Execute("""
            var ws = new WritableStream({
              write(chunk) { log.push('wrote:' + chunk); },
              close() { log.push('closed'); },
              abort(reason) { log.push('aborted:' + reason.message); }
            });
            var moved = structuredClone(ws, { transfer: [ws] });
            var writer = moved.getWriter();
            writer.write('a');
            writer.write('b');
            """);

        Log(engine).Should().Be("wrote:a,wrote:b");

        engine.Execute("writer.close();");
        Log(engine).Should().Be("wrote:a,wrote:b,closed");
    }

    [Fact]
    public void TheOriginalWritableStreamIsLockedAfterwards()
    {
        var engine = StreamEngine();
        engine.Execute("var ws = new WritableStream(); var moved = structuredClone(ws, { transfer: [ws] });");

        engine.Evaluate("ws.locked").AsBoolean().Should().BeTrue();
        engine.Evaluate("moved.locked").AsBoolean().Should().BeFalse();
        Err(engine, "ws.getWriter();").Should().Be("TypeError");
    }

    [Fact]
    public void ALockedWritableStreamIsADataCloneError()
    {
        var engine = StreamEngine();
        engine.Execute("var ws = new WritableStream(); var writer = ws.getWriter();");

        Err(engine, "structuredClone(ws, { transfer: [ws] });").Should().Be("DataCloneError");
    }

    [Fact]
    public void AbortingTheTransferredWritableStreamAbortsTheOriginalSink()
    {
        var engine = StreamEngine();

        engine.Execute("""
            var ws = new WritableStream({ abort(reason) { log.push('aborted:' + reason.message); } });
            var moved = structuredClone(ws, { transfer: [ws] });
            """);

        engine.Execute("moved.abort(new Error('give up'));");

        Log(engine).Should().Be("aborted:give up");
    }

    [Fact]
    public void ErroringTheOriginalSinkErrorsTheTransferredWritableStream()
    {
        var engine = StreamEngine();

        engine.Execute("""
            var sinkController;
            var ws = new WritableStream({ start(c) { sinkController = c; } });
            var moved = structuredClone(ws, { transfer: [ws] });
            var writer = moved.getWriter();
            writer.closed.then(
              function () { log.push('closed'); },
              function (e) { log.push('rejected:' + e.name + ':' + e.message); });
            """);

        engine.Execute("sinkController.error(new TypeError('sink is gone'));");

        Log(engine).Should().Be("rejected:TypeError:sink is gone");
    }

    // ---------------------------------------------------------------- a TransformStream

    [Fact]
    public void ATransferredTransformStreamStillTransforms()
    {
        var engine = StreamEngine();
        engine.Execute(DrainHelper);

        engine.Execute("""
            var ts = new TransformStream({ transform(chunk, c) { c.enqueue(chunk.toUpperCase()); } });
            var moved = structuredClone(ts, { transfer: [ts] });
            drain(moved.readable, 'got');
            var writer = moved.writable.getWriter();
            writer.write('a');
            writer.write('b');
            """);

        Log(engine).Should().Be("got:A,got:B");

        engine.Execute("writer.close();");
        Log(engine).Should().Be("got:A,got:B,got:done");
    }

    [Fact]
    public void ATransferredTransformStreamLeavesBothOriginalSidesLocked()
    {
        var engine = StreamEngine();
        engine.Execute("var ts = new TransformStream(); var moved = structuredClone(ts, { transfer: [ts] });");

        engine.Evaluate("ts.readable.locked").AsBoolean().Should().BeTrue();
        engine.Evaluate("ts.writable.locked").AsBoolean().Should().BeTrue();
        engine.Evaluate("moved.readable.locked").AsBoolean().Should().BeFalse();
        engine.Evaluate("moved.writable.locked").AsBoolean().Should().BeFalse();
        engine.Evaluate("moved.readable instanceof ReadableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("moved.writable instanceof WritableStream").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ATransformStreamWithALockedSideIsADataCloneError()
    {
        var readableLocked = StreamEngine();
        readableLocked.Execute("var ts = new TransformStream(); var reader = ts.readable.getReader();");
        Err(readableLocked, "structuredClone(ts, { transfer: [ts] });").Should().Be("DataCloneError");

        // Neither side moved: the check for both comes before either transfer.
        readableLocked.Evaluate("ts.writable.locked").AsBoolean().Should().BeFalse();

        var writableLocked = StreamEngine();
        writableLocked.Execute("var ts = new TransformStream(); var writer = ts.writable.getWriter();");
        Err(writableLocked, "structuredClone(ts, { transfer: [ts] });").Should().Be("DataCloneError");
        writableLocked.Evaluate("ts.readable.locked").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ATransformStreamAndOneOfItsSidesInOneTransferListIsADataCloneError()
    {
        // The four combinations of streams/transferable/transform-stream-members.any.js, which the corpus
        // runs too: transferring the transform stream locks and detaches both its sides, so naming one of
        // them alongside it can only fail — whichever order they are named in.
        var engine = StreamEngine();

        Err(engine, "var t = new TransformStream(); structuredClone([t, t.readable], { transfer: [t, t.readable] });")
            .Should().Be("DataCloneError");
        Err(engine, "var t = new TransformStream(); structuredClone([t.readable, t], { transfer: [t.readable, t] });")
            .Should().Be("DataCloneError");
        Err(engine, "var t = new TransformStream(); structuredClone([t, t.writable], { transfer: [t, t.writable] });")
            .Should().Be("DataCloneError");
        Err(engine, "var t = new TransformStream(); structuredClone([t.writable, t], { transfer: [t.writable, t] });")
            .Should().Be("DataCloneError");
    }

    [Fact]
    public void OneSideOfATransformStreamCanBeTransferredOnItsOwn()
    {
        var engine = StreamEngine();
        engine.Execute(DrainHelper);

        engine.Execute("""
            var ts = new TransformStream({ transform(chunk, c) { c.enqueue(chunk + '!'); } });
            var movedReadable = structuredClone(ts.readable, { transfer: [ts.readable] });
            drain(movedReadable, 'got');
            var writer = ts.writable.getWriter();
            writer.write('hi');
            """);

        Log(engine).Should().Be("got:hi!");
    }

    // ---------------------------------------------------------------- the port underneath

    [Fact]
    public void TheChannelAStreamTransferCreatesIsNotExposedAsAPort()
    {
        var engine = StreamEngine(WebApiFeatures.Default);

        engine.Execute("""
            var channel = new MessageChannel();
            var seen = null;
            channel.port2.onmessage = function (e) { seen = e; };
            var rs = new ReadableStream({ start(c) { c.enqueue('a'); c.close(); } });
            channel.port1.postMessage({ stream: rs }, [rs]);
            """);

        // event.ports is the caller's transfer list narrowed to ports, and a transferred stream contributes
        // none of its own even though it created a whole channel.
        engine.Evaluate("seen.ports.length").AsNumber().Should().Be(0);
        engine.Evaluate("seen.data.stream instanceof ReadableStream").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AStreamAndAPortInOneTransferListEachArriveWhereTheyBelong()
    {
        var engine = StreamEngine(WebApiFeatures.Default);
        engine.Execute(DrainHelper);

        engine.Execute("""
            var main = new MessageChannel();
            var side = new MessageChannel();
            main.port2.onmessage = function (e) {
              log.push('ports=' + e.ports.length);
              e.ports[0].onmessage = function (m) { log.push('side:' + m.data); };
              drain(e.data.stream, 'got');
            };
            var rs = new ReadableStream({ start(c) { c.enqueue('a'); c.close(); } });
            main.port1.postMessage({ stream: rs }, [rs, side.port2]);
            side.port1.postMessage('through the side channel');
            """);

        // The side channel's own message beats the stream's first chunk, and that is the protocol showing
        // through rather than an accident: a transferred stream's first chunk costs a `pull` one way and a
        // `chunk` the other, so it cannot arrive until the second round trip.
        Log(engine).Should().Be("ports=1,side:through the side channel,got:a,got:done");
    }

    [Fact]
    public void TransferringAStreamNeedsNoMessagingGlobals()
    {
        // The channel a transfer creates is engine-internal: it never reaches script, so it needs the
        // MessagePort *intrinsics* and not the Messaging feature's globals.
        var engine = StreamEngine();
        engine.Execute(DrainHelper);

        engine.Evaluate("typeof MessageChannel").AsString().Should().Be("undefined");

        engine.Execute("""
            var rs = new ReadableStream({ start(c) { c.enqueue('a'); c.close(); } });
            drain(structuredClone(rs, { transfer: [rs] }), 'got');
            """);

        Log(engine).Should().Be("got:a,got:done");
    }

    // ---------------------------------------------------------------- stranding

    [Fact]
    public void AStreamTransferredIntoAClosedPortStrandsAndTheSourceIsCancelled()
    {
        var engine = StreamEngine(WebApiFeatures.Default);

        engine.Execute("""
            var pulls = 0;
            var rs = new ReadableStream({
              pull(c) { pulls++; c.enqueue('c' + pulls); },
              cancel(reason) { log.push('source cancelled: ' + reason.name); }
            });

            var channel = new MessageChannel();
            channel.port2.close();
            channel.port1.postMessage({ stream: rs }, [rs]);
            """);

        // The message was serialized — the transfer happened, and the stream is gone from this realm — but
        // step 6 had nowhere to deliver it, so the channel the transfer created was ended by
        // StrandTransferredPorts. The pipe therefore stops rather than draining the source into nothing.
        Log(engine).Should().Be("source cancelled: TypeError");

        var pullsAfterStranding = engine.Evaluate("pulls").AsNumber();
        engine.Tasks.ProcessTasks();
        engine.Tasks.ProcessTasks();
        engine.Evaluate("pulls").AsNumber().Should().Be(pullsAfterStranding, "no pipe may be left running against a stranded side");
    }

    [Fact]
    public void AStreamStrandedByAThrowLaterInTheTransferListIsEndedToo()
    {
        var engine = StreamEngine(WebApiFeatures.Default);

        engine.Execute("""
            var pulls = 0;
            var rs = new ReadableStream({
              pull(c) { pulls++; c.enqueue('c' + pulls); },
              cancel(reason) { log.push('source cancelled: ' + reason.name); }
            });

            var detached = new MessageChannel().port1;
            detached.close();
            """);

        // The stream is transferred first and the already-detached port then refuses, so the whole
        // serialization is discarded with a channel already created for the stream.
        Err(engine, "structuredClone([rs, detached], { transfer: [rs, detached] });").Should().Be("DataCloneError");

        engine.Execute("void 0;");
        Log(engine).Should().Be("source cancelled: TypeError");

        var pullsAfterStranding = engine.Evaluate("pulls").AsNumber();
        engine.Tasks.ProcessTasks();
        engine.Tasks.ProcessTasks();
        engine.Evaluate("pulls").AsNumber().Should().Be(pullsAfterStranding);
    }

    [Fact]
    public void AStreamWhoseUndeliveredMessageIsDiscardedIsEndedToo()
    {
        var engine = StreamEngine(WebApiFeatures.Default);

        engine.Execute("""
            var pulls = 0;
            var rs = new ReadableStream({
              pull(c) { pulls++; c.enqueue('c' + pulls); },
              cancel(reason) { log.push('source cancelled: ' + reason.name); }
            });

            var channel = new MessageChannel();
            channel.port1.postMessage({ stream: rs }, [rs]);

            // Never started, so the message is still sitting on port2's queue when it is closed.
            channel.port2.close();
            """);

        Log(engine).Should().Be("source cancelled: TypeError");

        var pullsAfterStranding = engine.Evaluate("pulls").AsNumber();
        engine.Tasks.ProcessTasks();
        engine.Tasks.ProcessTasks();
        engine.Evaluate("pulls").AsNumber().Should().Be(pullsAfterStranding);
    }
}
#endif
