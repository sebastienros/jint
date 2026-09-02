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
/// The <c>Input</c> domain: a mouse, and nothing else yet.
/// </summary>
/// <remarks>
/// <para>
/// <b>One command, and the rest are honestly absent.</b> <c>dispatchKeyEvent</c>, <c>insertText</c>,
/// <c>imeSetComposition</c>, the touch and drag commands and the synthesized gestures all answer
/// <c>-32601</c>, so a client feature-detecting any of them is told the truth rather than being given a
/// silent success. The keyboard half — typing, editing, <kbd>Tab</kbd> traversal and <c>testdriver.js</c> —
/// is the next campaign item, and R2's <c>InputDispatcher</c> already has the model it will hang off.
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

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
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
