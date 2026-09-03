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
    /// A key press as a user makes it: the whole of a <c>keydown</c> — the event, the <c>keypress</c> a
    /// printable key adds, and the edit or the traversal the key asks for — and then the <c>keyup</c>.
    /// </summary>
    /// <remarks>
    /// This is the two protocol events at once, which is what a synthesized press is; a client sends them
    /// separately and reaches <see cref="DispatchKey(PageRuntime, in KeyOptions, KeyInputKind)"/> twice.
    /// </remarks>
    internal static void DispatchKey(Engine engine, in KeyOptions options)
    {
        if (PageRuntime.Find(engine) is not { } runtime)
        {
            return;
        }

        DispatchKey(runtime, options, KeyInputKind.KeyDown);
        DispatchKey(runtime, options, KeyInputKind.KeyUp);
    }

    /// <summary>
    /// Text at the caret with no key events at all, which is what <c>Input.insertText</c> becomes.
    /// </summary>
    /// <remarks>
    /// It is the shape an IME commit and a paste arrive in, and the shape a client library uses for a
    /// character its keyboard layout has no key for — Puppeteer's <c>sendCharacter</c> is exactly this.
    /// </remarks>
    internal static void InsertText(Engine engine, string text)
    {
        if (PageRuntime.Find(engine) is { } runtime)
        {
            InsertText(runtime, text);
        }
    }

    /// <summary>The same, for a caller that already holds the page.</summary>
    internal static void InsertText(PageRuntime runtime, string text)
    {
        if (runtime.Document is not { } document)
        {
            return;
        }

        var dom = runtime.Dom;
        var focused = FocusController.ActiveElement(BrowserEventRealm.Of(dom.Engine), document);

        if (focused is null)
        {
            return;
        }

        if (TextEditing.IsEditable(focused))
        {
            TextEditing.Insert(dom, new TextEditing.TextControl(focused), text, "insertText");
            return;
        }

        if (ContentEditing.HostOf(focused) is { } host)
        {
            ContentEditing.Insert(dom, host, text, "insertText");
        }
    }

    /// <summary>
    /// One key event as a client sends it: <c>keyDown</c>, <c>rawKeyDown</c>, <c>keyUp</c> or <c>char</c>,
    /// dispatched at the focused element.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The four types are three questions</b> — which event fires, whether a <c>keypress</c> follows, and
    /// whether a character may be inserted. <c>keyDown</c> fires <c>keydown</c> and, for a key that produces
    /// text, <c>keypress</c>, and then runs the whole default action. <c>rawKeyDown</c> fires <c>keydown</c>
    /// and runs the default action <i>without</i> the insertion, because it is what a client sends for a key
    /// whose character is coming separately or is not coming at all — which is every editing key.
    /// <c>char</c> is that character on its own: a <c>keypress</c> and then the insertion. <c>keyUp</c> fires
    /// <c>keyup</c>, always, because it is not part of any default action.
    /// </para>
    /// <para>
    /// <b>The target is the focused element</b>, or the body when nothing is focused — HTML's "the currently
    /// focused area of the document", whose fallback is the document's viewport and whose event target is the
    /// body. A <c>keydown</c> a listener cancels stops the default action, which is what a page relies on for
    /// <c>e.preventDefault()</c> in a key handler.
    /// </para>
    /// <para>
    /// <b>Modifier state is the client's.</b> Every client tracks which modifiers it is holding and puts them
    /// in each event's bit field, so nothing here has to remember that <kbd>Shift</kbd> went down: the arrow
    /// key that follows says so itself.
    /// </para>
    /// </remarks>
    internal static void DispatchKey(PageRuntime runtime, in KeyOptions options, KeyInputKind kind)
    {
        if (runtime.Document is not { } document)
        {
            return;
        }

        var dom = runtime.Dom;
        var realm = BrowserEventRealm.Of(dom.Engine);
        var focused = FocusController.ActiveElement(realm, document);

        if (focused is null)
        {
            return;
        }

        if (kind == KeyInputKind.KeyUp)
        {
            dom.WrapNode(focused).DispatchEvent(KeyEvent(dom, realm, "keyup", options, cancelable: true));
            return;
        }

        if (kind == KeyInputKind.Char)
        {
            if (Keypress(dom, realm, focused, options))
            {
                RunDefaultAction(dom, document, focused, options, allowInsertion: true);
            }

            return;
        }

        if (!dom.WrapNode(focused).DispatchEvent(KeyEvent(dom, realm, "keydown", options, cancelable: true)))
        {
            return;
        }

        // https://w3c.github.io/uievents/#event-type-keypress — legacy, fired only for a key that produces a
        // character, and cancelable, so a page's `return false` in an onkeypress still blocks the insertion.
        if (kind == KeyInputKind.KeyDown && options.ProducedText.Length > 0 && !Keypress(dom, realm, focused, options))
        {
            return;
        }

        RunDefaultAction(dom, document, focused, options, allowInsertion: kind == KeyInputKind.KeyDown);
    }

    /// <summary>Dispatches <c>keypress</c>, answering whether nothing cancelled it.</summary>
    private static bool Keypress(DomRealm dom, BrowserEventRealm realm, IElement focused, in KeyOptions options)
        => dom.WrapNode(focused).DispatchEvent(KeyEvent(dom, realm, "keypress", options, cancelable: true));

    /// <summary>
    /// What the key does when nothing cancelled it: move focus, submit a form, or edit whatever is focused.
    /// </summary>
    /// <remarks>
    /// <c>commands</c> — the macOS editing commands a client attaches to a key event — are accepted and
    /// ignored except for <c>selectAll</c>, which is the one that maps onto something this editor has. A
    /// command nothing here knows leaves the key's ordinary default action to run, which is what a client
    /// sending <c>moveToEndOfLine</c> beside an <kbd>End</kbd> key needs.
    /// </remarks>
    private static void RunDefaultAction(DomRealm dom, IDocument document, IElement focused, in KeyOptions options, bool allowInsertion)
    {
        if (options.HasCommand("selectAll"))
        {
            SelectAll(dom, focused);
            return;
        }

        switch (options.Key)
        {
            case "Tab":
                MoveFocus(dom, document, focused, backwards: (options.Modifiers & EventModifiers.Shift) != EventModifiers.None);
                return;

            case "Enter" when focused is IHtmlInputElement input && TextEditing.IsEditable(input):
                // The value is committed by the press, so `change` fires here rather than waiting for focus to
                // leave — which is what a page listening for it on a search box is written against.
                TextEditing.CommitChange(dom, input);
                ImplicitSubmission(dom, input);
                return;
        }

        if (TextEditing.HandleKeyDown(dom, focused, options, allowInsertion))
        {
            return;
        }

        if (ContentEditing.HostOf(focused) is { } host)
        {
            ContentEditing.HandleKeyDown(dom, host, options, allowInsertion);
        }
    }

    /// <summary>Select-all, wherever the focus is — a text control or an editing host.</summary>
    private static void SelectAll(DomRealm dom, IElement focused)
    {
        var selectAll = KeyOptions.For("a") with { Modifiers = EventModifiers.Control };

        if (TextEditing.HandleKeyDown(dom, focused, selectAll, allowInsertion: false))
        {
            return;
        }

        if (ContentEditing.HostOf(focused) is { } host)
        {
            ContentEditing.HandleKeyDown(dom, host, selectAll, allowInsertion: false);
        }
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

    /// <summary>
    /// One keyboard event over <paramref name="options"/>.
    /// </summary>
    /// <remarks>
    /// <c>charCode</c> and <c>keyCode</c> are left to <c>LegacyKeyCodes</c>' table on <c>keydown</c> and
    /// <c>keyup</c> and are pinned on <c>keypress</c>, because there they are the character the key really
    /// produced rather than a derivation from <c>key</c> — which is the whole difference for <kbd>Enter</kbd>,
    /// whose <c>key</c> is a name and whose character is <c>U+000D</c>, so a page's <c>keyCode === 13</c> on a
    /// <c>keypress</c> is answered as every browser answers it.
    /// </remarks>
    private static JsKeyboardEvent KeyEvent(DomRealm dom, BrowserEventRealm realm, string type, in KeyOptions options, bool cancelable)
    {
        double? charCode = string.Equals(type, "keypress", StringComparison.Ordinal)
            && options.ProducedText is { Length: > 0 } produced
                ? produced[0]
                : null;

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
                charCode,
                KeyCode: charCode));

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

