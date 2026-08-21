#if NET8_0_OR_GREATER
#nullable enable

using System.Threading;
using Jint;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A <c>ReadableStream</c>, a <c>WritableStream</c> or a <c>TransformStream</c> handed from one
/// <see cref="Engine"/> to another over a <c>MessagePort</c> —
/// https://streams.spec.whatwg.org/#transferrable-streams, seen from outside the assembly.
/// </summary>
/// <remarks>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party: two
/// engines connected with <c>Engine.Advanced.CreateMessagePortPair</c>, a stream named in a
/// <c>postMessage</c> transfer list, and <c>Advanced.ProcessTasks()</c> on each in turn.
/// </para>
/// <para>
/// <b>Both engines have to be pumped, and neither ever runs on the other's thread.</b> A transferred stream
/// is a pipe on the sender feeding a channel, so a chunk costs a round trip: the receiver's read posts a
/// <c>pull</c>, the sender's next pump answers it with a <c>chunk</c>, and the receiver's pump after that
/// delivers it. Pumping alternately is what a host with its own loop does, and it is what makes the ordering
/// assertions here mean something — two threads would test the operating system's scheduler instead.
/// </para>
/// </remarks>
public class WebApiTransferableStreamTests
{
    private static Engine TransferEngine() => new(options => options.UseWebApis(
        WebApiFeatures.Streams | WebApiFeatures.Messaging | WebApiFeatures.StructuredClone));

