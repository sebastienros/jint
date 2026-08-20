#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>MessageChannel</c>, <c>MessagePort</c> and <c>MessageEvent</c> against the HTML Standard's web
/// messaging section — https://html.spec.whatwg.org/multipage/web-messaging.html.
/// </summary>
/// <remarks>
/// Everything here is one engine talking to itself through an entangled pair, which is what
/// <c>new MessageChannel()</c> gives. The cross-engine form of exactly the same pair is
/// <c>Engine.Advanced.CreateMessagePortPair</c>, exercised from a third party's side in
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

    [Fact]
    public void IsAbsentUntilTheFeatureIsEnabled()
    {
        var engine = new Engine();

        engine.Evaluate("typeof MessageChannel").AsString().Should().Be("undefined");
        engine.Evaluate("typeof MessagePort").AsString().Should().Be("undefined");
        engine.Evaluate("typeof MessageEvent").AsString().Should().Be("undefined");
    }

    [Fact]
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

    [Fact]
    public void IsPartOfTheDefaultFeatureSet()
    {
        (WebApiFeatures.Default & WebApiFeatures.Messaging).Should().Be(WebApiFeatures.Messaging);
    }

    [Fact]
    public void UsesTheBitTheEnumReserved()
    {
        ((int) WebApiFeatures.Messaging).Should().Be(1 << 14);
    }

    [Fact]
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

    [Fact]
    public void DoesNotClobberAGlobalTheHostRegistered()
    {
        var engine = new Engine(options => options
            .Configure(e => e.SetValue("MessageChannel", "mine"))
            .UseWebApis());

        engine.Evaluate("MessageChannel").AsString().Should().Be("mine");
    }

    [Fact]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = MessagingEngine();

        engine.Evaluate("new ShadowRealm().evaluate('typeof MessageChannel')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof MessagePort')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof MessageChannel").AsString().Should().Be("function");
    }

    // ---------------------------------------------------------------- the interfaces

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void RefusesAnOperationOnAReceiverThatIsNotAPort()
    {
        var engine = MessagingEngine();

        Err(engine, "MessagePort.prototype.postMessage.call({}, 1)").Should().Be("TypeError");
        Err(engine, "MessagePort.prototype.start.call({})").Should().Be("TypeError");
        Err(engine, "MessagePort.prototype.close.call({})").Should().Be("TypeError");
        Err(engine, "Object.getOwnPropertyDescriptor(MessagePort.prototype, 'onmessage').get.call({})").Should().Be("TypeError");
    }

    [Fact]
    public void SupportsSubclassingMessageChannel()
    {
        var engine = MessagingEngine();

        // OrdinaryCreateFromConstructor, so `class extends MessageChannel` gets the subclass prototype.
        engine.Execute("class C extends MessageChannel {}; var c = new C();");
        engine.Evaluate("c instanceof C").AsBoolean().Should().BeTrue();
        engine.Evaluate("c.port1 instanceof MessagePort").AsBoolean().Should().BeTrue();
    }

    // ---------------------------------------------------------------- delivery

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void ClosingAPortTwiceIsHarmless()
    {
        var engine = MessagingEngine();

        engine.Execute("var ch = new MessageChannel(); ch.port1.close(); ch.port1.close(); ch.port2.close();");
        Log(engine).Should().Be("");
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void RefusesAnUncloneableMessageSynchronously()
    {
        var engine = MessagingEngine();
        engine.Execute("var ch = new MessageChannel();");

        Err(engine, "ch.port1.postMessage(function () {})").Should().Be("DataCloneError");
        Err(engine, "ch.port1.postMessage(Symbol('s'))").Should().Be("DataCloneError");
        Err(engine, "ch.port1.postMessage(new Promise(function () {}))").Should().Be("DataCloneError");
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void RefusesToTransferAPort()
    {
        var engine = MessagingEngine();
        engine.Execute("var ch = new MessageChannel(); var other = new MessageChannel();");

        // Transferring a port is out of scope for this version, so every port-in-transfer-list case the
        // specification distinguishes — the port itself, its entangled peer, an unrelated port — is one
        // DataCloneError.
        Err(engine, "ch.port1.postMessage(0, [ch.port1])").Should().Be("DataCloneError");
        Err(engine, "ch.port1.postMessage(0, [ch.port2])").Should().Be("DataCloneError");
        Err(engine, "ch.port1.postMessage(0, [other.port1])").Should().Be("DataCloneError");
    }

    [Fact]
    public void RefusesAMalformedTransferOption()
    {
        var engine = MessagingEngine();
        engine.Execute("var ch = new MessageChannel();");

        Err(engine, "ch.port1.postMessage(0, 5)").Should().Be("TypeError");
        Err(engine, "ch.port1.postMessage(0, [1])").Should().Be("TypeError");
        Err(engine, "ch.port1.postMessage(0, { transfer: 5 })").Should().Be("TypeError");
    }

    // ---------------------------------------------------------------- listeners

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
        engine.Advanced.ProcessTasks();
        Log(engine).Should().Be("a,b");
    }

    // ---------------------------------------------------------------- MessageEvent

    [Fact]
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

    [Fact]
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

    [Fact]
    public void RefusesAMessageEventInitMemberOfTheWrongType()
    {
        var engine = MessagingEngine();

        Err(engine, "new MessageEvent('m', { source: {} })").Should().Be("TypeError");
        Err(engine, "new MessageEvent('m', { ports: [{}] })").Should().Be("TypeError");
        Err(engine, "new MessageEvent('m', 5)").Should().Be("TypeError");
    }

    [Fact]
    public void RefusesAMessageEventAccessorOnAPlainEvent()
    {
        var engine = MessagingEngine();

        Err(engine, "Object.getOwnPropertyDescriptor(MessageEvent.prototype, 'data').get.call(new Event('x'))")
            .Should().Be("TypeError");
    }
}
#endif
