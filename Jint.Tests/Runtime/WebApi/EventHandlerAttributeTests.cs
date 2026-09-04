#if NET8_0_OR_GREATER
#nullable enable

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// What an event handler IDL attribute is, read on every interface that has one —
/// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes.
/// </summary>
/// <remarks>
/// <para>
/// Three rules, and one definition of them (<c>Jint/WebApi/Events/EventHandlerAttributes.cs</c>): the handler
/// is <b>one entry of the target's own event listener list</b> that keeps the position it first took,
/// <c>EventHandler</c> is <c>[LegacyTreatNonObjectAsNull]</c> so a non-object clears it rather than raising a
/// <c>TypeError</c>, and an object that is not callable is stored and read back but never invoked.
/// </para>
/// <para>
/// <b>Every case dispatches the event by hand.</b> These interfaces reach their events by wildly different
/// routes — a socket frame, an entangled port, an abort algorithm — and none of that is what is under test:
/// what is under test is the listener list the attribute writes to, which <c>dispatchEvent</c> reaches on
/// every one of them because every one of them is an <c>EventTarget</c>. The interface-specific arrivals are
/// pinned in each interface's own file, and <c>MessagePort</c>'s implicit <c>start()</c> — the one handler
/// attribute in the engine with a side effect — in
/// <see cref="MessagingTests.AssigningOnMessageStartsThePortEvenForAListenerRegisteredSeparately"/>.
/// </para>
/// </remarks>
public class EventHandlerAttributeTests
{
    [TestCase(WebApiFeatures.Events, "new AbortController().signal", "onabort", "abort")]
    [TestCase(WebApiFeatures.Scheduler, "new TaskController().signal", "onprioritychange", "prioritychange")]
    [TestCase(WebApiFeatures.Messaging, "new BroadcastChannel('handlers')", "onmessage", "message")]
    [TestCase(WebApiFeatures.Messaging, "new BroadcastChannel('handlers')", "onmessageerror", "messageerror")]
    [TestCase(WebApiFeatures.Messaging, "new MessageChannel().port1", "onmessage", "message")]
    [TestCase(WebApiFeatures.Messaging, "new MessageChannel().port1", "onmessageerror", "messageerror")]
    public void AHandlerAttributeKeepsItsPositionAndTakesANonObjectAsNull(
        WebApiFeatures features,
        string construct,
        string attribute,
        string type)
    {
        var engine = new Engine(options => options.UseWebApis(features | WebApiFeatures.Events));

        engine.Execute($$"""
            var log = [];
            var target = {{construct}};
            var fire = () => target.dispatchEvent(new Event('{{type}}'));

            target.{{attribute}} = () => log.push('handler');
            target.addEventListener('{{type}}', () => log.push('listener'));
            target.{{attribute}} = () => log.push('replaced');
            fire();
            """);

        // Reassigning replaced the callback in place, so the handler still runs before a listener added after
        // it — a remove-and-add would have put it last.
        Log(engine).Should().Be("replaced|listener");

        engine.Execute($"target.{attribute} = 42; fire();");
        engine.Evaluate($"target.{attribute}").IsNull().Should().BeTrue("EventHandler is [LegacyTreatNonObjectAsNull]");
        Log(engine).Should().Be("replaced|listener|listener");

        // An object that is not callable is kept and read back, and the dispatch simply passes it over.
        engine.Execute($"var bag = {{}}; target.{attribute} = bag; fire();");
        engine.Evaluate($"target.{attribute} === bag").AsBoolean().Should().BeTrue();
        Log(engine).Should().Be("replaced|listener|listener|listener");

        // And a handler assigned after it was cleared appends a fresh entry, so it now runs last.
        engine.Execute($"target.{attribute} = null; target.{attribute} = () => log.push('appended'); fire();");
        Log(engine).Should().Be("replaced|listener|listener|listener|listener|appended");
    }

    /// <summary>
    /// The handler is invisible to <c>removeEventListener</c>, because in the specification its callback is
    /// the event handler processing algorithm rather than the function the script assigned.
    /// </summary>
    [TestCase(WebApiFeatures.Events, "new AbortController().signal", "onabort", "abort")]
    [TestCase(WebApiFeatures.Scheduler, "new TaskController().signal", "onprioritychange", "prioritychange")]
    [TestCase(WebApiFeatures.Messaging, "new BroadcastChannel('handlers')", "onmessage", "message")]
    [TestCase(WebApiFeatures.Messaging, "new MessageChannel().port1", "onmessage", "message")]
    public void RemoveEventListenerCannotReachAHandlerAttribute(
        WebApiFeatures features,
        string construct,
        string attribute,
        string type)
    {
        var engine = new Engine(options => options.UseWebApis(features | WebApiFeatures.Events));

        engine.Execute($$"""
            var log = [];
            var target = {{construct}};
            var handler = () => log.push('handler');
            target.{{attribute}} = handler;
            target.removeEventListener('{{type}}', handler);
            target.dispatchEvent(new Event('{{type}}'));
            """);

        Log(engine).Should().Be("handler");
        engine.Evaluate($"target.{attribute} === handler").AsBoolean().Should().BeTrue();
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join('|')").AsString();
}
#endif
