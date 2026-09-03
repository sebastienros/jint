using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// The modifier keys an event carries — <c>ctrlKey</c> and its kind, plus the ones only
/// <c>getModifierState()</c> can reach.
/// </summary>
/// <remarks>
/// One field rather than fourteen booleans, because <c>getModifierState</c>
/// (https://w3c.github.io/uievents/#dom-keyboardevent-getmodifierstate) is a lookup over the whole set and
/// four of the fourteen are also IDL attributes. The names are UI Events' modifier key values, which are the
/// same strings <c>KeyboardEvent.key</c> uses for those keys.
/// </remarks>
[Flags]
internal enum EventModifiers
{
    None = 0,
    Alt = 1 << 0,
    AltGraph = 1 << 1,
    CapsLock = 1 << 2,
    Control = 1 << 3,
    Fn = 1 << 4,
    FnLock = 1 << 5,
    Meta = 1 << 6,
    NumLock = 1 << 7,
    ScrollLock = 1 << 8,
    Shift = 1 << 9,
    Symbol = 1 << 10,
    SymbolLock = 1 << 11,
    Hyper = 1 << 12,
    Super = 1 << 13,
}

/// <summary>
/// A <c>UIEvent</c> instance — the base of every event a user interaction produces.
/// <para>
/// https://w3c.github.io/uievents/#interface-uievent
/// </para>
/// </summary>
/// <remarks>
/// <c>view</c> is declared <c>Window?</c>. It is carried as a <see cref="JsValue"/> rather than as a target,
/// because this engine's window is the global object and nothing else needs to look inside it.
/// </remarks>
internal class JsUiEvent : JsEvent
{
    internal JsUiEvent(Engine engine, JsString type, EventInit init, double timeStamp, JsValue view, double detail, double? which = null)
        : base(engine, type, init, timeStamp)
    {
        View = view;
        Detail = detail;
        WhichInit = which;
    }

    /// <summary>https://w3c.github.io/uievents/#dom-uievent-view.</summary>
    internal JsValue View { get; private set; }

    /// <summary>https://w3c.github.io/uievents/#dom-uievent-detail — a click count, a wheel tick count.</summary>
    internal double Detail { get; private set; }

    /// <summary>
    /// https://w3c.github.io/uievents/#dom-uievent-inituievent — the legacy initializer: <i>initialize an
    /// event</i>, then this interface's own two members.
    /// </summary>
    /// <remarks>
    /// Every one of these is a no-op while the event is being dispatched, which is
    /// <c>JsEvent.InitializeEvent</c>'s own rule — the caller checks the dispatch flag once, before the
    /// arguments are converted, and every derived initializer inherits it by calling up.
    /// </remarks>
    internal virtual void Initialize(JsString type, bool bubbles, bool cancelable, JsValue view, double detail)
    {
        InitializeEvent(type, bubbles, cancelable);
        View = view;
        Detail = detail;
    }

    /// <summary>
    /// The <c>which</c> the init dictionary supplied, or <see langword="null"/> when it said nothing — which is
    /// what lets a derived interface compute its own legacy value instead.
    /// </summary>
    internal double? WhichInit { get; }

    /// <summary>
    /// https://w3c.github.io/uievents/#dom-uievent-which — a legacy attribute whose value the standard leaves
    /// to the interface. It is zero unless the dictionary said otherwise; <c>MouseEvent</c> and
    /// <c>KeyboardEvent</c> override it with the values every implementation agrees on.
    /// </summary>
    internal virtual double Which => WhichInit ?? 0;
}

