using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// Editing a text control without a layout: the selection, the commands a key can run, and the
/// <c>beforeinput</c> / <c>input</c> / <c>change</c> events each of them fires.
/// <para>
/// https://html.spec.whatwg.org/multipage/input.html#input-value-selection and
/// https://w3c.github.io/input-events/
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// A text control's whole editable state is a string and two offsets, none of which needs a rendering: the
/// caret is <c>selectionStart == selectionEnd</c>, a selection is the range between them, and every edit is a
/// splice. What a layout would add is line geometry, and only one thing here needs it —
/// <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd>, which move by the newlines in the value because nothing wraps, so
/// a visual line and a logical one are the same thing here and are not in a browser.
/// </para>
/// <para>
/// <b>The selection has a direction and it is load-bearing.</b> <kbd>Shift</kbd> extends from the
/// <i>anchor</i> — the end the caret is not at — so <c>selectionDirection</c> is what says which offset moves,
/// and a selection dragged back through its anchor flips it. That is why every move goes through
/// <see cref="Apply"/> rather than through <c>Math.Min</c> and <c>Math.Max</c>.
/// </para>
/// <para>
/// <b>Two AngleSharp behaviours are worked around in the caller rather than relied on.</b> Assigning
/// <c>Value</c> leaves <c>SelectionStart</c> and <c>SelectionEnd</c> where they were, so they can end up past
/// the end of the new value — HTML says the assignment moves the cursor to the end — so every edit here sets
/// the selection explicitly afterwards and every read clamps. And the selection members answer on a
/// <c>type=checkbox</c> input where HTML raises <c>InvalidStateError</c>, so the type test is this file's,
/// not AngleSharp's.
/// </para>
/// </remarks>
internal static class TextEditing
{
    /// <summary>
    /// The value a control had when it was focused, so <c>change</c> can fire on the way out exactly when the
    /// user changed something — https://html.spec.whatwg.org/multipage/interaction.html#focus-update-steps.
    /// </summary>
    private static readonly ConditionalWeakTable<Element, EditSnapshot> _valuesAtFocus = new();

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#do-not-apply — the input types whose value is text a
    /// user types, plus <c>&lt;textarea&gt;</c>, which is the whole of what this version edits.
    /// </summary>
    internal static bool IsEditable(DomRealm dom, Element element)
    {
        if (element.NamespaceUri != Namespaces.Html || EventDom.Disabled(dom, element) || element.HasContentAttribute("readonly"))
        {
            return false;
        }
        return element.LocalName == "textarea" || element.LocalName == "input"
            && EventDom.InputType(element) is "text" or "search" or "url" or "tel" or "password" or "email" or "number"
            && element.GetHtmlState()!.InputValue!.GetEditingSelection(dom.CancellationToken) is not null;
    }

    /// <summary>Whether the control holds one line, which is what makes <kbd>Enter</kbd> submit rather than insert.</summary>
    internal static bool IsSingleLine(Element element) => EventDom.IsHtml(element, "input");

