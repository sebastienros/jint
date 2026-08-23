#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Abort;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Events;

/// <summary>
/// <c>EventTarget.prototype</c> — the interface prototype object.
/// <para>
/// https://dom.spec.whatwg.org/#interface-eventtarget
/// </para>
/// </summary>
/// <remarks>
/// The three operations carry a WebIDL regular operation's property attributes
/// (https://webidl.spec.whatwg.org/#es-operations), so <c>Object.keys(EventTarget.prototype)</c> lists all
/// three exactly as it does in a browser.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class EventTargetPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly EventTargetConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString EventTargetToStringTag = new("EventTarget");

    internal EventTargetPrototype(
        Engine engine,
        Realm realm,
        EventTargetConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-addeventlistener
    /// </summary>
    [JsFunction(Name = "addEventListener", Length = 2, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue AddEventListener(JsValue thisObject, JsCallArguments arguments)
    {
        var target = Brand(thisObject);
        EventTargetArguments.AddListener(_realm, target, arguments);
        return Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-removeeventlistener
    /// </summary>
    [JsFunction(Name = "removeEventListener", Length = 2, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue RemoveEventListener(JsValue thisObject, JsCallArguments arguments)
    {
        var target = Brand(thisObject);
        EventTargetArguments.RemoveListener(_realm, target, arguments);
        return Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-dispatchevent
    /// </summary>
    [JsFunction(Name = "dispatchEvent", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsBoolean DispatchEvent(JsValue thisObject, JsValue eventArgument)
    {
        var target = Brand(thisObject);
        return JsBoolean.Create(EventTargetArguments.DispatchEvent(_engine, _realm, target, eventArgument));
    }

    private JsEventTarget Brand(JsValue thisObject)
    {
        if (thisObject is JsEventTarget target)
        {
            return target;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not an EventTarget");
        return null!;
    }
}

/// <summary>
/// The WebIDL argument conversions of <c>EventTarget</c>'s three operations, and the operations themselves
/// once a receiver has been established.
/// </summary>
/// <remarks>
/// Lifted out of <see cref="EventTargetPrototype"/> because the global <c>addEventListener</c>,
/// <c>removeEventListener</c> and <c>dispatchEvent</c> — which are not methods of this prototype at all, being
/// bound to the engine's synthetic global target — have to convert their arguments <i>identically</i>, down to
/// the message a <c>TypeError</c> carries. A browser reaches the same conclusion by a different route: its
/// <c>Window</c> inherits these very functions from <c>EventTarget.prototype</c>, which is why its arity error
/// also says "on 'EventTarget'" rather than "on 'Window'".
/// </remarks>
internal static class EventTargetArguments
{
    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-addeventlistener over an already-branded target.
    /// </summary>
    internal static void AddListener(Realm realm, JsEventTarget target, JsCallArguments arguments)
    {
        RequireArguments(realm, arguments, 2, "addEventListener");

        var type = TypeConverter.ToString(arguments.At(0));
        var callback = ReadCallback(realm, arguments.At(1), "addEventListener");
        var options = FlattenMoreOptions(realm, arguments.At(2));

        target.AddListener(new EventListenerRegistration(type, callback)
        {
            Capture = options.Capture,
            Passive = options.Passive,
            Once = options.Once,
            Signal = options.Signal,
        });
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-removeeventlistener over an already-branded target.
    /// </summary>
    internal static void RemoveListener(Realm realm, JsEventTarget target, JsCallArguments arguments)
    {
        RequireArguments(realm, arguments, 2, "removeEventListener");

        var type = TypeConverter.ToString(arguments.At(0));
        var callback = ReadCallback(realm, arguments.At(1), "removeEventListener");
        var capture = FlattenOptions(arguments.At(2));

        target.RemoveListener(type, callback, capture);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-dispatchevent over an already-branded target.
    /// </summary>
    /// <returns>False when the event was canceled, which is what <c>dispatchEvent</c> returns.</returns>
    internal static bool DispatchEvent(Engine engine, Realm realm, JsEventTarget target, JsValue eventArgument)
    {
        if (eventArgument is not JsEvent ev)
        {
            Throw.TypeError(realm, "Failed to execute 'dispatchEvent' on 'EventTarget': parameter 1 is not of type 'Event'.");
            return false;
        }

        // Step 1. Every event this engine can produce has its initialized flag set from construction, so the
        // only way here is a re-entrant dispatch of an event that is already being dispatched.
        if (ev.DispatchFlag)
        {
            var invalidState = realm.Intrinsics.DomException.CreateException(
                DomExceptionNames.InvalidState,
                "Failed to execute 'dispatchEvent' on 'EventTarget': the event is already being dispatched.");

            var location = engine._lastSyntaxElement?.Location ?? default;
            Throw.JavaScriptException(engine, invalidState, in location);
        }

        // Step 2: an event a script dispatches is never trusted, whoever created it.
        ev.IsTrusted = false;

        return target.DispatchEvent(ev);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-flatten-options — a boolean is the capture flag itself, anything
    /// else is a dictionary whose <c>capture</c> member it is.
    /// </summary>
    private static bool FlattenOptions(JsValue options)
    {
        if (options is ObjectInstance dictionary)
        {
            return TypeConverter.ToBoolean(dictionary.Get(CommonEventProperties.Capture));
        }

        // The union `(EventListenerOptions or boolean)` resolves everything that is not an object to its
        // boolean member, so `addEventListener(t, f, 1)` captures and `addEventListener(t, f)` does not.
        return TypeConverter.ToBoolean(options);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#event-flatten-more. The members are read in the order WebIDL converts an
    /// inherited dictionary's members in: <c>capture</c> first, then <c>once</c>, <c>passive</c>,
    /// <c>signal</c>.
    /// </summary>
    private static ListenerOptions FlattenMoreOptions(Realm realm, JsValue options)
    {
        var capture = FlattenOptions(options);

        if (options is not ObjectInstance dictionary)
        {
            return new ListenerOptions(capture, Passive: false, Once: false, Signal: null);
        }

        var once = TypeConverter.ToBoolean(dictionary.Get(CommonEventProperties.Once));

        // "If options["passive"] exists" — an absent member leaves passive null, and the default passive
        // value is false for every type an engine without a document can see.
        var passive = TypeConverter.ToBoolean(dictionary.Get(CommonEventProperties.Passive));

        // `AbortSignal signal` is a non-nullable interface type with no default: an absent member means no
        // signal, and anything present that is not an AbortSignal — null included — is a TypeError.
        var signalValue = dictionary.Get(CommonEventProperties.Signal);
        JsAbortSignal? signal = null;
        if (!signalValue.IsUndefined())
        {
            signal = signalValue as JsAbortSignal;
            if (signal is null)
            {
                Throw.TypeError(realm, "Failed to execute 'addEventListener' on 'EventTarget': member signal is not of type 'AbortSignal'.");
            }
        }

        return new ListenerOptions(capture, passive, once, signal);
    }

    /// <summary>
    /// The <c>EventListener?</c> callback interface conversion,
    /// https://webidl.spec.whatwg.org/#es-callback-interface: <see langword="null"/> and
    /// <see langword="undefined"/> give the null callback, an object is taken as it is, and anything else is a
    /// <c>TypeError</c>.
    /// </summary>
    private static JsValue ReadCallback(Realm realm, JsValue callback, string operationName)
    {
        if (callback.IsUndefined() || callback.IsNull())
        {
            return JsValue.Null;
        }

        if (callback is not ObjectInstance)
        {
            Throw.TypeError(realm, $"Failed to execute '{operationName}' on 'EventTarget': parameter 2 is not of type 'EventListener'.");
        }

        return callback;
    }

    /// <summary>
    /// WebIDL's arity check: an operation whose required arguments were not all supplied raises a
    /// <c>TypeError</c> before anything else is converted.
    /// </summary>
    private static void RequireArguments(Realm realm, JsCallArguments arguments, int required, string operationName)
    {
        if (arguments.Length < required)
        {
            Throw.TypeError(
                realm,
                $"Failed to execute '{operationName}' on 'EventTarget': {required} arguments required, but only {arguments.Length} present.");
        }
    }

    /// <summary>
    /// The result of https://dom.spec.whatwg.org/#event-flatten-more.
    /// </summary>
    private readonly record struct ListenerOptions(bool Capture, bool Passive, bool Once, JsAbortSignal? Signal);
}
#endif
