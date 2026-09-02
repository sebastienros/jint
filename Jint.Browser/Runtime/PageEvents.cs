using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.Browser.Runtime;

/// <summary>
/// The events a navigation, a history traversal and a form submission fire, and the one way they are
/// dispatched.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every one is a Jint event on Jint's dispatcher</b>, for the reason the design gives: AngleSharp's own
/// firing goes into AngleSharp's listener lists, which hold nothing a script registered, so an event a page
/// can hear has to be dispatched here.
/// </para>
/// <para>
/// <b>The payload members are own data properties of the event, not accessors on an interface prototype.</b>
/// A browser has a <c>PopStateEvent</c>, a <c>HashChangeEvent</c>, a <c>SubmitEvent</c> and a
/// <c>FormDataEvent</c>, each with its own prototype and its own <c>@@toStringTag</c>; here each is an
/// <c>Event</c> carrying the members that interface declares. What a page reads — <c>e.state</c>,
/// <c>e.newURL</c>, <c>e.submitter</c>, <c>e.formData</c> — answers exactly as it should; what differs is
/// <c>Object.prototype.toString.call(e)</c>, <c>e instanceof PopStateEvent</c> and the fact that the members
/// are own rather than inherited. Declaring four interfaces is the change that closes it, and it is a
/// binding change rather than a runtime one.
/// </para>
/// </remarks>
internal static class PageEvents
{
    /// <summary>Fires a plain trusted event at <paramref name="target"/> and answers whether it survived.</summary>
    internal static bool Fire(PageRuntime runtime, JsEventTarget target, string type, bool bubbles = false, bool cancelable = false)
        => Dispatch(runtime, target, Create(runtime, type, bubbles, cancelable));

    /// <summary>Creates a trusted event of <paramref name="type"/> in the runtime's realm.</summary>
    internal static JsEvent Create(PageRuntime runtime, string type, bool bubbles = false, bool cancelable = false)
        => runtime.Engine._mainRealm.Intrinsics.Event.CreateTrustedEvent(
            JsString.Create(type),
            new EventInit(bubbles, cancelable, Composed: false));

    /// <summary>
    /// Dispatches <paramref name="ev"/> and answers <see langword="false"/> when a listener cancelled it.
    /// </summary>
    /// <remarks>
    /// A listener that throws is already the diagnostics sink's business — the engine reports it and carries
    /// on to the next listener — so what is caught here is only what a page with no sink would otherwise
    /// erupt with, which must never fault the navigation that dispatched it.
    /// </remarks>
    internal static bool Dispatch(PageRuntime runtime, JsEventTarget target, JsEvent ev)
    {
        try
        {
            return target.DispatchEvent(ev);
        }
        catch (JavaScriptException exception)
        {
            runtime.Recorder.Add(new PageError(
                PageErrorKind.UncaughtCallbackError,
                PageRecorder.Diagnostics.Describe(exception.Error, exception),
                ev.TypeName));
            return true;
        }
    }

    /// <summary>Adds an own, read-only data member to an event, the way an interface attribute reads.</summary>
    /// <remarks>
    /// Enumerable, because WebIDL's interface members are — the rule
    /// <c>WebIdlPropertyAttributeTests</c> holds every generated member to — and non-writable, because an
    /// event's attributes are getters with no setter.
    /// </remarks>
    internal static void Member(JsEvent ev, string name, JsValue value)
        => ev.DefineOwnPropertyUnchecked(name, new PropertyDescriptor(value, PropertyFlag.OnlyEnumerable));

    /// <summary>
    /// HTML's <c>BeforeUnloadEvent</c>: cancelable, and cancelled by three different things a page may do.
    /// </summary>
    /// <remarks>
    /// <c>returnValue</c> is writable, which is the one member of these events that has to be: assigning a
    /// non-empty string to it is one of HTML's three ways of asking to stay, alongside
    /// <c>preventDefault()</c> and returning a value from an <c>onbeforeunload</c> handler. The third is what
    /// <see cref="JsBeforeUnloadEvent.CancelsOnNonNullHandlerResult"/> is for.
    /// </remarks>
    internal static JsBeforeUnloadEvent BeforeUnload(PageRuntime runtime)
    {
        var ev = new JsBeforeUnloadEvent(runtime.Engine, JsString.Create("beforeunload"));
        ev.IsTrusted = true;
        ev.DefineOwnPropertyUnchecked("returnValue", new PropertyDescriptor(JsString.Empty, PropertyFlag.ConfigurableEnumerableWritable));
        return ev;
    }

    /// <summary>Whether a <c>beforeunload</c> event asked the page to stay.</summary>
    internal static bool AskedToStay(JsBeforeUnloadEvent ev)
    {
        if (ev.CanceledFlag)
        {
            return true;
        }

        var returnValue = ev.Get("returnValue");
        return returnValue.IsString() && returnValue.AsString().Length != 0;
    }
}

/// <summary>
/// <c>beforeunload</c>: the one event whose <c>onbeforeunload</c> handler cancels by returning a value
/// rather than by returning <see langword="false"/>.
/// </summary>
/// <remarks>
/// It exists as a class only for that override. Everything else about it — the type name, the cancelable
/// flag, the <c>returnValue</c> member — is set by <see cref="PageEvents.BeforeUnload"/>, and a page cannot
/// tell it from an <c>Event</c> except by the rule.
/// </remarks>
internal sealed class JsBeforeUnloadEvent : JsEvent
{
    internal JsBeforeUnloadEvent(Engine engine, JsString type)
        : base(engine, type, new EventInit(Bubbles: false, Cancelable: true, Composed: false), EventConstructor.TimeStampNow(engine))
    {
        _prototype = engine._mainRealm.Intrinsics.Event.PrototypeObject;
    }

    /// <inheritdoc />
    internal override bool CancelsOnNonNullHandlerResult => true;
}
