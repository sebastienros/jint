#if NET8_0_OR_GREATER
#nullable enable

using System.Threading;
using Jint;
using Jint.Native;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>BroadcastChannel</c> seen from outside the assembly: what a host has to write to get it, and the one
/// setting it has — <see cref="Options.MessagingOptions.Broker"/>, which decides which engines are in earshot
/// of one another.
/// </summary>
/// <remarks>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party.
/// </para>
/// <para>
/// The cross-engine tests run every engine on the test's own thread and pump them with
/// <c>Advanced.ProcessTasks()</c>, which is the shape a host with its own loop uses. That is deliberate: two
/// threads would test the operating system's scheduler as much as the code, whereas pumping in turn tests
/// exactly the contract — a message serialized on one engine's turn is deserialized and dispatched on the
/// other engine's turn, and never in between.
/// </para>
/// </remarks>
public class WebApiBroadcastChannelTests
{
    private static Engine BroadcastEngine() => new(options => options.UseWebApis(WebApiFeatures.Messaging));

    private static Engine BroadcastEngine(BroadcastChannelBroker broker) =>
        new(options =>
        {
            options.UseWebApis(WebApiFeatures.Messaging);
            options.WebApi.Messaging.Broker = broker;
        });

    // ---------------------------------------------------------------- the opt-in

