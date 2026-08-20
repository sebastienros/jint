#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>EventTarget</c> — registration, removal and the flat dispatch algorithm —
/// https://dom.spec.whatwg.org/#interface-eventtarget.
/// </summary>
/// <remarks>
/// Jint has no node tree, so the specification's event path is always the single item «target». Both passes
/// of the dispatch algorithm still run over it, which is what these tests pin: the capture flag decides
/// <i>which</i> pass a listener runs in rather than being ignored, and <c>stopPropagation</c> is what ends the
/// second pass.
/// </remarks>
public class EventTargetTests
{
    private static Engine WebEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));
        engine.Execute("var log = []; var target = new EventTarget();");
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    [Fact]
    public void InvokesAListenerWithTheEventAndTheTargetAsThis()
    {
        var engine = WebEngine();

        engine.Execute("""
            target.addEventListener('ping', function (e) {
                log.push(e.type, e.target === target, e.currentTarget === target, this === target, e.eventPhase);
            });
            target.dispatchEvent(new Event('ping'));
            """);

        // eventPhase is AT_TARGET (2) throughout a flat dispatch.
        Log(engine).Should().Be("ping,true,true,true,2");
    }

    [Fact]
    public void ClearsTheDispatchStateWhenTheDispatchEnds()
    {
        var engine = WebEngine();

        engine.Execute("""
            var e = new Event('ping');
            target.addEventListener('ping', function () {});
            target.dispatchEvent(e);
            """);

        // The target survives the dispatch (only a shadow tree clears it); everything else is unwound.
        engine.Evaluate("e.target === target").AsBoolean().Should().BeTrue();
        engine.Evaluate("e.currentTarget").IsNull().Should().BeTrue();
        engine.Evaluate("e.eventPhase").AsNumber().Should().Be(0);
    }

    [Fact]
    public void OnlyInvokesListenersOfTheMatchingType()
    {
        var engine = WebEngine();

        engine.Execute("""
            target.addEventListener('a', function () { log.push('a'); });
            target.addEventListener('b', function () { log.push('b'); });
            target.dispatchEvent(new Event('b'));
            """);

        Log(engine).Should().Be("b");
    }

    [Fact]
    public void AcceptsAnObjectWithHandleEvent()
    {
        var engine = WebEngine();

        engine.Execute("""
            var handler = { handleEvent: function (e) { log.push('handled', this === handler); } };
            target.addEventListener('ping', handler);
            target.dispatchEvent(new Event('ping'));
            """);

        // "Call a user object's operation" uses the callback object itself as the this value in this case.
        Log(engine).Should().Be("handled,true");
    }

    [Fact]
    public void LooksUpHandleEventOnEveryInvocation()
    {
        var engine = WebEngine();

        engine.Execute("""
            var handler = { handleEvent: function () { log.push('first'); } };
            target.addEventListener('ping', handler);
            target.dispatchEvent(new Event('ping'));
            handler.handleEvent = function () { log.push('second'); };
            target.dispatchEvent(new Event('ping'));
            """);

        Log(engine).Should().Be("first,second");
    }

    [Fact]
    public void RaisesATypeErrorForAnObjectWhoseHandleEventIsNotCallable()
    {
        var engine = WebEngine();

        engine.Execute("target.addEventListener('ping', {});");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("target.dispatchEvent(new Event('ping'))"))
            .Message.Should().Contain("handleEvent");
    }

    [Fact]
    public void IgnoresADuplicateRegistration()
    {
        var engine = WebEngine();

        engine.Execute("""
            function once() { log.push('x'); }
            target.addEventListener('ping', once);
            target.addEventListener('ping', once);
            target.addEventListener('ping', once, { capture: false });
            target.dispatchEvent(new Event('ping'));
            """);

        Log(engine).Should().Be("x");
    }

    [Fact]
    public void TreatsADifferentCaptureFlagAsADifferentListener()
    {
        var engine = WebEngine();

        engine.Execute("""
            function both() { log.push('x'); }
            target.addEventListener('ping', both);
            target.addEventListener('ping', both, true);
            target.dispatchEvent(new Event('ping'));
            """);

        Log(engine).Should().Be("x,x");
    }

    [Fact]
    public void RunsCapturingListenersBeforeNonCapturingOnes()
    {
        var engine = WebEngine();

        engine.Execute("""
            target.addEventListener('ping', function () { log.push('bubble'); });
            target.addEventListener('ping', function () { log.push('capture'); }, true);
            target.dispatchEvent(new Event('ping'));
            """);

        // The dispatch algorithm runs the capturing pass over the whole path first, so on a single-target
        // dispatch a capturing listener wins whatever order the two were registered in.
        Log(engine).Should().Be("capture,bubble");
    }

    [Fact]
    public void RemovesAListenerByIdentityAndCaptureFlag()
    {
        var engine = WebEngine();

        engine.Execute("""
            function handler() { log.push('x'); }
            target.addEventListener('ping', handler, true);

            target.removeEventListener('ping', handler);              // wrong capture flag
            target.removeEventListener('ping', function () {});        // a different function object
            target.removeEventListener('other', handler, true);        // a different type
            target.dispatchEvent(new Event('ping'));

            target.removeEventListener('ping', handler, true);
            target.dispatchEvent(new Event('ping'));
            """);

        Log(engine).Should().Be("x");
    }

    [Fact]
    public void RemovesAOnceListenerBeforeItRuns()
    {
        var engine = WebEngine();

        engine.Execute("""
            var e = new Event('ping');
            target.addEventListener('ping', function () { log.push('once'); }, { once: true });
            target.dispatchEvent(e);
            target.dispatchEvent(new Event('ping'));
            """);

        Log(engine).Should().Be("once");
    }

    [Fact]
    public void RemovesAOnceListenerEvenWhenItThrows()
    {
        var engine = WebEngine();

        engine.Execute("target.addEventListener('ping', function () { log.push('once'); throw new Error('boom'); }, { once: true });");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("target.dispatchEvent(new Event('ping'))"));
        engine.Execute("target.dispatchEvent(new Event('ping'));");

        Log(engine).Should().Be("once");
    }

    [Fact]
    public void StopImmediatePropagationSkipsTheRemainingListeners()
    {
        var engine = WebEngine();

        engine.Execute("""
            target.addEventListener('ping', function (e) { log.push('first'); e.stopImmediatePropagation(); });
            target.addEventListener('ping', function () { log.push('second'); });
            target.dispatchEvent(new Event('ping'));
            """);

        Log(engine).Should().Be("first");
    }

    [Fact]
    public void StopPropagationEndsTheDispatchAfterTheCurrentPass()
    {
        var engine = WebEngine();

        engine.Execute("""
            target.addEventListener('ping', function (e) { log.push('capture'); e.stopPropagation(); }, true);
            target.addEventListener('ping', function () { log.push('capture2'); }, true);
            target.addEventListener('ping', function () { log.push('bubble'); });
            target.dispatchEvent(new Event('ping'));
            """);

        // stopPropagation does not stop the pass it was called in — only the next one.
        Log(engine).Should().Be("capture,capture2");
    }

    [Fact]
    public void IgnoresAListenerAddedDuringTheDispatchAndHonoursOneRemoved()
    {
        var engine = WebEngine();

        engine.Execute("""
            function late() { log.push('late'); }
            function third() { log.push('third'); }
            target.addEventListener('ping', function () {
                log.push('first');
                target.addEventListener('ping', late);
                target.removeEventListener('ping', third);
            });
            target.addEventListener('ping', third);
            target.dispatchEvent(new Event('ping'));
            """);

        // The pass runs over a clone, so `late` is not in it; `third` is, but its removed flag is checked.
        Log(engine).Should().Be("first");

        engine.Execute("target.dispatchEvent(new Event('ping'));");
        Log(engine).Should().Be("first,first,late");
    }

    [Fact]
    public void ReturnsFalseOnlyWhenTheEventWasCanceled()
    {
        var engine = WebEngine();

        engine.Evaluate("target.dispatchEvent(new Event('ping'))").AsBoolean().Should().BeTrue();

        engine.Execute("target.addEventListener('ping', function (e) { e.preventDefault(); });");

        // A non-cancelable event cannot be canceled, so dispatchEvent still answers true.
        engine.Evaluate("target.dispatchEvent(new Event('ping'))").AsBoolean().Should().BeTrue();
        engine.Evaluate("target.dispatchEvent(new Event('ping', { cancelable: true }))").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void APassiveListenerCannotCancelTheEvent()
    {
        var engine = WebEngine();

        engine.Execute("""
            var e = new Event('ping', { cancelable: true });
            target.addEventListener('ping', function (ev) { ev.preventDefault(); }, { passive: true });
            var result = target.dispatchEvent(e);
            """);

        engine.Evaluate("result").AsBoolean().Should().BeTrue();
        engine.Evaluate("e.defaultPrevented").AsBoolean().Should().BeFalse();

        // The flag is unset again once the passive listener returns.
        engine.Execute("""
            var e2 = new Event('ping', { cancelable: true });
            target.addEventListener('ping', function (ev) { ev.preventDefault(); });
            target.dispatchEvent(e2);
            """);
        engine.Evaluate("e2.defaultPrevented").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void RefusesToDispatchAnEventThatIsAlreadyBeingDispatched()
    {
        var engine = WebEngine();

        engine.Execute("""
            var caught = null;
            target.addEventListener('ping', function (e) {
                try { target.dispatchEvent(e); } catch (err) { caught = err; }
            });
            target.dispatchEvent(new Event('ping'));
            """);

        engine.Evaluate("caught.name").AsString().Should().Be("InvalidStateError");
        engine.Evaluate("caught instanceof DOMException").AsBoolean().Should().BeTrue();

        // And the event is usable again afterwards.
        engine.Evaluate("target.dispatchEvent(new Event('ping'))").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void RefusesADispatchArgumentThatIsNotAnEvent()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("target.dispatchEvent({})"))
            .Message.Should().Contain("Event");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("target.dispatchEvent()"));
    }

    [Fact]
    public void EnforcesTheWebIdlArityAndCallbackType()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("target.addEventListener('ping')"))
            .Message.Should().Contain("2 arguments required");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("target.addEventListener('ping', 42)"))
            .Message.Should().Contain("EventListener");

        // null and undefined are the null callback, which is not an error — it is simply nothing to add.
        engine.Execute("target.addEventListener('ping', null); target.addEventListener('ping', undefined);");
        engine.Evaluate("target.dispatchEvent(new Event('ping'))").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void FlattensABooleanOrANonObjectAsTheCaptureFlag()
    {
        var engine = WebEngine();

        engine.Execute("""
            target.addEventListener('ping', function () { log.push('capture'); }, 1);
            target.addEventListener('ping', function () { log.push('bubble'); }, 0);
            target.dispatchEvent(new Event('ping'));
            """);

        Log(engine).Should().Be("capture,bubble");
    }

    [Fact]
    public void AListenerThatThrowsEruptsFromDispatchEvent()
    {
        // The specification says to report the exception and carry on; Jint has no reportError channel yet, so
        // it propagates instead — the same choice the timer callbacks make, and documented on JsEventTarget.
        var engine = WebEngine();

        engine.Execute("""
            target.addEventListener('ping', function () { log.push('first'); throw new Error('boom'); });
            target.addEventListener('ping', function () { log.push('second'); });
            var e = new Event('ping');
            """);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("target.dispatchEvent(e)"))
            .Message.Should().Be("boom");

        Log(engine).Should().Be("first");

        // The dispatch state is still unwound, so neither the event nor the target is left broken.
        engine.Evaluate("e.currentTarget").IsNull().Should().BeTrue();
        engine.Evaluate("e.eventPhase").AsNumber().Should().Be(0);
    }

    [Fact]
    public void IsConstructibleAndSubclassable()
    {
        var engine = WebEngine();

        engine.Execute("""
            class Bus extends EventTarget {
                constructor() { super(); this.name = 'bus'; }
            }
            var bus = new Bus();
            bus.addEventListener('ping', function () { log.push(this.name); });
            bus.dispatchEvent(new Event('ping'));
            """);

        Log(engine).Should().Be("bus");
        engine.Evaluate("bus instanceof EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("EventTarget.length").AsNumber().Should().Be(0);
        engine.Evaluate("Object.prototype.toString.call(new EventTarget())").AsString().Should().Be("[object EventTarget]");
    }

    [Fact]
    public void HasTheIdlArities()
    {
        var engine = WebEngine();

        engine.Evaluate("EventTarget.prototype.addEventListener.length").AsNumber().Should().Be(2);
        engine.Evaluate("EventTarget.prototype.removeEventListener.length").AsNumber().Should().Be(2);
        engine.Evaluate("EventTarget.prototype.dispatchEvent.length").AsNumber().Should().Be(1);
    }

    [Fact]
    public void RefusesAReceiverThatIsNotAnEventTarget()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("EventTarget.prototype.dispatchEvent.call({}, new Event('x'))"))
            .Message.Should().Contain("EventTarget");
    }
}
#endif
