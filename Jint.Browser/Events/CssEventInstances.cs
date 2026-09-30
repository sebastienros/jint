using Jint.Runtime;
using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// An <c>AnimationEvent</c> instance.
/// <para>
/// https://drafts.csswg.org/css-animations-1/#interface-animationevent
/// </para>
/// </summary>
/// <remarks>
/// Nothing animates here, so no <c>animationstart</c> or <c>animationend</c> is ever fired; a page can still
/// construct and dispatch one, and a library that feature-detects the interface finds it.
/// </remarks>
internal sealed class JsAnimationEvent : JsEvent
{
    internal JsAnimationEvent(Engine engine, JsString type, EventInit init, double timeStamp, string animationName, double elapsedTime, string pseudoElement)
        : base(engine, type, init, timeStamp)
    {
        AnimationName = animationName;
        ElapsedTime = elapsedTime;
        PseudoElement = pseudoElement;
    }

    /// <summary>https://drafts.csswg.org/css-animations-1/#dom-animationevent-animationname.</summary>
    internal string AnimationName { get; }

    /// <summary>https://drafts.csswg.org/css-animations-1/#dom-animationevent-elapsedtime.</summary>
    internal double ElapsedTime { get; }

    /// <summary>https://drafts.csswg.org/css-animations-1/#dom-animationevent-pseudoelement.</summary>
    internal string PseudoElement { get; }
}

/// <summary>
/// A <c>TransitionEvent</c> instance.
/// <para>
/// https://drafts.csswg.org/css-transitions-1/#interface-transitionevent
/// </para>
/// </summary>
/// <remarks>As for <see cref="JsAnimationEvent"/>: constructible and dispatchable, never fired.</remarks>
internal sealed class JsTransitionEvent : JsEvent
{
    internal JsTransitionEvent(Engine engine, JsString type, EventInit init, double timeStamp, string propertyName, double elapsedTime, string pseudoElement)
        : base(engine, type, init, timeStamp)
    {
        PropertyName = propertyName;
        ElapsedTime = elapsedTime;
        PseudoElement = pseudoElement;
    }

    /// <summary>https://drafts.csswg.org/css-transitions-1/#dom-transitionevent-propertyname.</summary>
    internal string PropertyName { get; }

    /// <summary>https://drafts.csswg.org/css-transitions-1/#dom-transitionevent-elapsedtime.</summary>
    internal double ElapsedTime { get; }

    /// <summary>https://drafts.csswg.org/css-transitions-1/#dom-transitionevent-pseudoelement.</summary>
    internal string PseudoElement { get; }
}

/// <summary>
/// A <c>GamepadEvent</c> instance.
/// <para>
/// https://w3c.github.io/gamepad/#gamepadevent-interface
/// </para>
/// </summary>
/// <remarks>
/// Its dictionary's <c>gamepad</c> is a required <c>Gamepad</c>, and no gamepad is ever connected, so no page
/// can build one: the interface is here for the feature detection that decides whether to listen for
/// <c>gamepadconnected</c> at all.
/// </remarks>
internal sealed class JsGamepadEvent : JsEvent
{
    internal JsGamepadEvent(Engine engine, JsString type, EventInit init, double timeStamp, JsValue gamepad)
        : base(engine, type, init, timeStamp)
    {
        Gamepad = gamepad;
    }

    /// <summary>https://w3c.github.io/gamepad/#dom-gamepadevent-gamepad.</summary>
    internal JsValue Gamepad { get; }
}

/// <summary>
/// A <c>FontFaceSetLoadEvent</c> instance.
/// <para>
/// https://drafts.csswg.org/css-font-loading/#fontfacesetloadevent
/// </para>
/// </summary>
internal sealed class JsFontFaceSetLoadEvent : JsEvent
{
    internal JsFontFaceSetLoadEvent(Engine engine, JsString type, EventInit init, double timeStamp, JsArray fontFaces)
        : base(engine, type, init, timeStamp)
    {
        FontFaces = fontFaces;
    }

    /// <summary>https://drafts.csswg.org/css-font-loading/#dom-fontfacesetloadevent-fontfaces — <c>[SameObject]</c>.</summary>
    internal JsArray FontFaces { get; }

    /// <summary>https://webidl.spec.whatwg.org/#dfn-create-frozen-array</summary>
    internal static JsArray Frozen(Realm realm, JsValue[] values)
    {
        var array = realm.Intrinsics.Array.ConstructFast(values);
        array.SetIntegrityLevel(IntegrityLevel.Frozen);
        return array;
    }

    /// <summary>
    /// "Fire a font load event": a trusted, non-bubbling, non-cancelable event in <paramref name="dom"/>'s
    /// realm carrying <paramref name="faces"/>.
    /// </summary>
    internal static JsFontFaceSetLoadEvent CreateTrusted(Dom.DomRealm dom, string type, JsValue[] faces)
    {
        var events = BrowserEventRealm.Of(dom.Engine, dom.OwningRealm);
        var created = new JsFontFaceSetLoadEvent(
            dom.Engine,
            JsString.Create(type),
            new EventInit(Bubbles: false, Cancelable: false, Composed: false),
            EventConstructor.TimeStampNow(dom.Engine),
            Frozen(dom.OwningRealm, faces))
        {
            IsTrusted = true,
        };

        created._prototype = events.PrototypeOf(BrowserEventInterfaces.FontFaceSetLoadEvent);
        return created;
    }
}
