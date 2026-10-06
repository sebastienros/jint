using Jint.HtmlParser;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// <c>contenteditable</c>, in the one shape a browser with no rendering can be exact about: a caret in a text
/// node, and text spliced at it.
/// <para>
/// https://html.spec.whatwg.org/multipage/interaction.html#contenteditable
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Every edit here stays inside one text node, and that is the whole boundary.</b> Insertion, deletion and
/// caret movement splice a <c>Text</c> node's data and move an offset in it; nothing splits a node, merges
/// two, inserts an element or moves a boundary across one. So <kbd>Enter</kbd>, which HTML answers with a new
/// paragraph or a <c>&lt;br&gt;</c>, does nothing rather than something structural and wrong — the alternative
/// is a half-built editing engine whose failures are indistinguishable from a page's own bugs. The one
/// structural act is creating the first text node of an empty host, because otherwise there is nowhere for the
/// first character to go.
/// </para>
/// <para>
/// <b>The caret is the document's own selection</b> — <c>Selection</c> from <c>window.getSelection()</c> —
/// rather than a second one this file keeps, so a page that reads <c>focusNode</c> and <c>focusOffset</c> is
/// told where typing is going. A host focused with nothing selected in it starts with the caret at the end of
/// its last text node, which is where a click below its text would put one.
/// </para>
/// <para>
/// The events are <see cref="TextEditing"/>'s, so an editing host fires exactly the <c>beforeinput</c> /
/// <c>input</c> pair a form control does, with the same <c>inputType</c> values.
/// </para>
/// </remarks>
internal static class ContentEditing
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#editing-host — the nearest ancestor of
    /// <paramref name="element"/> whose <c>contenteditable</c> state is true, or <see langword="null"/> when
    /// it is in none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the <i>host</i> rather than the element, which is what makes a click inside
    /// <c>&lt;a contenteditable&gt;&lt;span&gt;…&lt;/span&gt;&lt;/a&gt;</c> focus the anchor and a click on
    /// <c>&lt;a&gt;&lt;span contenteditable&gt;…&lt;/span&gt;&lt;/a&gt;</c> focus the span.
    /// </para>
    /// </remarks>
    internal static Element? HostOf(Element? element) => HostOf(element, null);

    internal static Element? HostOf(Element? element, DomReadWork? work)
    {
        // A form control is neither an editing host nor inside one for this purpose: its value is text of its
        // own, and a `<input readonly>` in an editing host must not have its keys spliced into the host's.
        if (element is { NamespaceUri: Namespaces.Html, LocalName: "input" or "textarea" or "select" or "button" })
        {
            return null;
        }

        for (var candidate = element; candidate is not null; candidate = (candidate.ParentNode as Element))
        {
            work?.Step();
            if (!BrowserHtmlSemantics.IsEditableEligible(candidate)) return null;
            // Eligible SVG/MathML roots can inherit editing, but only HTML elements are editing hosts.
            if (candidate.NamespaceUri != Namespaces.Html) continue;
            if (candidate.ParentNode is Document document
                && DomDocumentState.IsDesignModeEnabled(document)) return candidate;
            if ((work is null ? candidate.GetAttributeNS(null, "contenteditable") : work.Attribute(candidate, "contenteditable")) is not { } raw)
            {
                continue;
            }

            // "plaintext-only" is an editing host whose content is text, which is the only kind this edits
            // anyway, so the two true keywords and it are the same answer here.
            var state = BrowserHtmlSemantics.ContentEditableState(raw, work);
            if (state is BrowserContentEditableState.True or BrowserContentEditableState.PlaintextOnly)
            {
                return candidate;
            }

            if (state == BrowserContentEditableState.False)
            {
                return null;
            }

            // An invalid value is the missing-value default, which is "inherit": keep walking.
        }

        return null;
    }

    /// <summary>Runs the command a key asks for on an editing host.</summary>
    /// <returns><see langword="true"/> when the key was consumed by the editor.</returns>
    internal static bool HandleKeyDown(DomRealm dom, Element host, in KeyOptions options, bool allowInsertion)
    {
        using var mutation = dom.MutateLayout();
        var extend = (options.Modifiers & EventModifiers.Shift) != EventModifiers.None;
        var shortcut = (options.Modifiers & (EventModifiers.Control | EventModifiers.Meta)) != EventModifiers.None;

        // Before the caret, because finding one materializes the first text node of an empty host — and a key
        // this file does not handle must leave the document exactly as it found it.
        if (!Handles(options, shortcut, allowInsertion))
        {
            return false;
        }

        if (!shortcut && allowInsertion && options.ProducedText is { Length: > 0 } produced && options.Key.Length == 1)
        {
            return Insert(dom, host, produced, "insertText");
        }

        if (Caret(dom, host, create: options.Key is not ("Backspace" or "Delete")) is not { } caret)
        {
            return options.Key is "Backspace" or "Delete";
        }

        if (shortcut)
        {
            Place(dom, caret.Text, 0, caret.Text.Data.Length);
            return true;
        }

        switch (options.Key)
        {
            case "Backspace":
                return Delete(dom, host, caret, forward: false);

            case "Delete":
                return Delete(dom, host, caret, forward: true);

            case "ArrowLeft":
                return Move(dom, caret, caret.Start == caret.End ? caret.Start - 1 : caret.Start, extend);

            case "ArrowRight":
                return Move(dom, caret, caret.Start == caret.End ? caret.End + 1 : caret.End, extend);

            case "Home" or "ArrowUp":
                return Move(dom, caret, 0, extend);

            case "End" or "ArrowDown":
                return Move(dom, caret, caret.Text.Data.Length, extend);
        }

        if (allowInsertion && options.ProducedText is { Length: > 0 } text && options.Key.Length == 1)
        {
            return Insert(dom, host, text, "insertText");
        }

        return false;
    }

    /// <summary>
    /// Whether the key is one of the commands below — the question that has to be answered before a caret is
    /// asked for.
    /// </summary>
    /// <remarks>
    /// <kbd>Enter</kbd> is deliberately not one: HTML answers it with a paragraph or a <c>&lt;br&gt;</c>, and
    /// that is structural.
    /// </remarks>
    private static bool Handles(in KeyOptions options, bool shortcut, bool allowInsertion)
    {
        if (shortcut)
        {
            return options.Key is "a" or "A";
        }

        return options.Key is "Backspace" or "Delete" or "ArrowLeft" or "ArrowRight"
                or "Home" or "End" or "ArrowUp" or "ArrowDown"
            || (allowInsertion && options.Key.Length == 1 && options.ProducedText.Length > 0);
    }

    /// <summary>
    /// Replaces what is selected inside the host with <paramref name="text"/>, which is what
    /// <c>Input.insertText</c> and a printable key both do.
    /// </summary>
    internal static bool Insert(DomRealm dom, Element host, string text, string inputType)
    {
        using var mutation = dom.MutateLayout();
        if (!TextEditing.FireBeforeInput(dom, host, inputType, JsString.Create(text)))
        {
            return true;
        }

        if (Caret(dom, host) is not { } caret)
        {
            return false;
        }

        var data = caret.Text.Data ?? "";
        caret.Text.Data = string.Concat(data.AsSpan(0, caret.Start), text, data.AsSpan(caret.End));
        Place(dom, caret.Text, caret.Start + text.Length, caret.Start + text.Length);

        TextEditing.FireInput(dom, host, inputType, JsString.Create(text));
        return true;
    }

    private static bool Delete(DomRealm dom, Element host, in EditingCaret caret, bool forward)
    {
        using var mutation = dom.MutateLayout();
        var data = caret.Text.Data ?? "";
        var start = caret.Start;
        var end = caret.End;

        if (start == end)
        {
            if (forward)
            {
                if (end >= data.Length)
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

        if (!TextEditing.FireBeforeInput(dom, host, inputType, JsValue.Null))
        {
            return true;
        }

        caret.Text.Data = string.Concat(data.AsSpan(0, start), data.AsSpan(end));
        Place(dom, caret.Text, start, start);

        TextEditing.FireInput(dom, host, inputType, JsValue.Null);
        return true;
    }

    private static bool Move(DomRealm dom, in EditingCaret caret, int position, bool extend)
    {
        var length = (caret.Text.Data ?? "").Length;
        var clamped = Math.Clamp(position, 0, length);

        if (!extend)
        {
            Place(dom, caret.Text, clamped, clamped);
            return true;
        }

        var anchor = caret.Start == caret.End ? caret.Start : clamped > caret.Start ? caret.Start : caret.End;
        Place(dom, caret.Text, Math.Min(anchor, clamped), Math.Max(anchor, clamped));
        return true;
    }

    /// <summary>
    /// Where the caret is, as an offset pair in one text node of <paramref name="host"/>, or
    /// <see langword="null"/> when the page has no selection to keep one in.
    /// </summary>
    private static EditingCaret? Caret(DomRealm dom, Element host, bool create = true)
    {
        if (PageRuntime.Find(dom.Engine) is not { } runtime)
        {
            return null;
        }

        var selection = runtime.Views.Selection;

        if (selection.Range is { } range && range.Start.Container.Node is Text head
            && ReferenceEquals(range.End.Container.Node, head)
            && IsInside(head, host))
        {
            var length = (uint) head.Data.Length;
            return new EditingCaret(head, (int) Math.Min(range.Start.Offset, length), (int) Math.Min(range.End.Offset, length));
        }

        if (LastTextOf(dom, host, create) is not { } text)
        {
            return null;
        }

        var end = (text.Data ?? "").Length;
        return new EditingCaret(text, end, end);
    }

    /// <summary>Puts the document's selection at one offset pair inside <paramref name="text"/>.</summary>
    private static void Place(DomRealm dom, Text text, int start, int end)
    {
        if (PageRuntime.Find(dom.Engine) is not { } runtime || text.OwnerDocument is not { } document)
        {
            return;
        }

        var range = new DomRange(document);
        range.SetStart(new DomNodeIdentity(text), (uint) start);
        range.SetEnd(new DomNodeIdentity(text), (uint) end);
        runtime.Views.Selection.Range = range;
    }

    /// <summary>
    /// The last text node of the host, or one created for it. An empty editing host has nowhere to put a
    /// character, and creating that node is the one structural thing this file does.
    /// </summary>
    private static Text? LastTextOf(DomRealm dom, Element host, bool create)
    {
        Text? last = null;
        var work = 0;
        void Check()
        {
            if ((++work & 255) == 0)
            {
                dom.CancellationToken.ThrowIfCancellationRequested();
                dom.NativeReadCheckpoint(256);
            }
        }
        dom.CancellationToken.ThrowIfCancellationRequested();

        // Read actual ordinary links; template contents and separate shadow trees are not descendants.
        var current = host.FirstChild;
        while (current is not null)
        {
            Check();
            if (current is Text text) last = text;
            if (current.FirstChild is { } child)
            {
                current = child;
                continue;
            }
            while (current.NextSibling is null && !ReferenceEquals(current.ParentNode, host))
            {
                Check();
                current = current.ParentNode!;
            }
            current = current.NextSibling;
        }

        dom.CancellationToken.ThrowIfCancellationRequested();
        if (last is not null || !create)
        {
            return last;
        }

        if (host.OwnerDocument is not { } document)
        {
            return null;
        }

        var created = document.CreateTextNode("");
        host.AppendChild(created);
        return created;
    }

    private static bool IsInside(Node node, Element host)
    {
        for (Node? candidate = node; candidate is not null; candidate = candidate.ParentNode)
        {
            if (ReferenceEquals(candidate, host))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>One caret, as a text node and the two offsets in it.</summary>
    /// <param name="Text">The text node the caret is in.</param>
    /// <param name="Start">The lower offset.</param>
    /// <param name="End">The upper offset, equal to <paramref name="Start"/> for a collapsed caret.</param>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct EditingCaret(Text Text, int Start, int End);
}
