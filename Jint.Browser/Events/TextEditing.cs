using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// Editing a text control without a layout: the selection, the six edits a key can make, and the
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
/// splice. What a layout would add is line geometry — <kbd>ArrowUp</kbd>, <kbd>ArrowDown</kbd> and a
/// <kbd>Home</kbd> that stops at the start of a wrapped visual line — and those are the keys this deliberately
/// does not handle rather than guessing at.
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
    private static readonly ConditionalWeakTable<IElement, EditSnapshot> _valuesAtFocus = new();

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#do-not-apply — the input types whose value is text a
    /// user types, plus <c>&lt;textarea&gt;</c>, which is the whole of what this version edits.
    /// </summary>
    internal static bool IsEditable(IElement element) => element switch
    {
        IHtmlTextAreaElement textArea => !textArea.IsDisabled && !textArea.IsReadOnly,
        IHtmlInputElement input => !input.IsDisabled && !input.IsReadOnly && input.Type is
            "text" or "search" or "url" or "tel" or "password" or "email" or "number",
        _ => false,
    };

    /// <summary>Whether the control holds one line, which is what makes <kbd>Enter</kbd> submit rather than insert.</summary>
    internal static bool IsSingleLine(IElement element) => element is IHtmlInputElement;

    internal static void RememberValueAtFocus(IElement element)
    {
        if (IsEditable(element) || element is IHtmlSelectElement)
        {
            _valuesAtFocus.AddOrUpdate(element, new EditSnapshot(ValueOf(element)));
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#focus-update-steps step 3's first clause: a
    /// control whose value the user changed since it was focused fires <c>change</c> on the way out.
    /// </summary>
    internal static void FireChangeIfEdited(DomRealm dom, IElement element)
    {
        if (!_valuesAtFocus.TryGetValue(element, out var snapshot))
        {
            return;
        }

        _valuesAtFocus.Remove(element);

        if (string.Equals(snapshot.Value, ValueOf(element), StringComparison.Ordinal))
        {
            return;
        }

        ActivationBehaviors.Fire(dom.WrapNode(element), "change", bubbles: true, composed: false);
    }

    /// <summary>
    /// Runs the edit a <c>keydown</c> asks for, firing <c>beforeinput</c> before it and <c>input</c> after.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the key was consumed by the editor, so the caller knows not to treat it as
    /// anything else.
    /// </returns>
    internal static bool HandleKeyDown(DomRealm dom, IElement element, JsKeyboardEvent key)
    {
        if (!IsEditable(element))
        {
            return false;
        }

        var control = new TextControl(element);

        switch (key.Key)
        {
            case "Backspace":
                return DeleteAround(dom, control, forward: false);

            case "Delete":
                return DeleteAround(dom, control, forward: true);

            case "ArrowLeft":
                return MoveCaret(control, control.Start == control.End ? control.Start - 1 : control.Start, key.Modifiers);

            case "ArrowRight":
                return MoveCaret(control, control.Start == control.End ? control.End + 1 : control.End, key.Modifiers);

            case "Home":
                return MoveCaret(control, 0, key.Modifiers);

            case "End":
                return MoveCaret(control, control.Value.Length, key.Modifiers);

            case "Enter" when !IsSingleLine(element):
                return Insert(dom, control, "\n", "insertLineBreak");
        }

        // A printable key is one whose `key` is the character it produced. Anything longer is a named key, and
        // a character typed with Control or Meta held is a shortcut rather than an insertion.
        if (key.Key.Length == 1 && (key.Modifiers & (EventModifiers.Control | EventModifiers.Meta)) == EventModifiers.None)
        {
            return Insert(dom, control, key.Key, "insertText");
        }

        return false;
    }

    /// <summary>
    /// https://w3c.github.io/input-events/#interface-InputEvent — replace the selection with
    /// <paramref name="text"/>, unless a <c>beforeinput</c> listener cancels it.
    /// </summary>
    internal static bool Insert(DomRealm dom, in TextControl control, string text, string inputType)
    {
        if (!FireBeforeInput(dom, control.Element, inputType, JsString.Create(text)))
        {
            return true;
        }

        var value = control.Value;
        var start = control.Start;
        var end = control.End;

        control.Value = string.Concat(value.AsSpan(0, start), text, value.AsSpan(end));
        control.SetSelection(start + text.Length);

        FireInput(dom, control.Element, inputType, JsString.Create(text));
        return true;
    }

    private static bool DeleteAround(DomRealm dom, in TextControl control, bool forward)
    {
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

        control.Value = string.Concat(value.AsSpan(0, start), value.AsSpan(end));
        control.SetSelection(start);

        FireInput(dom, control.Element, inputType, JsValue.Null);
        return true;
    }

    /// <summary>
    /// Caret movement, which fires nothing: it is not an edit, and <c>selectionchange</c> is a document-level
    /// event this version does not fire. Shift extends the selection from its anchor instead of collapsing it.
    /// </summary>
    private static bool MoveCaret(in TextControl control, int position, EventModifiers modifiers)
    {
        var clamped = Math.Clamp(position, 0, control.Value.Length);

        if ((modifiers & EventModifiers.Shift) != EventModifiers.None)
        {
            control.SetSelection(Math.Min(control.Start, clamped), Math.Max(control.End, clamped));
        }
        else
        {
            control.SetSelection(clamped);
        }

        return true;
    }

    /// <summary>
    /// https://w3c.github.io/input-events/#event-type-beforeinput — cancelable, so a listener can refuse the
    /// edit. Returns whether the edit may proceed.
    /// </summary>
    private static bool FireBeforeInput(DomRealm dom, IElement element, string inputType, JsValue data)
        => FireInputEvent(dom, element, "beforeinput", inputType, data, cancelable: true);

    /// <summary>https://w3c.github.io/input-events/#event-type-input — not cancelable; the edit already happened.</summary>
    private static void FireInput(DomRealm dom, IElement element, string inputType, JsValue data)
        => FireInputEvent(dom, element, "input", inputType, data, cancelable: false);

    private static bool FireInputEvent(DomRealm dom, IElement element, string type, string inputType, JsValue data, bool cancelable)
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
                data,
                isComposing: false,
                inputType));

        return dom.WrapNode(element).DispatchEvent(ev);
    }

    private static string ValueOf(IElement element) => element switch
    {
        IHtmlInputElement input => input.Value ?? "",
        IHtmlTextAreaElement textArea => textArea.Value ?? "",
        IHtmlSelectElement select => select.Value ?? "",
        _ => "",
    };

    /// <summary>The value a control had when it was focused.</summary>
    private sealed class EditSnapshot(string value)
    {
        internal string Value { get; } = value;
    }

    /// <summary>
    /// One text control's value and selection, whichever of the two AngleSharp interfaces backs it, with both
    /// offsets clamped to the value on every read.
    /// </summary>
    internal readonly struct TextControl
    {
        private readonly IHtmlInputElement? _input;
        private readonly IHtmlTextAreaElement? _textArea;

        internal TextControl(IElement element)
        {
            Element = element;
            _input = element as IHtmlInputElement;
            _textArea = element as IHtmlTextAreaElement;
        }

        internal IElement Element { get; }

        internal string Value
        {
            get => _input is not null ? _input.Value ?? "" : _textArea?.Value ?? "";
            set
            {
                if (_input is not null)
                {
                    _input.Value = value;
                }
                else if (_textArea is not null)
                {
                    _textArea.Value = value;
                }
            }
        }

        internal int Start => Math.Clamp(_input?.SelectionStart ?? _textArea?.SelectionStart ?? 0, 0, Value.Length);

        internal int End => Math.Clamp(_input?.SelectionEnd ?? _textArea?.SelectionEnd ?? 0, Start, Value.Length);

        internal void SetSelection(int caret) => SetSelection(caret, caret);

        internal void SetSelection(int start, int end)
        {
            var length = Value.Length;
            var from = Math.Clamp(start, 0, length);
            var to = Math.Clamp(end, from, length);

            _input?.Select(from, to, "none");
            _textArea?.Select(from, to, "none");
        }
    }
}
