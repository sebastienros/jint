using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// The synthesized input model: a click that focuses and activates, and a key press that edits and traverses.
/// <para>
/// Design doc §8, "Input without layout".
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// This is the surface CDP's <c>Input</c> domain maps onto (campaign item C4). What C4 adds above it is the
/// coordinate half — the flat renderer that turns an <c>(x, y)</c> into the element a click landed on — and
/// the protocol's own key repetition and modifier bookkeeping. Everything below the element is here, so the
/// two halves can be tested apart.
/// </para>
/// <para>
/// <b>Trust.</b> Everything this dispatcher fires is trusted, because a protocol client driving a page is
/// standing in for a user. <c>HTMLElement.click()</c> goes through <see cref="FireSyntheticClick"/> with
/// <c>trusted: false</c> instead — HTML's <c>click()</c> says to "fire a synthetic pointer event named click …
/// <i>with the not trusted flag set</i>", so <c>el.click()</c> is observably a script's click and a
/// <c>Input.dispatchMouseEvent</c> is observably a user's.
/// </para>
/// </remarks>
internal static class InputDispatcher
{
    /// <summary>The button number of the primary (left) mouse button, which is the one that clicks.</summary>
    private const double PrimaryButton = 0;

    /// <summary>The button number of the secondary (right) mouse button, which opens a context menu.</summary>
    private const double SecondaryButton = 2;

