using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Input;
using Jint.DevTools.Session;
using Jint.WebApi.Events;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Input</c> domain: a mouse, a keyboard and a touch surface.
/// </summary>
/// <remarks>
/// <para>
/// <b>Five commands, and the rest are honestly absent.</b> The drag commands and the synthesized gestures
/// answer <c>-32601</c>, so a client feature-detecting either is told the truth rather than being given a
/// silent success: a drag needs a data transfer that never leaves the page, and a synthesized scroll or pinch
/// is a gesture recognizer over a rendering there is none of.
/// </para>
/// <para>
/// <b>A touch event is a hit test per contact and then a gesture.</b> The contacts a client sends are
/// hit-tested against the same flat box model a mouse event is, and what a page then reads —
/// <c>touches</c>, <c>targetTouches</c>, <c>changedTouches</c>, and the compatibility mouse events a
/// single-finger tap leaves behind — is <c>InputDispatcher.DispatchTouch</c>'s, where Touch Events Level 2's
/// rules for each are written down. Whether the page <i>detects</i> a touch device at all is a different
/// question and a different command: <c>Emulation.setTouchEmulationEnabled</c>.
/// </para>
/// <para>
/// <b>A key event is a focus lookup and then a sequence.</b> The target is whatever the page has focused —
/// the body when nothing is — and which events fire is decided by the protocol's <c>type</c>:
/// <c>keyDown</c>, <c>rawKeyDown</c>, <c>keyUp</c> and <c>char</c> are the four, and
/// <c>InputDispatcher.DispatchKey</c> is where the three questions they answer are written down. What happens
/// after that is R2's and this item's: the editing commands, <kbd>Tab</kbd> traversal, <kbd>Enter</kbd>'s
/// implicit submission, and the <c>beforeinput</c> / <c>input</c> / <c>change</c> events an edit fires.
/// </para>
/// <para>
/// <b>A mouse event is a hit test and then a sequence.</b> The coordinate is resolved against the flat box
/// model, which is the same model <c>DOM.getBoxModel</c> and <c>Element.getBoundingClientRect</c> answer
/// from — so a client that reads a box and clicks its centre reaches the element it read. What happens after
/// that is R2's: the pointer and mouse events in a browser's order, the focusing steps, and the activation
/// behaviour a click has, so a link navigates, a submit button submits and a checkbox toggles.
/// </para>
/// <para>
/// <b>Everything it fires is trusted</b>, because a protocol client driving a page stands in for a user; a
/// page telling <c>event.isTrusted</c> apart is exactly the distinction that makes that meaningful.
/// </para>
/// <para>
/// Answered on the page loop, like every command of a page target.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Input/"/>.
/// </para>
/// </remarks>
internal sealed class InputDomain : InputDomainBase
{
    private readonly PageTarget _target;

