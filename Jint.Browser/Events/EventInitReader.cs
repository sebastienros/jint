using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// The dictionary members every UI event constructor reads, converted per
/// <a href="https://webidl.spec.whatwg.org/#es-dictionary">WebIDL's dictionary conversion</a>.
/// </summary>
/// <remarks>
/// <para>
/// A dictionary that is <see langword="undefined"/> or <see langword="null"/> gives every member its declared
/// default, and an absent member gives its own — which is why every read goes through a helper carrying the
/// default rather than through <c>Get</c> plus a conversion. Anything that is not an object is refused by
/// <see cref="EventConstructor.ReadEventInit"/>, which every constructor calls first, so the readers here can
/// assume the value is an object or absent.
/// </para>
/// <para>
/// The names are interned once. A page constructing a <c>MouseEvent</c> per pointer move would otherwise
/// allocate one <see cref="JsString"/> per member per event.
/// </para>
/// </remarks>
internal static class EventInitReader
{
    private static readonly JsString _view = new("view");
    private static readonly JsString _detail = new("detail");
    private static readonly JsString _which = new("which");
    private static readonly JsString _screenX = new("screenX");
    private static readonly JsString _screenY = new("screenY");
    private static readonly JsString _clientX = new("clientX");
    private static readonly JsString _clientY = new("clientY");
    private static readonly JsString _button = new("button");
    private static readonly JsString _buttons = new("buttons");
    private static readonly JsString _relatedTarget = new("relatedTarget");
    private static readonly JsString _ctrlKey = new("ctrlKey");
    private static readonly JsString _shiftKey = new("shiftKey");
    private static readonly JsString _altKey = new("altKey");
    private static readonly JsString _metaKey = new("metaKey");
    private static readonly JsString _modifierAltGraph = new("modifierAltGraph");
    private static readonly JsString _modifierCapsLock = new("modifierCapsLock");
    private static readonly JsString _modifierFn = new("modifierFn");
    private static readonly JsString _modifierFnLock = new("modifierFnLock");
    private static readonly JsString _modifierHyper = new("modifierHyper");
    private static readonly JsString _modifierNumLock = new("modifierNumLock");
    private static readonly JsString _modifierScrollLock = new("modifierScrollLock");
    private static readonly JsString _modifierSuper = new("modifierSuper");
    private static readonly JsString _modifierSymbol = new("modifierSymbol");
    private static readonly JsString _modifierSymbolLock = new("modifierSymbolLock");

    /// <summary>The dictionary itself, or <see langword="null"/> when it was absent, undefined or null.</summary>
    internal static ObjectInstance? Dictionary(JsValue[] arguments)
        => arguments.Length > 1 ? arguments[1] as ObjectInstance : null;

    /// <summary>A <c>boolean</c> member — https://webidl.spec.whatwg.org/#es-boolean.</summary>
    internal static bool Bool(ObjectInstance? init, JsString name, bool fallback = false)
    {
        var value = init?.Get(name);
        return value is null || value.IsUndefined() ? fallback : TypeConverter.ToBoolean(value);
    }

    /// <summary>
    /// A <c>long</c> / <c>double</c> / <c>float</c> member. All three land on a JavaScript number in the end,
    /// and the integral ones are truncated by <see cref="Long"/> at the call site that needs the truncation to
    /// be observable.
    /// </summary>
    internal static double Number(ObjectInstance? init, JsString name, double fallback = 0)
    {
        var value = init?.Get(name);
        return value is null || value.IsUndefined() ? fallback : TypeConverter.ToNumber(value);
    }

    /// <summary>A <c>long</c> member — https://webidl.spec.whatwg.org/#idl-long.</summary>
    internal static double Long(ObjectInstance? init, JsString name, double fallback = 0)
    {
        var value = init?.Get(name);
        return value is null || value.IsUndefined() ? fallback : TypeConverter.ToInt32(value);
    }

    /// <summary>An <c>unsigned short</c> member — https://webidl.spec.whatwg.org/#idl-unsigned-short.</summary>
    internal static double UnsignedShort(ObjectInstance? init, JsString name, double fallback = 0)
    {
        var value = init?.Get(name);
        return value is null || value.IsUndefined() ? fallback : TypeConverter.ToUint16(value);
    }

