using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// The touch half of the synthesized input model: <c>Input.dispatchTouchEvent</c>, and the compatibility
/// mouse events a tap leaves behind.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything a contact needs that one event cannot hold lives on the engine.</b> Touch Events'
/// <c>touches</c> and <c>targetTouches</c> are about every contact currently on the surface, and a
/// <c>Touch</c>'s <c>target</c> is the element the contact <i>started</i> on, so a gesture outlives the
/// command that began it — <see cref="BrowserEventRealm.Touches"/> is where it is kept, and it is per engine
/// so that a navigation mid-gesture leaves the next document with nothing outstanding.
/// </para>
/// <para>
/// <b>No pointer event is fired for a touch.</b> Pointer Events would have this dispatch
/// <c>pointerdown</c>/<c>pointerup</c> with <c>pointerType: "touch"</c>, a <c>pointerId</c> per contact and
/// the boundary events around them; that is a second pointer model over the same contacts, and this package
/// has one — the mouse's, which <see cref="DispatchMouse"/> owns. What a tap does produce is Touch Events
/// §8's four compatibility mouse events, which is what a page written against <c>click</c> is listening for.
/// </para>
/// </remarks>
internal static partial class InputDispatcher
{
    /// <summary>
    /// One <c>Input.dispatchTouchEvent</c>: hit-test each contact against the flat box model, then fire the
    /// touch events Touch Events Level 2 asks for at the element each contact started on.
    /// </summary>
    /// <param name="runtime">The page, which owns the document, the layout and the focus.</param>
    /// <param name="input">Which of the four events, the contacts it carries, and the modifiers held.</param>
    /// <remarks>
    /// <para>
    /// <b>One event per changed contact</b>, which is what the protocol defines <c>touchPoints</c> as — "one
    /// event per any changed point (compared to previous touch event in a sequence) is generated, emulating
    /// pressing/moving/releasing points one by one". So a two-finger start is two <c>touchstart</c> events,
    /// the second of which already sees both contacts in its <c>touches</c>.
    /// </para>
    /// <para>
    /// <b>The three lists are §5.2's three questions, answered from the gesture rather than from the
    /// command.</b> <c>changedTouches</c> is the one contact this event is about; <c>touches</c> is every
    /// contact on the surface <i>after</i> this event's own change, so a <c>touchend</c> does not list the
    /// finger it is announcing; and <c>targetTouches</c> is the subset of those that started on this event's
    /// target. A contact's target is fixed when it goes down, so a finger dragged off its button still ends
    /// on the button.
    /// </para>
    /// <para>
    /// <b>Cancelability is the specification's, not the protocol's.</b> <c>touchstart</c>, <c>touchmove</c>
    /// and <c>touchend</c> are cancelable and <c>touchcancel</c> is not (§5.1), which is what makes a
    /// <c>touchcancel</c> listener unable to stop anything and is why nothing here reads its answer.
    /// </para>
    /// <para>
    /// <b>Touch emulation is not consulted.</b> A client that sends this command is asking for a touch, and
    /// refusing it because nobody sent <c>Emulation.setTouchEmulationEnabled</c> would make one command's
    /// behaviour depend on another; what that command decides is what a page <i>detects</i> — whether
    /// <c>ontouchstart</c> is there to be feature-detected and what <c>navigator.maxTouchPoints</c> says —
    /// which is <c>Runtime/TouchEmulation</c>'s subject. A page that added a <c>touchstart</c> listener hears
    /// one either way.
    /// </para>
    /// </remarks>
    internal static void DispatchTouch(PageRuntime runtime, in TouchInput input)
    {
        if (runtime.Document is not { } document)
        {
            return;
        }

        var realm = BrowserEventRealm.Of(runtime.Dom.Engine);
        var sequence = realm.Touches ??= new TouchSequence();

        switch (input.Kind)
        {
            case TouchInputKind.Start:
                Press(runtime, document, realm, sequence, input);
                return;

            case TouchInputKind.Move:
                Move(runtime, realm, sequence, input);
                return;

            case TouchInputKind.End:
                Release(runtime, realm, sequence, "touchend", cancelable: true, input.Modifiers);
                return;

            case TouchInputKind.Cancel:
                // §8's compatibility events are what a *tap* leaves behind, and an abandoned gesture is the
                // one thing that is certainly not one.
                sequence.MouseCompatibilitySuppressed = true;
                Release(runtime, realm, sequence, "touchcancel", cancelable: false, input.Modifiers);
                return;

            default:
                return;
        }
    }