    internal InputDomain(PageTarget target)
    {
        _target = target;
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Input/#method-dispatchMouseEvent.
    /// </summary>
    /// <remarks>
    /// <c>timestamp</c>, <c>force</c>, <c>tangentialPressure</c>, <c>tiltX</c>, <c>tiltY</c>, <c>twist</c>
    /// and <c>pointerType</c> are accepted and not reported: the events carry the pointer defaults a
    /// headless browser has no digitizer to improve on, and a timestamp is the page's own clock rather than
    /// the client's, which is what a page comparing two events' <c>timeStamp</c> needs.
    /// </remarks>
    protected override ValueTask<EmptyResult> DispatchMouseEventAsync(DispatchMouseEventRequest parameters, CommandContext context)
    {
        var pending = _target.PendingNavigationCount;
        if (PageRuntime.Find(_target.Runtime.Engine) is { } runtime)
        {
            InputDispatcher.DispatchMouse(runtime, new MouseInput(
                Kind(parameters.Type),
                parameters.X,
                parameters.Y,
                Button(parameters.Button),
                parameters.Buttons ?? 0,
                parameters.ClickCount ?? 0,
                Modifiers(parameters.Modifiers ?? 0),
                parameters.DeltaX ?? 0,
                parameters.DeltaY ?? 0));
        }

        return CompleteAsync(pending);
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Input/#method-dispatchTouchEvent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The protocol's own two rules about the list are enforced rather than assumed</b>: a
    /// <c>touchStart</c> or <c>touchMove</c> with no contacts, and a <c>touchEnd</c> or <c>touchCancel</c>
    /// with any, are <c>-32602</c>. Both are what the parameter's description says, and a client that breaks
    /// either is describing a gesture that cannot happen — a start with nothing touching, or an end that
    /// still has fingers down — which is worth a refusal a client can read rather than a silent
    /// reinterpretation.
    /// </para>
    /// <para>
    /// <c>tangentialPressure</c>, <c>tiltX</c>, <c>tiltY</c>, <c>twist</c> and <c>timestamp</c> are accepted
    /// and not reported: Touch Events publishes no member for the first four — they are a stylus's, and its
    /// interface is <c>PointerEvent</c> — and a timestamp is the page's own clock rather than the client's,
    /// which is what a page comparing two events' <c>timeStamp</c> needs. The protocol's defaults for
    /// <c>radiusX</c>, <c>radiusY</c>, <c>rotationAngle</c> and <c>force</c> are applied here, so an omitted
    /// one reaches the page as the value Chrome would have sent rather than as zero.
    /// </para>
    /// <para>
    /// An omitted <c>id</c> is the contact's position in the list, which keeps a multi-touch gesture from a
    /// client that sends none from arriving as several contacts sharing one identifier.
    /// </para>
    /// </remarks>
    protected override ValueTask<EmptyResult> DispatchTouchEventAsync(DispatchTouchEventRequest parameters, CommandContext context)
    {
        var kind = TouchKind(parameters.Type);
        var points = TouchPoints(kind, parameters.TouchPoints);
        var pending = _target.PendingNavigationCount;

        if (PageRuntime.Find(_target.Runtime.Engine) is { } runtime)
        {
            InputDispatcher.DispatchTouch(runtime, new TouchInput(kind, points, Modifiers(parameters.Modifiers ?? 0)));
        }

        return CompleteAsync(pending);
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Input/#method-dispatchKeyEvent.
    /// </summary>
    /// <remarks>
    /// <c>timestamp</c>, <c>nativeVirtualKeyCode</c>, <c>keyIdentifier</c>, <c>isKeypad</c> and
    /// <c>isSystemKey</c> are accepted and not reported: the first is the page's own clock rather than the
    /// client's, and none of the rest is observable through any member UI Events publishes.
    /// <c>windowsVirtualKeyCode</c> is not read either: <c>KeyboardEvent.keyCode</c> comes from UI Events'
    /// own legacy table (<c>LegacyKeyCodes</c>) on a <c>keydown</c> and from the character the key produced on
    /// a <c>keypress</c>. That is one answer rather than two, and it means a client that omits the code still
    /// gets the value a page's <c>keyCode === 13</c> tests for.
    /// </remarks>
    protected override ValueTask<EmptyResult> DispatchKeyEventAsync(DispatchKeyEventRequest parameters, CommandContext context)
    {
        var pending = _target.PendingNavigationCount;
        if (PageRuntime.Find(_target.Runtime.Engine) is { } runtime)
        {
            InputDispatcher.DispatchKey(
                runtime,
                new KeyOptions(
                    parameters.Key ?? "",
                    parameters.Code ?? "",
                    parameters.Text ?? "",
                    parameters.Location ?? 0d,
                    parameters.AutoRepeat ?? false,
                    Modifiers(parameters.Modifiers ?? 0),
                    parameters.Commands),
                KeyKind(parameters.Type));
        }

        return CompleteAsync(pending);
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Input/#method-insertText — text with no key
    /// events at all, which is how an IME commit and a character with no key on the layout arrive.
    /// </summary>
    protected override ValueTask<EmptyResult> InsertTextAsync(InsertTextRequest parameters, CommandContext context)
    {
        var pending = _target.PendingNavigationCount;
        if (PageRuntime.Find(_target.Runtime.Engine) is { } runtime)
        {
            InputDispatcher.InsertText(runtime, parameters.Text);
        }

        return CompleteAsync(pending);
    }

    private async ValueTask<EmptyResult> CompleteAsync(int pending)
    {
        if (_target.NavigationPublicationAfter(pending) is { } published)
        {
            await published.ConfigureAwait(false);
        }

        return EmptyResult.Instance;
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Input/#method-imeSetComposition — accepted, and
    /// it changes nothing.
    /// </summary>
    /// <remarks>
    /// It sets the <i>candidate</i> text an IME is still composing, which a browser underlines in the control
    /// and replaces on the next keystroke. There is nothing to underline here and the composition never
    /// reaches the value; what does reach it is the commit, which arrives as <c>insertText</c> and is
    /// implemented. Refusing this would stop a client that sets a composition before committing it, which is
    /// why it is answered rather than <c>-32601</c>.
    /// </remarks>
    protected override ValueTask<EmptyResult> ImeSetCompositionAsync(ImeSetCompositionRequest parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>Which of the four key events the client asked for, refusing a fifth it made up.</summary>
    private static KeyInputKind KeyKind(string type) => type switch
    {
        DispatchKeyEventRequestTypeValues.KeyDown => KeyInputKind.KeyDown,
        DispatchKeyEventRequestTypeValues.RawKeyDown => KeyInputKind.RawKeyDown,
        DispatchKeyEventRequestTypeValues.KeyUp => KeyInputKind.KeyUp,
        DispatchKeyEventRequestTypeValues.Char => KeyInputKind.Char,
        _ => Throw.InvalidParams<KeyInputKind>("Unknown key event type: " + type),
    };

    /// <summary>Which of the four touch events the client asked for, refusing a fifth it made up.</summary>
    private static TouchInputKind TouchKind(string type) => type switch
    {
        DispatchTouchEventRequestTypeValues.TouchStart => TouchInputKind.Start,
        DispatchTouchEventRequestTypeValues.TouchMove => TouchInputKind.Move,
        DispatchTouchEventRequestTypeValues.TouchEnd => TouchInputKind.End,
        DispatchTouchEventRequestTypeValues.TouchCancel => TouchInputKind.Cancel,
        _ => Throw.InvalidParams<TouchInputKind>("Unknown touch event type: " + type),
    };

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Input/#type-TouchPoint — the contacts, with the
    /// protocol's documented defaults and its two rules about how many there may be.
    /// </summary>
    private static TouchPointInput[] TouchPoints(TouchInputKind kind, TouchPoint[]? sent)
    {
        // `required` says the member has to be present, not that its value cannot be null, so a client that
        // writes `"touchPoints": null` reaches here with nothing rather than with an empty list.
        var points = sent ?? [];
        var ending = kind is TouchInputKind.End or TouchInputKind.Cancel;

        if (ending && points.Length > 0)
        {
            Throw.InvalidParams("Touch end and cancel must not have any touch points");
        }

        if (!ending && points.Length == 0)
        {
            Throw.InvalidParams("Touch start and move must have at least one touch point");
        }

        if (points.Length == 0)
        {
            return [];
        }

        var contacts = new TouchPointInput[points.Length];

        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            contacts[i] = new TouchPointInput(
                point.Id ?? i,
                point.X,
                point.Y,
                point.RadiusX ?? 1,
                point.RadiusY ?? 1,
                point.RotationAngle ?? 0,
                point.Force ?? 1);
        }

        return contacts;
    }

    /// <summary>Which of the four events the client asked for, refusing a fifth it made up.</summary>
    private static MouseInputKind Kind(string type) => type switch
    {
        DispatchMouseEventRequestTypeValues.MouseMoved => MouseInputKind.Moved,
        DispatchMouseEventRequestTypeValues.MousePressed => MouseInputKind.Pressed,
        DispatchMouseEventRequestTypeValues.MouseReleased => MouseInputKind.Released,
        DispatchMouseEventRequestTypeValues.MouseWheel => MouseInputKind.Wheel,
        _ => Throw.InvalidParams<MouseInputKind>("Unknown mouse event type: " + type),
    };

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Input/#type-MouseButton — the protocol's names
    /// for the button numbers UI Events reports.
    /// </summary>
    private static double Button(string? button) => button switch
    {
        MouseButtonValues.Left => 0,
        MouseButtonValues.Middle => 1,
        MouseButtonValues.Right => 2,
        MouseButtonValues.Back => 3,
        MouseButtonValues.Forward => 4,

        // "none" and an omitted button both mean no button, which UI Events reports as the primary one on a
        // move: MouseEvent.button has no value for "nothing pressed" and every browser sends 0.
        _ => 0,
    };

    /// <summary>
    /// The protocol's modifier bit field: <c>Alt=1</c>, <c>Ctrl=2</c>, <c>Meta=4</c>, <c>Shift=8</c>.
    /// </summary>
    private static EventModifiers Modifiers(int modifiers)
    {
        var result = EventModifiers.None;

        if ((modifiers & 1) != 0)
        {
            result |= EventModifiers.Alt;
        }

        if ((modifiers & 2) != 0)
        {
            result |= EventModifiers.Control;
        }

        if ((modifiers & 4) != 0)
        {
            result |= EventModifiers.Meta;
        }

        if ((modifiers & 8) != 0)
        {
            result |= EventModifiers.Shift;
        }

        return result;
    }
}
