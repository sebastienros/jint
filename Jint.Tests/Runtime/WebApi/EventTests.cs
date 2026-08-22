#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>Event</c> and <c>CustomEvent</c> against the DOM standard —
/// https://dom.spec.whatwg.org/#interface-event.
/// </summary>
public class EventTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Events));

    [Fact]
    public void StartsInTheStateTheInnerEventCreationStepsLeaveIt()
    {
        var engine = WebEngine();
        engine.Execute("var e = new Event('greeting');");

        engine.Evaluate("e.type").AsString().Should().Be("greeting");
        engine.Evaluate("e.bubbles").AsBoolean().Should().BeFalse();
        engine.Evaluate("e.cancelable").AsBoolean().Should().BeFalse();
        engine.Evaluate("e.composed").AsBoolean().Should().BeFalse();
        engine.Evaluate("e.defaultPrevented").AsBoolean().Should().BeFalse();
        engine.Evaluate("e.eventPhase").AsNumber().Should().Be(0);
        engine.Evaluate("e.target").IsNull().Should().BeTrue();
        engine.Evaluate("e.currentTarget").IsNull().Should().BeTrue();

        // "Initialize event's isTrusted attribute to false" — a script-constructed event is never trusted.
        engine.Evaluate("e.isTrusted").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ReadsTheEventInitDictionary()
    {
        var engine = WebEngine();

        engine.Evaluate("new Event('x', { bubbles: true }).bubbles").AsBoolean().Should().BeTrue();
        engine.Evaluate("new Event('x', { cancelable: true }).cancelable").AsBoolean().Should().BeTrue();
        engine.Evaluate("new Event('x', { composed: true }).composed").AsBoolean().Should().BeTrue();

        // Dictionary members are booleans, so any value is coerced rather than rejected.
        engine.Evaluate("new Event('x', { bubbles: 1 }).bubbles").AsBoolean().Should().BeTrue();
        engine.Evaluate("new Event('x', { bubbles: '' }).bubbles").AsBoolean().Should().BeFalse();

        // null and undefined are the empty dictionary.
        engine.Evaluate("new Event('x', null).bubbles").AsBoolean().Should().BeFalse();
        engine.Evaluate("new Event('x', undefined).cancelable").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ReadsTheDictionaryMembersInWebIdlOrder()
    {
        var engine = WebEngine();

        engine.Execute("""
            var order = [];
            var probe = {};
            ['composed', 'cancelable', 'bubbles'].forEach(function (name) {
                Object.defineProperty(probe, name, { get: function () { order.push(name); return false; } });
            });
            new Event('x', probe);
            """);

        // https://webidl.spec.whatwg.org/#es-dictionary converts members in lexicographical order.
        engine.Evaluate("order.join(',')").AsString().Should().Be("bubbles,cancelable,composed");
    }

    [Fact]
    public void RequiresATypeArgument()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Event()"))
            .Message.Should().Contain("1 argument required");

        // Anything else is a DOMString, so it is stringified rather than refused.
        engine.Evaluate("new Event(42).type").AsString().Should().Be("42");
        engine.Evaluate("new Event(undefined).type").AsString().Should().Be("undefined");
    }

    [Fact]
    public void RefusesANonObjectInitDictionary()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Event('x', 1)"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Event('x', 'nope')"));
    }

    [Fact]
    public void ExposesThePhaseConstantsOnBothTheInterfaceObjectAndThePrototype()
    {
        var engine = WebEngine();

        engine.Evaluate("[Event.NONE, Event.CAPTURING_PHASE, Event.AT_TARGET, Event.BUBBLING_PHASE].join(',')")
            .AsString().Should().Be("0,1,2,3");
        engine.Evaluate("[Event.prototype.NONE, Event.prototype.CAPTURING_PHASE, Event.prototype.AT_TARGET, Event.prototype.BUBBLING_PHASE].join(',')")
            .AsString().Should().Be("0,1,2,3");

        // … and in the order the IDL declares them, which that section defines them in and which the record
        // conversion behind `new URLSearchParams(Event)` reads through [[OwnPropertyKeys]].
        engine.Evaluate("Object.keys(Event).join(',')").AsString()
            .Should().Be("NONE,CAPTURING_PHASE,AT_TARGET,BUBBLING_PHASE");

        // https://webidl.spec.whatwg.org/#es-constants: { writable: false, enumerable: true, configurable: false }.
        var descriptor = "Object.getOwnPropertyDescriptor(Event, 'AT_TARGET')";
        engine.Evaluate($"{descriptor}.writable").AsBoolean().Should().BeFalse();
        engine.Evaluate($"{descriptor}.enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate($"{descriptor}.configurable").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void CancelsOnlyWhenCancelable()
    {
        var engine = WebEngine();

        engine.Execute("var plain = new Event('x'); plain.preventDefault();");
        engine.Evaluate("plain.defaultPrevented").AsBoolean().Should().BeFalse();

        engine.Execute("var cancelable = new Event('x', { cancelable: true }); cancelable.preventDefault();");
        engine.Evaluate("cancelable.defaultPrevented").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ComposedPathIsEmptyOutsideADispatchAndTheTargetInsideOne()
    {
        var engine = WebEngine();

        engine.Evaluate("new Event('x').composedPath().length").AsNumber().Should().Be(0);

        engine.Execute("""
            var target = new EventTarget();
            var seen = null;
            target.addEventListener('x', function (e) { seen = e.composedPath(); });
            var e = new Event('x');
            target.dispatchEvent(e);
            """);

        engine.Evaluate("seen.length").AsNumber().Should().Be(1);
        engine.Evaluate("seen[0] === target").AsBoolean().Should().BeTrue();

        // The path is emptied when the dispatch ends.
        engine.Evaluate("e.composedPath().length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void CarriesATimeStampRelativeToTheEngineTimeOrigin()
    {
        var engine = WebEngine();

        engine.Evaluate("typeof new Event('x').timeStamp").AsString().Should().Be("number");
        engine.Evaluate("new Event('x').timeStamp >= 0").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ExposesEveryAttributeAsAPrototypeAccessor()
    {
        var engine = WebEngine();

        // A WebIDL attribute lives on the interface prototype object unless it is unforgeable, so the one own
        // property an instance carries is `isTrusted`.
        engine.Evaluate("Object.getOwnPropertyNames(new Event('x')).join(',')").AsString().Should().Be("isTrusted");

        foreach (var name in new[] { "type", "target", "srcElement", "currentTarget", "eventPhase", "bubbles", "cancelable", "defaultPrevented", "composed", "timeStamp" })
        {
            var descriptor = $"Object.getOwnPropertyDescriptor(Event.prototype, '{name}')";
            engine.Evaluate($"typeof {descriptor}.get").AsString().Should().Be("function");
            engine.Evaluate($"{descriptor}.set").IsUndefined().Should().BeTrue();
            engine.Evaluate($"{descriptor}.enumerable").AsBoolean().Should().BeTrue();
            engine.Evaluate($"{descriptor}.configurable").AsBoolean().Should().BeTrue();
        }

        // `returnValue` is the one writable attribute of the interface, so it is an accessor pair.
        var returnValue = "Object.getOwnPropertyDescriptor(Event.prototype, 'returnValue')";
        engine.Evaluate($"typeof {returnValue}.get").AsString().Should().Be("function");
        engine.Evaluate($"typeof {returnValue}.set").AsString().Should().Be("function");
        engine.Evaluate($"{returnValue}.enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate($"{returnValue}.configurable").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// <c>[LegacyUnforgeable] readonly attribute boolean isTrusted</c>
    /// (https://dom.spec.whatwg.org/#dom-event-istrusted). WebIDL
    /// (https://webidl.spec.whatwg.org/#LegacyUnforgeable) makes such a member "non-configurable and …
    /// exist as an own property on the object itself rather than on its prototype", and
    /// https://webidl.spec.whatwg.org/#es-attributes gives it
    /// <c>{ [[Enumerable]]: true, [[Configurable]]: false }</c>.
    /// </summary>
    [Fact]
    public void IsTrustedIsAnUnforgeableOwnAccessorOfEveryInstance()
    {
        var engine = WebEngine();

        engine.Evaluate("Event.prototype.hasOwnProperty('isTrusted')").AsBoolean().Should().BeFalse();
        engine.Evaluate("new Event('x').hasOwnProperty('isTrusted')").AsBoolean().Should().BeTrue();

        engine.Execute("var d = Object.getOwnPropertyDescriptor(new Event('x'), 'isTrusted');");
        engine.Evaluate("typeof d.get").AsString().Should().Be("function");
        engine.Evaluate("d.set").IsUndefined().Should().BeTrue();
        engine.Evaluate("d.enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate("d.configurable").AsBoolean().Should().BeFalse();
        engine.Evaluate("d.get.name").AsString().Should().Be("get isTrusted");
        engine.Evaluate("d.get.length").AsNumber().Should().Be(0);

        // One getter for the whole interface, not one per instance — which is what
        // dom/events/Event-isTrusted.any.js asserts.
        engine.Evaluate("""
            Object.getOwnPropertyDescriptor(new Event('a'), 'isTrusted').get ===
            Object.getOwnPropertyDescriptor(new Event('b'), 'isTrusted').get
            """).AsBoolean().Should().BeTrue();

        // Unforgeable: it cannot be redefined, shadowed or deleted.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Object.defineProperty(new Event('x'), 'isTrusted', { value: true })"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Object.defineProperty(new Event('x'), 'isTrusted', { get: function () { return true; } })"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Object.defineProperty(new Event('x'), 'isTrusted', { enumerable: false })"));
        engine.Evaluate("delete new Event('x').isTrusted").AsBoolean().Should().BeFalse();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("'use strict'; delete new Event('x').isTrusted"));

        // A read-only accessor: an assignment is a no-op in sloppy mode and a TypeError in strict mode.
        engine.Execute("var e = new Event('x'); e.isTrusted = true;");
        engine.Evaluate("e.isTrusted").AsBoolean().Should().BeFalse();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("'use strict'; new Event('x').isTrusted = true;"));

        // The own property is enumerable, so it shows up in every script-visible enumeration.
        engine.Evaluate("Object.keys(new Event('x')).join(',')").AsString().Should().Be("isTrusted");
        engine.Evaluate("JSON.stringify(new Event('x'))").AsString().Should().Be("""{"isTrusted":false}""");
        engine.Evaluate("Object.keys({ ...new Event('x') }).join(',')").AsString().Should().Be("isTrusted");
        engine.Evaluate("'isTrusted' in new Event('x')").AsBoolean().Should().BeTrue();
        engine.Evaluate("new Event('x').propertyIsEnumerable('isTrusted')").AsBoolean().Should().BeTrue();

        // …on every event, whichever interface it came from, and however it was created.
        engine.Evaluate("new CustomEvent('x').hasOwnProperty('isTrusted')").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.keys(new CustomEvent('x')).join(',')").AsString().Should().Be("isTrusted");
        engine.Evaluate("""
            class MyEvent extends Event {}
            Object.getOwnPropertyDescriptor(new MyEvent('x'), 'isTrusted').get ===
            Object.getOwnPropertyDescriptor(new Event('x'), 'isTrusted').get
            """).AsBoolean().Should().BeTrue();

        // The getter still brand-checks its receiver.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("d.get.call({})"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("d.get.call(Event.prototype)"));
    }

    /// <summary>
    /// An event's own <c>isTrusted</c> is the earliest of its own string keys, so a subclass field declared
    /// in a JavaScript constructor comes after it — the ordering the interface's own creation implies.
    /// </summary>
    [Fact]
    public void TheUnforgeableOwnPropertyComesBeforeAnythingScriptAdds()
    {
        var engine = WebEngine();

        engine.Execute("""
            class MyEvent extends Event {
                constructor(type) { super(type); this.extra = 1; }
            }
            var e = new MyEvent('x');
            """);

        engine.Evaluate("Object.getOwnPropertyNames(e).join(',')").AsString().Should().Be("isTrusted,extra");
        engine.Evaluate("Object.keys(e).join(',')").AsString().Should().Be("isTrusted,extra");
        engine.Evaluate("JSON.stringify(e)").AsString().Should().Be("""{"isTrusted":false,"extra":1}""");

        // The added property is ordinary; the unforgeable one still refuses to move.
        engine.Evaluate("delete e.extra").AsBoolean().Should().BeTrue();
        engine.Evaluate("delete e.isTrusted").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.getOwnPropertyNames(e).join(',')").AsString().Should().Be("isTrusted");
    }

    /// <summary>
    /// The engine's own events are trusted, which is the whole point of the attribute —
    /// https://dom.spec.whatwg.org/#concept-event-fire.
    /// </summary>
    [Fact]
    public void AnEngineFiredEventIsTrusted()
    {
        var engine = WebEngine();

        engine.Execute("""
            var controller = new AbortController();
            var seen = null;
            controller.signal.addEventListener('abort', function (e) { seen = e; });
            controller.abort();
            """);

        engine.Evaluate("seen.isTrusted").AsBoolean().Should().BeTrue();
        engine.Evaluate("seen.hasOwnProperty('isTrusted')").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyDescriptor(seen, 'isTrusted').configurable").AsBoolean().Should().BeFalse();
    }

    /// <summary>
    /// <c>readonly attribute EventTarget? srcElement</c>, whose "getter steps are to return this's target" —
    /// https://dom.spec.whatwg.org/#dom-event-srcelement.
    /// </summary>
    [Fact]
    public void SrcElementIsAnAliasOfTarget()
    {
        var engine = WebEngine();

        engine.Evaluate("new Event('x').srcElement").IsNull().Should().BeTrue();

        engine.Execute("""
            var target = new EventTarget();
            var during = null;
            var after = null;
            target.addEventListener('x', function (e) { during = e.srcElement; });
            var e = new Event('x');
            target.dispatchEvent(e);
            after = e.srcElement;
            """);

        engine.Evaluate("during === target").AsBoolean().Should().BeTrue();

        // "target" is not unset when the dispatch ends, and srcElement follows it wherever it goes.
        engine.Evaluate("after === e.target").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// <c>attribute boolean returnValue</c> — "The returnValue getter steps are to return false if this's
    /// canceled flag is set; otherwise true. The returnValue setter steps are to set the canceled flag with
    /// this if the given value is false; otherwise do nothing" —
    /// https://dom.spec.whatwg.org/#dom-event-returnvalue.
    /// </summary>
    [Fact]
    public void ReturnValueIsTheCanceledFlagInverted()
    {
        var engine = WebEngine();

        engine.Evaluate("new Event('x').returnValue").AsBoolean().Should().BeTrue();

        // The setter runs "set the canceled flag", which a non-cancelable event ignores — exactly as
        // preventDefault() does.
        engine.Execute("var plain = new Event('x'); plain.returnValue = false;");
        engine.Evaluate("plain.returnValue").AsBoolean().Should().BeTrue();
        engine.Evaluate("plain.defaultPrevented").AsBoolean().Should().BeFalse();

        engine.Execute("var cancelable = new Event('x', { cancelable: true }); cancelable.returnValue = false;");
        engine.Evaluate("cancelable.returnValue").AsBoolean().Should().BeFalse();
        engine.Evaluate("cancelable.defaultPrevented").AsBoolean().Should().BeTrue();

        // "otherwise do nothing" — assigning true never clears a flag that is already set.
        engine.Execute("cancelable.returnValue = true;");
        engine.Evaluate("cancelable.returnValue").AsBoolean().Should().BeFalse();

        // The IDL type is `boolean`, so the assigned value goes through ToBoolean rather than being rejected.
        engine.Execute("var zero = new Event('x', { cancelable: true }); zero.returnValue = 0;");
        engine.Evaluate("zero.defaultPrevented").AsBoolean().Should().BeTrue();
        engine.Execute("var one = new Event('x', { cancelable: true }); one.returnValue = 1;");
        engine.Evaluate("one.defaultPrevented").AsBoolean().Should().BeFalse();

        // preventDefault() and the setter are the same algorithm seen from two sides.
        engine.Execute("var prevented = new Event('x', { cancelable: true }); prevented.preventDefault();");
        engine.Evaluate("prevented.returnValue").AsBoolean().Should().BeFalse();
    }

    /// <summary>
    /// "Set the canceled flag" is gated on the in-passive-listener flag as well as on <c>cancelable</c>
    /// (https://dom.spec.whatwg.org/#set-the-canceled-flag), so a passive listener's <c>returnValue = false</c>
    /// is ignored just as its <c>preventDefault()</c> is — which is what
    /// <c>dom/events/AddEventListenerOptions-passive.any.js</c> asserts.
    /// </summary>
    [Fact]
    public void ReturnValueIsIgnoredInsideAPassiveListener()
    {
        var engine = WebEngine();

        engine.Execute("""
            function run(options) {
                var target = new EventTarget();
                var prevented;
                var handler = function (e) { e.returnValue = false; prevented = e.defaultPrevented; };
                target.addEventListener('x', handler, options);
                var uncanceled = target.dispatchEvent(new Event('x', { bubbles: true, cancelable: true }));
                target.removeEventListener('x', handler, options);
                return prevented + '/' + uncanceled;
            }
            """);

        engine.Evaluate("run(undefined)").AsString().Should().Be("true/false");
        engine.Evaluate("run({})").AsString().Should().Be("true/false");
        engine.Evaluate("run({ passive: false })").AsString().Should().Be("true/false");
        engine.Evaluate("run({ passive: true })").AsString().Should().Be("false/true");
        engine.Evaluate("run({ passive: 0 })").AsString().Should().Be("true/false");
        engine.Evaluate("run({ passive: 1 })").AsString().Should().Be("false/true");
    }

    [Fact]
    public void RefusesAReceiverThatIsNotAnEvent()
    {
        var engine = WebEngine();

        // Event.prototype is an interface prototype object, not an instance of the interface.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Event.prototype.type"))
            .Message.Should().Contain("Event");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Event.prototype.preventDefault.call({})"));
    }

    [Fact]
    public void HasTheIdlShape()
    {
        var engine = WebEngine();

        engine.Evaluate("Event.length").AsNumber().Should().Be(1);
        engine.Evaluate("Event.name").AsString().Should().Be("Event");
        engine.Evaluate("Event.prototype.constructor === Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(Event) === Function.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(Event.prototype) === Object.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(new Event('x'))").AsString().Should().Be("[object Event]");

        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate("Event('x')"));
        exception.Message.Should().Contain("requires 'new'");
    }

    [Fact]
    public void SupportsBeingSubclassed()
    {
        var engine = WebEngine();

        engine.Execute("""
            class MyEvent extends Event {
                constructor(type, extra) { super(type, { cancelable: true }); this.extra = extra; }
            }
            var e = new MyEvent('x', 7);
            """);

        engine.Evaluate("e instanceof MyEvent").AsBoolean().Should().BeTrue();
        engine.Evaluate("e instanceof Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("e.type").AsString().Should().Be("x");
        engine.Evaluate("e.cancelable").AsBoolean().Should().BeTrue();
        engine.Evaluate("e.extra").AsNumber().Should().Be(7);

        // A subclass instance is a perfectly good event to dispatch.
        engine.Execute("""
            var target = new EventTarget();
            var got = null;
            target.addEventListener('x', function (ev) { got = ev; });
            target.dispatchEvent(e);
            """);
        engine.Evaluate("got === e").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CustomEventCarriesItsDetail()
    {
        var engine = WebEngine();

        // The IDL default is null, not undefined.
        engine.Evaluate("new CustomEvent('x').detail").IsNull().Should().BeTrue();
        engine.Evaluate("new CustomEvent('x', {}).detail").IsNull().Should().BeTrue();
        engine.Evaluate("new CustomEvent('x', { detail: undefined }).detail").IsNull().Should().BeTrue();

        engine.Evaluate("new CustomEvent('x', { detail: 42 }).detail").AsNumber().Should().Be(42);
        engine.Evaluate("new CustomEvent('x', { detail: { a: 1 } }).detail.a").AsNumber().Should().Be(1);
        engine.Evaluate("new CustomEvent('x', { detail: 1, bubbles: true }).bubbles").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CustomEventInheritsFromEvent()
    {
        var engine = WebEngine();

        engine.Evaluate("new CustomEvent('x') instanceof Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(CustomEvent.prototype) === Event.prototype").AsBoolean().Should().BeTrue();

        // An interface object that inherits has the inherited interface object as its [[Prototype]].
        engine.Evaluate("Object.getPrototypeOf(CustomEvent) === Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("CustomEvent.prototype.constructor === CustomEvent").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(new CustomEvent('x'))").AsString().Should().Be("[object CustomEvent]");

        // detail belongs to CustomEvent alone.
        engine.Evaluate("'detail' in Event.prototype").AsBoolean().Should().BeFalse();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Object.getOwnPropertyDescriptor(CustomEvent.prototype, 'detail').get.call(new Event('x'))"));
    }

    [Fact]
    public void ReachingCustomEventFirstStillBuildsEventUnderneath()
    {
        // The intrinsics are lazy and CustomEvent depends on Event, so the inheritance has to hold whichever
        // of the two a script mentions first.
        var engine = WebEngine();

        engine.Evaluate("Object.getPrototypeOf(new CustomEvent('x')) === CustomEvent.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(CustomEvent) === Event").AsBoolean().Should().BeTrue();
    }
}
#endif
