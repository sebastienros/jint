#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>BroadcastChannel</c> against the HTML Standard's
/// https://html.spec.whatwg.org/multipage/web-messaging.html#broadcasting-to-other-browsing-contexts.
/// </summary>
/// <remarks>
/// Everything here is one engine talking to itself: a default engine gets a private
/// <c>BroadcastChannelBroker</c>, so channels it creates hear each other and nothing leaves the engine. The
/// cross-engine form — one broker shared through <c>Options.WebApi.Messaging.Broker</c> — is exercised from a
/// third party's side in <c>Jint.Tests.PublicInterface.WebApiBroadcastChannelTests</c>.
/// <para>
/// The other half of every assertion is <i>when</i> delivery happens: each destination gets a task of its own,
/// and <c>Engine.Execute</c> drains the loop once the script has finished — which is why a message posted by a
/// script has already been delivered when <c>Execute</c> returns, and is not yet delivered on the line after
/// the <c>postMessage</c> call.
/// </para>
/// </remarks>
public class BroadcastChannelTests
{
    private static Engine BroadcastEngine(WebApiFeatures features = WebApiFeatures.Default)
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
        new Engine().Evaluate("typeof BroadcastChannel").AsString().Should().Be("undefined");
    }

    [Test]
    public void RidesTheMessagingFeatureRatherThanOneOfItsOwn()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Messaging));

        engine.Evaluate("typeof BroadcastChannel").AsString().Should().Be("function");

        // ... and the Events globals really are absent, so this is not accidentally testing Default.
        engine.Evaluate("typeof EventTarget").AsString().Should().Be("undefined");

        // A channel still has the EventTarget operations, because the prototype chain does not need the global.
        engine.Execute("var c = new BroadcastChannel('x'); c.addEventListener('message', function () {});");
    }

    [Test]
    public void IsPartOfTheDefaultFeatureSet()
    {
        new Engine(options => options.UseWebApis()).Evaluate("typeof BroadcastChannel").AsString().Should().Be("function");
    }

    [Test]
    public void GivesTheGlobalTheAttributesWebIdlAsksFor()
    {
        var engine = BroadcastEngine();

        // An interface object is writable and configurable but not enumerable —
        // https://webidl.spec.whatwg.org/#es-interfaces.
        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("BroadcastChannel");
        descriptor.Should().NotBe(PropertyDescriptor.Undefined);
        descriptor.Writable.Should().BeTrue();
        descriptor.Configurable.Should().BeTrue();
        descriptor.Enumerable.Should().BeFalse();
    }

    [Test]
    public void InheritsFromEventTarget()
    {
        var engine = BroadcastEngine();

        engine.Evaluate("Object.getPrototypeOf(BroadcastChannel) === EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(BroadcastChannel.prototype) === EventTarget.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("new BroadcastChannel('x') instanceof EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(new BroadcastChannel('x'))").AsString().Should().Be("[object BroadcastChannel]");
    }

    [Test]
    public void HasTheInterfaceShapeTheIdlDeclares()
    {
        var engine = BroadcastEngine();

        engine.Evaluate("BroadcastChannel.length").AsNumber().Should().Be(1);
        engine.Evaluate("BroadcastChannel.name").AsString().Should().Be("BroadcastChannel");
        engine.Evaluate("BroadcastChannel.prototype.postMessage.length").AsNumber().Should().Be(1);
        engine.Evaluate("BroadcastChannel.prototype.close.length").AsNumber().Should().Be(0);

        // `name` is a readonly attribute: an accessor with a getter and no setter.
        engine.Evaluate("typeof Object.getOwnPropertyDescriptor(BroadcastChannel.prototype, 'name').get").AsString().Should().Be("function");
        engine.Evaluate("Object.getOwnPropertyDescriptor(BroadcastChannel.prototype, 'name').set === undefined").AsBoolean().Should().BeTrue();

        // The two event handlers are accessor pairs.
        engine.Evaluate("typeof Object.getOwnPropertyDescriptor(BroadcastChannel.prototype, 'onmessage').set").AsString().Should().Be("function");
        engine.Evaluate("typeof Object.getOwnPropertyDescriptor(BroadcastChannel.prototype, 'onmessageerror').set").AsString().Should().Be("function");
    }

    // ---------------------------------------------------------------- the constructor

    [Test]
    public void RequiresAName()
    {
        var engine = BroadcastEngine();

        Err(engine, "new BroadcastChannel();").Should().Be("TypeError");

        // A present argument is converted, so `undefined` is a channel called "undefined" and 42 is one
        // called "42" — which is what a DOMString parameter with no default means.
        engine.Evaluate("new BroadcastChannel(undefined).name").AsString().Should().Be("undefined");
        engine.Evaluate("new BroadcastChannel(42).name").AsString().Should().Be("42");
        engine.Evaluate("new BroadcastChannel('').name").AsString().Should().Be("");
    }

    [Test]
    public void RefusesToBeCalledWithoutNew()
    {
        var engine = BroadcastEngine();

        Err(engine, "BroadcastChannel('x');").Should().Be("TypeError");
    }

    [Test]
    public void EveryOperationIsBranded()
    {
        var engine = BroadcastEngine();

        Err(engine, "BroadcastChannel.prototype.postMessage.call({}, 1);").Should().Be("TypeError");
        Err(engine, "BroadcastChannel.prototype.close.call({});").Should().Be("TypeError");
        Err(engine, "Object.getOwnPropertyDescriptor(BroadcastChannel.prototype, 'name').get.call({});").Should().Be("TypeError");
        Err(engine, "Object.getOwnPropertyDescriptor(BroadcastChannel.prototype, 'onmessage').get.call({});").Should().Be("TypeError");
    }

    // ---------------------------------------------------------------- delivery

    [Test]
    public void ReachesEveryOtherChannelOfThatNameAndNotTheSender()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            a.onmessage = function (e) { log.push('a:' + e.data); };
            b.onmessage = function (e) { log.push('b:' + e.data); };
            a.postMessage('hello');
            """);

        // Step 6: "Remove source from destinations." A channel never hears itself.
        Log(engine).Should().Be("b:hello");
    }

    [Test]
    public void ReachesTheDestinationsInCreationOrder()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var first = new BroadcastChannel('room');
            var second = new BroadcastChannel('room');
            var third = new BroadcastChannel('room');
            var sender = new BroadcastChannel('room');
            third.onmessage = function () { log.push('third'); };
            first.onmessage = function () { log.push('first'); };
            second.onmessage = function () { log.push('second'); };
            sender.postMessage(1);
            """);

        // Step 7 sorts the destinations, and within one agent cluster that reduces to creation order — which
        // is deliberately not the order the listeners happened to be attached in.
        Log(engine).Should().Be("first,second,third");
    }

    [Test]
    public void ADifferentNameHearsNothing()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            var other = new BroadcastChannel('other room');
            b.onmessage = function (e) { log.push('b'); };
            other.onmessage = function (e) { log.push('other'); };
            a.postMessage(1);
            """);

        Log(engine).Should().Be("b");
    }

    [Test]
    public void TheNameIsComparedAsAnExactString()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('Room');
            var b = new BroadcastChannel('room');
            b.onmessage = function () { log.push('b'); };
            a.postMessage(1);
            """);

        Log(engine).Should().Be("");
    }

    [Test]
    public void DeliveryIsATaskAndNotASynchronousCall()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.onmessage = function (e) { log.push(e.data); };
            a.postMessage('later');
            log.push('synchronously: ' + log.length);
            """);

        // The specification queues a global task per destination, so nothing has been dispatched by the time
        // the next statement runs; the drain at the end of Execute is what delivers it.
        Log(engine).Should().Be("synchronously: 0,later");
    }

    [Test]
    public void EveryAlreadyQueuedJobRunsBeforeTheMessage()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.onmessage = function () { log.push('message'); };
            Promise.resolve().then(function () { log.push('microtask'); });
            a.postMessage(1);
            Promise.resolve().then(function () { log.push('afterwards'); });
            """);

        // Step 8 queues one task per destination, and Jint's event loop is a single queue — so a broadcast
        // takes its place in it when it is posted, exactly as an EventSource's or a WebSocket's message does.
        // That is deliberately a hop earlier than a MessagePort, whose delivery costs two jobs because its
        // port message queue can be disabled; the difference is visible only to a job queued between the post
        // and the delivery, as the third line here is.
        Log(engine).Should().Be("microtask,message,afterwards");
    }

    [Test]
    public void AddEventListenerAloneIsEnoughToReceive()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.addEventListener('message', function (e) { log.push(e.data); });
            a.postMessage('heard');
            """);

        // Unlike a MessagePort there is no port message queue to enable, so there is no start() and nothing
        // waits for onmessage to be assigned.
        Log(engine).Should().Be("heard");
    }

    [Test]
    public void TheEventIsATrustedMessageEvent()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.onmessage = function (e) {
                log.push(e instanceof MessageEvent);
                log.push(e.type);
                log.push(e.isTrusted);
                log.push(e.origin === '');
                log.push(e.lastEventId === '');
                log.push(e.source === null);
                log.push(e.ports.length === 0);
                log.push(e.target === b);
            };
            a.postMessage(1);
            """);

        Log(engine).Should().Be("true,message,true,true,true,true,true,true");
    }

    [Test]
    public void ManyMessagesArriveInThePostedOrder()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.onmessage = function (e) { log.push(e.data); };
            for (var i = 0; i < 4; i++) { a.postMessage(i); }
            """);

        Log(engine).Should().Be("0,1,2,3");
    }

    // ---------------------------------------------------------------- the event handler attributes

    [Test]
    public void OnMessageIsAnOrdinaryEventHandlerAttribute()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var c = new BroadcastChannel('room');
            log.push(c.onmessage === null);
            var f = function () {};
            c.onmessage = f;
            log.push(c.onmessage === f);
            c.onmessage = 'not an object';
            log.push(c.onmessage === null);
            """);

        Log(engine).Should().Be("true,true,true");
    }

    [Test]
    public void OnMessageTakesItsTurnAmongTheOtherListeners()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.addEventListener('message', function () { log.push('first'); });
            b.onmessage = function () { log.push('handler'); };
            b.addEventListener('message', function () { log.push('last'); });
            a.postMessage(1);
            """);

        Log(engine).Should().Be("first,handler,last");
    }

    [Test]
    public void OnMessageErrorExistsAndNeverFires()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            log.push(b.onmessageerror === null);
            b.onmessageerror = function () { log.push('messageerror'); };
            b.onmessage = function () { log.push('message'); };
            a.postMessage(new Map([['k', 1]]));
            """);

        // The only thing that fires one is a deserialization that fails, and a record this engine's own
        // serializer built always deserializes.
        Log(engine).Should().Be("true,message");
    }

    // ---------------------------------------------------------------- close

    [Test]
    public void PostingOnAClosedChannelThrowsInvalidStateError()
    {
        var engine = BroadcastEngine();

        engine.Execute("var c = new BroadcastChannel('room'); c.close();");

        // Step 1, and deliberately not the MessagePort behaviour: a closed port's postMessage merely goes
        // nowhere, a closed BroadcastChannel refuses.
        Err(engine, "c.postMessage(1);").Should().Be("InvalidStateError");

        // The error is a DOMException, which is how every web API reports a refusal to script.
        engine.Evaluate("(function () { try { c.postMessage(1); } catch (e) { return e instanceof DOMException; } })()")
            .AsBoolean().Should().BeTrue();
    }

    [Test]
    public void TheClosedCheckHappensBeforeTheMessageIsSerialized()
    {
        var engine = BroadcastEngine();

        engine.Execute("var c = new BroadcastChannel('room'); c.close();");

        // Step 1 precedes step 2, and the order is observable: an uncloneable value on a closed channel is
        // still an InvalidStateError rather than a DataCloneError.
        Err(engine, "c.postMessage(function () {});").Should().Be("InvalidStateError");
    }

    [Test]
    public void AClosedChannelReceivesNothing()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.onmessage = function (e) { log.push(e.data); };
            b.close();
            a.postMessage('lost');
            """);

        Log(engine).Should().Be("");
    }

    [Test]
    public void ClosingIsIdempotent()
    {
        var engine = BroadcastEngine();

        engine.Execute("var c = new BroadcastChannel('room'); c.close(); c.close(); c.close();");

        // The name survives closing — the attribute is not "the name while open".
        engine.Evaluate("c.name").AsString().Should().Be("room");
    }

    [Test]
    public void ClosingDuringADispatchStopsTheDestinationsBehindIt()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var first = new BroadcastChannel('room');
            var second = new BroadcastChannel('room');
            var sender = new BroadcastChannel('room');
            first.onmessage = function () { log.push('first'); second.close(); };
            second.onmessage = function () { log.push('second'); };
            sender.postMessage(1);
            """);

        // Both tasks were queued when the message was posted, so what stops the second one is step 8.1 —
        // "if destination's closed flag is true, then abort these steps" — checked when the task runs.
        Log(engine).Should().Be("first");
    }

    [Test]
    public void AChannelClosedAfterAPostStillDoesNotHearIt()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.onmessage = function (e) { log.push(e.data); };
            a.postMessage('in flight');
            b.close();
            """);

        Log(engine).Should().Be("");
    }

    // ---------------------------------------------------------------- structured clone

    [Test]
    public void AnUncloneableValueIsASynchronousDataCloneError()
    {
        var engine = BroadcastEngine();

        engine.Execute("var a = new BroadcastChannel('room'); var b = new BroadcastChannel('room');");
        engine.Execute("b.onmessage = function () { log.push('delivered'); };");

        // Step 2 runs on the caller, before any destination is looked at, so the error reaches the script that
        // called postMessage — and nothing is delivered.
        Err(engine, "a.postMessage(function () {});").Should().Be("DataCloneError");
        Log(engine).Should().Be("");
    }

    [Test]
    public void ItSerializesEvenWhenNobodyIsListening()
    {
        var engine = BroadcastEngine();

        engine.Execute("var lonely = new BroadcastChannel('nobody else');");

        // Step 2 precedes the collection of destinations, so a DataCloneError does not depend on there being
        // anyone to deliver to.
        Err(engine, "lonely.postMessage(Symbol('x'));").Should().Be("DataCloneError");
    }

    [Test]
    public void TheMessageIsAStructuredCloneTakenAtThePost()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.onmessage = function (e) {
                log.push(e.data.when instanceof Date);
                log.push(e.data.when.getTime() === 86400000);
                log.push(e.data.map instanceof Map);
                log.push(e.data.map.get('k'));
                log.push(e.data.mutated === undefined);
                log.push(e.data !== sent);
            };
            var sent = { when: new Date(86400000), map: new Map([['k', 'v']]) };
            a.postMessage(sent);
            sent.mutated = true;
            """);

        Log(engine).Should().Be("true,true,true,v,true,true");
    }

    [Test]
    public void ThereIsNoTransferListAndNothingIsDetached()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.onmessage = function (e) { log.push(new Uint8Array(e.data)[0]); };
            var buffer = new ArrayBuffer(2);
            new Uint8Array(buffer)[0] = 7;
            a.postMessage(buffer, [buffer]);
            log.push('sender byteLength: ' + buffer.byteLength);
            """);

        // postMessage takes one argument; a second one is ignored rather than treated as a transfer list, so
        // the sender's buffer is untouched — which is the only answer available when a message has many
        // destinations.
        Log(engine).Should().Be("sender byteLength: 2,7");
    }

    [Test]
    public void EachDestinationGetsItsOwnCopyOfTheMessage()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var sender = new BroadcastChannel('room');
            var first = new BroadcastChannel('room');
            var second = new BroadcastChannel('room');
            var received = [];
            first.onmessage = function (e) { received.push(e.data); };
            second.onmessage = function (e) { received.push(e.data); };
            sender.postMessage({ n: 1 });
            """);

        engine.Evaluate("received.length").AsNumber().Should().Be(2);
        engine.Evaluate("received[0] !== received[1]").AsBoolean().Should().BeTrue();

        engine.Execute("received[0].n = 99;");
        engine.Evaluate("received[1].n").AsNumber().Should().Be(1);
    }

    [Test]
    public void TwoDestinationsDoNotShareOneArrayBuffersStorage()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var sender = new BroadcastChannel('room');
            var first = new BroadcastChannel('room');
            var second = new BroadcastChannel('room');
            var received = [];
            first.onmessage = function (e) { received.push(e.data); };
            second.onmessage = function (e) { received.push(e.data); };
            sender.postMessage(new Uint8Array([1, 2, 3]));
            """);

        // One record, two deserializations: the storage a deserializer would ordinarily adopt has to be
        // copied here, or a write through one receiver's view would be visible through the other's.
        engine.Evaluate("received.length").AsNumber().Should().Be(2);
        engine.Evaluate("received[0].buffer !== received[1].buffer").AsBoolean().Should().BeTrue();

        engine.Execute("received[0][0] = 99;");
        engine.Evaluate("received[1][0]").AsNumber().Should().Be(1);
        engine.Evaluate("received[0][0]").AsNumber().Should().Be(99);
    }

    [Test]
    public void ACyclicGraphSurvivesTheRoundTrip()
    {
        var engine = BroadcastEngine();

        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.onmessage = function (e) {
                log.push(e.data.self === e.data);
                log.push(e.data.n);
            };
            var cyclic = { n: 5 };
            cyclic.self = cyclic;
            a.postMessage(cyclic);
            """);

        Log(engine).Should().Be("true,5");
    }

    // ---------------------------------------------------------------- the evaluation cycle

    [Test]
    public void ARestoreEndsEveryChannelTheEngineCreated()
    {
        var engine = BroadcastEngine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("""
            var sender = new BroadcastChannel('room');
            var receiver = new BroadcastChannel('room');
            receiver.onmessage = function () { log.push('heard'); };
            """);

        var receiver = engine.GetValue("receiver");
        var sender = engine.GetValue("sender");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The channel objects still exist — the host kept references — but the restore ended them, exactly as
        // it ends a MessagePort: their listeners are closures over the cycle that has gone.
        engine.SetValue("oldSender", sender);
        engine.SetValue("oldReceiver", receiver);
        engine.Execute("var log = []; function err(f) { try { f(); return 'no error'; } catch (e) { return e.name; } }");

        Err(engine, "oldSender.postMessage('too late');").Should().Be("InvalidStateError");
        Log(engine).Should().Be("");

        // ... and a fresh pair in the new cycle works immediately, which is what a pooled engine wants.
        engine.Execute("""
            var a = new BroadcastChannel('room');
            var b = new BroadcastChannel('room');
            b.onmessage = function (e) { log.push(e.data); };
            a.postMessage('next cycle');
            """);

        Log(engine).Should().Be("next cycle");
    }

    [Test]
    public void AChannelFromAnEndedCycleIsGoneFromTheBroker()
    {
        var engine = BroadcastEngine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("var stale = new BroadcastChannel('room'); stale.onmessage = function () { log.push('stale'); };");
        var stale = engine.GetValue("stale");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        engine.SetValue("stale", stale);

        // Exactly one delivery: the surviving object is no longer a subscriber, so a message posted in the new
        // cycle reaches only the channel created in it.
        engine.Execute("""
            var log = [];
            var sender = new BroadcastChannel('room');
            var fresh = new BroadcastChannel('room');
            fresh.onmessage = function () { log.push('fresh'); };
            sender.postMessage(1);
            """);

        Log(engine).Should().Be("fresh");
    }

    // ---------------------------------------------------------------- the broker keeps nothing it need not

    [Test]
    public void AClosedChannelLeavesTheBrokerAndAnEmptyNameGoesWithIt()
    {
        var broker = new BroadcastChannelBroker();
        var engine = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.Messaging);
            options.WebApi.Messaging.Broker = broker;
        });

        broker.ActiveNameCount.Should().Be(0);

        engine.Execute("var a = new BroadcastChannel('room'); var b = new BroadcastChannel('room'); var c = new BroadcastChannel('other');");
        broker.ActiveNameCount.Should().Be(2);

        engine.Execute("a.close();");
        broker.ActiveNameCount.Should().Be(2);

        // The last subscriber of a name takes the name with it — otherwise a script cycling through channel
        // names would grow the broker for as long as the process lives.
        engine.Execute("b.close();");
        broker.ActiveNameCount.Should().Be(1);

        engine.Execute("c.close();");
        broker.ActiveNameCount.Should().Be(0);
    }

    [Test]
    public void ARestoreTakesEveryNameWithIt()
    {
        var broker = new BroadcastChannelBroker();
        var engine = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.Messaging);
            options.WebApi.Messaging.Broker = broker;
        });

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Execute("var a = new BroadcastChannel('room'); var b = new BroadcastChannel('other');");
        broker.ActiveNameCount.Should().Be(2);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // A broker the host shares outlives the engine, so a restore that merely stopped delivering would
        // leave this engine reachable from it for good.
        broker.ActiveNameCount.Should().Be(0);
    }
}
#endif
