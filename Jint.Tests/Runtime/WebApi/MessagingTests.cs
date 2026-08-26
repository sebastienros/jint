#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Messaging;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>MessageChannel</c>, <c>MessagePort</c> and <c>MessageEvent</c> against the HTML Standard's web
/// messaging section — https://html.spec.whatwg.org/multipage/web-messaging.html.
/// </summary>
/// <remarks>
/// Everything here is one engine talking to itself through an entangled pair, which is what
/// <c>new MessageChannel()</c> gives. The cross-engine form of exactly the same pair is
/// <c>Engine.WebApi.CreateMessagePortPair</c>, exercised from a third party's side in
/// <c>Jint.Tests.PublicInterface.WebApiMessagingTests</c>.
/// <para>
/// The other half of every assertion is <i>when</i> delivery happens: a message is an event-loop task, and
/// <c>Engine.Execute</c> drains the loop once the script has finished — which is why a message posted by a
/// script has already been delivered when <c>Execute</c> returns.
/// </para>
/// </remarks>
public class MessagingTests
{
    private static Engine MessagingEngine(WebApiFeatures features = WebApiFeatures.Default)
    {
        var engine = new Engine(options => options.UseWebApis(features));

        engine.Execute("var log = [];");
        engine.Execute("function err(f) { try { f(); return 'no error'; } catch (e) { return e.name; } }");
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    private static string Err(Engine engine, string body) => engine.Evaluate("err(function() { " + body + " })").AsString();

    // ---------------------------------------------------------------- installation

    [Test]
    public void IsAbsentUntilTheFeatureIsEnabled()
    {
        var engine = new Engine();

        engine.Evaluate("typeof MessageChannel").AsString().Should().Be("undefined");
        engine.Evaluate("typeof MessagePort").AsString().Should().Be("undefined");
        engine.Evaluate("typeof MessageEvent").AsString().Should().Be("undefined");
    }

    [Test]
    public void IsInstalledByTheMessagingFeatureOnItsOwn()
    {
        // Messaging does not depend on the Events flag: MessagePort and MessageEvent inherit from the
        // EventTarget and Event *intrinsics*, which exist whether or not their globals were installed.
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Messaging));

        engine.Evaluate("typeof MessageChannel").AsString().Should().Be("function");
        engine.Evaluate("typeof MessagePort").AsString().Should().Be("function");
        engine.Evaluate("typeof MessageEvent").AsString().Should().Be("function");

        // ... and the Events globals really are absent, so this is not accidentally testing Default.
        engine.Evaluate("typeof EventTarget").AsString().Should().Be("undefined");