    /// <summary>A <c>DOMString</c> member — https://webidl.spec.whatwg.org/#es-DOMString.</summary>
    internal static string Text(ObjectInstance? init, JsString name, string fallback = "")
    {
        var value = init?.Get(name);
        return value is null || value.IsUndefined() ? fallback : TypeConverter.ToString(value);
    }

    /// <summary>
    /// A <c>DOMString?</c> member — https://webidl.spec.whatwg.org/#idl-nullable-type. Both an absent member
    /// and an explicit <c>null</c> answer <see langword="null"/>, which is the difference from
    /// <see cref="Text"/>: a <c>StorageEvent</c> whose <c>key</c> is null is the one that says the whole area
    /// was cleared, and the empty string would be a different event.
    /// </summary>
    internal static string? NullableText(ObjectInstance? init, JsString name)
    {
        var value = init?.Get(name);
        return value is null || value.IsUndefined() || value.IsNull() ? null : TypeConverter.ToString(value);
    }

    /// <summary>An <c>any</c> member, whose absence is <see langword="undefined"/> unless a default says otherwise.</summary>
    internal static JsValue Any(ObjectInstance? init, JsString name, JsValue fallback)
    {
        var value = init?.Get(name);
        return value is null || value.IsUndefined() ? fallback : value;
    }

    /// <summary>
    /// A member whose absence has to stay distinguishable from an explicit zero — the legacy
    /// <c>keyCode</c>/<c>charCode</c>, where "the dictionary said nothing" takes the legacy table and "the
    /// dictionary said zero" is honoured.
    /// </summary>
    internal static double? OptionalUnsignedLong(ObjectInstance? init, JsString name)
    {
        var value = init?.Get(name);
        return value is null || value.IsUndefined() ? null : TypeConverter.ToUint32(value);
    }

    /// <summary>
    /// An <c>EventTarget?</c> member. A DOM wrapper is already one; the global object is the window, which the
    /// engine backs with its synthetic global target the way
    /// <c>EventTarget.prototype.addEventListener.call(window, …)</c> does.
    /// </summary>
    internal static JsEventTarget? Target(Engine engine, ObjectInstance? init, JsString name)
    {
        var value = init?.Get(name);
        return value is null ? null : TargetOf(engine, value);
    }

    /// <summary>The same conversion for a value that did not come from a dictionary.</summary>
    internal static JsEventTarget? TargetOf(Engine engine, JsValue value)
    {
        if (value is JsEventTarget target)
        {
            return target;
        }

        if (ReferenceEquals(value, engine._mainRealm.GlobalObject))
        {
            return engine._webApi?.GlobalEventTarget;
        }

        return null;
    }

    /// <summary>
    /// https://w3c.github.io/uievents/#dictdef-uievent-init — the members every UI event inherits.
    /// </summary>
    /// <remarks>
    /// <c>which</c> stays nullable so that "the dictionary said nothing" is distinguishable from "the
    /// dictionary said zero": the first lets each interface compute its own legacy value, the second is
    /// honoured as given.
    /// </remarks>
    internal static (JsValue View, double Detail, double? Which) UiInit(Engine engine, ObjectInstance? init)
        => (View(engine, init), Long(init, _detail), OptionalUnsignedLong(init, _which));

    /// <summary>
    /// The <c>view</c> member on its own — https://w3c.github.io/uievents/#dom-uievent-view, whose IDL type is
    /// <c>Window?</c> and therefore a WebIDL interface type: anything that is not a <c>Window</c> and not null
    /// is a <c>TypeError</c> rather than a value quietly kept.
    /// </summary>
    /// <remarks>
    /// This engine's <c>Window</c> is the global object itself (the page runtime's window installer says
    /// why), so the check is an identity comparison against it — which is exactly as narrow as a browser's,
    /// there being one window per page here.
    /// </remarks>
    internal static JsValue View(Engine engine, ObjectInstance? init)
    {
        var view = Any(init, _view, JsValue.Null);

        if (view.IsNull() || view.IsUndefined())
        {
            return JsValue.Null;
        }

        if (!ReferenceEquals(view, engine._mainRealm.GlobalObject))
        {
            Throw.TypeError(
                engine.Realm,
                "Failed to construct 'UIEvent': member view is not of type 'Window'.");
        }

        return view;
    }

