#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.Scheduling;

/// <summary>
/// <c>TaskSignal.prototype</c> — the interface prototype object.
/// <para>
/// https://wicg.github.io/scheduling-apis/#tasksignal
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>AbortSignal.prototype</c>, so a task signal has <c>aborted</c>,
/// <c>reason</c>, <c>throwIfAborted()</c> and the whole of <c>EventTarget</c>, and
/// <c>signal instanceof AbortSignal</c> holds.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class TaskSignalPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly TaskSignalConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString TaskSignalToStringTag = new("TaskSignal");

    internal TaskSignalPrototype(
        Engine engine,
        Realm realm,
        TaskSignalConstructor constructor,
        ObjectInstance abortSignalPrototype) : base(engine, realm)
    {
        _prototype = abortSignalPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#dom-tasksignal-priority — "the priority getter steps are to
    /// return this's priority".
    /// </summary>
    [JsAccessor("priority", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString PriorityGet(JsValue thisObject) => TaskPriorityNames.ToJsString(Brand(thisObject).Priority);

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#dom-tasksignal-onprioritychange — an event handler IDL
    /// attribute, https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes, whose
    /// event handler event type is <c>prioritychange</c>.
    /// </summary>
    /// <remarks>
    /// The same registration mechanics <c>onabort</c> has: the handler is one entry of the signal's own
    /// listener list, so it takes its turn in registration order among the
    /// <c>addEventListener('prioritychange', …)</c> listeners; reassigning replaces the value in place, and
    /// assigning a non-object removes the entry outright.
    /// </remarks>
    [JsAccessor("onprioritychange", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnPriorityChangeGet(JsValue thisObject)
    {
        return Brand(thisObject).FindEventHandler(JsTaskSignal.PriorityChangeEventType)?.Callback ?? Null;
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#dom-tasksignal-onprioritychange, setter half. <c>EventHandler</c>
    /// is a nullable callback function annotated <c>[LegacyTreatNonObjectAsNull]</c>, so assigning anything
    /// that is not an object clears the handler rather than raising a <c>TypeError</c>.
    /// </summary>
    [JsAccessor("onprioritychange", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnPriorityChangeSet(JsValue thisObject, JsValue value)
    {
        var signal = Brand(thisObject);
        var existing = signal.FindEventHandler(JsTaskSignal.PriorityChangeEventType);

        if (value is not ObjectInstance)
        {
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

        signal.AddListener(new EventListenerRegistration(JsTaskSignal.PriorityChangeEventType, value) { IsEventHandler = true });
        return Undefined;
    }

    private JsTaskSignal Brand(JsValue thisObject)
    {
        if (thisObject is JsTaskSignal signal)
        {
            return signal;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a TaskSignal");
        return null!;
    }
}
#endif