    /// <summary>Two engines with an entangled port each, and a <c>log</c> array on both.</summary>
    private static (Engine Host, Engine Worker) ConnectedPair()
    {
        var host = TransferEngine();
        var worker = TransferEngine();

        var pair = host.Advanced.CreateMessagePortPair(worker);
        host.SetValue("port", pair.Local);
        worker.SetValue("port", pair.Remote);

        foreach (var engine in new[] { host, worker })
        {
            engine.Execute("var log = [];");
            engine.Execute("""
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
                """);
        }

        return (host, worker);
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    /// <summary>
    /// Reads the log without running any script, which is the only honest way to ask an engine whether it has
    /// received something: <c>Evaluate</c> drains the event loop once the script it ran has finished, so
    /// asking in script is itself enough to make the answer yes.
    /// </summary>
    private static int LogLength(Engine engine) => (int) engine.GetValue("log").Get("length").AsNumber();

    /// <summary>Pumps each engine <paramref name="rounds"/> times in turn, without running any script.</summary>
    private static void PumpBoth(Engine host, Engine worker, int rounds)
    {
        for (var i = 0; i < rounds; i++)
        {
            host.Advanced.ProcessTasks();
            worker.Advanced.ProcessTasks();
        }
    }

    // ---------------------------------------------------------------- a ReadableStream across engines

    [Fact]
    public void AReadableStreamTransferredToAnotherEngineDeliversItsChunksThere()
    {
        var (host, worker) = ConnectedPair();

        worker.Execute("port.onmessage = function (e) { drain(e.data, 'got'); };");

        host.Execute("""
            var rs = new ReadableStream({ start(c) { c.enqueue('a'); c.enqueue('b'); c.close(); } });
            port.postMessage(rs, [rs]);
            """);

        // The transfer happened on the sender the moment postMessage returned; nothing has been delivered.
        host.Evaluate("rs.locked").AsBoolean().Should().BeTrue();
        Log(worker).Should().Be("");

        PumpBoth(host, worker, 8);

        worker.Evaluate("log.join(',')").AsString().Should().Be("got:a,got:b,got:done");
    }

    [Fact]
    public void TheStreamArrivesAsAReadableStreamOfTheReceivingRealm()
    {
        var (host, worker) = ConnectedPair();

        worker.Execute("var arrived = null; var portCount = -1; port.onmessage = function (e) { arrived = e.data; portCount = e.ports.length; };");
        host.Execute("var rs = new ReadableStream(); port.postMessage(rs, [rs]);");

        worker.Advanced.ProcessTasks();

        worker.Evaluate("arrived instanceof ReadableStream").AsBoolean().Should().BeTrue();
        worker.Evaluate("Object.getPrototypeOf(arrived) === ReadableStream.prototype").AsBoolean().Should().BeTrue();
        worker.Evaluate("arrived.locked").AsBoolean().Should().BeFalse();

        // event.ports carries the caller's ports and not the channel the stream created for itself, which no
        // script on either engine can ever name.
        worker.Evaluate("portCount").AsNumber().Should().Be(0);
    }

    [Fact]
    public void NothingCrossesToAnEngineThatIsNeverPumped()
    {
        var (host, worker) = ConnectedPair();

        worker.Execute("port.onmessage = function (e) { drain(e.data, 'got'); };");
        host.Execute("""
            var rs = new ReadableStream({ start(c) { c.enqueue('a'); c.close(); } });
            port.postMessage(rs, [rs]);
            """);

        // Jint starts no threads, so the same contract a MessagePort carries applies to what rides one. The
        // observation has to be a non-pumping one, which is what LogLength is for.
        for (var i = 0; i < 5; i++)
        {
            host.Advanced.ProcessTasks();
        }

        LogLength(worker).Should().Be(0);
        Thread.Sleep(50);
        LogLength(worker).Should().Be(0);

        PumpBoth(host, worker, 8);
        Log(worker).Should().Be("got:a,got:done");
    }

    [Fact]
    public void ChunksAreClonesOfTheReceivingRealmRatherThanSharedObjects()
    {
        var (host, worker) = ConnectedPair();

        worker.Execute("var seen = []; port.onmessage = function (e) { drainInto(e.data); };");
        worker.Execute("""
            function drainInto(stream) {
              (async function () {
                var reader = stream.getReader();
                for (;;) {
                  var r = await reader.read();
                  if (r.done) { return; }
                  seen.push(r.value);
                }
              })();
            }
            """);

        host.Execute("""
            var rs = new ReadableStream({ start(c) { c.enqueue(new Map([['k', { n: 1 }]])); c.close(); } });
            port.postMessage(rs, [rs]);
            """);

        PumpBoth(host, worker, 8);

        worker.Evaluate("seen.length").AsNumber().Should().Be(1);
        worker.Evaluate("seen[0] instanceof Map").AsBoolean().Should().BeTrue();
        worker.Evaluate("seen[0].get('k').n").AsNumber().Should().Be(1);
        worker.Evaluate("Object.getPrototypeOf(seen[0]) === Map.prototype").AsBoolean().Should().BeTrue();
    }

    // ---------------------------------------------------------------- backpressure across the boundary

    [Fact]
    public void TheSendingEngineDoesNotRunAheadOfTheReceivingOne()
    {
        var (host, worker) = ConnectedPair();

        // A source with a large supply. Without backpressure it would be drained into the channel as fast as
        // the sender is pumped, whatever the receiver did. Capped rather than endless so that a loss of
        // backpressure fails an assertion instead of hanging the suite.
        host.Execute("""
            var produced = 0;
            var rs = new ReadableStream({
              pull(c) { if (produced >= 200) { c.close(); return; } c.enqueue('c' + produced++); }
            });
            port.postMessage(rs, [rs]);
            """);

        worker.Execute("var arrived = null; port.onmessage = function (e) { arrived = e.data; };");
        worker.Advanced.ProcessTasks();

        // Nobody has read yet, so the receiving side's high water mark of 0 has never asked for anything: the
        // sender is parked with at most a couple of chunks in hand.
        for (var i = 0; i < 20; i++)
        {
            host.Advanced.ProcessTasks();
        }

        var parked = host.Evaluate("produced").AsNumber();
        parked.Should().BeLessThanOrEqualTo(3);

        // One read releases a bounded amount of further production rather than the whole source.
        worker.Execute("var reader = arrived.getReader(); reader.read().then(function (r) { log.push(r.value); });");
        PumpBoth(host, worker, 6);

        Log(worker).Should().Be("c0");
        host.Evaluate("produced").AsNumber().Should().BeLessThanOrEqualTo(parked + 2);

        worker.Execute("reader.read().then(function (r) { log.push(r.value); });");
        PumpBoth(host, worker, 6);

        Log(worker).Should().Be("c0,c1");
    }

    // ---------------------------------------------------------------- errors and closing, both directions

    [Fact]
    public void AnErrorOnTheSendingSideReachesTheReceivingStream()
    {
        var (host, worker) = ConnectedPair();

        worker.Execute("""
            port.onmessage = function (e) {
              e.data.getReader().read().then(
                function () { log.push('resolved'); },
                function (err) { log.push('rejected:' + err.name + ':' + err.message); });
            };
            """);

        host.Execute("""
            var controller;
            var rs = new ReadableStream({ start(c) { controller = c; } });
            port.postMessage(rs, [rs]);
            """);

        PumpBoth(host, worker, 4);
        Log(worker).Should().Be("");

        host.Execute("controller.error(new RangeError('the source failed'));");
        PumpBoth(host, worker, 4);

        // The reason is structured-cloned, so the receiver gets an error of its own realm carrying the same
        // name and message.
        Log(worker).Should().Be("rejected:RangeError:the source failed");
    }

    [Fact]
    public void CancellingOnTheReceivingSideCancelsTheSourceOnTheSendingSide()
    {
        var (host, worker) = ConnectedPair();

        worker.Execute("var arrived = null; port.onmessage = function (e) { arrived = e.data; };");

        host.Execute("""
            var rs = new ReadableStream({
              start(c) { c.enqueue('a'); },
              cancel(reason) { log.push('source cancelled: ' + reason.message); }
            });
            port.postMessage(rs, [rs]);
            """);

        PumpBoth(host, worker, 4);
        Log(host).Should().Be("");

        worker.Execute("arrived.cancel(new Error('not interested'));");
        PumpBoth(host, worker, 4);

        Log(host).Should().Be("source cancelled: not interested");
    }

    [Fact]
    public void ClosingTheSourcePropagatesForwardAcrossEngines()
    {
        var (host, worker) = ConnectedPair();

        worker.Execute("port.onmessage = function (e) { drain(e.data, 'got'); };");

        host.Execute("""
            var controller;
            var rs = new ReadableStream({ start(c) { controller = c; } });
            port.postMessage(rs, [rs]);
            """);

        PumpBoth(host, worker, 4);
        Log(worker).Should().Be("");

        host.Execute("controller.enqueue('only');");
        PumpBoth(host, worker, 4);
        Log(worker).Should().Be("got:only");

        host.Execute("controller.close();");
        PumpBoth(host, worker, 4);
        Log(worker).Should().Be("got:only,got:done");
    }

    [Fact]
    public void ACloseAlreadyQueuedIsNotMistakenForALostChannel()
    {
        var (host, worker) = ConnectedPair();

        worker.Execute("var arrived = null; port.onmessage = function (e) { arrived = e.data; };");

        host.Execute("""
            var rs = new ReadableStream({ start(c) { c.enqueue('only'); c.close(); } });
            port.postMessage(rs, [rs]);
            """);

        worker.Advanced.ProcessTasks();

        // Two reads outstanding at once, so that the *chunk*'s own dispatch leaves a read request waiting and
        // ReadableStreamDefaultControllerEnqueue therefore pulls again on that very stack.
        worker.Execute("""
            var reader = arrived.getReader();
            reader.read().then(function (r) { log.push('1:' + r.value); }, function (e) { log.push('1!' + e.name); });
            reader.read().then(function (r) { log.push('2:' + r.value + ':' + r.done); }, function (e) { log.push('2!' + e.name); });
            """);

        // The host answers the pull, finishes the pipe and closes its end — all in one pump — so `chunk` and
        // `close` are both on the worker's queue *and* the far side reads as gone before the worker runs
        // again. That is the interleaving a cross-engine transfer makes reachable and a same-engine one does
        // not, and it is what JsMessagePort.IsChannelExhausted's queue test exists for: asking only "has the
        // far side gone" here would throw the `close` away and error a stream that is about to finish.
        host.Advanced.ProcessTasks();
        worker.Advanced.ProcessTasks();

        Log(worker).Should().Be("1:only,2:undefined:true");
    }

    // ---------------------------------------------------------------- a WritableStream across engines

    [Fact]
    public void AWritableStreamTransferredToAnotherEngineDeliversWritesBackToItsSink()
    {
        var (host, worker) = ConnectedPair();

        worker.Execute("""
            var writer = null;
            port.onmessage = function (e) { writer = e.data.getWriter(); writer.write('one'); writer.write('two'); };
            """);

        host.Execute("""
            var ws = new WritableStream({
              write(chunk) { log.push('wrote:' + chunk); },
              close() { log.push('closed'); }
            });
            port.postMessage(ws, [ws]);
            """);

        host.Evaluate("ws.locked").AsBoolean().Should().BeTrue();

        PumpBoth(host, worker, 8);
        Log(host).Should().Be("wrote:one,wrote:two");

        worker.Execute("writer.close();");
        PumpBoth(host, worker, 8);
        Log(host).Should().Be("wrote:one,wrote:two,closed");
    }

    // ---------------------------------------------------------------- a TransformStream across engines

    [Fact]
    public void ATransformStreamRoundTripsAcrossEngines()
    {
        var (host, worker) = ConnectedPair();

        // The transformer stays on the host: what crosses is a writable that feeds it and a readable that
        // drains it, each with a channel of its own.
        worker.Execute("""
            var writer = null;
            port.onmessage = function (e) {
              drain(e.data.readable, 'got');
              writer = e.data.writable.getWriter();
              writer.write('a');
              writer.write('b');
            };
            """);

        host.Execute("""
            var ts = new TransformStream({ transform(chunk, c) { c.enqueue(chunk.toUpperCase()); } });
            port.postMessage(ts, [ts]);
            """);

        host.Evaluate("ts.readable.locked").AsBoolean().Should().BeTrue();
        host.Evaluate("ts.writable.locked").AsBoolean().Should().BeTrue();

        PumpBoth(host, worker, 12);
        Log(worker).Should().Be("got:A,got:B");

        worker.Execute("writer.close();");
        PumpBoth(host, worker, 12);
        Log(worker).Should().Be("got:A,got:B,got:done");
    }

    // ---------------------------------------------------------------- what a restore does to the pipe

    [Fact]
    public void ARestoreOnTheSendingEngineEndsTheReceivingStream()
    {
        var (host, worker) = ConnectedPair();
        var snapshot = host.Advanced.CaptureGlobalSnapshot();

        worker.Execute("var arrived = null; port.onmessage = function (e) { arrived = e.data; };");

        host.Execute("""
            var controller;
            var rs = new ReadableStream({ start(c) { controller = c; } });
            port.postMessage(rs, [rs]);
            """);

        PumpBoth(host, worker, 4);
        worker.Evaluate("arrived instanceof ReadableStream").AsBoolean().Should().BeTrue();

        // The restore closes every port the host had, which is the cascade that ends the stream's channel too.
        host.Advanced.RestoreGlobalSnapshot(snapshot);

        worker.Execute("""
            arrived.getReader().read().then(
              function () { log.push('resolved'); },
              function (e) { log.push('rejected:' + e.name); });
            """);

        PumpBoth(host, worker, 4);

        // Errored rather than left readable forever: the channel can never carry another chunk.
        Log(worker).Should().Be("rejected:TypeError");
    }

    [Fact]
    public void ARestoreOnTheReceivingEngineEndsTheSendersPipe()
    {
        var (host, worker) = ConnectedPair();
        var snapshot = worker.Advanced.CaptureGlobalSnapshot();

        worker.Execute("var arrived = null; port.onmessage = function (e) { arrived = e.data; arrived.getReader(); };");

        host.Execute("""
            var pulls = 0;
            var controller;
            var rs = new ReadableStream({
              start(c) { controller = c; },
              pull() { pulls++; },
              cancel(reason) { log.push('source cancelled: ' + reason.name); }
            });
            port.postMessage(rs, [rs]);
            """);

        PumpBoth(host, worker, 4);

        // The receiving engine's cycle ends, which closes its ports — including the one the stream travels on.
        worker.Advanced.RestoreGlobalSnapshot(snapshot);

        // The sender notices at its next write, which is the first moment it has anything to say. Until then
        // it is parked on a read, costing nothing.
        Log(host).Should().Be("");
        host.Execute("controller.enqueue('too late');");

        Log(host).Should().Be("source cancelled: TypeError");

        // And nothing is left running against the side that has gone: the source is not pulled again however
        // long the host is pumped.
        var pullsAtTheEnd = host.Evaluate("pulls").AsNumber();
        for (var i = 0; i < 5; i++)
        {
            host.Advanced.ProcessTasks();
        }

        host.Evaluate("pulls").AsNumber().Should().Be(pullsAtTheEnd);
    }

    [Fact]
    public void DisposingTheReceivingEngineEndsTheSendersPipeTheSameWay()
    {
        var host = TransferEngine();
        var worker = TransferEngine();
        var pair = host.Advanced.CreateMessagePortPair(worker);
        host.SetValue("port", pair.Local);
        worker.SetValue("port", pair.Remote);

        host.Execute("var log = [];");
        worker.Execute("var arrived = null; port.onmessage = function (e) { arrived = e.data; arrived.getReader(); };");

        host.Execute("""
            var controller;
            var rs = new ReadableStream({
              start(c) { controller = c; },
              cancel(reason) { log.push('source cancelled: ' + reason.name); }
            });
            port.postMessage(rs, [rs]);
            """);

        host.Advanced.ProcessTasks();
        worker.Advanced.ProcessTasks();
        host.Advanced.ProcessTasks();

        worker.Dispose();

        host.Execute("controller.enqueue('nobody there');");
        host.Evaluate("log.join(',')").AsString().Should().Be("source cancelled: TypeError");
    }
}
#endif