    /// <summary>Every contact the client sent that is not already down, one <c>touchstart</c> each.</summary>
    private static void Press(
        PageRuntime runtime,
        IDocument document,
        BrowserEventRealm realm,
        TouchSequence sequence,
        in TouchInput input)
    {
        var layout = runtime.Layout.Current();

        foreach (var point in input.Points)
        {
            if (sequence.Find(point.Identifier) is not null)
            {
                // A contact the client says is already down: its position belongs to a touchmove, and
                // pressing it twice would put two entries with one identifier into `touches`.
                continue;
            }

            // The same hit test `Input.dispatchMouseEvent` runs, against the same flat box model — so a
            // client that reads a box and taps its centre reaches the element it read, and a point that hits
            // no box lands on the document element, the way a click below a short page does.
            var hit = layout.ElementFromPoint(point.X, point.Y) ?? document.DocumentElement;
            if (hit is null)
            {
                continue;
            }

            var contact = new ActiveTouch(point.Identifier, hit, point);
            sequence.Active.Add(contact);

            if (sequence.Active.Count > 1)
            {
                // https://w3c.github.io/touch-events/#mouse-events — the compatibility events stand for a
                // single-finger tap. A second contact makes the gesture something a mouse cannot express.
                sequence.MouseCompatibilitySuppressed = true;
            }

            if (!Dispatch(runtime, realm, sequence, contact, "touchstart", cancelable: true, input.Modifiers))
            {
                // §8: preventDefault on the touchstart — or on the first touchmove — means no compatibility
                // mouse events, which is how a page that handles touch itself stops the click it would
                // otherwise be given half a gesture later.
                sequence.MouseCompatibilitySuppressed = true;
            }
        }
    }

    /// <summary>Every contact whose state the client changed, one <c>touchmove</c> each.</summary>
    private static void Move(PageRuntime runtime, BrowserEventRealm realm, TouchSequence sequence, in TouchInput input)
    {
        foreach (var point in input.Points)
        {
            if (sequence.Find(point.Identifier) is not { } contact)
            {
                // A contact that was never pressed. Ignored rather than pressed here: `touches` would
                // otherwise hold a point no `touchstart` announced, which is a list a page cannot reconcile.
                continue;
            }

            if (!contact.Moves(point))
            {
                // "One event per any *changed* point": a client that re-sends the whole active list on every
                // move — which is the shape the protocol's parameter has — must not make the fingers that
                // stayed still fire.
                continue;
            }

            var first = !contact.HasMoved;
            contact.MoveTo(point);

            if (!Dispatch(runtime, realm, sequence, contact, "touchmove", cancelable: true, input.Modifiers) && first)
            {
                sequence.MouseCompatibilitySuppressed = true;
            }
        }
    }

    /// <summary>
    /// Every contact off the surface, one <c>touchend</c> or <c>touchcancel</c> each, and then the
    /// compatibility mouse events a tap owes.
    /// </summary>
    /// <remarks>
    /// The protocol's <c>touchEnd</c> and <c>touchCancel</c> carry no points at all — the domain refuses one
    /// that does — so what comes off is whatever is still down, which is why this takes no list.
    /// </remarks>
    private static void Release(
        PageRuntime runtime,
        BrowserEventRealm realm,
        TouchSequence sequence,
        string type,
        bool cancelable,
        EventModifiers modifiers)
    {
        ActiveTouch? released = null;

        while (sequence.Active.Count > 0)
        {
            var last = sequence.Active.Count - 1;
            var contact = sequence.Active[last];

            // Removed before the dispatch, because §5.2 says `touches` is what is touching the surface *now*
            // and the finger whose lifting this event announces is not.
            sequence.Active.RemoveAt(last);
            released ??= contact;

            Dispatch(runtime, realm, sequence, contact, type, cancelable, modifiers);
        }

        var compatibility = !sequence.MouseCompatibilitySuppressed;

        // The gesture is over before its mouse events run, so a listener on one of them that starts a touch
        // of its own begins a sequence rather than joining this one.
        realm.Touches = null;

        if (compatibility && released is not null)
        {
            CompatibilityMouseEvents(runtime, released, modifiers);
        }
    }

