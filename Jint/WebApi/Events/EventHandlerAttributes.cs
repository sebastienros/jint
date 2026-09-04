#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;

namespace Jint.WebApi.Events;

/// <summary>
/// The two halves of an event handler IDL attribute — <c>onload</c>, <c>onerror</c> and their kind.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// A handler is <b>one entry of the target's own event listener list</b>, so it takes its turn in
/// registration order among the <c>addEventListener</c> listeners rather than running before or after all of
/// them. Reassigning replaces the value in place — the entry keeps the position it was first given — and
/// assigning a non-object removes the entry outright.
/// </para>
/// <para>
/// <c>EventHandler</c> is a nullable callback function annotated <c>[LegacyTreatNonObjectAsNull]</c>, which
/// is why assigning a number or a string clears the handler rather than raising a <c>TypeError</c>; an object
/// that is not callable is stored and read back but never invoked.
/// </para>
/// <para>
/// <b>The entry is invisible to <c>removeEventListener</c></b>, because in the specification its callback is
/// the event handler processing algorithm rather than the function the script assigned. Clearing it and
/// assigning again therefore <i>appends</i> a fresh entry at the end of the list, since the one it had is
/// gone: "activate an event handler" does nothing once a listener exists, and "deactivate" removes it
/// outright.
/// </para>
/// <para>
/// <b>Setting a handler starts nothing.</b> Exactly one interface in the engine disagrees —
/// <c>MessagePort</c>, whose <c>onmessage</c> setter must also enable the port message queue
/// (https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messageport-onmessage) — and it says so by
/// calling <see cref="Set"/> and then <c>start()</c>, in that order, rather than by this method growing a
/// hook. The rule is scoped to that interface: the <c>MessageEventTarget</c> mixin a <c>Worker</c> and a
/// worker's global scope include carries no such rule, so on those <c>addEventListener('message', …)</c>
/// alone has to receive and both façades are enabled by the engine instead. A caller that needs a post-set
/// step writes it beside the call, where a reader can see it.
/// </para>
/// </remarks>
internal static class EventHandlerAttributes
{
    /// <summary>The handler currently registered for <paramref name="type"/>, or <c>null</c>.</summary>
    internal static JsValue Get(JsEventTarget target, string type)
        => target.FindEventHandler(type)?.Callback ?? JsValue.Null;

    /// <summary>Installs, replaces or removes the handler for <paramref name="type"/>.</summary>
    internal static JsValue Set(JsEventTarget target, string type, JsValue value)
    {
        var existing = target.FindEventHandler(type);

        if (value is not ObjectInstance)
        {
            if (existing is not null)
            {
                target.RemoveListener(existing);
            }

            return JsValue.Undefined;
        }

        if (existing is not null)
        {
            existing.Callback = value;
            return JsValue.Undefined;
        }

        target.AddListener(new EventListenerRegistration(type, value) { IsEventHandler = true });
        return JsValue.Undefined;
    }
}
#endif