/// <summary>
/// A <c>MouseEvent</c> instance.
/// <para>
/// https://w3c.github.io/uievents/#interface-mouseevent
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>The coordinates are inputs, not measurements.</b> There is no layout here, so <c>pageX</c> is
/// <c>clientX</c> plus a scroll offset that is always zero, and <c>offsetX</c> is <c>clientX</c> minus the
/// target's border-box origin, which the flat renderer (campaign item C4) will supply and which is zero until
/// it does. A page reading them gets exactly what the dispatcher was given; a page computing with them gets an
/// answer consistent with a document whose every box sits at the origin.
/// </para>
/// <para>
/// It is the interface that carries <see cref="JsEvent.IsActivationEvent"/>: dispatch's step 6.4 is "true if
/// event is a <c>MouseEvent</c> object and event's <c>type</c> attribute is <c>click</c>", so this is the one
/// place in the whole package that decides whether an activation behaviour may run at all.
/// </para>
/// </remarks>
internal class JsMouseEvent : JsUiEvent
{
    internal JsMouseEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        JsValue view,
        double detail,
        double? which,
        in MouseEventState state)
        : base(engine, type, init, timeStamp, view, detail, which)
    {
        ScreenX = state.ScreenX;
        ScreenY = state.ScreenY;
        ClientX = state.ClientX;
        ClientY = state.ClientY;
        Button = state.Button;
        Buttons = state.Buttons;
        Modifiers = state.Modifiers;
        RelatedTarget = state.RelatedTarget;
    }

    /// <summary>
    /// https://w3c.github.io/uievents/#dom-mouseevent-initmouseevent — the legacy initializer.
    /// </summary>
    /// <remarks>
    /// <c>buttons</c> is not among its arguments and is deliberately left alone: the legacy signature
    /// predates it, and inventing a value from <c>button</c> would be a guess a page cannot correct.
    /// </remarks>
    internal void Initialize(
        JsString type,
        bool bubbles,
        bool cancelable,
        JsValue view,
        double detail,
        in MouseEventState state)
    {
        Initialize(type, bubbles, cancelable, view, detail);
        ScreenX = state.ScreenX;
        ScreenY = state.ScreenY;
        ClientX = state.ClientX;
        ClientY = state.ClientY;
        Button = state.Button;
        Modifiers = state.Modifiers;
        RelatedTarget = state.RelatedTarget;
    }

    /// <summary>https://w3c.github.io/uievents/#dom-mouseevent-screenx.</summary>
    internal double ScreenX { get; private set; }

    /// <summary>https://w3c.github.io/uievents/#dom-mouseevent-screeny.</summary>
    internal double ScreenY { get; private set; }

    /// <summary>https://w3c.github.io/uievents/#dom-mouseevent-clientx.</summary>
    internal double ClientX { get; private set; }

    /// <summary>https://w3c.github.io/uievents/#dom-mouseevent-clienty.</summary>
    internal double ClientY { get; private set; }

    /// <summary>https://w3c.github.io/uievents/#dom-mouseevent-button — 0 primary, 1 auxiliary, 2 secondary.</summary>
    internal double Button { get; private set; }

    /// <summary>https://w3c.github.io/uievents/#dom-mouseevent-buttons — the bit set of buttons held down.</summary>
    internal double Buttons { get; }

    /// <summary>The modifier keys, read by the four IDL attributes and by <c>getModifierState()</c>.</summary>
    internal EventModifiers Modifiers { get; private set; }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-dispatch step 6.4 — <c>true</c> exactly for a
    /// <c>MouseEvent</c> whose type is <c>click</c>, which is what lets an activation behaviour run.
    /// </summary>
    internal sealed override bool IsActivationEvent
        => string.Equals(TypeName, "click", StringComparison.Ordinal);

    /// <summary>
    /// https://w3c.github.io/uievents/#dom-uievent-which for a mouse event: the button number plus one, so a
    /// primary click answers 1. It is the value every implementation reports and the one a page's
    /// <c>e.which === 1</c> test was written against.
    /// </summary>
    internal override double Which => WhichInit ?? Button + 1;
}