    /// <summary>
    /// https://w3c.github.io/touch-events/#mouse-events — the four events a single-finger tap that nothing
    /// cancelled leaves behind, at the point the finger came off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// They are the mouse's own: the same <c>mousemove</c>, <c>mousedown</c>, <c>mouseup</c> and <c>click</c>
    /// <see cref="DispatchMouse"/> fires, at the same coordinates and through the same helpers — so a tap and
    /// a click on one element run one activation behaviour rather than two nearly identical ones, and a tap
    /// on a link follows it, a tap on a submit button submits and a tap on a checkbox toggles.
    /// </para>
    /// <para>
    /// <b>The point is hit-tested again</b> rather than reusing the contact's target: a finger that started
    /// on a button and lifted elsewhere produced its touch events at the button, and the mouse events belong
    /// where the pointer really was — which is where a mouse at those coordinates would have been. A listener
    /// may also have changed the document since the <c>touchstart</c>.
    /// </para>
    /// <para>
    /// <b>No <c>pointerdown</c>, and no boundary events.</b> The first is the class remark's; the second is
    /// <see cref="DispatchMouse"/>'s own limitation, and a tap has no previous position to compute them from.
    /// </para>
    /// <para>
    /// <b>An image button's selected coordinate is measured here too</b>, from the same hit test and the same
    /// layout, and for the same reason the release arm of <see cref="DispatchMouse"/> measures it before its
    /// listeners: the activation behaviour that reads it runs inside the <c>click</c> below, after the three
    /// mouse listeners above, and any one of them may move, adopt or detach the input first. HTML asks
    /// whether "the user activated the button using a pointing device"
    /// (<a href="https://html.spec.whatwg.org/multipage/input.html#image-button-state-(type=image)">§4.10.5.1.20</a>),
    /// and a finger is one — without this a tap on an image button submitted <c>(0, 0)</c> where the identical
    /// click submitted the point it landed on.
    /// </para>
    /// </remarks>
    private static void CompatibilityMouseEvents(PageRuntime runtime, ActiveTouch released, EventModifiers modifiers)
    {
        if (runtime.Document is not { } document)
        {
            return;
        }

        var dom = runtime.Dom;
        var layout = runtime.Layout.Current();
        var hit = layout.ElementFromPoint(released.ClientX, released.ClientY) ?? document.DocumentElement;

        if (hit is null)
        {
            return;
        }

        var target = dom.WrapNode(hit);
        var options = new ClickOptions(
            released.ClientX,
            released.ClientY,
            released.ClientX,
            released.ClientY,
            PrimaryButton,
            Buttons: 0,
            Detail: 1,
            modifiers);

        var events = BrowserEventRealm.Of(dom.Engine);
        events.PendingImagePoint = ImagePointOf(hit, released.ClientX, released.ClientY, layout);

        try
        {
            Mouse(target, "mousemove", options, cancelable: true);

            if (Mouse(target, "mousedown", options with { Buttons = 1 }, cancelable: true)
                && NearestFocusable(hit) is { } focusTarget)
            {
                // https://html.spec.whatwg.org/multipage/interaction.html#focusing-steps — a tap focuses what
                // it lands on unless the page cancelled the mousedown, which is the rule a press follows too.
                FocusController.Focus(dom, focusTarget);
            }

            Mouse(target, "mouseup", options, cancelable: true);
            DispatchClickEvent(target, options, trusted: true);
        }
        finally
        {
            // Nothing is selected by measuring: the activation behaviour promotes it, or nothing does, and
            // the measurement must not outlive the sequence it was taken for.
            events.PendingImagePoint = null;
        }
    }