    internal static void RememberValueAtFocus(DomRealm dom, Element element)
    {
        if (IsEditable(dom, element) || EventDom.IsHtml(element, "select"))
        {
            _valuesAtFocus.AddOrUpdate(element, new EditSnapshot(ValueOf(dom, element)));
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#focus-update-steps step 3's first clause: a
    /// control whose value the user changed since it was focused fires <c>change</c> on the way out.
    /// </summary>
    internal static void FireChangeIfEdited(DomRealm dom, Element element)
    {
        var changed = Changed(dom, element);
        _valuesAtFocus.Remove(element);
        if (changed)
        {
            FireChange(dom, element);
        }
    }

    /// <summary>
    /// The same verdict, for a control that keeps focus: <kbd>Enter</kbd> in a single-line control commits the
    /// value, which is why a browser fires <c>change</c> there and not again when focus later leaves.
    /// </summary>
    internal static void CommitChange(DomRealm dom, Element element)
    {
        if (!Changed(dom, element))
        {
            return;
        }

        // Re-armed rather than removed, because the control is still focused and the next edit has to be able
        // to fire a second change.
        _valuesAtFocus.AddOrUpdate(element, new EditSnapshot(ValueOf(dom, element)));
        FireChange(dom, element);
    }

    /// <summary>
    /// Runs the command a key asks for, firing <c>beforeinput</c> before any edit and <c>input</c> after it.
    /// </summary>
    /// <param name="dom">The realm the events are created in.</param>
    /// <param name="element">The focused element, which may not be editable at all.</param>
    /// <param name="options">The key, as the dispatcher received it.</param>
    /// <param name="allowInsertion">
    /// Whether a printable key may insert. It may not on a <c>rawKeyDown</c>, which is the protocol's "a key
    /// went down and its character is coming separately" — so the editing commands run and the character does
    /// not, exactly as Chrome splits them.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the key was consumed by the editor, so the caller knows not to treat it as
    /// anything else.
    /// </returns>
    internal static bool HandleKeyDown(DomRealm dom, Element element, in KeyOptions options, bool allowInsertion)
    {
        if (!IsEditable(dom, element))
        {
            return false;
        }

        var control = new TextControl(dom, element);
        var extend = (options.Modifiers & EventModifiers.Shift) != EventModifiers.None;
        var shortcut = (options.Modifiers & (EventModifiers.Control | EventModifiers.Meta)) != EventModifiers.None;

        // Select-all is the one keyboard shortcut every platform spells the same way, and the one a client's
        // `commands: ["selectAll"]` names. No specification says so — a shortcut is a user-agent decision —
        // which is why it is the only one this editor claims.
        if (shortcut && options.Key is "a" or "A")
        {
            return SelectAll(control);
        }

        if (shortcut)
        {
            // Everything else with Control or Meta held is a shortcut this editor has no command for, and a
            // page's own keydown handler is what it was for.
            return false;
        }

        switch (options.Key)
        {
            case "Backspace":
                return DeleteAround(dom, control, forward: false);

            case "Delete":
                return DeleteAround(dom, control, forward: true);

            case "ArrowLeft":
                return Apply(control, extend ? control.Focus - 1 : control.IsCollapsed ? control.Start - 1 : control.Start, extend);

            case "ArrowRight":
                return Apply(control, extend ? control.Focus + 1 : control.IsCollapsed ? control.End + 1 : control.End, extend);

            case "Home":
                return Apply(control, 0, extend);

            case "End":
                return Apply(control, control.Value.Length, extend);

            case "ArrowUp":
                return Apply(control, LineMove(control, forward: false), extend);

            case "ArrowDown":
                return Apply(control, LineMove(control, forward: true), extend);

            case "Enter" when !IsSingleLine(element):
                return Insert(dom, control, "\n", "insertLineBreak");
        }

        // A printable key is one whose `key` is the character it produced; anything longer is a named key.
        if (allowInsertion && options.ProducedText is { Length: > 0 } text && options.Key.Length == 1)
        {
            return Insert(dom, control, text, "insertText");
        }

        return false;
    }

    /// <summary>
    /// https://w3c.github.io/input-events/#interface-InputEvent — replace the selection with
    /// <paramref name="text"/>, unless a <c>beforeinput</c> listener cancels it.
    /// </summary>
    /// <remarks>
    /// <c>maxlength</c> is applied here and nowhere else, because HTML applies it to what a <i>user</i>
    /// enters: an insertion is truncated to what fits and one that cannot fit anything is not an edit at all,
    /// so it fires nothing. A script assigning <c>value</c> is past none of that, which is what the
    /// "dirty value flag" wording means and what makes the two paths different.
    /// </remarks>
    internal static bool Insert(DomRealm dom, in TextControl control, string text, string inputType)
    {
        using var mutation = dom.MutateLayout();
        var value = control.Value;
        var start = control.Start;
        var end = control.End;

        if (MaxLengthOf(dom, control.Element) is { } maximum)
        {
            var room = maximum - (value.Length - (end - start));

            if (room <= 0)
            {
                return true;
            }

            if (text.Length > room)
            {
                text = text.Substring(0, (int) room);
            }
        }

        if (!FireBeforeInput(dom, control.Element, inputType, JsString.Create(text)))
        {
            return true;
        }

        if (!control.ApplyUserValue(string.Concat(value.AsSpan(0, start), text, value.AsSpan(end)), start + text.Length))
        {
            return true;
        }

        FireInput(dom, control.Element, inputType, JsString.Create(text));
        return true;
    }

    private static bool DeleteAround(DomRealm dom, in TextControl control, bool forward)
    {
        using var mutation = dom.MutateLayout();
        var value = control.Value;
        var start = control.Start;
        var end = control.End;

        if (start == end)
        {
            if (forward)
            {
                if (end >= value.Length)
                {
                    return true;
                }

                end++;
            }
            else
            {
                if (start == 0)
                {
                    return true;
                }

                start--;
            }
        }

        var inputType = forward ? "deleteContentForward" : "deleteContentBackward";

        if (!FireBeforeInput(dom, control.Element, inputType, JsValue.Null))
        {
            return true;
        }

        if (!control.ApplyUserValue(string.Concat(value.AsSpan(0, start), value.AsSpan(end)), start))
        {
            return true;
        }

        FireInput(dom, control.Element, inputType, JsValue.Null);
        return true;
    }

    /// <summary>
    /// Select-all, which is the whole value with the caret at its end — so a <kbd>Shift</kbd>+arrow after it
    /// shrinks the selection from the end a user would expect to be dragging.
    /// </summary>
    private static bool SelectAll(in TextControl control)
    {
        control.SetSelection(0, control.Value.Length, "forward");
        return true;
    }

    /// <summary>
    /// Where <kbd>ArrowUp</kbd> and <kbd>ArrowDown</kbd> put the caret: the same column of the line above or
    /// below, clamped to that line's length, and the value's own ends when there is no such line.
    /// </summary>
    /// <remarks>
    /// A single-line control has one line, so the two keys are <kbd>Home</kbd> and <kbd>End</kbd> — which is
    /// what a browser does with them there.
    /// </remarks>
    private static int LineMove(in TextControl control, bool forward)
    {
        var value = control.Value;

        if (IsSingleLine(control.Element))
        {
            return forward ? value.Length : 0;
        }

        if (value.Length == 0)
        {
            return 0;
        }

        var caret = control.Focus;
        var lineStart = caret == 0 ? 0 : value.LastIndexOf('\n', caret - 1) + 1;
        var column = caret - lineStart;

        if (forward)
        {
            var lineEnd = value.IndexOf('\n', lineStart);
            if (lineEnd < 0)
            {
                return value.Length;
            }

            var nextStart = lineEnd + 1;
            var nextEnd = value.IndexOf('\n', nextStart);
            var nextLength = (nextEnd < 0 ? value.Length : nextEnd) - nextStart;
            return nextStart + Math.Min(column, nextLength);
        }

        if (lineStart == 0)
        {
            return 0;
        }

        var previousStart = lineStart <= 1 ? 0 : value.LastIndexOf('\n', lineStart - 2) + 1;
        return previousStart + Math.Min(column, lineStart - 1 - previousStart);
    }

    /// <summary>
    /// Moves the caret, or the focus end of the selection when <paramref name="extend"/>. It fires no editing
    /// event, because moving is not an edit — the only thing it raises is the <c>selectionchange</c>
    /// <see cref="TextControl.SetSelection(int, int, string)"/> schedules when the selection really moved.
    /// </summary>
    private static bool Apply(in TextControl control, int position, bool extend)
    {
        var clamped = Math.Clamp(position, 0, control.Value.Length);

        if (!extend)
        {
            control.SetSelection(clamped);
            return true;
        }

        var anchor = control.Anchor;

        if (clamped == anchor)
        {
            control.SetSelection(anchor);
            return true;
        }

        control.SetSelection(Math.Min(anchor, clamped), Math.Max(anchor, clamped), clamped > anchor ? "forward" : "backward");
        return true;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#attr-fe-maxlength, read off the
    /// content attribute rather than through AngleSharp, which answers for an absent one.
    /// </summary>
    private static long? MaxLengthOf(DomRealm dom, Element element)
        => HtmlTextControlAttributes.GetMaximumAllowedLength(element, dom.NativeReadCheckpoint, dom.CancellationToken);

    /// <summary>
    /// https://w3c.github.io/input-events/#event-type-beforeinput — cancelable, so a listener can refuse the
    /// edit. Returns whether the edit may proceed.
    /// </summary>
    internal static bool FireBeforeInput(DomRealm dom, Element element, string inputType, JsValue data)
        => FireInputEvent(dom, element, "beforeinput", inputType, data, cancelable: true);

    /// <summary>https://w3c.github.io/input-events/#event-type-input — not cancelable; the edit already happened.</summary>
    internal static void FireInput(DomRealm dom, Element element, string inputType, JsValue data)
        => FireInputEvent(dom, element, "input", inputType, data, cancelable: false);

    private static bool FireInputEvent(DomRealm dom, Element element, string type, string inputType, JsValue data, bool cancelable)
    {
        var realm = BrowserEventRealm.Of(dom.Engine);

        var ev = realm.CreateTrusted(
            BrowserEventInterfaces.InputEvent,
            new JsInputEvent(
                dom.Engine,
                JsString.Create(type),
                new EventInit(Bubbles: true, cancelable, Composed: true),
                realm.TimeStamp,
                dom.Engine._mainRealm.GlobalObject,
                detail: 0,
                which: null,
                data,
                isComposing: false,
                inputType));

        return dom.WrapNode(element).DispatchEvent(ev);
    }

    /// <summary>Whether the value moved since the control was focused, for a control that recorded one.</summary>
    private static bool Changed(DomRealm dom, Element element)
        => _valuesAtFocus.TryGetValue(element, out var snapshot)
            && !string.Equals(snapshot.Value, ValueOf(dom, element), StringComparison.Ordinal);

    private static void FireChange(DomRealm dom, Element element)
        => ActivationBehaviors.Fire(dom.WrapNode(element), "change", bubbles: true, composed: false);

    private static string ValueOf(DomRealm dom, Element element) => element switch
    {
        { NamespaceUri: Namespaces.Html, LocalName: "input" } => element.GetHtmlState()!.InputValue!.GetValue(dom.CancellationToken),
        { NamespaceUri: Namespaces.Html, LocalName: "textarea" } => element.GetHtmlState()!.TextArea!.GetValue(dom.CancellationToken),
        IHtmlSelectElement select => select.Value ?? "",
        _ => "",
    };

    /// <summary>The value a control had when it was focused.</summary>
    private sealed class EditSnapshot(string value)
    {
        internal string Value { get; } = value;
    }

    /// <summary>Editing reads and mutations over the control's authoritative native state.</summary>
    internal readonly struct TextControl
    {
        private readonly DomRealm _dom;
        private readonly HtmlInputValueState? _input;
        private readonly HtmlTextAreaState? _textArea;

        internal TextControl(DomRealm dom, Element element)
        {
            _dom = dom;
            Element = element;
            _input = element.GetHtmlState()?.InputValue;
            _textArea = element.GetHtmlState()?.TextArea;
        }

        internal Element Element { get; }
        internal string Value => _input is not null
            ? _input.GetValue(_dom.CancellationToken)
            : _textArea!.GetValue(_dom.CancellationToken);
        private HtmlTextSelection Selection => _input is not null
            ? _input.GetEditingSelection(_dom.CancellationToken)!.Value
            : _textArea!.GetEditingSelection(_dom.CancellationToken);
        internal int Start => (int) Selection.Start;
        internal int End => (int) Selection.End;
        internal bool IsCollapsed => Start == End;
        private bool IsBackward => Selection.Direction == HtmlSelectionDirection.Backward;
        internal int Focus => IsBackward ? Start : End;
        internal int Anchor => IsBackward ? End : Start;

        internal void SetSelection(int caret) => SetSelection(caret, caret, "none");

        internal void SetSelection(int start, int end, string direction)
        {
            var previous = Selection;
            var changed = _input is not null
                ? _input.SetEditingSelection((uint) start, (uint) end, direction, _dom.CancellationToken)
                : _textArea!.SetEditingSelection((uint) start, (uint) end, direction, _dom.CancellationToken);
            if (changed && previous != Selection) SelectionChange.Schedule(_dom, Element);
        }

        // HTML user edits commit value, dirty state, origin and caret together; script value setters are separate.
        internal bool ApplyUserValue(string value, int caret)
        {
            if (_input is not null && _input.GetEditingSelection(_dom.CancellationToken) is null)
            {
                return false;
            }
            var previous = Selection;
            var selection = new HtmlTextSelection((uint) caret, (uint) caret, HtmlSelectionDirection.None);
            var applied = _input is not null
                ? _input.ApplyUserValue(value, selection, _dom.CancellationToken)
                : _textArea!.ApplyUserValue(value, selection, _dom.CancellationToken);
            if (applied && previous != Selection) SelectionChange.Schedule(_dom, Element);
            return applied;
        }
    }
}