        // A port still has the EventTarget operations, because the prototype chain does not need the global.
        engine.Execute("var ch = new MessageChannel(); ch.port1.addEventListener('message', function () {});");
    }

    [Test]
    public void IsPartOfTheDefaultFeatureSet()
    {
        (WebApiFeatures.Default & WebApiFeatures.Messaging).Should().Be(WebApiFeatures.Messaging);
    }

    [Test]
    public void UsesTheBitTheEnumReserved()
    {
        ((int) WebApiFeatures.Messaging).Should().Be(1 << 14);
    }

    [Test]
    public void GivesEachGlobalTheAttributesWebIdlAsksFor()
    {
        var engine = MessagingEngine();

        foreach (var name in new[] { "MessageChannel", "MessagePort", "MessageEvent" })
        {
            // An interface object is writable and configurable but not enumerable —
            // https://webidl.spec.whatwg.org/#es-interfaces.
            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);
            descriptor.Writable.Should().BeTrue();
            descriptor.Configurable.Should().BeTrue();
            descriptor.Enumerable.Should().BeFalse();

            // Still unmaterialized: enabling a feature nobody uses costs one descriptor.
            (descriptor._flags & PropertyFlag.CustomJsValue).Should().NotBe(PropertyFlag.None);
        }
    }

    [Test]
    public void DoesNotClobberAGlobalTheHostRegistered()
    {
        var engine = new Engine(options => options
            .Configure(e => e.SetValue("MessageChannel", "mine"))
            .UseWebApis());

        engine.Evaluate("MessageChannel").AsString().Should().Be("mine");
    }

    [Test]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = MessagingEngine();

        engine.Evaluate("new ShadowRealm().evaluate('typeof MessageChannel')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof MessagePort')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof MessageChannel").AsString().Should().Be("function");
    }

    // ---------------------------------------------------------------- the interfaces

    [Test]
    public void ExposesMessageChannelAsAnInterfaceObject()
    {
        var engine = MessagingEngine();

        engine.Evaluate("MessageChannel.name").AsString().Should().Be("MessageChannel");
        engine.Evaluate("MessageChannel.length").AsNumber().Should().Be(0);
        engine.Evaluate("Object.getPrototypeOf(MessageChannel) === Function.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("MessageChannel.prototype[Symbol.toStringTag]").AsString().Should().Be("MessageChannel");

        // An interface object with a constructor operation is not callable without new.
        Err(engine, "MessageChannel()").Should().Be("TypeError");
    }

    [Test]
    public void GivesAChannelTwoDistinctEntangledPorts()
    {
        var engine = MessagingEngine();
        engine.Execute("var ch = new MessageChannel();");

        engine.Evaluate("ch.port1 !== ch.port2").AsBoolean().Should().BeTrue();

        // The accessors answer the same objects every time, which is what makes `ch.port1.onmessage = f`
        // followed by `ch.port1.postMessage(x)` refer to one port.
        engine.Evaluate("ch.port1 === ch.port1").AsBoolean().Should().BeTrue();
        engine.Evaluate("ch.port1 instanceof MessagePort").AsBoolean().Should().BeTrue();
        engine.Evaluate("ch.port1 instanceof EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(ch.port1)").AsString().Should().Be("[object MessagePort]");
    }

    [Test]
    public void RefusesToConstructAMessagePortDirectly()
    {
        var engine = MessagingEngine();

        // MessagePort declares no constructor operation — https://webidl.spec.whatwg.org/#es-interface-call.
        Err(engine, "new MessagePort()").Should().Be("TypeError");

        engine.Evaluate("Object.getPrototypeOf(MessagePort) === EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(MessagePort.prototype) === EventTarget.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("MessagePort.prototype.postMessage.length").AsNumber().Should().Be(1);
        engine.Evaluate("MessagePort.prototype.start.length").AsNumber().Should().Be(0);
        engine.Evaluate("MessagePort.prototype.close.length").AsNumber().Should().Be(0);
    }

    [Test]
    public void RefusesAnOperationOnAReceiverThatIsNotAPort()
    {
        var engine = MessagingEngine();

        Err(engine, "MessagePort.prototype.postMessage.call({}, 1)").Should().Be("TypeError");
        Err(engine, "MessagePort.prototype.start.call({})").Should().Be("TypeError");
        Err(engine, "MessagePort.prototype.close.call({})").Should().Be("TypeError");
        Err(engine, "Object.getOwnPropertyDescriptor(MessagePort.prototype, 'onmessage').get.call({})").Should().Be("TypeError");
    }

    [Test]
    public void SupportsSubclassingMessageChannel()
    {
        var engine = MessagingEngine();

        // OrdinaryCreateFromConstructor, so `class extends MessageChannel` gets the subclass prototype.
        engine.Execute("class C extends MessageChannel {}; var c = new C();");
        engine.Evaluate("c instanceof C").AsBoolean().Should().BeTrue();
        engine.Evaluate("c.port1 instanceof MessagePort").AsBoolean().Should().BeTrue();
    }

    // ---------------------------------------------------------------- delivery

    [Test]
    public void DeliversAMessageToTheEntangledPortOnceItIsStarted()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.onmessage = function (e) { log.push('got:' + e.data); };
            ch.port2.postMessage('hello');
            log.push('posted');
            """);

        // "posted" is pushed synchronously; the message is a task, so it arrives on the drain Execute runs
        // once the script has finished.
        Log(engine).Should().Be("posted,got:hello");
    }

    [Test]
    public void DeliversInBothDirections()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.onmessage = function (e) { log.push('p1:' + e.data); };
            ch.port2.onmessage = function (e) { log.push('p2:' + e.data); };
            ch.port1.postMessage('to2');
            ch.port2.postMessage('to1');
            """);

        Log(engine).Should().Be("p2:to2,p1:to1");
    }

    [Test]
    public void DeliversMessagesInTheOrderTheyWerePosted()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.onmessage = function (e) { log.push(e.data); };
            for (var i = 0; i < 5; i++) { ch.port2.postMessage(i); }
            """);

        Log(engine).Should().Be("0,1,2,3,4");
    }

    [Test]
    public void DeliversAMessageAsATaskSoMicrotasksRunFirst()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.onmessage = function (e) { log.push('message'); };
            ch.port2.postMessage(1);
            Promise.resolve().then(function () { log.push('microtask'); });
            """);

        // A browser puts port delivery on a task queue, so every already-queued promise reaction runs first.
        Log(engine).Should().Be("microtask,message");
    }

    /// <summary>
    /// The other half of the rule above, and the one a single message cannot show: a message is a task, so
    /// everything one listener queues runs before the <i>next</i> message is even looked at — the same
    /// checkpoint per message that each timer callback gets.
    /// </summary>
    /// <remarks>
    /// The mechanism is the order of the two lines at the end of <c>JsMessagePort.DrainOne</c>: the next
    /// delivery job is armed <i>after</i> the dispatch, in a finally, so a listener's reactions are queued
    /// ahead of it. Arming it first — which is what the code used to do, for a throw-resilience reason the
    /// finally keeps — put the second message ahead of the first message's microtasks and made this read
    /// <c>m1,m2,p1,p2</c>.
    /// </remarks>
    [Test]
    public void EachMessageGetsItsOwnMicrotaskCheckpoint()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.onmessage = function (e) {
                log.push('m' + e.data);
                Promise.resolve().then(function () { log.push('p' + e.data); });
            };
            ch.port2.postMessage(1);
            ch.port2.postMessage(2);
            """);

        Log(engine).Should().Be("m1,p1,m2,p2");
    }

    /// <summary>
    /// The property the arm-before-dispatch order was written for, kept: a listener that throws erupts from
    /// the pump, and the message behind it is still delivered on the next turn.
    /// </summary>
    [Test]
    public void AThrowingListenerDoesNotStrandTheMessagesBehindIt()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.onmessage = function (e) {
                log.push('m' + e.data);
                if (e.data === 1) { throw new Error('boom'); }
            };
            """);

        // The throw erupts from whatever is draining the loop, which for a script-driven post is Execute.
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => engine.Execute("""
            ch.port2.postMessage(1);
            ch.port2.postMessage(2);
            """));

        Log(engine).Should().Be("m1");

        engine.Tasks.ProcessTasks();
        Log(engine).Should().Be("m1,m2");
    }

    [Test]
    public void FiresATrustedMessageEventWithTheSpecifiedDefaults()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            var received = null;
            ch.port1.onmessage = function (e) { received = e; };
            ch.port2.postMessage('payload');
            """);

        engine.Evaluate("received instanceof MessageEvent").AsBoolean().Should().BeTrue();
        engine.Evaluate("received.type").AsString().Should().Be("message");
        engine.Evaluate("received.data").AsString().Should().Be("payload");
        engine.Evaluate("received.isTrusted").AsBoolean().Should().BeTrue();
        engine.Evaluate("received.target === ch.port1").AsBoolean().Should().BeTrue();

        // The message port post message steps set data and nothing else, so every other member is its IDL
        // default.
        engine.Evaluate("received.origin").AsString().Should().Be("");
        engine.Evaluate("received.lastEventId").AsString().Should().Be("");
        engine.Evaluate("received.source").Should().Be(JsValue.Null);
        engine.Evaluate("received.ports.length").AsNumber().Should().Be(0);
        engine.Evaluate("Object.isFrozen(received.ports)").AsBoolean().Should().BeTrue();
        engine.Evaluate("received.bubbles").AsBoolean().Should().BeFalse();
        engine.Evaluate("received.cancelable").AsBoolean().Should().BeFalse();
    }

    // ---------------------------------------------------------------- the port message queue

    [Test]
    public void HoldsMessagesUntilTheQueueIsEnabled()
    {
        var engine = MessagingEngine();

        // addEventListener does NOT enable the port message queue — the classic gotcha the specification
        // spells out by giving only onmessage the implicit start().
        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.addEventListener('message', function (e) { log.push(e.data); });
            ch.port2.postMessage('first');
            ch.port2.postMessage('second');
            """);

        Log(engine).Should().Be("");

        engine.Execute("ch.port1.start();");
        Log(engine).Should().Be("first,second");
    }

    [Test]
    public void EnablesTheQueueWhenOnMessageIsAssigned()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port2.postMessage('queued');
            """);

        Log(engine).Should().Be("");

        // "The first time a MessagePort object's onmessage IDL attribute is set, the port's port message
        // queue must be enabled, as if the start() method had been called."
        engine.Execute("ch.port1.onmessage = function (e) { log.push(e.data); };");
        Log(engine).Should().Be("queued");
    }

    [Test]
    public void AssigningOnMessageStartsThePortEvenForAListenerRegisteredSeparately()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.addEventListener('message', function (e) { log.push('listener:' + e.data); });
            ch.port2.postMessage('x');
            """);

        Log(engine).Should().Be("");

        // Assigning null still counts as setting the attribute, so the queue is enabled and the separately
        // registered listener starts receiving.
        engine.Execute("ch.port1.onmessage = null;");
        Log(engine).Should().Be("listener:x");
    }

    [Test]
    public void KeepsOrderWhenTheQueueIsEnabledMidFlight()
    {
        var engine = MessagingEngine();

        // The interleaving that catches a port which dispatches on arrival instead of taking the head of its
        // own queue. 'a' arrives while the queue is disabled and waits on it; 'b' is posted before start()
        // but arrives after it, so a port that dispatched arrivals directly would run 'b' first and only
        // then get round to the 'a' it had parked. The two microtasks are what puts 'b' in flight across the
        // moment the queue is enabled.
        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.addEventListener('message', function (e) { log.push(e.data); });
            ch.port2.postMessage('a');
            Promise.resolve().then(function () { ch.port2.postMessage('b'); });
            Promise.resolve().then(function () { ch.port1.start(); });
            """);

        Log(engine).Should().Be("a,b");
    }

    [Test]
    public void StartingAnAlreadyStartedPortIsANoOp()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.onmessage = function (e) { log.push(e.data); };
            ch.port1.start();
            ch.port1.start();
            ch.port2.postMessage('once');
            """);

        Log(engine).Should().Be("once");
    }

    // ---------------------------------------------------------------- close

    [Test]
    public void StopsDeliveringOnceTheReceivingPortIsClosed()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.onmessage = function (e) { log.push(e.data); };
            ch.port1.close();
            ch.port2.postMessage('lost');
            """);

        Log(engine).Should().Be("");
    }

    [Test]
    public void ClosingAPortDisentanglesThePairInBothDirections()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port2.onmessage = function (e) { log.push('p2:' + e.data); };
            ch.port1.onmessage = function (e) { log.push('p1:' + e.data); };
            ch.port1.close();
            ch.port1.postMessage('from-closed');
            ch.port2.postMessage('to-closed');
            """);

        // "Set this's [[Detached]] to true. If this is entangled, disentangle it." — so neither direction
        // has a target any more. The peer is not itself closed, it simply has nowhere to post.
        Log(engine).Should().Be("");
    }

    [Test]
    public void DropsWhatAClosedPortHadStillQueued()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.addEventListener('message', function (e) { log.push(e.data); });
            ch.port2.postMessage('waiting');
            """);

        engine.Execute("ch.port1.close(); ch.port1.start();");
        Log(engine).Should().Be("");
    }

    [Test]
    public void ClosingAPortTwiceIsHarmless()
    {
        var engine = MessagingEngine();

        engine.Execute("var ch = new MessageChannel(); ch.port1.close(); ch.port1.close(); ch.port2.close();");
        Log(engine).Should().Be("");
    }

    [Test]
    public void StillSerializesWhenThereIsNowhereToDeliver()
    {
        var engine = MessagingEngine();
        engine.Execute("var ch = new MessageChannel(); ch.port1.close();");

        // Step 5 (serialize) comes before step 6 (return if there is no target), and the order is
        // observable: an uncloneable message still throws, on the caller, synchronously.
        Err(engine, "ch.port1.postMessage(function () {})").Should().Be("DataCloneError");

        // ... and a transfer named on a message that goes nowhere still detaches its buffer.
        engine.Execute("var buffer = new ArrayBuffer(4); ch.port1.postMessage(0, [buffer]);");
        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
    }

    // ---------------------------------------------------------------- the message itself

    [Test]
    public void DeliversAStructuredCloneRatherThanTheValueItself()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            var received = null;
            ch.port1.onmessage = function (e) { received = e.data; };
            var sent = { nested: { n: 1 }, list: [1, 2], when: new Date(0), map: new Map([['k', 'v']]) };
            sent.self = sent;
            ch.port2.postMessage(sent);
            """);

        engine.Evaluate("received === sent").AsBoolean().Should().BeFalse();
        engine.Evaluate("received.nested.n").AsNumber().Should().Be(1);
        engine.Evaluate("received.list.join('-')").AsString().Should().Be("1-2");
        engine.Evaluate("received.when instanceof Date").AsBoolean().Should().BeTrue();
        engine.Evaluate("received.map.get('k')").AsString().Should().Be("v");

        // The graph's own sharing survives, which is what makes it a clone of the graph and not of a tree.
        engine.Evaluate("received.self === received").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void SerializesAtPostTimeRatherThanAtDeliveryTime()
    {
        var engine = MessagingEngine();

        // Per the specification postMessage runs StructuredSerializeWithTransfer synchronously, so a
        // mutation made after the call cannot reach the message.
        engine.Execute("""
            var ch = new MessageChannel();
            var received = null;
            ch.port1.onmessage = function (e) { received = e.data; };
            var sent = { n: 1 };
            ch.port2.postMessage(sent);
            sent.n = 2;
            """);

        engine.Evaluate("received.n").AsNumber().Should().Be(1);
    }

    [Test]
    public void RefusesAnUncloneableMessageSynchronously()
    {
        var engine = MessagingEngine();
        engine.Execute("var ch = new MessageChannel();");

        Err(engine, "ch.port1.postMessage(function () {})").Should().Be("DataCloneError");
        Err(engine, "ch.port1.postMessage(Symbol('s'))").Should().Be("DataCloneError");
        Err(engine, "ch.port1.postMessage(new Promise(function () {}))").Should().Be("DataCloneError");
    }

    [Test]
    public void RequiresTheMessageArgument()
    {
        var engine = MessagingEngine();
        engine.Execute("var ch = new MessageChannel();");

        Err(engine, "ch.port1.postMessage()").Should().Be("TypeError");

        // ... but an explicit undefined is a message like any other.
        engine.Execute("ch.port2.onmessage = function (e) { log.push(typeof e.data); }; ch.port1.postMessage(undefined);");
        Log(engine).Should().Be("undefined");
    }

    // ---------------------------------------------------------------- transfer

    [Test]
    public void TransfersAnArrayBufferThroughTheChannel()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            var received = null;
            ch.port1.onmessage = function (e) { received = e.data; };
            var buffer = new ArrayBuffer(4);
            new Uint8Array(buffer)[0] = 42;
            ch.port2.postMessage({ payload: buffer }, [buffer]);
            """);

        // The storage moved: the sender's buffer is detached and the bytes arrived intact.
        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
        engine.Evaluate("received.payload.byteLength").AsNumber().Should().Be(4);
        engine.Evaluate("new Uint8Array(received.payload)[0]").AsNumber().Should().Be(42);
    }

    [Test]
    public void AcceptsTheTransferListEitherWay()
    {
        var engine = MessagingEngine();

        // WebIDL picks between postMessage(message, sequence<object>) and
        // postMessage(message, StructuredSerializeOptions) by asking whether the second argument is iterable.
        engine.Execute("""
            var ch = new MessageChannel();
            var a = new ArrayBuffer(2);
            var b = new ArrayBuffer(2);
            ch.port1.postMessage(a, [a]);
            ch.port1.postMessage(b, { transfer: [b] });
            """);

        engine.Evaluate("a.byteLength").AsNumber().Should().Be(0);
        engine.Evaluate("b.byteLength").AsNumber().Should().Be(0);
    }

    [Test]
    public void RefusesAMalformedTransferOption()
    {
        var engine = MessagingEngine();
        engine.Execute("var ch = new MessageChannel();");

        Err(engine, "ch.port1.postMessage(0, 5)").Should().Be("TypeError");
        Err(engine, "ch.port1.postMessage(0, [1])").Should().Be("TypeError");
        Err(engine, "ch.port1.postMessage(0, { transfer: 5 })").Should().Be("TypeError");
    }

    // ---------------------------------------------------------------- transferring a port

    /// <summary>
    /// A relay channel plus a channel to hand over: <c>relay</c> carries the transfer, <c>ch</c> is the
    /// channel whose <c>port2</c> gets moved. The receiving side of the relay adopts whatever port arrives.
    /// </summary>
    private const string HandoverSetup = """
        var relay = new MessageChannel();
        var ch = new MessageChannel();
        var moved = null;
        relay.port2.onmessage = function (e) {
            moved = e.ports[0];
            moved.onmessage = function (ev) { log.push(ev.data); };
        };
        """;

    [Test]
    public void TransfersAPortThroughTheChannel()
    {
        var engine = MessagingEngine();

        engine.Execute(HandoverSetup + "relay.port1.postMessage('handover', [ch.port2]);");

        // The receiver got a real MessagePort of its own realm, not a clone of the object.
        engine.Evaluate("moved instanceof MessagePort").AsBoolean().Should().BeTrue();
        engine.Evaluate("moved === ch.port2").AsBoolean().Should().BeFalse();

        // ... and it is entangled with the peer the transferred port had, which never learned anything
        // happened: a message posted on ch.port1 now arrives at the port the relay delivered.
        engine.Execute("ch.port1.postMessage('through the moved port');");
        Log(engine).Should().Be("through the moved port");

        // The other direction works too.
        engine.Execute("ch.port1.onmessage = function (e) { log.push('back:' + e.data); }; moved.postMessage('reply');");
        Log(engine).Should().Be("through the moved port,back:reply");
    }

    [Test]
    public void LeavesTheTransferredPortInert()
    {
        var engine = MessagingEngine();

        engine.Execute(HandoverSetup + """
            ch.port2.onmessage = function (e) { log.push('old port saw ' + e.data); };
            relay.port1.postMessage('handover', [ch.port2]);
            """);

        // [[Detached]]: postMessage is a silent no-op rather than a throw, and nothing will ever fire on the
        // detached object again — not even the listener it still carries.
        engine.Execute("ch.port2.postMessage('from the detached port'); ch.port1.postMessage('to the detached port');");
        engine.Execute("ch.port2.start(); ch.port2.close();");

        Log(engine).Should().Be("to the detached port");
        engine.Evaluate("log.length").AsNumber().Should().Be(1);
    }

    /// <summary>
    /// The port message queue travels with the port, and everything in flight keeps its place in it — which is
    /// what HTML gets by passing the queue itself into the data holder rather than a copy of its contents.
    /// </summary>
    [Test]
    public void CarriesTheQueuedMessagesWithTheTransferredPortAndKeepsTheirOrder()
    {
        var engine = MessagingEngine();

        // Two messages queued on a port that was never started, then the transfer, then one more posted while
        // the port is in transit — no object owns that channel side at that instant. All three must arrive, in
        // this order, on the port the transfer created.
        engine.Execute(HandoverSetup + """
            ch.port1.postMessage('queued-before-1');
            ch.port1.postMessage('queued-before-2');
            relay.port1.postMessage('handover', [ch.port2]);
            ch.port1.postMessage('posted-in-transit');
            """);

        Log(engine).Should().Be("queued-before-1,queued-before-2,posted-in-transit");

        // ... and the channel keeps working afterwards, so nothing about the handover left it half-drained.
        engine.Execute("ch.port1.postMessage('after');");
        Log(engine).Should().Be("queued-before-1,queued-before-2,posted-in-transit,after");
    }

    [Test]
    public void CarriesAStartedPortsUndeliveredBacklog()
    {
        var engine = MessagingEngine();

        // The specification permits transferring a port whose queue is already enabled; the new port starts
        // disabled again ("leaving value's port message queue in its initial disabled state"), so the backlog
        // waits for the receiver's own start().
        engine.Execute(HandoverSetup + """
            relay.port2.onmessage = function (e) { moved = e.ports[0]; };
            ch.port2.onmessage = function () { log.push('never'); };
            ch.port1.postMessage('backlog');
            relay.port1.postMessage('handover', [ch.port2]);
            """);

        // Nothing was delivered anywhere: the transfer happened synchronously inside postMessage, before the
        // event-loop task that would have dispatched 'backlog' to the old port ever ran.
        Log(engine).Should().Be("");

        engine.Execute("moved.onmessage = function (e) { log.push(e.data); };");
        Log(engine).Should().Be("backlog");
    }

    [Test]
    public void RefusesToTransferAPortThroughItself()
    {
        var engine = MessagingEngine();
        engine.Execute("var ch = new MessageChannel();");

        // Message port post message steps, step 2 — and it is decided before serialization, so nothing else
        // in the list is transferred either.
        engine.Execute("var buffer = new ArrayBuffer(4);");
        Err(engine, "ch.port1.postMessage(0, [buffer, ch.port1])").Should().Be("DataCloneError");
        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(4);
        engine.Evaluate("ch.port1.postMessage('still works') === undefined").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void DoomsAMessageThatTransfersTheEntangledPeer()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port2.onmessage = function (e) { log.push(e.data); };
            ch.port1.postMessage('doomed', [ch.port2]);
            """);

        // Step 4 dooms rather than refuses: no exception, and nothing is delivered.
        Log(engine).Should().Be("");

        // ... but step 5 still ran, so the peer really was transferred — and since the message carrying it can
        // never be picked up, the channel is lost, exactly as the specification's own note says.
        engine.Execute("ch.port1.postMessage('after the dooming');");
        Log(engine).Should().Be("");

        // Script cannot tell dooming from delivering-into-a-port-nobody-owns, because both are silent. The
        // difference is on the side: doomed means the record was never queued at all and the transfer it
        // carried was ended, rather than a record sitting in a queue that references the very side it is
        // sitting in.
        var sender = (JsMessagePort) engine.Evaluate("ch.port1");
        sender.Endpoint!.Peer!.Closed.Should().BeTrue();
    }

    [Test]
    public void EndsEveryTransferStrandedBehindAClosedPortHoweverDeeplyNested()
    {
        var engine = MessagingEngine();

        // x2 is never started, so everything posted to it piles up on its side. The first message carries
        // y2, and — because y2's side is then unbound — the second message piles up behind THAT, carrying z2.
        engine.Execute("""
            var x = new MessageChannel();
            var y = new MessageChannel();
            var z = new MessageChannel();
            x.port1.postMessage('carrying y2', [y.port2]);
            y.port1.postMessage('carrying z2', [z.port2]);
            """);

        var y1 = (JsMessagePort) engine.Evaluate("y.port1");
        var z1 = (JsMessagePort) engine.Evaluate("z.port1");
        y1.Endpoint!.Peer!.Closed.Should().BeFalse();
        z1.Endpoint!.Peer!.Closed.Should().BeFalse();

        // Closing the one port at the head of the chain has to end all of it: every side behind it is
        // unreachable, and each would otherwise go on accepting messages from a peer that is still alive.
        engine.Execute("x.port2.close();");

        y1.Endpoint!.Peer!.Closed.Should().BeTrue();
        z1.Endpoint!.Peer!.Closed.Should().BeTrue();
    }

    [Test]
    public void RefusesAPortThatAppearsTwiceInOneTransferList()
    {
        var engine = MessagingEngine();
        engine.Execute("var relay = new MessageChannel(); var ch = new MessageChannel();");

        // StructuredSerializeWithTransfer step 2.3, which is checked before anything is detached.
        Err(engine, "relay.port1.postMessage(0, [ch.port2, ch.port2])").Should().Be("DataCloneError");

        // The port is untouched, so it can still be transferred properly.
        engine.Execute("relay.port2.onmessage = function (e) { log.push(e.ports.length); }; relay.port1.postMessage(0, [ch.port2]);");
        Log(engine).Should().Be("1");
    }

    [Test]
    public void RefusesToTransferAnAlreadyDetachedPort()
    {
        var engine = MessagingEngine();

        engine.Execute(HandoverSetup + "relay.port1.postMessage('handover', [ch.port2]);");

        // StructuredSerializeWithTransfer step 5.2, for a port a previous transfer detached ...
        Err(engine, "relay.port1.postMessage(0, [ch.port2])").Should().Be("DataCloneError");

        // ... and for one close() detached.
        engine.Execute("var other = new MessageChannel(); other.port1.close();");
        Err(engine, "relay.port1.postMessage(0, [other.port1])").Should().Be("DataCloneError");
    }

    [Test]
    public void RefusesAPortThatWasNotInTheTransferList()
    {
        var engine = MessagingEngine();
        engine.Execute("var ch = new MessageChannel(); var other = new MessageChannel();");

        // A MessagePort is transferable but not serializable, so putting one in the message without naming it
        // in the transfer list is the plain "platform object that is not serializable" refusal.
        Err(engine, "ch.port1.postMessage(other.port1)").Should().Be("DataCloneError");
        Err(engine, "ch.port1.postMessage({ nested: other.port1 })").Should().Be("DataCloneError");

        // ... and the untransferred port is unharmed.
        engine.Execute("other.port2.onmessage = function (e) { log.push(e.data); }; other.port1.postMessage('fine');");
        Log(engine).Should().Be("fine");
    }

    [Test]
    public void ResolvesAPortThatIsBothTransferredAndReferencedToOneObject()
    {
        var engine = MessagingEngine();

        // StructuredDeserializeWithTransfer creates the transferred values BEFORE it walks the graph, so a
        // reference to the port from inside the message resolves to the very object `ports` hands over.
        engine.Execute("""
            var relay = new MessageChannel();
            var ch = new MessageChannel();
            var event = null;
            relay.port2.onmessage = function (e) { event = e; };
            relay.port1.postMessage({ port: ch.port2, again: [ch.port2] }, [ch.port2]);
            """);

        engine.Evaluate("event.data.port === event.ports[0]").AsBoolean().Should().BeTrue();
        engine.Evaluate("event.data.again[0] === event.ports[0]").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void GivesTheEventAFrozenPortsArrayInTransferListOrder()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var relay = new MessageChannel();
            var first = new MessageChannel();
            var second = new MessageChannel();
            var event = null;
            relay.port2.onmessage = function (e) { event = e; };
            relay.port1.postMessage('two ports', [second.port2, first.port2]);
            """);

        engine.Evaluate("event.ports.length").AsNumber().Should().Be(2);
        engine.Evaluate("Object.isFrozen(event.ports)").AsBoolean().Should().BeTrue();
        engine.Evaluate("Array.isArray(event.ports)").AsBoolean().Should().BeTrue();
        engine.Evaluate("event.ports === event.ports").AsBoolean().Should().BeTrue();

        // Transfer-list order, not creation order: ports[0] is second's half.
        engine.Execute("""
            event.ports[0].onmessage = function (e) { log.push('0:' + e.data); };
            event.ports[1].onmessage = function (e) { log.push('1:' + e.data); };
            second.port1.postMessage('from second');
            first.port1.postMessage('from first');
            """);

        Log(engine).Should().Be("0:from second,1:from first");
    }

    [Test]
    public void TransfersBothEndsOfAChannelInOneMessage()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var relay = new MessageChannel();
            var ch = new MessageChannel();
            relay.port2.onmessage = function (e) {
                e.ports[0].onmessage = function (ev) { log.push(ev.data); };
                e.ports[1].postMessage('both ends moved');
            };
            relay.port1.postMessage('handover', [ch.port1, ch.port2]);
            """);

        // Each side was re-pointed independently, and they are still each other's peer.
        Log(engine).Should().Be("both ends moved");
    }

    [Test]
    public void RelaysAPortThroughSeveralTransfers()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var first = new MessageChannel();
            var second = new MessageChannel();
            var ch = new MessageChannel();

            // The port arriving on first.port2 is immediately forwarded through the second relay.
            first.port2.onmessage = function (e) { second.port1.postMessage('forwarded', [e.ports[0]]); };
            second.port2.onmessage = function (e) { e.ports[0].onmessage = function (ev) { log.push(ev.data); }; };

            ch.port1.postMessage('queued at the very start');
            first.port1.postMessage('handover', [ch.port2]);
            """);

        // The one message survived two hops, and the channel still works at the far end.
        Log(engine).Should().Be("queued at the very start");

        engine.Execute("ch.port1.postMessage('after two hops');");
        Log(engine).Should().Be("queued at the very start,after two hops");
    }

    [Test]
    public void TransfersAPortThroughStructuredCloneIntoTheSameRealm()
    {
        var engine = MessagingEngine();

        // A transfer needs a target realm, not a target agent, so structuredClone can do it — and does in
        // every browser. The clone is entangled with the original's peer and the original is detached.
        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.onmessage = function (e) { log.push(e.data); };
            var clone = structuredClone(ch.port2, { transfer: [ch.port2] });
            clone.onmessage = function (e) { log.push('clone got ' + e.data); };
            clone.postMessage('from the clone');
            ch.port1.postMessage('to the clone');
            """);

        engine.Evaluate("clone instanceof MessagePort").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone === ch.port2").AsBoolean().Should().BeFalse();
        engine.Evaluate("ch.port2.postMessage('inert') === undefined").AsBoolean().Should().BeTrue();

        Log(engine).Should().Be("from the clone,clone got to the clone");
    }

    [Test]
    public void StructuredCloneStillRefusesAPortThatIsNotTransferred()
    {
        var engine = MessagingEngine();
        engine.Execute("var ch = new MessageChannel();");

        Err(engine, "structuredClone(ch.port1)").Should().Be("DataCloneError");

        // The refusal did not detach it.
        engine.Execute("ch.port2.onmessage = function (e) { log.push(e.data); }; ch.port1.postMessage('unharmed');");
        Log(engine).Should().Be("unharmed");
    }

    [Test]
    public void EndsAPortWhoseTransferCanNeverBePickedUp()
    {
        var engine = MessagingEngine();

        // The relay's target is closed, so the message is serialized (the port really is detached) and then
        // dropped. Nothing can ever bind that channel side, so it is closed rather than left waiting — which
        // is what the peer observes.
        engine.Execute("""
            var relay = new MessageChannel();
            var ch = new MessageChannel();
            relay.port2.close();
            relay.port1.postMessage('nowhere', [ch.port2]);
            ch.port1.onmessage = function (e) { log.push(e.data); };
            ch.port1.postMessage('into the void');
            """);

        engine.Evaluate("ch.port2.postMessage('inert') === undefined").AsBoolean().Should().BeTrue();
        Log(engine).Should().Be("");

        // Script cannot tell "closed" from "waiting for a receiver that will never come" — both make
        // postMessage a no-op — so the assertion that matters is on the side itself. A side left merely
        // unbound would go on accepting everything ch.port1 ever posts, into a queue nothing can drain.
        var remaining = (JsMessagePort) engine.Evaluate("ch.port1");
        remaining.Endpoint!.Peer!.Closed.Should().BeTrue();
    }

    [Test]
    public void EndsAPortInFlightToAnEngineThatRestores()
    {
        var host = MessagingEngine();
        var worker = MessagingEngine();

        var pair = host.WebApi.CreateMessagePortPair(worker);
        host.SetValue("port", pair.Local);
        worker.SetValue("port", pair.Remote);

        var snapshot = worker.Advanced.CaptureGlobalSnapshot();

        host.Execute("var ch = new MessageChannel(); port.postMessage('handover', [ch.port2]);");

        // The delivery job carries the worker's ended cycle, so it is discarded and the side that was in
        // flight can never be bound. The restore is what has to end it: the fence stops delivery, it does not
        // stop the host from serializing into a queue forever.
        worker.Advanced.RestoreGlobalSnapshot(snapshot);
        worker.Tasks.ProcessTasks();

        var remaining = (JsMessagePort) host.Evaluate("ch.port1");
        remaining.Endpoint!.Peer!.Closed.Should().BeTrue();

        // The port the host kept is still perfectly usable as an object; it simply has nowhere to post.
        host.Execute("ch.port1.postMessage('into the void');");
    }

    [Test]
    public void BroadcastChannelStillRefusesAPort()
    {
        var engine = MessagingEngine();
        engine.Execute("var ch = new MessageChannel(); var bc = new BroadcastChannel('room');");

        // BroadcastChannel's postMessage takes no transfer list at all — a message with several destinations
        // has nowhere to move a transferable to — so a port in the message is simply uncloneable.
        Err(engine, "bc.postMessage(ch.port1)").Should().Be("DataCloneError");
        Err(engine, "bc.postMessage(ch.port1, [ch.port1])").Should().Be("DataCloneError");

        engine.Execute("ch.port2.onmessage = function (e) { log.push(e.data); }; ch.port1.postMessage('unharmed');");
        Log(engine).Should().Be("unharmed");
    }

    // ---------------------------------------------------------------- listeners

    [Test]
    public void RunsTheHandlerAttributeInRegistrationOrderAmongTheListeners()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.addEventListener('message', function () { log.push('first'); });
            ch.port1.onmessage = function () { log.push('handler'); };
            ch.port1.addEventListener('message', function () { log.push('last'); });
            ch.port2.postMessage(0);
            """);

        Log(engine).Should().Be("first,handler,last");
    }

    [Test]
    public void ReadsBackAndReplacesTheHandlerAttribute()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            var f = function () { log.push('f'); };
            ch.port1.onmessage = f;
            log.push(ch.port1.onmessage === f);
            ch.port1.onmessage = function () { log.push('g'); };
            ch.port2.postMessage(0);
            """);

        Log(engine).Should().Be("true,g");

        // EventHandler is [LegacyTreatNonObjectAsNull], so a non-object clears it rather than throwing.
        engine.Execute("ch.port1.onmessage = 5; log.push(ch.port1.onmessage === null); ch.port2.postMessage(0);");
        Log(engine).Should().Be("true,g,true");
    }

    [Test]
    public void ExposesOnMessageErrorButNeverFiresIt()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.onmessageerror = function () { log.push('messageerror'); };
            ch.port2.postMessage('fine');
            """);

        // A record this engine's own serializer built always deserializes, so messageerror is reachable by
        // dispatchEvent alone. Assigning it also does not start the port, unlike onmessage.
        Log(engine).Should().Be("");

        engine.Execute("ch.port1.dispatchEvent(new MessageEvent('messageerror'));");
        Log(engine).Should().Be("messageerror");
    }

    [Test]
    public void LetsAListenerRemoveItselfWithOnce()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.addEventListener('message', function (e) { log.push(e.data); }, { once: true });
            ch.port1.start();
            ch.port2.postMessage('a');
            ch.port2.postMessage('b');
            """);

        Log(engine).Should().Be("a");
    }

    [Test]
    public void DeliversTheRestOfTheQueueAfterAListenerThrows()
    {
        var engine = MessagingEngine();

        // The throw erupts from whatever is pumping — here the drain Execute runs once the script has
        // finished, exactly as it does for a timer callback or an unhandled promise reaction.
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => engine.Execute("""
            var ch = new MessageChannel();
            ch.port1.onmessage = function (e) { log.push(e.data); if (e.data === 'a') { throw new Error('boom'); } };
            ch.port2.postMessage('a');
            ch.port2.postMessage('b');
            """));

        Log(engine).Should().Be("a");

        // The next message's job was armed before this one was dispatched, so the port is not stranded: the
        // rest of the queue arrives on the next pump.
        engine.Tasks.ProcessTasks();
        Log(engine).Should().Be("a,b");
    }

    // ---------------------------------------------------------------- MessageEvent

    [Test]
    public void ConstructsAMessageEvent()
    {
        var engine = MessagingEngine();

        engine.Evaluate("MessageEvent.name").AsString().Should().Be("MessageEvent");
        engine.Evaluate("MessageEvent.length").AsNumber().Should().Be(1);
        engine.Evaluate("Object.getPrototypeOf(MessageEvent) === Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(MessageEvent.prototype) === Event.prototype").AsBoolean().Should().BeTrue();

        Err(engine, "new MessageEvent()").Should().Be("TypeError");

        engine.Execute("var ev = new MessageEvent('m');");
        engine.Evaluate("ev instanceof Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("ev.type").AsString().Should().Be("m");
        engine.Evaluate("ev.isTrusted").AsBoolean().Should().BeFalse();
        engine.Evaluate("ev.data").Should().Be(JsValue.Null);
        engine.Evaluate("ev.origin").AsString().Should().Be("");
        engine.Evaluate("ev.lastEventId").AsString().Should().Be("");
        engine.Evaluate("ev.source").Should().Be(JsValue.Null);
        engine.Evaluate("ev.ports.length").AsNumber().Should().Be(0);
    }

    [Test]
    public void ReadsTheMessageEventInitDictionary()
    {
        var engine = MessagingEngine();

        engine.Execute("""
            var ch = new MessageChannel();
            var ev = new MessageEvent('m', {
                bubbles: true,
                data: { n: 1 },
                origin: 'https://example.test',
                lastEventId: '7',
                source: ch.port1,
                ports: [ch.port1, ch.port2]
            });
            """);

        engine.Evaluate("ev.bubbles").AsBoolean().Should().BeTrue();
        engine.Evaluate("ev.data.n").AsNumber().Should().Be(1);
        engine.Evaluate("ev.origin").AsString().Should().Be("https://example.test");
        engine.Evaluate("ev.lastEventId").AsString().Should().Be("7");
        engine.Evaluate("ev.source === ch.port1").AsBoolean().Should().BeTrue();

        // FrozenArray<MessagePort>: frozen, and the same object on every read.
        engine.Evaluate("ev.ports.length").AsNumber().Should().Be(2);
        engine.Evaluate("ev.ports[1] === ch.port2").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.isFrozen(ev.ports)").AsBoolean().Should().BeTrue();
        engine.Evaluate("ev.ports === ev.ports").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void RefusesAMessageEventInitMemberOfTheWrongType()
    {
        var engine = MessagingEngine();

        Err(engine, "new MessageEvent('m', { source: {} })").Should().Be("TypeError");
        Err(engine, "new MessageEvent('m', { ports: [{}] })").Should().Be("TypeError");
        Err(engine, "new MessageEvent('m', 5)").Should().Be("TypeError");
    }

    [Test]
    public void RefusesAMessageEventAccessorOnAPlainEvent()
    {
        var engine = MessagingEngine();

        Err(engine, "Object.getOwnPropertyDescriptor(MessageEvent.prototype, 'data').get.call(new Event('x'))")
            .Should().Be("TypeError");
    }
}
#endif
