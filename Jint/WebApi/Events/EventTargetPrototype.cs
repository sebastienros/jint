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
/// The three operations are non-enumerable here, where a WebIDL interface prototype object's operations are
/// enumerable — the same documented simplification <c>console</c> and <c>Event.prototype</c> carry, and the
/// only thing about this object that is not what a browser exposes.
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
    [JsFunction(Name = "addEventListener", Length = 2)]
    private JsValue AddEventListener(JsValue thisObject, JsCallArguments arguments)
    {
        var target = Brand(thisObject);
        RequireArguments(arguments, 2, "addEventListener");

        var type = TypeConverter.ToString(arguments.At(0));
        var callback = ReadCallback(arguments.At(1), "addEventListener");
        var options = FlattenMoreOptions(arguments.At(2));

        target.AddListener(new EventListenerRegistration(type, callback)
        {
            Capture = options.Capture,
            Passive = options.Passive,
            Once = options.Once,
            Signal = options.Signal,
        });

        return Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-removeeventlistener
    /// </summary>
    [JsFunction(Name = "removeEventListener", Length = 2)]
    private JsValue RemoveEventListener(JsValue thisObject, JsCallArguments arguments)
    {
        var target = Brand(thisObject);
        RequireArguments(arguments, 2, "removeEventListener");

        var type = TypeConverter.ToString(arguments.At(0));
        var callback = ReadCallback(arguments.At(1), "removeEventListener");
        var capture = FlattenOptions(arguments.At(2));

        target.RemoveListener(type, callback, capture);
        return Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-dispatchevent
    /// </summary>
    [JsFunction(Name = "dispatchEvent", Length = 1)]
    private JsBoolean DispatchEvent(JsValue thisObject, JsValue eventArgument)
    {
        var target = Brand(thisObject);

        if (eventArgument is not JsEvent ev)
        {
            Throw.TypeError(_realm, "Failed to execute 'dispatchEvent' on 'EventTarget': parameter 1 is not of type 'Event'.");
            return null!;
        }

        // Step 1. Every event this engine can produce has its initialized flag set from construction, so the
        // only way here is a re-entrant dispatch of an event that is already being dispatched.
        if (ev.DispatchFlag)
        {
            var invalidState = _realm.Intrinsics.DomException.CreateException(
                DomExceptionNames.InvalidState,
                "Failed to execute 'dispatchEvent' on 'EventTarget': the event is already being dispatched.");

            var location = _engine._lastSyntaxElement?.Location ?? default;
            Throw.JavaScriptException(_engine, invalidState, in location);
        }

        // Step 2: an event a script dispatches is never trusted, whoever created it.
        ev.IsTrusted = false;

        return JsBoolean.Create(target.DispatchEvent(ev));
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
    private ListenerOptions FlattenMoreOptions(JsValue options)
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
                Throw.TypeError(_realm, "Failed to execute 'addEventListener' on 'EventTarget': member signal is not of type 'AbortSignal'.");
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
    private JsValue ReadCallback(JsValue callback, string operationName)
    {
        if (callback.IsUndefined() || callback.IsNull())
        {
            return Null;
        }

        if (callback is not ObjectInstance)
        {
            Throw.TypeError(_realm, $"Failed to execute '{operationName}' on 'EventTarget': parameter 2 is not of type 'EventListener'.");
        }

        return callback;
    }

    /// <summary>
    /// WebIDL's arity check: an operation whose required arguments were not all supplied raises a
    /// <c>TypeError</c> before anything else is converted.
    /// </summary>
    private void RequireArguments(JsCallArguments arguments, int required, string operationName)
    {
        if (arguments.Length < required)
        {
            Throw.TypeError(
                _realm,
                $"Failed to execute '{operationName}' on 'EventTarget': {required} arguments required, but only {arguments.Length} present.");
        }
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

    /// <summary>
    /// The result of https://dom.spec.whatwg.org/#event-flatten-more.
    /// </summary>
    private readonly record struct ListenerOptions(bool Capture, bool Passive, bool Once, JsAbortSignal? Signal);
}
#endif