    /// <summary>Builds one <c>TouchEvent</c> over the gesture and dispatches it at the contact's target.</summary>
    /// <returns><see langword="true"/> when nothing cancelled it.</returns>
    private static bool Dispatch(
        PageRuntime runtime,
        BrowserEventRealm realm,
        TouchSequence sequence,
        ActiveTouch changed,
        string type,
        bool cancelable,
        EventModifiers modifiers)
    {
        var dom = runtime.Dom;
        var engine = dom.Engine;
        var scrollY = runtime.Layout.ScrollY;
        var target = dom.WrapNode(changed.Target);

        // One Touch per contact, shared by whichever of the three lists it appears in: a page comparing
        // `touches[0]` with `changedTouches[0]` is comparing two views of one contact, and they are one
        // object here because they are one contact.
        var changedTouch = changed.ToTouch(dom, realm, scrollY);
        var touches = new JsTouch[sequence.Active.Count];
        var targetTouches = new List<JsTouch>(sequence.Active.Count);

        for (var i = 0; i < sequence.Active.Count; i++)
        {
            var active = sequence.Active[i];
            var touch = ReferenceEquals(active, changed) ? changedTouch : active.ToTouch(dom, realm, scrollY);
            touches[i] = touch;

            // https://w3c.github.io/touch-events/#dom-touchevent-targettouches — every contact touching the
            // surface that *started* on this event's target, which is what lets a two-finger gesture on one
            // element tell its own fingers from the other hand's.
            if (ReferenceEquals(active.Target, changed.Target))
            {
                targetTouches.Add(touch);
            }
        }

        var ev = new JsTouchEvent(
            engine,
            JsString.Create(type),
            new EventInit(Bubbles: true, cancelable, Composed: true),
            realm.TimeStamp,
            engine._mainRealm.GlobalObject,
            detail: 0,
            which: null,
            realm.NewTouchList(touches),
            realm.NewTouchList(targetTouches.ToArray()),
            realm.NewTouchList([changedTouch]),
            modifiers);

        ev._prototype = realm.PrototypeOf(BrowserEventInterfaces.TouchEvent);
        ev.IsTrusted = true;
        return target.DispatchEvent(ev);
    }
}

/// <summary>
/// One contact of a gesture: where it went down, what it is on now, and the element it belongs to for the
/// rest of its life.
/// </summary>
/// <remarks>
/// https://w3c.github.io/touch-events/#dom-touch-target — "the <c>EventTarget</c> on which the touch point
/// started when it was first placed on the surface, even if the touch point has since moved outside the
/// interactive area of that element". That sentence is why this is a mutable contact with a fixed
/// <see cref="Target"/> rather than a value rebuilt from each command.
/// </remarks>
internal sealed class ActiveTouch
{
    internal ActiveTouch(double identifier, IElement target, in TouchPointInput point)
    {
        Identifier = identifier;
        Target = target;
        ClientX = point.X;
        ClientY = point.Y;
        _radiusX = point.RadiusX;
        _radiusY = point.RadiusY;
        _rotationAngle = point.RotationAngle;
        _force = point.Force;
    }

    private double _radiusX;
    private double _radiusY;
    private double _rotationAngle;
    private double _force;

    /// <summary>https://w3c.github.io/touch-events/#dom-touch-identifier.</summary>
    internal double Identifier { get; }

    /// <summary>The element the contact went down on, fixed for its life.</summary>
    internal IElement Target { get; }

    /// <summary>Whether the contact has already moved once, which is what §8's first-move rule turns on.</summary>
    internal bool HasMoved { get; private set; }

    /// <summary>https://w3c.github.io/touch-events/#dom-touch-clientx.</summary>
    internal double ClientX { get; private set; }

    /// <summary>https://w3c.github.io/touch-events/#dom-touch-clienty.</summary>
    internal double ClientY { get; private set; }

    /// <summary>Whether <paramref name="point"/> says anything about this contact that has changed.</summary>
    internal bool Moves(in TouchPointInput point)
        => point.X != ClientX
            || point.Y != ClientY
            || point.RadiusX != _radiusX
            || point.RadiusY != _radiusY
            || point.RotationAngle != _rotationAngle
            || point.Force != _force;

    /// <summary>Takes the client's numbers, keeping the identifier and the target.</summary>
    internal void MoveTo(in TouchPointInput point)
    {
        ClientX = point.X;
        ClientY = point.Y;
        _radiusX = point.RadiusX;
        _radiusY = point.RadiusY;
        _rotationAngle = point.RotationAngle;
        _force = point.Force;
        HasMoved = true;
    }