    /// <summary>
    /// A click as a user makes it: focus what was clicked, then dispatch a trusted <c>click</c> whose
    /// activation behaviour runs unless a listener cancels it.
    /// </summary>
    /// <remarks>
    /// The full pointer/mouse sequence a browser fires — <c>pointerdown</c>, <c>mousedown</c>,
    /// <c>pointerup</c>, <c>mouseup</c>, then <c>click</c> — belongs with the coordinate model in C4, because
    /// only that half knows where the pointer was between the events. What is here is the part that changes the
    /// document: focus, then the click that activates.
    /// </remarks>
    internal static void DispatchClick(DomNodeObject target, in ClickOptions options)
    {
        var dom = target.DomRealm;

        // https://html.spec.whatwg.org/multipage/interaction.html#focusing-steps — clicking a focusable
        // element focuses it, and clicking anything else moves focus to the nearest focusable ancestor, which
        // is what makes a click on a <span> inside a <button> focus the button.
        if (target.Node is IElement clicked && NearestFocusable(clicked) is { } focusTarget)
        {
            FocusController.Focus(dom, focusTarget);
        }

        DispatchClickEvent(target, options, trusted: true);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#fire-a-synthetic-pointer-event — what
    /// <c>element.click()</c> and a <c>&lt;label&gt;</c>'s forward both do. It focuses nothing, which is the
    /// difference from <see cref="DispatchClick"/> and is what HTML says.
    /// </summary>
    internal static void FireSyntheticClick(DomNodeObject target, bool trusted)
        => DispatchClickEvent(target, ClickOptions.Synthetic, trusted);

    /// <summary>
    /// A mouse event at a coordinate, which is what <c>Input.dispatchMouseEvent</c> becomes: hit-test the
    /// point against the flat box model, then fire the sequence a browser fires at what was hit.
    /// </summary>
    /// <param name="runtime">The page, which owns the document, the layout and the focus.</param>
    /// <param name="input">Where, which button, and how many clicks.</param>
    /// <remarks>
    /// <para>
    /// <b>The four sequences.</b> A move is <c>pointermove</c> then <c>mousemove</c>. A press is
    /// <c>pointerdown</c>, <c>mousedown</c> and — unless a listener cancelled the <c>mousedown</c>, which is
    /// how a page stops a click stealing focus — the focusing steps. A release is <c>pointerup</c>,
    /// <c>mouseup</c>, then either <c>contextmenu</c> for the secondary button or <c>click</c> for the
    /// primary one, with <c>dblclick</c> after a <c>click</c> whose count is two. A wheel is a
    /// <c>wheel</c> event whose default action, if nothing cancels it, is the virtual scroll.
    /// </para>
    /// <para>
    /// <b>The click's target is the press and the release together.</b> UI Events dispatches a click at the
    /// nearest common inclusive ancestor of the two, which is what makes a press that slides off its button
    /// before releasing activate nothing. A release with no press before it — a client that sends only
    /// <c>mouseReleased</c> — is dispatched at the release target alone.
    /// </para>
    /// <para>
    /// <b>Everything here is trusted</b>, because a protocol client driving a page stands in for a user; and
    /// a point that hits no box is dispatched at the document element, which is where a browser sends a click
    /// on the empty space below a short page.
    /// </para>
    /// <para>
    /// <b>A move fires no boundary events</b> — no <c>pointerover</c>, <c>mouseover</c>, <c>mouseout</c>,
    /// <c>mouseenter</c> or <c>mouseleave</c>. Those need the pointer's <i>previous</i> position and the
    /// ancestor chains of both elements, which is a second piece of state and a second algorithm; a client's
    /// <c>hover</c> sends one move, so what it drives today is a <c>mousemove</c> listener and not a
    /// hover menu written against <c>mouseenter</c>. Stated rather than approximated: firing <c>mouseover</c>
    /// on every move without the matching <c>mouseout</c> would be worse than firing neither.
    /// </para>
    /// </remarks>
    internal static void DispatchMouse(PageRuntime runtime, in MouseInput input)
    {
        if (runtime.Document is not { } document)
        {
            return;
        }

        var dom = runtime.Dom;
        var events = BrowserEventRealm.Of(dom.Engine);
        var layout = runtime.Layout.Current();

        var hit = layout.ElementFromPoint(input.X, input.Y) ?? document.DocumentElement;
        if (hit is null)
        {
            return;
        }

        var target = dom.WrapNode(hit);
        var options = input.ToClickOptions();

        switch (input.Kind)
        {
            case MouseInputKind.Moved:
                Pointer(target, "pointermove", options, cancelable: true);
                Mouse(target, "mousemove", options, cancelable: true);
                return;

            case MouseInputKind.Pressed:
                Pointer(target, "pointerdown", options, cancelable: true);

                if (Mouse(target, "mousedown", options, cancelable: true)
                    && NearestFocusable(hit) is { } focusTarget)
                {
                    FocusController.Focus(dom, focusTarget);
                }

                events.MousePressTarget = hit;
                return;

            case MouseInputKind.Released:
                Pointer(target, "pointerup", options, cancelable: true);
                Mouse(target, "mouseup", options, cancelable: true);

                var clicked = dom.WrapNode(CommonAncestor(events.MousePressTarget, hit) ?? hit);
                events.MousePressTarget = null;

                // https://w3c.github.io/uievents/#event-type-contextmenu — the secondary button opens a menu
                // rather than activating anything, so no click is dispatched for it at all.
                if (input.Button == SecondaryButton)
                {
                    Mouse(clicked, "contextmenu", options, cancelable: true);
                    return;
                }

                if (input.Button != PrimaryButton)
                {
                    return;
                }

                DispatchClickEvent(clicked, options, trusted: true);

                if (input.ClickCount == 2)
                {
                    Mouse(clicked, "dblclick", options, cancelable: true);
                }

                return;

            case MouseInputKind.Wheel:
                if (Wheel(target, options, input.DeltaX, input.DeltaY))
                {
                    runtime.Layout.ScrollBy(input.DeltaY);
                }

                return;

            default:
                return;
        }
    }

    private static void DispatchClickEvent(DomNodeObject target, in ClickOptions options, bool trusted)
    {
        var click = PointerEvent(target, "click", options, cancelable: true);
        click.IsTrusted = trusted;
        target.DispatchEvent(click);
    }

    /// <summary>Dispatches one <c>PointerEvent</c>, answering whether nothing cancelled it.</summary>
    private static bool Pointer(DomNodeObject target, string type, in ClickOptions options, bool cancelable)
        => target.DispatchEvent(PointerEvent(target, type, options, cancelable));

    /// <summary>Dispatches one <c>MouseEvent</c>, answering whether nothing cancelled it.</summary>
    private static bool Mouse(DomNodeObject target, string type, in ClickOptions options, bool cancelable)
    {
        var realm = BrowserEventRealm.Of(target.DomRealm.Engine);
        var ev = new JsMouseEvent(
            target.DomRealm.Engine,
            JsString.Create(type),
            new EventInit(Bubbles: true, cancelable, Composed: true),
            realm.TimeStamp,
            target.DomRealm.Engine._mainRealm.GlobalObject,
            detail: options.Detail,
            which: null,
            MouseState(options));

        ev._prototype = realm.PrototypeOf(BrowserEventInterfaces.MouseEvent);
        ev.IsTrusted = true;
        return target.DispatchEvent(ev);
    }

    /// <summary>Dispatches one <c>WheelEvent</c>, answering whether nothing cancelled it.</summary>
    private static bool Wheel(DomNodeObject target, in ClickOptions options, double deltaX, double deltaY)
    {
        var realm = BrowserEventRealm.Of(target.DomRealm.Engine);
        var ev = new JsWheelEvent(
            target.DomRealm.Engine,
            JsString.Create("wheel"),
            new EventInit(Bubbles: true, Cancelable: true, Composed: true),
            realm.TimeStamp,
            target.DomRealm.Engine._mainRealm.GlobalObject,
            detail: 0,
            which: null,
            MouseState(options),
            deltaX,
            deltaY,
            deltaZ: 0,

            // 0 is DOM_DELTA_PIXEL, which is what the protocol's deltaX/deltaY are measured in.
            deltaMode: 0);

        ev._prototype = realm.PrototypeOf(BrowserEventInterfaces.WheelEvent);
        ev.IsTrusted = true;
        return target.DispatchEvent(ev);
    }

    /// <summary>
    /// A <c>PointerEvent</c> over <paramref name="options"/>. A click is one in every current browser, and
    /// <c>PointerEvent</c> inherits <c>MouseEvent</c>, so it is still the activation event dispatch step 6.4
    /// asks for.
    /// </summary>
    private static JsPointerEvent PointerEvent(DomNodeObject target, string type, in ClickOptions options, bool cancelable)
    {
        var engine = target.DomRealm.Engine;
        var realm = BrowserEventRealm.Of(engine);

        var ev = new JsPointerEvent(
            engine,
            JsString.Create(type),
            new EventInit(Bubbles: true, cancelable, Composed: true),
            realm.TimeStamp,
            engine._mainRealm.GlobalObject,
            detail: options.Detail,
            which: null,
            MouseState(options),
            new PointerEventState(
                PointerId: 1,
                Width: 1,
                Height: 1,
                Pressure: 0,
                TangentialPressure: 0,
                TiltX: 0,
                TiltY: 0,
                Twist: 0,
                PointerType: "mouse",
                IsPrimary: true));

        ev._prototype = realm.PrototypeOf(BrowserEventInterfaces.PointerEvent);
        ev.IsTrusted = true;
        return ev;
    }

    private static MouseEventState MouseState(in ClickOptions options) => new(
        options.ScreenX,
        options.ScreenY,
        options.ClientX,
        options.ClientY,
        options.Button,
        options.Buttons,
        options.Modifiers,
        RelatedTarget: null);

    /// <summary>
    /// The nearest common inclusive ancestor of two elements, or <see langword="null"/> when one is missing.
    /// </summary>
    private static IElement? CommonAncestor(IElement? first, IElement? second)
    {
        if (first is null || second is null)
        {
            return null;
        }

        for (IElement? candidate = first; candidate is not null; candidate = candidate.ParentElement)
        {
            for (IElement? other = second; other is not null; other = other.ParentElement)
            {
                if (ReferenceEquals(candidate, other))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// A key press as a user makes it: <c>keydown</c> at the focused element, <c>keypress</c> for a printable
    /// key, the edit or the traversal the key asks for, then <c>keyup</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The target is the focused element, or the body when nothing is focused — HTML's "the currently focused
    /// area of the document", whose fallback is the document's viewport and whose event target is the body.
    /// </para>
    /// <para>
    /// A <c>keydown</c> a listener cancels stops the default action, which is exactly what a page relies on for
    /// <c>e.preventDefault()</c> in a key handler; <c>keyup</c> fires either way, because it is not part of the
    /// default action.
    /// </para>
    /// </remarks>
    internal static void DispatchKey(Engine engine, in KeyOptions options)
    {
        var dom = DomRealm.Of(engine);
        var document = PageRuntime.Find(engine)?.Document;

        if (document is null)
        {
            return;
        }

        DispatchKey(dom, document, options);
    }

    /// <summary>The same, for a caller that already holds the document.</summary>
    internal static void DispatchKey(DomRealm dom, IDocument document, in KeyOptions options)
    {
        var realm = BrowserEventRealm.Of(dom.Engine);
        var focused = FocusController.ActiveElement(realm, document);

        if (focused is null)
        {
            return;
        }

        var target = dom.WrapNode(focused);
        var keyDown = KeyEvent(dom, realm, "keydown", options, cancelable: true);

        if (target.DispatchEvent(keyDown))
        {
            RunDefaultAction(dom, document, focused, keyDown, options);
        }

        // The focused element may have changed — Tab moved it, Enter submitted — so keyup goes where focus is
        // now, which is what a browser does.
        var after = FocusController.ActiveElement(realm, document) ?? focused;
        dom.WrapNode(after).DispatchEvent(KeyEvent(dom, realm, "keyup", options, cancelable: true));
    }

    private static void RunDefaultAction(DomRealm dom, IDocument document, IElement focused, JsKeyboardEvent keyDown, in KeyOptions options)
    {
        // https://w3c.github.io/uievents/#event-type-keypress — legacy, fired only for a key that produces a
        // character, and cancelable, so a page's `return false` in an onkeypress still blocks the insertion.
        if (options.Key.Length == 1)
        {
            var keyPress = KeyEvent(dom, BrowserEventRealm.Of(dom.Engine), "keypress", options, cancelable: true);
            if (!dom.WrapNode(focused).DispatchEvent(keyPress))
            {
                return;
            }
        }

        switch (options.Key)
        {
            case "Tab":
                MoveFocus(dom, document, focused, backwards: (options.Modifiers & EventModifiers.Shift) != EventModifiers.None);
                return;

            case "Enter" when focused is IHtmlInputElement input && TextEditing.IsEditable(input):
                ImplicitSubmission(dom, input);
                return;
        }

        TextEditing.HandleKeyDown(dom, focused, keyDown);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#sequential-focus-navigation — <kbd>Tab</kbd>,
    /// and <kbd>Shift</kbd>+<kbd>Tab</kbd> the other way.
    /// </summary>
    private static void MoveFocus(DomRealm dom, IDocument document, IElement from, bool backwards)
    {
        if (FocusController.NextInTabOrder(document, from, backwards) is { } next)
        {
            FocusController.Focus(dom, next);
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#implicit-submission —
    /// <kbd>Enter</kbd> in a single-line text control submits its form, through the default button when there
    /// is one.
    /// </summary>
    /// <remarks>
    /// The "more than one field that blocks implicit submission" clause is HTML's, and it is the rule that
    /// makes <kbd>Enter</kbd> submit a one-field search form and do nothing in a two-field login form that has
    /// no submit button.
    /// </remarks>
    private static void ImplicitSubmission(DomRealm dom, IHtmlInputElement input)
    {
        if (input.Form is not { } form)
        {
            return;
        }

        if (DefaultButton(form) is { } button)
        {
            if (!IsDisabled(button))
            {
                FireSyntheticClick(dom.WrapNode(button), trusted: true);
            }

            return;
        }

        if (BlockingFieldCount(form) > 1)
        {
            return;
        }

        FormSubmission.Submit(dom, form, submitter: null);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#default-button — the first submit button in tree
    /// order among the form's controls.
    /// </summary>
    private static IHtmlElement? DefaultButton(IHtmlFormElement form)
    {
        foreach (var element in form.Elements)
        {
            if (element is IHtmlElement html && FormSubmission.IsSubmitButton(html))
            {
                return html;
            }
        }

        return null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#field-that-blocks-implicit-submission —
    /// the input types whose presence in more than one makes <kbd>Enter</kbd> do nothing.
    /// </summary>
    private static int BlockingFieldCount(IHtmlFormElement form)
    {
        var count = 0;

        foreach (var element in form.Elements)
        {
            if (element is IHtmlInputElement input && input.Type is
                "text" or "search" or "url" or "tel" or "email" or "password"
                or "date" or "month" or "week" or "time" or "datetime-local" or "number")
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsDisabled(IHtmlElement element) => element switch
    {
        IHtmlButtonElement button => button.IsDisabled,
        IHtmlInputElement input => input.IsDisabled,
        _ => false,
    };

    private static JsKeyboardEvent KeyEvent(DomRealm dom, BrowserEventRealm realm, string type, in KeyOptions options, bool cancelable)
    {
        var ev = new JsKeyboardEvent(
            dom.Engine,
            JsString.Create(type),
            new EventInit(Bubbles: true, cancelable, Composed: true),
            realm.TimeStamp,
            dom.Engine._mainRealm.GlobalObject,
            detail: 0,
            which: null,
            new KeyboardEventState(
                options.Key,
                options.Code,
                options.Location,
                options.Repeat,
                IsComposing: false,
                options.Modifiers,
                CharCode: null,
                KeyCode: null));

        ev._prototype = realm.PrototypeOf(BrowserEventInterfaces.KeyboardEvent);
        ev.IsTrusted = true;
        return ev;
    }

    /// <summary>
    /// The nearest focusable element at or above <paramref name="element"/> — HTML's "if the element is not a
    /// focusable area, then the nearest ancestor that is".
    /// </summary>
    private static IElement? NearestFocusable(IElement element)
    {
        for (INode? node = element; node is not null; node = node.Parent)
        {
            if (node is IElement candidate && FocusController.IsFocusable(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

/// <summary>
/// What a synthesized click carries — the <c>MouseEventInit</c> members a dispatcher supplies, in one value.
/// </summary>
/// <param name="ScreenX">The <c>screenX</c> to report.</param>
/// <param name="ScreenY">The <c>screenY</c> to report.</param>
/// <param name="ClientX">The <c>clientX</c> to report, which is also what <c>pageX</c> and <c>offsetX</c> answer.</param>
/// <param name="ClientY">The <c>clientY</c> to report.</param>
/// <param name="Button">Which button — 0 primary, 1 auxiliary, 2 secondary.</param>
/// <param name="Buttons">The bit set of buttons held down.</param>
/// <param name="Detail">The click count; 1 for a single click, which is what <c>UIEvent.detail</c> reports.</param>
/// <param name="Modifiers">The modifier keys held.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ClickOptions(
    double ScreenX,
    double ScreenY,
    double ClientX,
    double ClientY,
    double Button,
    double Buttons,
    double Detail,
    EventModifiers Modifiers)
{
    /// <summary>
    /// What HTML's <i>fire a synthetic pointer event</i> initializes: every coordinate and modifier zero, and
    /// a <c>detail</c> of one.
    /// </summary>
    internal static ClickOptions Synthetic { get; } = new(0, 0, 0, 0, 0, 0, 1, EventModifiers.None);

    /// <summary>A primary-button click at a point, which is what a coordinate dispatcher produces.</summary>
    internal static ClickOptions At(double clientX, double clientY)
        => new(clientX, clientY, clientX, clientY, 0, 0, 1, EventModifiers.None);
}

/// <summary>Which of the four mouse events <c>Input.dispatchMouseEvent</c> was asked for.</summary>
internal enum MouseInputKind
{
    /// <summary>The pointer moved to the point.</summary>
    Moved,

    /// <summary>A button went down at the point.</summary>
    Pressed,

    /// <summary>A button came up at the point.</summary>
    Released,

    /// <summary>The wheel turned at the point.</summary>
    Wheel,
}

/// <summary>
/// One mouse event as a client sends it: where it happened, which button, and how many clicks.
/// </summary>
/// <param name="Kind">Which of the four events this is.</param>
/// <param name="X">The viewport <c>x</c> coordinate.</param>
/// <param name="Y">The viewport <c>y</c> coordinate.</param>
/// <param name="Button">Which button — 0 primary, 1 auxiliary, 2 secondary.</param>
/// <param name="Buttons">The bit set of buttons held down.</param>
/// <param name="ClickCount">The click count, which becomes the event's <c>detail</c>.</param>
/// <param name="Modifiers">The modifier keys held.</param>
/// <param name="DeltaX">The horizontal wheel delta, in CSS pixels.</param>
/// <param name="DeltaY">The vertical wheel delta, in CSS pixels.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct MouseInput(
    MouseInputKind Kind,
    double X,
    double Y,
    double Button,
    double Buttons,
    int ClickCount,
    EventModifiers Modifiers,
    double DeltaX,
    double DeltaY)
{
    /// <summary>
    /// What the dispatched events carry. <c>screenX</c> and <c>screenY</c> are the client coordinates: the
    /// window has no position on a screen that does not exist, so the two spaces coincide.
    /// </summary>
    internal ClickOptions ToClickOptions()
        => new(X, Y, X, Y, Button, Buttons, ClickCount, Modifiers);
}

/// <summary>
/// What a synthesized key press carries — the <c>KeyboardEventInit</c> members a dispatcher supplies.
/// </summary>
/// <param name="Key">The <c>key</c> value: the character for a printable key, the named value otherwise.</param>
/// <param name="Code">The <c>code</c> value — the physical key, which a protocol client supplies and this never invents.</param>
/// <param name="Location">The <c>location</c>: 0 standard, 1 left, 2 right, 3 numpad.</param>
/// <param name="Repeat">Whether the key is auto-repeating.</param>
/// <param name="Modifiers">The modifier keys held.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct KeyOptions(
    string Key,
    string Code,
    double Location,
    bool Repeat,
    EventModifiers Modifiers)
{
    /// <summary>One key with no modifiers, which is what a test and a plain <c>Input.dispatchKeyEvent</c> send.</summary>
    internal static KeyOptions For(string key) => new(key, "", 0, false, EventModifiers.None);
}
