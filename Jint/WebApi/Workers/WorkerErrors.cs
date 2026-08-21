#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;

namespace Jint.WebApi.Workers;

/// <summary>
/// The two refusals this feature raises at script, and the one-line event-handler attribute plumbing its two
/// façades share.
/// </summary>
internal static class WorkerErrors
{
    /// <summary>
    /// https://webidl.spec.whatwg.org/#quotaexceedederror, with the ceiling and the count the refused
    /// operation would have reached — the same shape the timer queue and the socket ceiling already refuse
    /// with, so <c>e.constructor === QuotaExceededError</c> and <c>e.quota</c> answer as a script expects.
    /// </summary>
    internal static void ThrowQuotaExceededError(Engine engine, Realm realm, string message, double quota, double requested)
    {
        var exception = realm.Intrinsics.QuotaExceededError.CreateException(message, quota, requested);
        var location = engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(engine, exception, in location);
    }

    /// <summary>
    /// A <c>DOMException</c> of the named kind. <c>SecurityError</c> is what a provider's refusal reaches the
    /// script as: it is a policy decision rather than a fetch failure, and it is the shape a browser already
    /// throws synchronously from <c>new Worker()</c> for a script it will not run.
    /// </summary>
    internal static void ThrowDomException(Engine engine, Realm realm, string name, string message)
    {
        var exception = realm.Intrinsics.DomException.CreateException(name, message);
        var location = engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(engine, exception, in location);
    }

    /// <summary>
    /// The getter half of an event handler IDL attribute,
    /// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes.
    /// </summary>
    internal static JsValue GetEventHandler(JsEventTarget target, string type)
        => target.FindEventHandler(type)?.Callback ?? JsValue.Null;

    /// <summary>
    /// HTML's "set the current value of the event handler". <c>EventHandler</c> is a nullable callback
    /// function annotated <c>[LegacyTreatNonObjectAsNull]</c>, so assigning anything that is not an object
    /// clears the handler rather than raising a <c>TypeError</c>; reassigning replaces the value in place, so
    /// the listener keeps the position it was first given among the <c>addEventListener</c> ones.
    /// </summary>
    /// <remarks>
    /// <b>It deliberately does not start anything.</b> <c>MessagePort</c>'s <c>onmessage</c> setter enables
    /// that port's queue, because the specification scopes that rule to the <c>MessagePort</c> interface; the
    /// <c>MessageEventTarget</c> mixin a <c>Worker</c> and a worker's global scope include carries no such
    /// rule, so on those two <c>addEventListener('message', …)</c> alone has to receive — the exact opposite
    /// of a <c>MessageChannel</c>, where <c>start()</c> is required. Both façades are therefore enabled by the
    /// engine rather than by an assignment.
    /// </remarks>
    internal static void SetEventHandler(JsEventTarget target, string type, JsValue value)
    {
        var existing = target.FindEventHandler(type);

        if (value is not ObjectInstance)
        {
            // "Deactivate an event handler": the listener goes away entirely.
            if (existing is not null)
            {
                target.RemoveListener(existing);
            }

            return;
        }

        if (existing is not null)
        {
            existing.Callback = value;
            return;
        }

        target.AddListener(new EventListenerRegistration(type, value) { IsEventHandler = true });
    }
}
#endif