/// <summary>Which of the four events <c>Input.dispatchKeyEvent</c> was asked for.</summary>
internal enum KeyInputKind
{
    /// <summary>A key went down, with the character it produces.</summary>
    KeyDown,

    /// <summary>A key went down and its character, if any, is a separate event.</summary>
    RawKeyDown,

    /// <summary>A key came up.</summary>
    KeyUp,

    /// <summary>A character, with no key event of its own.</summary>
    Char,
}

/// <summary>
/// What a synthesized key press carries — the <c>KeyboardEventInit</c> members a dispatcher supplies.
/// </summary>
/// <param name="Key">The <c>key</c> value: the character for a printable key, the named value otherwise.</param>
/// <param name="Code">The <c>code</c> value — the physical key, which a protocol client supplies and this never invents.</param>
/// <param name="Text">The text the key produces, which a client sends and which is empty for a key that produces none.</param>
/// <param name="Location">The <c>location</c>: 0 standard, 1 left, 2 right, 3 numpad.</param>
/// <param name="Repeat">Whether the key is auto-repeating.</param>
/// <param name="Modifiers">The modifier keys held.</param>
/// <param name="Commands">The editing commands a client attached to the event, or <see langword="null"/>.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct KeyOptions(
    string Key,
    string Code,
    string Text,
    double Location,
    bool Repeat,
    EventModifiers Modifiers,
    string[]? Commands)
{
    /// <summary>One key with no modifiers, which is what a test and a plain <c>Input.dispatchKeyEvent</c> send.</summary>
    internal static KeyOptions For(string key) => new(key, "", "", 0, false, EventModifiers.None, null);

    /// <summary>
    /// The text this key produces: what the client said, or the <c>key</c> itself when it is one character,
    /// which is how a key that produces a character is told from a named one.
    /// </summary>
    /// <remarks>
    /// A character typed with <kbd>Control</kbd> or <kbd>Meta</kbd> held produces none — it is a shortcut, and
    /// every client already sends it with an empty <c>text</c>; the fallback has to agree, or a synthesized
    /// <kbd>Ctrl</kbd>+<kbd>A</kbd> would fire a <c>keypress</c> no browser fires.
    /// </remarks>
    internal string ProducedText
    {
        get
        {
            if (Text.Length > 0)
            {
                return Text;
            }

            var shortcut = (Modifiers & (EventModifiers.Control | EventModifiers.Meta)) != EventModifiers.None;
            return Key.Length == 1 && !shortcut ? Key : "";
        }
    }

    /// <summary>Whether the client attached <paramref name="command"/> to this event.</summary>
    internal bool HasCommand(string command)
        => Commands is not null && Array.IndexOf(Commands, command) >= 0;
}