/// <summary>
/// The <c>MouseEventInit</c> members a <see cref="JsMouseEvent"/> keeps, read together so a constructor's
/// state stays one value.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct MouseEventState(
    double ScreenX,
    double ScreenY,
    double ClientX,
    double ClientY,
    double Button,
    double Buttons,
    EventModifiers Modifiers,
    JsEventTarget? RelatedTarget);

/// <summary>
/// A <c>PointerEvent</c> instance.
/// <para>
/// https://w3c.github.io/pointerevents/#pointerevent-interface
/// </para>
/// </summary>
/// <remarks>
/// The geometry members — <c>width</c>, <c>height</c>, <c>tiltX</c>, <c>tiltY</c>, <c>twist</c> — are the
/// specification's defaults unless a dispatcher supplied otherwise, because a headless browser has no digitizer
/// to report a contact area from. <c>getCoalescedEvents()</c> and <c>getPredictedEvents()</c> answer empty
/// lists, which is what they answer for a synthesized event in a browser too.
/// </remarks>
internal sealed class JsPointerEvent : JsMouseEvent
{
    internal JsPointerEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        JsValue view,
        double detail,
        double? which,
        in MouseEventState mouse,
        in PointerEventState pointer)
        : base(engine, type, init, timeStamp, view, detail, which, mouse)
    {
        PointerId = pointer.PointerId;
        Width = pointer.Width;
        Height = pointer.Height;
        Pressure = pointer.Pressure;
        TangentialPressure = pointer.TangentialPressure;
        TiltX = pointer.TiltX;
        TiltY = pointer.TiltY;
        Twist = pointer.Twist;
        PointerType = pointer.PointerType;
        IsPrimary = pointer.IsPrimary;
    }

    /// <summary>https://w3c.github.io/pointerevents/#dom-pointerevent-pointerid.</summary>
    internal double PointerId { get; }

    /// <summary>https://w3c.github.io/pointerevents/#dom-pointerevent-width.</summary>
    internal double Width { get; }

    /// <summary>https://w3c.github.io/pointerevents/#dom-pointerevent-height.</summary>
    internal double Height { get; }

    /// <summary>https://w3c.github.io/pointerevents/#dom-pointerevent-pressure.</summary>
    internal double Pressure { get; }

    /// <summary>https://w3c.github.io/pointerevents/#dom-pointerevent-tangentialpressure.</summary>
    internal double TangentialPressure { get; }

    /// <summary>https://w3c.github.io/pointerevents/#dom-pointerevent-tiltx.</summary>
    internal double TiltX { get; }

    /// <summary>https://w3c.github.io/pointerevents/#dom-pointerevent-tilty.</summary>
    internal double TiltY { get; }

    /// <summary>https://w3c.github.io/pointerevents/#dom-pointerevent-twist.</summary>
    internal double Twist { get; }

    /// <summary>https://w3c.github.io/pointerevents/#dom-pointerevent-pointertype.</summary>
    internal string PointerType { get; }

    /// <summary>https://w3c.github.io/pointerevents/#dom-pointerevent-isprimary.</summary>
    internal bool IsPrimary { get; }
}

/// <summary>The <c>PointerEventInit</c> members a <see cref="JsPointerEvent"/> keeps.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct PointerEventState(
    double PointerId,
    double Width,
    double Height,
    double Pressure,
    double TangentialPressure,
    double TiltX,
    double TiltY,
    double Twist,
    string PointerType,
    bool IsPrimary);

