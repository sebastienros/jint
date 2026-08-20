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

    // ---------------------------------------------------------------- helpers

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
