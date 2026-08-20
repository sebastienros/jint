#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.Abort;

/// <summary>
/// <c>AbortSignal.prototype</c> — the interface prototype object.
/// <para>
/// https://dom.spec.whatwg.org/#interface-AbortSignal
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>EventTarget.prototype</c>, so a signal has <c>addEventListener</c> and the
/// rest, and <c>signal instanceof EventTarget</c> holds.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class AbortSignalPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly AbortSignalConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString AbortSignalToStringTag = new("AbortSignal");

    internal AbortSignalPrototype(
        Engine engine,
        Realm realm,
        AbortSignalConstructor constructor,
        ObjectInstance eventTargetPrototype) : base(engine, realm)
    {
        _prototype = eventTargetPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-abortsignal-aborted
    /// </summary>
    [JsAccessor("aborted", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean AbortedGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).Aborted);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-abortsignal-reason
    /// </summary>
    [JsAccessor("reason", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue ReasonGet(JsValue thisObject) => Brand(thisObject).Reason;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-abortsignal-onabort — the <c>onabort</c> event handler IDL attribute,
    /// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes.
    /// </summary>
    /// <remarks>
    /// The handler is one entry of the signal's own event listener list, so it takes its turn in registration
    /// order among the <c>addEventListener('abort', …)</c> listeners rather than running before or after all
    /// of them. Reassigning the attribute replaces the value in place — the entry keeps the position it was
    /// first given, which is HTML's "activate an event handler" doing nothing once a listener exists — and
    /// assigning a non-object removes the entry outright, so a later assignment appends a fresh one at the
    /// end. It is invisible to <c>removeEventListener</c>, because in the specification its callback is the
    /// event handler processing algorithm rather than the function the script assigned.
    /// </remarks>
    [JsAccessor("onabort", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnAbortGet(JsValue thisObject)
    {
        return Brand(thisObject).FindEventHandler(JsAbortSignal.AbortEventType)?.Callback ?? Null;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-abortsignal-onabort, setter half. <c>EventHandler</c> is a nullable
    /// callback function annotated <c>[LegacyTreatNonObjectAsNull]</c>, so assigning anything that is not an
    /// object clears the handler rather than raising a <c>TypeError</c>. An object that is not callable is
    /// stored and read back — the getter returns what was assigned — but is never invoked, which is what
    /// HTML's processing algorithm amounts to for a value that cannot be called.
    /// </summary>
    [JsAccessor("onabort", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnAbortSet(JsValue thisObject, JsValue value)
    {
        var signal = Brand(thisObject);
        var existing = signal.FindEventHandler(JsAbortSignal.AbortEventType);

        if (value is not ObjectInstance)
        {
            // "Deactivate an event handler": the listener goes away entirely.
            if (existing is not null)
            {
                signal.RemoveListener(existing);
            }

            return Undefined;
        }

        if (existing is not null)
        {
            existing.Callback = value;
            return Undefined;
        }

        signal.AddListener(new EventListenerRegistration(JsAbortSignal.AbortEventType, value) { IsEventHandler = true });
        return Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-abortsignal-throwifaborted
    /// </summary>
    [JsFunction(Name = "throwIfAborted", Length = 0)]
    private JsValue ThrowIfAborted(JsValue thisObject)
    {
        var signal = Brand(thisObject);
        if (!signal.Aborted)
        {
            return Undefined;
        }

        // The reason is thrown as it is — it is whatever the aborter chose, and only defaults to an
        // AbortError DOMException when nobody chose anything.
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, signal.Reason, in location);
        return Undefined;
    }

    private JsAbortSignal Brand(JsValue thisObject)
    {
        if (thisObject is JsAbortSignal signal)
        {
            return signal;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not an AbortSignal");
        return null!;
    }
}
#endif