/// <summary>
/// A <c>WheelEvent</c> instance.
/// <para>
/// https://w3c.github.io/uievents/#interface-wheelevent
/// </para>
/// </summary>
internal sealed class JsWheelEvent : JsMouseEvent
{
    internal JsWheelEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        JsValue view,
        double detail,
        double? which,
        in MouseEventState mouse,
        double deltaX,
        double deltaY,
        double deltaZ,
        double deltaMode)
        : base(engine, type, init, timeStamp, view, detail, which, mouse)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
        DeltaZ = deltaZ;
        DeltaMode = deltaMode;
    }

    /// <summary>https://w3c.github.io/uievents/#dom-wheelevent-deltax.</summary>
    internal double DeltaX { get; }

    /// <summary>https://w3c.github.io/uievents/#dom-wheelevent-deltay.</summary>
    internal double DeltaY { get; }

    /// <summary>https://w3c.github.io/uievents/#dom-wheelevent-deltaz.</summary>
    internal double DeltaZ { get; }

    /// <summary>https://w3c.github.io/uievents/#dom-wheelevent-deltamode — 0 pixel, 1 line, 2 page.</summary>
    internal double DeltaMode { get; }
}

/// <summary>
/// A <c>KeyboardEvent</c> instance.
/// <para>
/// https://w3c.github.io/uievents/#interface-keyboardevent
/// </para>
/// </summary>
/// <remarks>
/// The three legacy members are computed rather than stored, from UI Events' own legacy tables
/// (https://w3c.github.io/uievents/#legacy-key-attributes): <c>charCode</c> is non-zero only on
/// <c>keypress</c>, <c>keyCode</c> is the virtual key code for <c>keydown</c>/<c>keyup</c> and the character
/// code for <c>keypress</c>, and <c>which</c> is whichever of the two is non-zero. A dispatcher may pin them
/// explicitly through the init dictionary, which is what <c>KeyboardEventInit</c>'s legacy members are for.
/// </remarks>
internal sealed class JsKeyboardEvent : JsUiEvent
{
    internal JsKeyboardEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        JsValue view,
        double detail,
        double? which,
        in KeyboardEventState state)
        : base(engine, type, init, timeStamp, view, detail, which)
    {
        Key = state.Key;
        Code = state.Code;
        Location = state.Location;
        Repeat = state.Repeat;
        IsComposing = state.IsComposing;
        Modifiers = state.Modifiers;
        CharCode = state.CharCode ?? LegacyKeyCodes.CharCodeFor(TypeName, state.Key);
        KeyCode = state.KeyCode ?? LegacyKeyCodes.KeyCodeFor(TypeName, state.Key);
    }

    /// <summary>
    /// https://w3c.github.io/uievents/#dom-keyboardevent-initkeyboardevent — the legacy initializer, whose
    /// signature is UI Events' own: no <c>code</c>, and the modifiers as a space-separated list of key values
    /// rather than as booleans.
    /// </summary>
    /// <remarks>
    /// The legacy signature has no <c>code</c>, no <c>isComposing</c> and no <c>charCode</c>/<c>keyCode</c>,
    /// so those are left as they were and the two legacy codes are recomputed from the new key and type,
    /// which is what the tables they come from are for.
    /// </remarks>
    internal void Initialize(
        JsString type,
        bool bubbles,
        bool cancelable,
        JsValue view,
        string key,
        double location,
        EventModifiers modifiers,
        bool repeat)
    {
        Initialize(type, bubbles, cancelable, view, detail: 0);
        Key = key;
        Location = location;
        Modifiers = modifiers;
        Repeat = repeat;
        CharCode = LegacyKeyCodes.CharCodeFor(TypeName, key);
        KeyCode = LegacyKeyCodes.KeyCodeFor(TypeName, key);
    }

    /// <summary>https://w3c.github.io/uievents/#dom-keyboardevent-key.</summary>
    internal string Key { get; private set; }

    /// <summary>https://w3c.github.io/uievents/#dom-keyboardevent-code.</summary>
    internal string Code { get; }

    /// <summary>https://w3c.github.io/uievents/#dom-keyboardevent-location.</summary>
    internal double Location { get; private set; }

    /// <summary>https://w3c.github.io/uievents/#dom-keyboardevent-repeat.</summary>
    internal bool Repeat { get; private set; }

    /// <summary>https://w3c.github.io/uievents/#dom-keyboardevent-iscomposing.</summary>
    internal bool IsComposing { get; }

    /// <summary>The modifier keys.</summary>
    internal EventModifiers Modifiers { get; private set; }

    /// <summary>https://w3c.github.io/uievents/#dom-keyboardevent-charcode.</summary>
    internal double CharCode { get; private set; }

    /// <summary>https://w3c.github.io/uievents/#dom-keyboardevent-keycode.</summary>
    internal double KeyCode { get; private set; }

    /// <summary>
    /// https://w3c.github.io/uievents/#dom-uievent-which for a keyboard event: the character code when there is
    /// one, and the virtual key code otherwise.
    /// </summary>
    internal override double Which => CharCode != 0 ? CharCode : KeyCode;
}