    /// <summary>
    /// https://w3c.github.io/uievents/#dictdef-eventmodifierinit — the fourteen modifier members,
    /// four of which are also IDL attributes.
    /// </summary>
    internal static EventModifiers Modifiers(ObjectInstance? init)
    {
        if (init is null)
        {
            return EventModifiers.None;
        }

        var modifiers = EventModifiers.None;
        Add(ref modifiers, init, _ctrlKey, EventModifiers.Control);
        Add(ref modifiers, init, _shiftKey, EventModifiers.Shift);
        Add(ref modifiers, init, _altKey, EventModifiers.Alt);
        Add(ref modifiers, init, _metaKey, EventModifiers.Meta);
        Add(ref modifiers, init, _modifierAltGraph, EventModifiers.AltGraph);
        Add(ref modifiers, init, _modifierCapsLock, EventModifiers.CapsLock);
        Add(ref modifiers, init, _modifierFn, EventModifiers.Fn);
        Add(ref modifiers, init, _modifierFnLock, EventModifiers.FnLock);
        Add(ref modifiers, init, _modifierHyper, EventModifiers.Hyper);
        Add(ref modifiers, init, _modifierNumLock, EventModifiers.NumLock);
        Add(ref modifiers, init, _modifierScrollLock, EventModifiers.ScrollLock);
        Add(ref modifiers, init, _modifierSuper, EventModifiers.Super);
        Add(ref modifiers, init, _modifierSymbol, EventModifiers.Symbol);
        Add(ref modifiers, init, _modifierSymbolLock, EventModifiers.SymbolLock);
        return modifiers;

        static void Add(ref EventModifiers modifiers, ObjectInstance init, JsString name, EventModifiers flag)
        {
            if (Bool(init, name))
            {
                modifiers |= flag;
            }
        }
    }

    /// <summary>https://w3c.github.io/uievents/#dictdef-mouseevent-init.</summary>
    internal static MouseEventState MouseInit(Engine engine, ObjectInstance? init)
        => new(
            Long(init, _screenX),
            Long(init, _screenY),
            Long(init, _clientX),
            Long(init, _clientY),
            init is null ? 0 : (short) TypeConverter.ToUint16(Any(init, _button, JsNumber.PositiveZero)),
            UnsignedShort(init, _buttons),
            Modifiers(init),
            Target(engine, init, _relatedTarget));

    /// <summary>The <c>relatedTarget</c> member on its own, for <c>FocusEventInit</c>.</summary>
    internal static JsEventTarget? RelatedTarget(Engine engine, ObjectInstance? init)
        => Target(engine, init, _relatedTarget);

    /// <summary>
    /// A <c>double?</c> member — https://webidl.spec.whatwg.org/#idl-nullable-type. The distinction from
    /// <see cref="Number"/> is the whole point: an axis a device cannot read is <c>null</c> rather than zero,
    /// and the two are what a page branches on.
    /// </summary>
    internal static double? NullableNumber(ObjectInstance? init, JsString name)
    {
        var value = init?.Get(name);
        return value is null || value.IsUndefined() || value.IsNull() ? null : TypeConverter.ToNumber(value);
    }

    /// <summary>
    /// https://w3c.github.io/deviceorientation/#dictdef-devicemotioneventaccelerationinit — a dictionary-typed
    /// member with <b>no</b> default, so an absent one leaves the IDL attribute null and a present one (which
    /// per https://webidl.spec.whatwg.org/#es-dictionary includes <c>null</c>) produces a real object whose
    /// axes are the three <c>double?</c> members.
    /// </summary>
    internal static JsValue Acceleration(BrowserEventRealm realm, ObjectInstance? init, JsString name)
    {
        var value = init?.Get(name);
        if (value is null || value.IsUndefined())
        {
            return JsValue.Null;
        }

        var members = value as ObjectInstance;
        return realm.NewAcceleration(
            NullableNumber(members, _x),
            NullableNumber(members, _y),
            NullableNumber(members, _z));
    }