    [Fact]
    public void ADefaultEngineHasNoBroadcastChannel()
    {
        var engine = new Engine();

        engine.Evaluate("typeof BroadcastChannel").AsString().Should().Be("undefined");
        engine.Evaluate("'BroadcastChannel' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void TheMessagingFlagInstallsIt()
    {
        var engine = BroadcastEngine();

        engine.Evaluate("typeof BroadcastChannel").AsString().Should().Be("function");
        engine.Evaluate("new BroadcastChannel('x').name").AsString().Should().Be("x");
    }

    [Fact]
    public void TheDefaultSetIncludesIt()
    {
        new Engine(options => options.UseWebApis()).Evaluate("typeof BroadcastChannel").AsString().Should().Be("function");
    }

    [Fact]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new JsString("host's own BroadcastChannel");

        var engine = new Engine(options => options
            .AddLazyGlobal("BroadcastChannel", _ => marker)
            .UseWebApis(WebApiFeatures.Messaging));

        engine.Evaluate("BroadcastChannel").Should().Be(marker);

        // The names the host did not claim are still installed.
        engine.Evaluate("typeof MessageChannel").AsString().Should().Be("function");
    }

    [Fact]
    public void AShadowRealmHasNoBroadcastChannel()
    {
        BroadcastEngine().Evaluate("new ShadowRealm().evaluate('typeof BroadcastChannel')").AsString().Should().Be("undefined");
    }

    [Fact]
    public void EnablingMessagingOnALiveEngineBringsItToo()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));
        engine.Evaluate("typeof BroadcastChannel").AsString().Should().Be("undefined");

        engine.Advanced.EnableWebApis(WebApiFeatures.Messaging);

        engine.Execute("""
            var log = [];
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.onmessage = function (e) { log.push(e.data); };
            a.postMessage('live');
            """);

        engine.Evaluate("log.join(',')").AsString().Should().Be("live");
    }

    [Fact]
    public void ALiveEnableCanSupplyTheBroker()
    {
        var broker = new BroadcastChannelBroker();

        var listener = BroadcastEngine(broker);
        listener.Execute("var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };");

        var sender = new Engine(options => options.UseWebApis(WebApiFeatures.Console));
        sender.Advanced.EnableWebApis(WebApiFeatures.Messaging, webApi => webApi.Messaging.Broker = broker);

        sender.Execute("new BroadcastChannel('room').postMessage('from a live enable');");
        listener.Advanced.ProcessTasks();

        listener.Evaluate("received[0]").AsString().Should().Be("from a live enable");
    }

    [Fact]
    public void ALiveEnableCanSupplyTheBrokerToAnEngineThatAlreadyHasWebApiState()
    {
        var broker = new BroadcastChannelBroker();

        var listener = BroadcastEngine(broker);
        listener.Execute("var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };");

        // Timers already gave this engine its web-API state, so enabling messaging has to attach the broker to
        // the state that exists rather than create one — the other half of the live door.
        var sender = new Engine(options => options.UseWebApis(WebApiFeatures.Timers));
        sender.Advanced.EnableWebApis(WebApiFeatures.Messaging, webApi => webApi.Messaging.Broker = broker);

        sender.Execute("new BroadcastChannel('room').postMessage('attached to a live state');");
        listener.Advanced.ProcessTasks();

        listener.Evaluate("received[0]").AsString().Should().Be("attached to a live state");
    }

    // ---------------------------------------------------------------- the broker

    [Fact]
    public void EachEngineGetsAPrivateBrokerWhenTheHostNamesNone()
    {
        var first = BroadcastEngine();
        var second = BroadcastEngine();

        second.Execute("var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };");
        first.Execute("new BroadcastChannel('room').postMessage('should not cross');");

        second.Advanced.ProcessTasks();
        ReceivedCount(second).Should().Be(0);
    }

    [Fact]
    public void SharingOneBrokerIsWhatPutsTwoEnginesInEarshot()
    {
        var broker = new BroadcastChannelBroker();
        var (sender, listener) = (BroadcastEngine(broker), BroadcastEngine(broker));

        listener.Execute("var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };");

        sender.Execute("var c = new BroadcastChannel('room'); c.postMessage({ ask: 'ping', payload: [1, 2, 3] });");

        // Nothing has been pumped on the listener yet, so nothing has arrived.
        ReceivedCount(listener).Should().Be(0);

        listener.Advanced.ProcessTasks();
        listener.Evaluate("received.length").AsNumber().Should().Be(1);
        listener.Evaluate("received[0].ask").AsString().Should().Be("ping");
        listener.Evaluate("received[0].payload.join('-')").AsString().Should().Be("1-2-3");
    }

    [Fact]
    public void AnEngineWithoutTheSharedBrokerIsIsolated()
    {
        var broker = new BroadcastChannelBroker();
        var sender = BroadcastEngine(broker);
        var inside = BroadcastEngine(broker);
        var outside = BroadcastEngine();

        const string Setup = "var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };";
        inside.Execute(Setup);
        outside.Execute(Setup);

        sender.Execute("new BroadcastChannel('room').postMessage('members only');");

        inside.Advanced.ProcessTasks();
        outside.Advanced.ProcessTasks();

        inside.Evaluate("received[0]").AsString().Should().Be("members only");
        ReceivedCount(outside).Should().Be(0);
    }

    [Fact]
    public void OneBrokerReachesEveryEngineThatJoinedIt()
    {
        var broker = new BroadcastChannelBroker();
        var sender = BroadcastEngine(broker);
        var listeners = new[] { BroadcastEngine(broker), BroadcastEngine(broker), BroadcastEngine(broker) };

        foreach (var listener in listeners)
        {
            listener.Execute("var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };");
        }

        sender.Execute("new BroadcastChannel('room').postMessage('everyone');");

        foreach (var listener in listeners)
        {
            listener.Advanced.ProcessTasks();
            listener.Evaluate("received[0]").AsString().Should().Be("everyone");
        }
    }

    [Fact]
    public void OneOptionsInstanceSharedByTwoEnginesSharesItsBroker()
    {
        // The other spelling: not one broker assigned to two Options, but one Options used to build two
        // engines — which is the shape a host pooling engines from a single configuration has.
        var options = new Options();
        options.UseWebApis(WebApiFeatures.Messaging);
        options.WebApi.Messaging.Broker = new BroadcastChannelBroker();

        var sender = new Engine(options);
        var listener = new Engine(options);

        listener.Execute("var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };");
        sender.Execute("new BroadcastChannel('room').postMessage('shared options');");

        listener.Advanced.ProcessTasks();
        listener.Evaluate("received[0]").AsString().Should().Be("shared options");
    }

    [Fact]
    public void TwoEnginesSharingOptionsButNoBrokerStayPrivate()
    {
        // The default is deliberately per engine rather than one instance on the options object: two engines
        // built from one shared Options must not see each other's channels unless the host said so.
        var options = new Options();
        options.UseWebApis(WebApiFeatures.Messaging);

        var sender = new Engine(options);
        var listener = new Engine(options);

        listener.Execute("var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };");
        sender.Execute("new BroadcastChannel('room').postMessage('should not cross');");

        listener.Advanced.ProcessTasks();
        ReceivedCount(listener).Should().Be(0);
    }

    [Fact]
    public void ACrossEngineMessageIsACloneNotASharedObject()
    {
        var broker = new BroadcastChannelBroker();
        var sender = BroadcastEngine(broker);
        var listener = BroadcastEngine(broker);

        listener.Execute("var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };");
        sender.Execute("new BroadcastChannel('room').postMessage(new Map([['k', { n: 1 }]]));");
        listener.Advanced.ProcessTasks();

        // The receiving engine gets objects of its own realm, which is what makes sharing a JsValue across
        // engines unnecessary — and it is the only reason this is safe at all.
        listener.Evaluate("received[0] instanceof Map").AsBoolean().Should().BeTrue();
        listener.Evaluate("received[0].get('k').n").AsNumber().Should().Be(1);
        listener.Evaluate("Object.getPrototypeOf(received[0]) === Map.prototype").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void NothingIsDeliveredToAnEngineThatIsNeverPumped()
    {
        var broker = new BroadcastChannelBroker();
        var sender = BroadcastEngine(broker);
        var listener = BroadcastEngine(broker);

        listener.Execute("var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };");
        sender.Execute("new BroadcastChannel('room').postMessage('never');");

        // Jint starts no threads, so an engine nobody pumps runs nothing — the same contract the timers and
        // the message ports have. Note that the observation has to be a non-pumping one: Evaluate drains the
        // event loop when the script it ran has finished.
        ReceivedCount(listener).Should().Be(0);
        Thread.Sleep(50);
        ReceivedCount(listener).Should().Be(0);

        listener.Advanced.ProcessTasks();
        listener.Evaluate("received[0]").AsString().Should().Be("never");
    }

    [Fact]
    public void TwoEnginesInEarshotEachGetTheirOwnArrayBufferStorage()
    {
        var broker = new BroadcastChannelBroker();
        var sender = BroadcastEngine(broker);
        var first = BroadcastEngine(broker);
        var second = BroadcastEngine(broker);

        const string Setup = "var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };";
        first.Execute(Setup);
        second.Execute(Setup);

        sender.Execute("new BroadcastChannel('room').postMessage(new Uint8Array([1, 2, 3]));");

        first.Advanced.ProcessTasks();
        second.Advanced.ProcessTasks();

        // One serialization record, two deserializations, and each has to own its bytes — otherwise a write on
        // one engine's thread would be visible on another's, which is the one thing a message is never allowed
        // to be.
        first.Execute("received[0][0] = 99;");
        second.Evaluate("received[0][0]").AsNumber().Should().Be(1);
        first.Evaluate("received[0][0]").AsNumber().Should().Be(99);
    }

    // ---------------------------------------------------------------- the evaluation cycle

    [Fact]
    public void AMessageInFlightWhenTheReceiverRestoresIsDropped()
    {
        var broker = new BroadcastChannelBroker();
        var sender = BroadcastEngine(broker);
        var listener = BroadcastEngine(broker);

        listener.Execute("var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };");
        var snapshot = listener.Advanced.CaptureGlobalSnapshot();

        // Posted while the listener is in the cycle the channel belongs to, but not yet pumped.
        sender.Execute("new BroadcastChannel('room').postMessage('from the previous cycle');");

        listener.Advanced.RestoreGlobalSnapshot(snapshot);
        listener.Advanced.ProcessTasks();

        // A channel's listeners are closures over the cycle it was created in, so running them against the
        // restored globals is exactly the cross-cycle channel a restore forbids. `received` is part of the
        // snapshot, so it is back — and still empty.
        listener.Evaluate("received.length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void AMessagePostedAfterTheReceiverRestoresIsDropped()
    {
        var broker = new BroadcastChannelBroker();
        var sender = BroadcastEngine(broker);
        var listener = BroadcastEngine(broker);

        listener.Execute("var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };");
        var snapshot = listener.Advanced.CaptureGlobalSnapshot();

        listener.Advanced.RestoreGlobalSnapshot(snapshot);

        // The channel itself is what belonged to the ended cycle, so it does not matter that this message was
        // posted afterwards: it is no longer a subscriber and it is closed.
        sender.Execute("new BroadcastChannel('room').postMessage('too late');");
        listener.Advanced.ProcessTasks();

        // A fresh channel is what a pooled engine wants, and it works immediately.
        listener.Execute("var received = []; var fresh = new BroadcastChannel('room'); fresh.onmessage = function (e) { received.push(e.data); };");
        sender.Execute("new BroadcastChannel('room').postMessage('next cycle');");
        listener.Advanced.ProcessTasks();

        listener.Evaluate("received.join(',')").AsString().Should().Be("next cycle");
    }

    [Fact]
    public void ARestoreOnTheSenderDoesNotStopTheReceiver()
    {
        var broker = new BroadcastChannelBroker();
        var sender = BroadcastEngine(broker);
        var listener = BroadcastEngine(broker);

        listener.Execute("var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };");
        var snapshot = sender.Advanced.CaptureGlobalSnapshot();

        sender.Execute("new BroadcastChannel('room').postMessage('sent then restored');");
        sender.Advanced.RestoreGlobalSnapshot(snapshot);
        listener.Advanced.ProcessTasks();

        // The message was serialized when it was posted and carries nothing of the sender's, so the sender's
        // cycle ending afterwards has no bearing on it.
        listener.Evaluate("received[0]").AsString().Should().Be("sent then restored");
    }

    [Fact]
    public void DisposingAnEngineTakesItsChannelsOutOfASharedBroker()
    {
        var broker = new BroadcastChannelBroker();
        var sender = BroadcastEngine(broker);
        var listener = BroadcastEngine(broker);
        var leaving = BroadcastEngine(broker);

        const string Setup = "var received = []; var c = new BroadcastChannel('room'); c.onmessage = function (e) { received.push(e.data); };";
        listener.Execute(Setup);
        leaving.Execute(Setup);

        leaving.Dispose();

        sender.Execute("new BroadcastChannel('room').postMessage('after the dispose');");

        // The engine that is still here is unaffected; the one that left is not asked for a job at all, which
        // is what keeps a long-lived broker from retaining every engine that ever joined it.
        listener.Advanced.ProcessTasks();
        listener.Evaluate("received[0]").AsString().Should().Be("after the dispose");

        leaving.Advanced.ProcessTasks();
        ReceivedCount(leaving).Should().Be(0);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// How many messages an engine has taken delivery of, read <b>without</b> pumping it — which
    /// <c>Evaluate</c> would do, since a drain is what it ends with.
    /// </summary>
    private static int ReceivedCount(Engine engine) => (int) engine.GetValue("received").Get("length").AsNumber();
}
#endif
