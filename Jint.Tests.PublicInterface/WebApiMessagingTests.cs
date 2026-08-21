#if NET8_0_OR_GREATER
#nullable enable

using System.Threading;
using Jint;
using Jint.Native;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Channel messaging seen from outside the assembly: what a host has to write to get it, and the one web API
/// that lets two <see cref="Engine"/>s talk to each other —
/// <c>Engine.Advanced.CreateMessagePortPair</c>.
/// </summary>
/// <remarks>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party.
/// </para>
/// <para>
/// The cross-engine tests run both engines on the test's own thread and pump them alternately with
/// <c>Advanced.ProcessTasks()</c>, which is the shape a host with its own loop uses. That is deliberate:
/// two threads would test the operating system's scheduler as much as the code, whereas alternating pumps
/// test exactly the contract — a message serialized on one engine's turn is deserialized and dispatched on
/// the other engine's turn, and never in between.
/// </para>
/// </remarks>
public class WebApiMessagingTests
{
    private static Engine MessagingEngine() => new(options => options.UseWebApis(WebApiFeatures.Messaging));

    // ---------------------------------------------------------------- the opt-in

    [Fact]
    public void ADefaultEngineHasNoChannelMessaging()
    {
        var engine = new Engine();

        engine.Evaluate("typeof MessageChannel").AsString().Should().Be("undefined");
        engine.Evaluate("typeof MessagePort").AsString().Should().Be("undefined");
        engine.Evaluate("typeof MessageEvent").AsString().Should().Be("undefined");
        engine.Evaluate("'MessageChannel' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void TheFeatureFlagInstallsIt()
    {
        var engine = MessagingEngine();

        engine.Evaluate("typeof MessageChannel").AsString().Should().Be("function");
        engine.Evaluate("new MessageChannel().port1 instanceof MessagePort").AsBoolean().Should().BeTrue();

        // DOMException comes with any web API, because it is how this one reports a refusal.
        engine.Evaluate("typeof DOMException").AsString().Should().Be("function");
    }

    [Fact]
    public void TheDefaultSetIncludesIt()
    {
        WebApiFeatures.Default.Should().HaveFlag(WebApiFeatures.Messaging);

        new Engine(options => options.UseWebApis()).Evaluate("typeof MessageChannel").AsString().Should().Be("function");
    }

    [Fact]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new JsString("host's own MessageChannel");

        var engine = new Engine(options => options
            .AddLazyGlobal("MessageChannel", _ => marker)
            .UseWebApis(WebApiFeatures.Messaging));

        engine.Evaluate("MessageChannel").Should().Be(marker);

        // The names the host did not claim are still installed.
        engine.Evaluate("typeof MessagePort").AsString().Should().Be("function");
    }

    [Fact]
    public void AShadowRealmHasNoChannelMessaging()
    {
        var engine = MessagingEngine();

        engine.Evaluate("new ShadowRealm().evaluate('typeof MessageChannel')").AsString().Should().Be("undefined");
    }

    // ---------------------------------------------------------------- CreateMessagePortPair

    [Fact]
    public void CreateMessagePortPairRefusesAnEngineWithoutTheFeature()
    {
        var enabled = MessagingEngine();
        var plain = new Engine();

        Assert.Throws<InvalidOperationException>(() => enabled.Advanced.CreateMessagePortPair(plain));
        Assert.Throws<InvalidOperationException>(() => plain.Advanced.CreateMessagePortPair(enabled));
        Assert.Throws<ArgumentNullException>(() => enabled.Advanced.CreateMessagePortPair(null!));
    }

    [Fact]
    public void CreateMessagePortPairGivesEachEngineItsOwnPort()
    {
        var first = MessagingEngine();
        var second = MessagingEngine();

        var pair = first.Advanced.CreateMessagePortPair(second);

        first.SetValue("port", pair.Local);
        second.SetValue("port", pair.Remote);

        first.Evaluate("port instanceof MessagePort").AsBoolean().Should().BeTrue();
        second.Evaluate("port instanceof MessagePort").AsBoolean().Should().BeTrue();

        // Each port is a value of its own engine, so the two are never the same object.
        pair.Local.Should().NotBeSameAs(pair.Remote);

        // ... and each really does belong to its own engine's realm.
        first.Evaluate("Object.getPrototypeOf(port) === MessagePort.prototype").AsBoolean().Should().BeTrue();
        second.Evaluate("Object.getPrototypeOf(port) === MessagePort.prototype").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CreateMessagePortPairWithTheSameEngineIsASameEngineChannel()
    {
        var engine = MessagingEngine();
        var pair = engine.Advanced.CreateMessagePortPair(engine);

        engine.SetValue("a", pair.Local);
        engine.SetValue("b", pair.Remote);
        engine.Execute("var log = []; a.onmessage = function (e) { log.push(e.data); }; b.postMessage('hi');");

        engine.Evaluate("log.join(',')").AsString().Should().Be("hi");
    }

    // ---------------------------------------------------------------- cross-engine round trips

    [Fact]
    public void MessagesCrossBetweenTwoEnginesPumpedAlternately()
    {
        var (host, worker) = ConnectedPair();

        // Nothing has been pumped on the worker yet, so nothing has arrived.
        host.Execute("port.postMessage({ ask: 'ping', payload: [1, 2, 3] });");
        ReceivedCount(worker).Should().Be(0);

        worker.Advanced.ProcessTasks();
        worker.Evaluate("received.length").AsNumber().Should().Be(1);
        worker.Evaluate("received[0].ask").AsString().Should().Be("ping");
        worker.Evaluate("received[0].payload.join('-')").AsString().Should().Be("1-2-3");

        // ... and back the other way.
        worker.Execute("port.postMessage('pong');");
        ReceivedCount(host).Should().Be(0);

        host.Advanced.ProcessTasks();
        host.Evaluate("received[0]").AsString().Should().Be("pong");
    }

    [Fact]
    public void MessagesCrossInTheOrderTheyWerePosted()
    {
        var (host, worker) = ConnectedPair();

        host.Execute("for (var i = 0; i < 4; i++) { port.postMessage(i); }");
        worker.Advanced.ProcessTasks();

        worker.Evaluate("received.join(',')").AsString().Should().Be("0,1,2,3");
    }

    [Fact]
    public void ACrossEngineMessageIsACloneNotASharedObject()
    {
        var (host, worker) = ConnectedPair();

        host.Execute("var sent = new Map([['k', { n: 1 }]]); port.postMessage(sent);");
        worker.Advanced.ProcessTasks();

        // The receiving engine gets objects of its own realm, which is what makes sharing a JsValue across
        // engines unnecessary — and it is the only reason this is safe at all.
        worker.Evaluate("received[0] instanceof Map").AsBoolean().Should().BeTrue();
        worker.Evaluate("received[0].get('k').n").AsNumber().Should().Be(1);
        worker.Evaluate("Object.getPrototypeOf(received[0]) === Map.prototype").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ATransferredArrayBufferMovesToTheOtherEngineAndLeavesTheSenderDetached()
    {
        var (host, worker) = ConnectedPair();

        host.Execute("""
            var buffer = new ArrayBuffer(4);
            var bytes = new Uint8Array(buffer);
            bytes[0] = 1;
            bytes[3] = 255;
            port.postMessage(buffer, [buffer]);
            """);

        // Detached on the sender the moment postMessage returned — the transfer is part of serialization, not
        // of delivery, so it does not wait for the receiver to be pumped.
        host.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);

        worker.Advanced.ProcessTasks();
        worker.Evaluate("received[0] instanceof ArrayBuffer").AsBoolean().Should().BeTrue();
        worker.Evaluate("received[0].byteLength").AsNumber().Should().Be(4);
        worker.Evaluate("new Uint8Array(received[0])[0]").AsNumber().Should().Be(1);
        worker.Evaluate("new Uint8Array(received[0])[3]").AsNumber().Should().Be(255);
    }

    [Fact]
    public void ACrossEnginePortHoldsMessagesUntilItIsStarted()
    {
        var host = MessagingEngine();
        var worker = MessagingEngine();
        var pair = host.Advanced.CreateMessagePortPair(worker);
        host.SetValue("port", pair.Local);
        worker.SetValue("port", pair.Remote);

        // addEventListener alone does not enable the port message queue, on either side of an engine
        // boundary.
        worker.Execute("var received = []; port.addEventListener('message', function (e) { received.push(e.data); });");

        host.Execute("port.postMessage('waiting');");
        worker.Advanced.ProcessTasks();
        worker.Evaluate("received.length").AsNumber().Should().Be(0);

        worker.Execute("port.start();");
        worker.Evaluate("received[0]").AsString().Should().Be("waiting");
    }

    [Fact]
    public void ClosingOneEnginesPortStopsTheOther()
    {
        var (host, worker) = ConnectedPair();

        worker.Execute("port.close();");
        host.Execute("port.postMessage('lost');");
        worker.Advanced.ProcessTasks();

        worker.Evaluate("received.length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void NothingIsDeliveredToAnEngineThatIsNeverPumped()
    {
        var (host, worker) = ConnectedPair();

        host.Execute("port.postMessage('never');");

        // Jint starts no threads, so an engine nobody pumps runs nothing — the same contract the timers have.
        // Note that the observation has to be a non-pumping one: Evaluate drains the event loop when the
        // script it ran has finished, so asking the worker in script whether it has received anything is
        // itself enough to make it receive.
        ReceivedCount(worker).Should().Be(0);
        Thread.Sleep(50);
        ReceivedCount(worker).Should().Be(0);

        worker.Advanced.ProcessTasks();
        ReceivedCount(worker).Should().Be(1);
        worker.Evaluate("received[0]").AsString().Should().Be("never");
    }

    // ---------------------------------------------------------------- the generation fence

    [Fact]
    public void AMessageInFlightWhenTheReceiverRestoresIsDropped()
    {
        var (host, worker) = ConnectedPair();
        var snapshot = worker.Advanced.CaptureGlobalSnapshot();

        // Posted while the worker is in the cycle the port belongs to, but not yet pumped.
        host.Execute("port.postMessage('from the previous cycle');");

        // The worker ends that cycle before it ever runs the delivery job.
        worker.Advanced.RestoreGlobalSnapshot(snapshot);
        worker.Advanced.ProcessTasks();

        // A port's listeners are closures over the cycle it was created in, so running them against the
        // restored globals is exactly the cross-cycle channel the fence forbids. This half is caught by the
        // eager flush the restore performs; the test below is the one that pins the generation stamp, which
        // is what covers everything the flush cannot reach.
        worker.Evaluate("received.length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void AMessagePostedAfterTheReceiverRestoresIsDropped()
    {
        var (host, worker) = ConnectedPair();
        var snapshot = worker.Advanced.CaptureGlobalSnapshot();

        worker.Advanced.RestoreGlobalSnapshot(snapshot);

        // The port itself is what belongs to the ended cycle, so it does not matter that this message was
        // posted afterwards: the channel is over.
        host.Execute("port.postMessage('too late');");
        worker.Advanced.ProcessTasks();

        worker.Evaluate("received.length").AsNumber().Should().Be(0);

        // A fresh pair is what a pooled engine wants, and it works immediately.
        var replacement = host.Advanced.CreateMessagePortPair(worker);
        host.SetValue("port2", replacement.Local);
        worker.SetValue("port2", replacement.Remote);
        worker.Execute("port2.onmessage = function (e) { received.push(e.data); };");
        host.Execute("port2.postMessage('next cycle');");
        worker.Advanced.ProcessTasks();

        worker.Evaluate("received[0]").AsString().Should().Be("next cycle");
    }

    [Fact]
    public void ARestoreOnTheSenderDoesNotStopTheReceiver()
    {
        var (host, worker) = ConnectedPair();
        var snapshot = host.Advanced.CaptureGlobalSnapshot();

        host.Execute("port.postMessage('sent then restored');");
        host.Advanced.RestoreGlobalSnapshot(snapshot);
        worker.Advanced.ProcessTasks();

        // The message was serialized when it was posted and carries nothing of the sender's, so the sender's
        // cycle ending afterwards has no bearing on it. Only the RECEIVER's generation fences delivery.
        worker.Evaluate("received[0]").AsString().Should().Be("sent then restored");
    }

    // ---------------------------------------------------------------- transferring a port between engines

    [Fact]
    public void APortTransferredToAnotherEngineIsEntangledWithTheOriginalPeer()
    {
        var (host, worker) = ConnectedPair();

        worker.Execute("var moved = null; port.onmessage = function (e) { moved = e.ports[0]; };");

        // The host makes a private channel and hands one end to the worker over the channel it already has.
        host.Execute("""
            var side = new MessageChannel();
            side.port1.onmessage = function (e) { received.push('side:' + e.data); };
            port.postMessage('here is a port', [side.port2]);
            """);

        worker.Advanced.ProcessTasks();

        // The worker's engine built a MessagePort of its OWN realm — no JsValue crossed — and the event
        // carries it in a frozen ports array.
        worker.Evaluate("moved instanceof MessagePort").AsBoolean().Should().BeTrue();
        worker.Evaluate("Object.getPrototypeOf(moved) === MessagePort.prototype").AsBoolean().Should().BeTrue();
        // ... and it is entangled with the host's side.port1, which never learned that anything moved.
        worker.Execute("moved.postMessage('hello from the worker');");
        host.Advanced.ProcessTasks();
        host.Evaluate("received.join(',')").AsString().Should().Be("side:hello from the worker");
    }

    [Fact]
    public void ATransferredPortIsInertOnTheSendingEngine()
    {
        var (host, worker) = ConnectedPair();

        host.Execute("""
            var side = new MessageChannel();
            side.port1.onmessage = function (e) { received.push('side:' + e.data); };
            port.postMessage('handover', [side.port2]);

            // Detached: postMessage is a silent no-op rather than a throw.
            side.port2.postMessage('from the detached port');
            """);

        worker.Execute("port.onmessage = function (e) { e.ports[0].onmessage = function (ev) { received.push(ev.data); }; };");
        worker.Advanced.ProcessTasks();
        host.Advanced.ProcessTasks();

        // Nothing came back through the detached object, and the worker got the one message the channel
        // really carried.
        host.Evaluate("received.length").AsNumber().Should().Be(0);
        worker.Evaluate("received.length").AsNumber().Should().Be(0);

        host.Execute("side.port1.postMessage('through the moved port');");
        worker.Advanced.ProcessTasks();
        worker.Evaluate("received.join(',')").AsString().Should().Be("through the moved port");
    }

    [Fact]
    public void ATransferCarriesTheQueuedMessagesAndKeepsThemAhead()
    {
        var (host, worker) = ConnectedPair();

        // Two messages queued on a port that was never started, the transfer, then one more posted while the
        // port belongs to no engine at all — the window between the two engines' turns.
        host.Execute("""
            var side = new MessageChannel();
            side.port1.postMessage('queued-1');
            side.port1.postMessage('queued-2');
            port.postMessage('handover', [side.port2]);
            side.port1.postMessage('in-transit');
            """);

        worker.Execute("port.onmessage = function (e) { e.ports[0].onmessage = function (ev) { received.push(ev.data); }; };");
        worker.Advanced.ProcessTasks();

        worker.Evaluate("received.join(',')").AsString().Should().Be("queued-1,queued-2,in-transit");

        host.Execute("side.port1.postMessage('after');");
        worker.Advanced.ProcessTasks();
        worker.Evaluate("received.join(',')").AsString().Should().Be("queued-1,queued-2,in-transit,after");
    }

    /// <summary>
    /// The shape the whole design is for: the peer of a transferred port lives on an engine that is neither
    /// the sender nor the receiver, and never hears about the move.
    /// </summary>
    [Fact]
    public void APortRelayedThroughAThirdEngineStillTalksToTheFirst()
    {
        var a = MessagingEngine();
        var b = MessagingEngine();
        var c = MessagingEngine();

        Wire(a, b, "ab");
        Wire(b, c, "bc");

        // A keeps one end of a private channel and sends the other to B ...
        a.Execute("""
            var received = [];
            var side = new MessageChannel();
            side.port1.onmessage = function (e) { received.push(e.data); };
            side.port1.postMessage('queued before anything moved');
            ab.postMessage('for you', [side.port2]);
            """);

        // ... B does not even look at it, it just forwards it on to C ...
        b.Execute("ab.onmessage = function (e) { bc.postMessage('passing it on', [e.ports[0]]); };");
        b.Advanced.ProcessTasks();

        // ... and C ends up talking straight to A.
        c.Execute("var received = []; bc.onmessage = function (e) { var p = e.ports[0]; p.onmessage = function (ev) { received.push(ev.data); }; p.postMessage('hello from C'); };");
        c.Advanced.ProcessTasks();

        a.Advanced.ProcessTasks();
        a.Evaluate("received.join(',')").AsString().Should().Be("hello from C");

        // The message A queued before the first hop survived both of them, and arrives at C in order ahead of
        // anything posted afterwards.
        a.Execute("side.port1.postMessage('after two hops');");
        c.Advanced.ProcessTasks();
        c.Evaluate("received.join(',')").AsString().Should().Be("queued before anything moved,after two hops");

        // B was only ever a courier: it never bound the side, and its own port is untouched.
        b.Evaluate("typeof ab").AsString().Should().Be("object");
    }

    [Fact]
    public void ATransferredPortIsEndedWhenTheReceivingEngineRestores()
    {
        var (host, worker) = ConnectedPair();
        var snapshot = worker.Advanced.CaptureGlobalSnapshot();

        host.Execute("""
            var side = new MessageChannel();
            side.port1.onmessage = function (e) { received.push(e.data); };
            port.postMessage('handover', [side.port2]);
            """);

        // The worker ends the cycle before it ever runs the delivery job, so the side that was in flight can
        // never be bound to anything. It has to be ENDED rather than left waiting: the host's own port would
        // otherwise go on serializing into a queue nobody can ever drain.
        worker.Advanced.RestoreGlobalSnapshot(snapshot);
        worker.Advanced.ProcessTasks();

        host.Execute("side.port1.postMessage('into the void');");
        host.Advanced.ProcessTasks();
        host.Evaluate("received.length").AsNumber().Should().Be(0);

        // The port that was in flight was detached by the transfer, so it cannot be handed over again
        // either — the sender's half of the same fact.
        Refusal(host, "port.postMessage('again', [side.port2])").Should().Be("DataCloneError");
    }

    [Fact]
    public void ADoubleTransferOfTheSamePortIsRefused()
    {
        var (host, worker) = ConnectedPair();

        host.Execute("var side = new MessageChannel(); port.postMessage('first', [side.port2]);");

        Refusal(host, "port.postMessage('second', [side.port2])").Should().Be("DataCloneError");

        // The first transfer is unaffected: the worker still gets exactly one port.
        worker.Execute("var ports = 0; port.onmessage = function (e) { ports += e.ports.length; };");
        worker.Advanced.ProcessTasks();
        worker.Evaluate("ports").AsNumber().Should().Be(1);
    }

    [Fact]
    public void APortCannotBeSentThroughItself()
    {
        var (host, _) = ConnectedPair();

        Refusal(host, "port.postMessage('nope', [port])").Should().Be("DataCloneError");

        // Nothing was detached, so the channel is untouched.
        host.Execute("port.postMessage('still fine');");
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// The <c>DOMException</c> name <paramref name="body"/> is refused with, or <c>"no error"</c>.
    /// </summary>
    private static string Refusal(Engine engine, string body) => engine.Evaluate(
        "(function () { try { " + body + "; return 'no error'; } catch (e) { return e.name; } })()").AsString();

    /// <summary>
    /// Gives two engines a port each under <paramref name="name"/>, so a chain of engines can be wired up.
    /// </summary>
    private static void Wire(Engine first, Engine second, string name)
    {
        var pair = first.Advanced.CreateMessagePortPair(second);
        first.SetValue(name, pair.Local);
        second.SetValue(name, pair.Remote);
    }

    /// <summary>
    /// How many messages an engine has taken delivery of, read <b>without</b> pumping it — which
    /// <c>Evaluate</c> would do, since a drain is what it ends with.
    /// </summary>
    private static int ReceivedCount(Engine engine) => (int) engine.GetValue("received").Get("length").AsNumber();

    /// <summary>
    /// Two messaging engines wired together, each with a <c>port</c> global whose <c>onmessage</c> appends to
    /// a <c>received</c> array.
    /// </summary>
    private static (Engine Host, Engine Worker) ConnectedPair()
    {
        var host = MessagingEngine();
        var worker = MessagingEngine();

        var pair = host.Advanced.CreateMessagePortPair(worker);
        host.SetValue("port", pair.Local);
        worker.SetValue("port", pair.Remote);

        const string Setup = "var received = []; port.onmessage = function (e) { received.push(e.data); };";
        host.Execute(Setup);
        worker.Execute(Setup);

        return (host, worker);
    }
}
#endif
