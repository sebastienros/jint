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