/// <summary>The <c>KeyboardEventInit</c> members a <see cref="JsKeyboardEvent"/> keeps.</summary>
/// <remarks>
/// <see cref="CharCode"/> and <see cref="KeyCode"/> are nullable so that "the dictionary said nothing" is
/// distinguishable from "the dictionary said zero" — the first takes the legacy table, the second is honoured.
/// </remarks>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct KeyboardEventState(
    string Key,
    string Code,
    double Location,
    bool Repeat,
    bool IsComposing,
    EventModifiers Modifiers,
    double? CharCode,
    double? KeyCode);

/// <summary>
/// An <c>InputEvent</c> instance.
/// <para>
/// https://w3c.github.io/uievents/#interface-inputevent
/// </para>
/// </summary>
/// <remarks>
/// <c>dataTransfer</c> is always <see cref="JsValue.Null"/>: it is non-null only for a paste or a drop, and
/// this version has neither a clipboard nor drag and drop. <c>getTargetRanges()</c> answers an empty array,
/// which is what it answers outside <c>contenteditable</c> in a browser.
/// </remarks>
internal sealed class JsInputEvent : JsUiEvent
{
    internal JsInputEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        JsValue view,
        double detail,
        double? which,
        JsValue data,
        bool isComposing,
        string inputType)
        : base(engine, type, init, timeStamp, view, detail, which)
    {
        Data = data;
        IsComposing = isComposing;
        InputType = inputType;
    }

    /// <summary>https://w3c.github.io/uievents/#dom-inputevent-data — nullable, so a <see cref="JsValue"/>.</summary>
    internal JsValue Data { get; }

    /// <summary>https://w3c.github.io/uievents/#dom-inputevent-iscomposing.</summary>
    internal bool IsComposing { get; }

    /// <summary>https://w3c.github.io/input-events/#dom-inputevent-inputtype.</summary>
    internal string InputType { get; }
}

/// <summary>
/// A <c>CompositionEvent</c> instance.
/// <para>
/// https://w3c.github.io/uievents/#interface-compositionevent
/// </para>
/// </summary>
internal sealed class JsCompositionEvent : JsUiEvent
{
    internal JsCompositionEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        JsValue view,
        double detail,
        double? which,
        string data)
        : base(engine, type, init, timeStamp, view, detail, which)
    {
        Data = data;
    }

    /// <summary>https://w3c.github.io/uievents/#dom-compositionevent-data.</summary>
    internal string Data { get; }
}

/// <summary>
/// A <c>FocusEvent</c> instance.
/// <para>
/// https://w3c.github.io/uievents/#interface-focusevent
/// </para>
/// </summary>
/// <remarks>
/// Its whole own state is <c>relatedTarget</c>, which the engine's <see cref="JsEvent.RelatedTarget"/> slot
/// already carries because dispatch retargets it per path item. The class exists for the brand and the
/// prototype, not for a field.
/// </remarks>
internal sealed class JsFocusEvent : JsUiEvent
{
    internal JsFocusEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        JsValue view,
        double detail,
        double? which,
        JsEventTarget? relatedTarget)
        : base(engine, type, init, timeStamp, view, detail, which)
    {
        RelatedTarget = relatedTarget;
    }
}
