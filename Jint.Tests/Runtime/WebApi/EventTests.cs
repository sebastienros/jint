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

        // WebIDL attributes live on the interface prototype object, so the instance owns none of them.
        engine.Evaluate("Object.getOwnPropertyNames(new Event('x')).length").AsNumber().Should().Be(0);

        foreach (var name in new[] { "type", "target", "currentTarget", "eventPhase", "bubbles", "cancelable", "defaultPrevented", "composed", "isTrusted", "timeStamp" })
        {
            var descriptor = $"Object.getOwnPropertyDescriptor(Event.prototype, '{name}')";
            engine.Evaluate($"typeof {descriptor}.get").AsString().Should().Be("function");
            engine.Evaluate($"{descriptor}.set").IsUndefined().Should().BeTrue();
            engine.Evaluate($"{descriptor}.enumerable").AsBoolean().Should().BeTrue();
            engine.Evaluate($"{descriptor}.configurable").AsBoolean().Should().BeTrue();
        }
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
