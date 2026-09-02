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

    private static void DispatchClickEvent(DomNodeObject target, in ClickOptions options, bool trusted)
    {
        var dom = target.DomRealm;
        var realm = BrowserEventRealm.Of(dom.Engine);

        // A click is a PointerEvent in every current browser, and PointerEvent inherits MouseEvent, so it is
        // still the activation event dispatch step 6.4 asks for.
        var click = new JsPointerEvent(
            dom.Engine,
            JsString.Create("click"),
            new EventInit(Bubbles: true, Cancelable: true, Composed: true),
            realm.TimeStamp,
            dom.Engine._mainRealm.GlobalObject,
            detail: options.Detail,
            which: null,
            new MouseEventState(
                options.ScreenX,
                options.ScreenY,
                options.ClientX,
                options.ClientY,
                options.Button,
                options.Buttons,
                options.Modifiers,
                RelatedTarget: null),
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

        click._prototype = realm.PrototypeOf(BrowserEventInterfaces.PointerEvent);
        click.IsTrusted = trusted;

        target.DispatchEvent(click);
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
