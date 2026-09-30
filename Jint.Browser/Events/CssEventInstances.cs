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
