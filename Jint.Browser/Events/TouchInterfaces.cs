using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// One <c>Touch</c>'s state, read from its <c>TouchInit</c> dictionary.
/// </summary>
/// <param name="Identifier">https://w3c.github.io/touch-events/#dom-touch-identifier.</param>
/// <param name="Target">https://w3c.github.io/touch-events/#dom-touch-target — required, and an <c>EventTarget</c>.</param>
/// <param name="ScreenX">https://w3c.github.io/touch-events/#dom-touch-screenx.</param>
/// <param name="ScreenY">https://w3c.github.io/touch-events/#dom-touch-screeny.</param>
/// <param name="ClientX">https://w3c.github.io/touch-events/#dom-touch-clientx.</param>
/// <param name="ClientY">https://w3c.github.io/touch-events/#dom-touch-clienty.</param>
/// <param name="PageX">https://w3c.github.io/touch-events/#dom-touch-pagex.</param>
/// <param name="PageY">https://w3c.github.io/touch-events/#dom-touch-pagey.</param>
/// <param name="RadiusX">https://w3c.github.io/touch-events/#dom-touch-radiusx.</param>
/// <param name="RadiusY">https://w3c.github.io/touch-events/#dom-touch-radiusy.</param>
/// <param name="RotationAngle">https://w3c.github.io/touch-events/#dom-touch-rotationangle.</param>
/// <param name="Force">https://w3c.github.io/touch-events/#dom-touch-force.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TouchState(
    double Identifier,
    JsEventTarget? Target,
    double ScreenX,
    double ScreenY,
    double ClientX,
    double ClientY,
    double PageX,
    double PageY,
    double RadiusX,
    double RadiusY,
    double RotationAngle,
    double Force);

/// <summary>
/// A <c>Touch</c> instance — one contact point of a touch input.
/// <para>
/// https://w3c.github.io/touch-events/#touch-interface
/// </para>
/// </summary>
/// <remarks>
/// <b>Every member is what somebody said, and nothing measures anything.</b> A <c>Touch</c> is built from a
/// <c>TouchInit</c> a script wrote, or from a contact a client described to
/// <c>Input.dispatchTouchEvent</c> — and the platform half of the interface (a digitizer's radii, its
/// pressure) is the value that came in either way, since there is no digitizer to improve on it. What is
/// computed rather than supplied is <see cref="Target"/>, which the dispatcher resolves by hit-testing the
/// contact against the flat box model, and <c>pageY</c>, which adds the page's scroll offset.
/// </remarks>
internal sealed class JsTouch : ObjectInstance
{
    private readonly TouchState _state;

    internal JsTouch(Engine engine, ObjectInstance prototype, in TouchState state)
        : base(engine, ObjectClass.Object)
    {
        _state = state;
        Prototype = prototype;
    }

    internal double Identifier => _state.Identifier;

    internal JsValue Target => _state.Target?.EventTargetValue ?? JsValue.Null;

    internal double ScreenX => _state.ScreenX;

    internal double ScreenY => _state.ScreenY;

    internal double ClientX => _state.ClientX;

    internal double ClientY => _state.ClientY;

    internal double PageX => _state.PageX;

    internal double PageY => _state.PageY;

    internal double RadiusX => _state.RadiusX;

    internal double RadiusY => _state.RadiusY;

    internal double RotationAngle => _state.RotationAngle;

    internal double Force => _state.Force;

    public override string ToString() => "[object Touch]";
}

/// <summary>
/// A <c>TouchList</c> instance — the three lists a <c>TouchEvent</c> carries.
/// <para>
/// https://w3c.github.io/touch-events/#touchlist-interface
/// </para>
/// </summary>
/// <remarks>
/// An <see cref="ArrayLikeObject"/> for the reason <c>Collections/DomCollectionBase</c> is one: <c>list[0]</c>,
/// <c>for..of</c>, spread and the <c>Array.prototype</c> generics reach the engine's one-callback-per-element
/// lane with no <c>Reference</c> and no descriptor. <c>length</c> is a prototype accessor, as WebIDL requires,
/// which is what <see cref="OwnsLength"/> being false says.
/// </remarks>
internal sealed class JsTouchList : ArrayLikeObject
{
    private readonly JsTouch[] _touches;

    internal JsTouchList(Engine engine, ObjectInstance prototype, JsTouch[] touches)
        : base(engine)
    {
        _touches = touches;
        Prototype = prototype;
    }

    public override uint Length => (uint) _touches.Length;

    /// <summary>The WebIDL <c>length</c> attribute is supplied by the interface prototype.</summary>
    protected override bool OwnsLength => false;

    public override bool TryGetIndex(uint index, out JsValue value)
    {
        if (index >= (uint) _touches.Length)
        {
            value = JsValue.Undefined;
            return false;
        }

        value = _touches[(int) index];
        return true;
    }

    protected override bool HasIndex(uint index) => index < (uint) _touches.Length;

    /// <summary>
    /// https://w3c.github.io/touch-events/#dom-touchlist-item — an index past the end answers <c>null</c>,
    /// which is what a nullable indexed getter's operation form says and is not the same as the
    /// <c>undefined</c> <c>list[9]</c> gives.
    /// </summary>
    internal JsValue Item(JsValue[] arguments)
    {
        var index = TypeConverter.ToUint32(arguments.At(0));
        return TryGetIndex(index, out var value) ? value : JsValue.Null;
    }

    public override string ToString() => "[object TouchList]";
}

/// <summary>
/// A <c>TouchEvent</c> instance.
/// <para>
/// https://w3c.github.io/touch-events/#touchevent-interface
/// </para>
/// </summary>
/// <remarks>
/// The three lists are exactly the three lists the constructor was handed, each already a
/// <see cref="JsTouchList"/>. Nothing here recomputes <c>targetTouches</c> from <c>touches</c>: that
/// filtering belongs to whatever built the event — the init dictionary's three sequences for a
/// <c>new TouchEvent</c>, and the gesture for a dispatched one, which is
/// <c>Events/InputDispatcher.Touch</c> and is where §5.2's three questions are answered.
/// </remarks>
internal sealed class JsTouchEvent : JsUiEvent
{
    internal JsTouchEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        JsValue view,
        double detail,
        double? which,
        JsTouchList touches,
        JsTouchList targetTouches,
        JsTouchList changedTouches,
        EventModifiers modifiers)
        : base(engine, type, init, timeStamp, view, detail, which)
    {
        Touches = touches;
        TargetTouches = targetTouches;
        ChangedTouches = changedTouches;
        Modifiers = modifiers;
    }

    /// <summary>https://w3c.github.io/touch-events/#dom-touchevent-touches.</summary>
    internal JsTouchList Touches { get; }

    /// <summary>https://w3c.github.io/touch-events/#dom-touchevent-targettouches.</summary>
    internal JsTouchList TargetTouches { get; }

    /// <summary>https://w3c.github.io/touch-events/#dom-touchevent-changedtouches.</summary>
    internal JsTouchList ChangedTouches { get; }

    /// <summary>
    /// The <c>EventModifierInit</c> members, of which this interface exposes four as IDL attributes and none
    /// through a <c>getModifierState()</c> — Touch Events declares no such operation.
    /// </summary>
    internal EventModifiers Modifiers { get; }
}