    /// <summary>
    /// https://w3c.github.io/deviceorientation/#dictdef-devicemotioneventrotationrateinit — the same shape as
    /// <see cref="Acceleration"/> over the three rotation axes.
    /// </summary>
    internal static JsValue RotationRate(BrowserEventRealm realm, ObjectInstance? init, JsString name)
    {
        var value = init?.Get(name);
        if (value is null || value.IsUndefined())
        {
            return JsValue.Null;
        }

        var members = value as ObjectInstance;
        return realm.NewRotationRate(
            NullableNumber(members, _alpha),
            NullableNumber(members, _beta),
            NullableNumber(members, _gamma));
    }

    /// <summary>
    /// https://w3c.github.io/touch-events/#dictdef-touchinit — the <b>required</b> dictionary
    /// <c>new Touch(…)</c> takes, whose <c>identifier</c> and <c>target</c> members are themselves required.
    /// </summary>
    internal static TouchState TouchInit(BrowserEventRealm realm, JsValue[] arguments)
    {
        var principal = realm.PrincipalRealm;

        if (arguments.Length < 1)
        {
            Throw.TypeError(principal, "Failed to construct 'Touch': 1 argument required, but only 0 present.");
        }

        if (arguments[0] is not ObjectInstance init)
        {
            Throw.TypeError(principal, "Failed to construct 'Touch': The provided value is not of type 'TouchInit'.");
            return default;
        }

        RequireTouchMember(principal, init, _identifier, "identifier");
        RequireTouchMember(principal, init, _target, "target");

        var target = TargetOf(realm.Engine, init.Get(_target));
        if (target is null)
        {
            Throw.TypeError(principal, "Failed to construct 'Touch': member target is not of type 'EventTarget'.");
        }

        return new TouchState(
            Long(init, _identifier),
            target,
            Number(init, _screenX),
            Number(init, _screenY),
            Number(init, _clientX),
            Number(init, _clientY),
            Number(init, _pageX),
            Number(init, _pageY),
            Number(init, _radiusX),
            Number(init, _radiusY),
            Number(init, _rotationAngle),
            Number(init, _force));
    }

    /// <summary>
    /// A <c>sequence&lt;Touch&gt;</c> member — https://webidl.spec.whatwg.org/#es-sequence, which is the
    /// iterable protocol rather than an array test, so a page may hand over any iterable. An element that is
    /// not a <c>Touch</c> is a <c>TypeError</c>, and the iterator is closed first as the conversion requires.
    /// </summary>
    internal static JsTouchList TouchSequence(BrowserEventRealm realm, ObjectInstance? init, JsString name)
    {
        var value = init?.Get(name);
        if (value is null || value.IsUndefined())
        {
            return realm.NewTouchList([]);
        }

        var principal = realm.PrincipalRealm;
        var iterator = value.GetIterator(principal);
        var touches = new List<JsTouch>();

        while (iterator.TryIteratorStepValue(out var item))
        {
            if (item is not JsTouch touch)
            {
                iterator.Close(CompletionType.Throw);
                Throw.TypeError(principal, "Failed to construct 'TouchEvent': member " + name + " is not of type 'Touch'.");
                return null!;
            }

            touches.Add(touch);
        }

        return realm.NewTouchList([.. touches]);
    }

    private static void RequireTouchMember(Realm realm, ObjectInstance init, JsString name, string member)
    {
        if (init.Get(name).IsUndefined())
        {
            Throw.TypeError(realm, "Failed to construct 'Touch': required member " + member + " is undefined.");
        }
    }

    private static readonly JsString _x = new("x");
    private static readonly JsString _y = new("y");
    private static readonly JsString _z = new("z");
    private static readonly JsString _alpha = new("alpha");
    private static readonly JsString _beta = new("beta");
    private static readonly JsString _gamma = new("gamma");
    private static readonly JsString _identifier = new("identifier");
    private static readonly JsString _target = new("target");
    private static readonly JsString _pageX = new("pageX");
    private static readonly JsString _pageY = new("pageY");
    private static readonly JsString _radiusX = new("radiusX");
    private static readonly JsString _radiusY = new("radiusY");
    private static readonly JsString _rotationAngle = new("rotationAngle");
    private static readonly JsString _force = new("force");
}
