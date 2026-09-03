using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// The document's focus: which element has it, the four events moving it fires, and which elements can take it
/// at all.
/// <para>
/// https://html.spec.whatwg.org/multipage/interaction.html#focus
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Focus without layout is exact, and this is why.</b> The parts of HTML's focus model that need a rendering
/// are the ones that decide <i>where</i> a click landed and whether an element is being rendered at all; the
/// part that decides <i>what happens</i> when focus moves is pure tree and pure event, and that is all of this
/// file. The one place the gap shows is focusability: an element hidden by a stylesheet is focusable here and is
/// not in a browser, because deciding otherwise would need the cascade and a box.
/// </para>
/// <para>
/// <b>AngleSharp's own focus is not used and cannot be.</b> <c>IHtmlElement.DoFocus()</c> never assigns
/// <c>IDocument.ActiveElement</c> — measured against the pinned 1.7.2 — so its focus is unobservable, and
/// <c>IHtmlElement.TabIndex</c> answers 0 for every element including a bare <c>&lt;div&gt;</c>, where HTML says
/// −1 for anything without the content attribute. Focusability is therefore computed from the element's own
/// kind and its <c>tabindex</c> content attribute, and the focused element is held on
/// <see cref="BrowserEventRealm"/>.
/// </para>
/// </remarks>
internal static class FocusController
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#dom-document-activeelement — the focused
    /// element, falling back to the body element, and to <see langword="null"/> for a document with no body.
    /// </summary>
    internal static IElement? ActiveElement(BrowserEventRealm realm, IDocument document)
    {
        var focused = realm.FocusedElement;

        // A focused element removed from the tree stops being the active element, which is what HTML's
        // "if the element is no longer being rendered" clause amounts to without a rendering.
        if (focused is not null && ReferenceEquals(focused.Owner, document) && IsConnectedTo(focused, document))
        {
            return focused;
        }

        realm.FocusedElement = null;
        return document.Body;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#dom-focus — the focusing steps, plus the focus
    /// update steps that fire the four events.
    /// </summary>
    /// <remarks>
    /// The event order is HTML's focus update steps: the old chain first, so <c>blur</c> then <c>focusout</c>
    /// at the element losing focus, then the new chain, so <c>focus</c> then <c>focusin</c> at the element
    /// gaining it. <c>blur</c> and <c>focus</c> do not bubble; <c>focusout</c> and <c>focusin</c> do. Each
    /// carries the other element as its <c>relatedTarget</c>.
    /// </remarks>
    internal static void Focus(DomRealm dom, IElement element)
    {
        if (!IsFocusable(element))
        {
            return;
        }

        var realm = BrowserEventRealm.Of(dom.Engine);
        var previous = realm.FocusedElement;

        if (ReferenceEquals(previous, element))
        {
            return;
        }

        realm.FocusedElement = element;
        RunFocusUpdateSteps(dom, previous, element);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#dom-blur — the unfocusing steps. HTML notes
    /// that "the <c>blur()</c> method... historically... move[s] focus to the viewport", which is what leaving
    /// nothing focused means here.
    /// </summary>
    internal static void Blur(DomRealm dom, IElement element)
    {
        var realm = BrowserEventRealm.Of(dom.Engine);

        if (!ReferenceEquals(realm.FocusedElement, element))
        {
            return;
        }

        realm.FocusedElement = null;
        RunFocusUpdateSteps(dom, element, next: null);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#focus-update-steps, reduced to the two chains a
    /// document with no nested browsing context has: one element each.
    /// </summary>
    private static void RunFocusUpdateSteps(DomRealm dom, IElement? previous, IElement? next)
    {
        if (previous is not null)
        {
            // HTML step 3's first clause: a control whose value the user changed since it was focused fires
            // `change` on the way out. TextEditing records the value at focus time; nothing recorded means
            // nothing was edited.
            TextEditing.FireChangeIfEdited(dom, previous);

            var losing = dom.WrapNode(previous);
            Fire(dom, losing, "blur", bubbles: false, related: next);
            Fire(dom, losing, "focusout", bubbles: true, related: next);
        }

        if (next is not null)
        {
            TextEditing.RememberValueAtFocus(next);

            var gaining = dom.WrapNode(next);
            Fire(dom, gaining, "focus", bubbles: false, related: previous);
            Fire(dom, gaining, "focusin", bubbles: true, related: previous);
        }
    }

    private static void Fire(DomRealm dom, DomNodeObject target, string type, bool bubbles, IElement? related)
    {
        var realm = BrowserEventRealm.Of(dom.Engine);

        var ev = realm.CreateTrusted(
            BrowserEventInterfaces.FocusEvent,
            new JsFocusEvent(
                dom.Engine,
                JsString.Create(type),
                new EventInit(bubbles, Cancelable: false, Composed: true),
                realm.TimeStamp,
                dom.Engine._mainRealm.GlobalObject,
                detail: 0,
                which: null,
                related is null ? null : dom.WrapNode(related)));

        target.DispatchEvent(ev);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#focusable-area, decided without a rendering: an
    /// element is focusable when its kind makes it so or when it carries a valid <c>tabindex</c>, and when it
    /// is neither disabled nor hidden by the <c>hidden</c> content attribute.
    /// </summary>
    internal static bool IsFocusable(IElement element)
    {
        if (IsDisabled(element) || element.HasAttribute("hidden") || element.HasAttribute("inert"))
        {
            return false;
        }

        if (TabIndexAttribute(element) is not null)
        {
            return true;
        }

        return IsInherentlyFocusable(element);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#sequential-focus-navigation — whether the
    /// element takes part in <kbd>Tab</kbd> traversal, which a negative <c>tabindex</c> opts out of while
    /// leaving the element focusable by other means.
    /// </summary>
    internal static bool IsTabbable(IElement element)
    {
        if (!IsFocusable(element))
        {
            return false;
        }

        var tabIndex = TabIndexAttribute(element);
        return tabIndex is null ? IsInherentlyFocusable(element) : tabIndex >= 0;
    }

    /// <summary>
    /// The element after <paramref name="from"/> in sequential focus navigation order, or the first one when
    /// <paramref name="from"/> is <see langword="null"/>; <paramref name="backwards"/> walks the other way.
    /// </summary>
    /// <remarks>
    /// The order is HTML's, reduced to what a document without a rendering can decide: every element with a
    /// positive <c>tabindex</c> first, ordered by that value and then by tree order, and then everything else
    /// in tree order. It wraps, because there is nothing above the document to hand focus to.
    /// </remarks>
    internal static IElement? NextInTabOrder(IDocument document, IElement? from, bool backwards)
    {
        var order = TabOrder(document);
        if (order.Count == 0)
        {
            return null;
        }

        var index = from is null ? -1 : order.FindIndex(e => ReferenceEquals(e, from));

        if (index < 0)
        {
            return backwards ? order[^1] : order[0];
        }

        var next = backwards ? index - 1 : index + 1;
        return order[(next + order.Count) % order.Count];
    }

    private static List<IElement> TabOrder(IDocument document)
    {
        var positive = new List<(int TabIndex, int Position, IElement Element)>();
        var zero = new List<IElement>();
        var position = 0;

        foreach (var element in document.All)
        {
            if (!IsTabbable(element))
            {
                continue;
            }

            var tabIndex = TabIndexAttribute(element);
            if (tabIndex is > 0)
            {
                positive.Add((tabIndex.Value, position, element));
            }
            else
            {
                zero.Add(element);
            }

            position++;
        }

        if (positive.Count == 0)
        {
            return zero;
        }

        positive.Sort(static (a, b) => a.TabIndex != b.TabIndex ? a.TabIndex.CompareTo(b.TabIndex) : a.Position.CompareTo(b.Position));

        var order = new List<IElement>(positive.Count + zero.Count);
        foreach (var entry in positive)
        {
            order.Add(entry.Element);
        }

        order.AddRange(zero);
        return order;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#the-autofocus-attribute — focus the first
    /// element asking for it, once, after the document has parsed.
    /// </summary>
    internal static void FlushAutofocus(DomRealm dom, IDocument document)
    {
        if (BrowserEventRealm.Of(dom.Engine).FocusedElement is not null)
        {
            return;
        }

        foreach (var element in document.All)
        {
            if (element.HasAttribute("autofocus") && IsFocusable(element))
            {
                Focus(dom, element);
                return;
            }
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#attr-tabindex, parsed as a valid integer — the
    /// attribute's presence is what matters, so an unparseable value is the same as an absent one.
    /// </summary>
    private static int? TabIndexAttribute(IElement element)
    {
        var raw = element.GetAttribute("tabindex");
        return raw is not null && int.TryParse(raw.Trim(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static bool IsInherentlyFocusable(IElement element) => element switch
    {
        IHtmlAnchorElement or IHtmlAreaElement => element.HasAttribute("href"),
        IHtmlButtonElement or IHtmlSelectElement or IHtmlTextAreaElement => true,
        IHtmlInputElement input => !string.Equals(input.Type, "hidden", StringComparison.Ordinal),
        IHtmlInlineFrameElement => true,
        // An editing host is a focusable area; an element merely *inside* one is not, which is what makes a
        // click on a <span> inside <a contenteditable> focus the anchor. ContentEditing.HostOf says which is
        // which, and says why AngleSharp's own IsContentEditable cannot answer it.
        IHtmlElement html => html.LocalName is "summary" || ReferenceEquals(ContentEditing.HostOf(html), html),
        _ => false,
    };

    private static bool IsDisabled(IElement element) => element switch
    {
        IHtmlButtonElement button => button.IsDisabled,
        IHtmlInputElement input => input.IsDisabled,
        IHtmlSelectElement select => select.IsDisabled,
        IHtmlTextAreaElement textArea => textArea.IsDisabled,
        IHtmlOptionElement option => option.IsDisabled,
        _ => false,
    };

    private static bool IsConnectedTo(IElement element, IDocument document)
    {
        for (INode? node = element; node is not null; node = node.Parent)
        {
            if (ReferenceEquals(node, document))
            {
                return true;
            }
        }

        return false;
    }
}