    /// <summary>The <c>Touch</c> a page reads this contact as.</summary>
    /// <remarks>
    /// <c>screenX</c>/<c>screenY</c> are the client coordinates, for the reason <see cref="MouseInput"/>
    /// gives: the window has no position on a screen that does not exist. <c>pageY</c> adds the page's scroll
    /// offset and <c>pageX</c> adds nothing, because the flat box model is exactly the viewport wide and
    /// <c>window.scrollX</c> is a constant zero.
    /// </remarks>
    internal JsTouch ToTouch(DomRealm dom, BrowserEventRealm realm, double scrollY)
        => realm.NewTouch(new TouchState(
            Identifier,
            dom.WrapNode(Target),
            ScreenX: ClientX,
            ScreenY: ClientY,
            ClientX: ClientX,
            ClientY: ClientY,
            PageX: ClientX,
            PageY: ClientY + scrollY,
            _radiusX,
            _radiusY,
            _rotationAngle,
            _force));
}

/// <summary>
/// The gesture a page is in the middle of: every contact still on the surface, and what the gesture has
/// already decided about §8's compatibility events.
/// </summary>
internal sealed class TouchSequence
{
    /// <summary>Every contact currently on the surface, in the order it went down.</summary>
    internal List<ActiveTouch> Active { get; } = [];

    /// <summary>
    /// Whether the compatibility mouse events are off for this gesture — a second finger, a cancelled
    /// <c>touchstart</c> or first <c>touchmove</c>, or a <c>touchcancel</c>.
    /// </summary>
    internal bool MouseCompatibilitySuppressed { get; set; }

    /// <summary>The contact with <paramref name="identifier"/>, or <see langword="null"/>.</summary>
    internal ActiveTouch? Find(double identifier)
    {
        foreach (var contact in Active)
        {
            if (contact.Identifier == identifier)
            {
                return contact;
            }
        }

        return null;
    }
}

/// <summary>Which of the four events <c>Input.dispatchTouchEvent</c> was asked for.</summary>
internal enum TouchInputKind
{
    /// <summary>Contacts went down on the surface.</summary>
    Start,

    /// <summary>Contacts moved across it.</summary>
    Move,

    /// <summary>Every contact came off it.</summary>
    End,

    /// <summary>The gesture was abandoned rather than completed.</summary>
    Cancel,
}

/// <summary>
/// One contact as a client describes it.
/// <para>
/// https://chromedevtools.github.io/devtools-protocol/tot/Input/#type-TouchPoint
/// </para>
/// </summary>
/// <param name="Identifier">The protocol's <c>id</c>, which tracks one finger across a gesture.</param>
/// <param name="X">The viewport <c>x</c> coordinate, in CSS pixels.</param>
/// <param name="Y">The viewport <c>y</c> coordinate, in CSS pixels.</param>
/// <param name="RadiusX">The contact's horizontal radius; the protocol's default is 1.</param>
/// <param name="RadiusY">The contact's vertical radius; the protocol's default is 1.</param>
/// <param name="RotationAngle">The contact ellipse's rotation in degrees; the protocol's default is 0.</param>
/// <param name="Force">The normalized pressure; the protocol's default is 1.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TouchPointInput(
    double Identifier,
    double X,
    double Y,
    double RadiusX,
    double RadiusY,
    double RotationAngle,
    double Force)
{
    /// <summary>One contact at a point, with the protocol's own defaults for everything else.</summary>
    internal static TouchPointInput At(double x, double y, double identifier = 0)
        => new(identifier, x, y, RadiusX: 1, RadiusY: 1, RotationAngle: 0, Force: 1);
}

/// <summary>One touch event as a client sends it: which of the four, the contacts, and the modifiers.</summary>
/// <param name="Kind">Which of the four events this is.</param>
/// <param name="Points">The contacts the client described, empty for an end and a cancel.</param>
/// <param name="Modifiers">The modifier keys held.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TouchInput(
    TouchInputKind Kind,
    TouchPointInput[] Points,
    EventModifiers Modifiers)
{
    /// <summary>A gesture with no contacts, which is what an end and a cancel carry.</summary>
    internal static TouchInput Of(TouchInputKind kind, EventModifiers modifiers = EventModifiers.None)
        => new(kind, [], modifiers);
}
