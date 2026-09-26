using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
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
/// <c>Document.ActiveElement</c> — measured against the pinned 1.7.2 — so its focus is unobservable, and
/// <c>IHtmlElement.TabIndex</c> answers 0 for every element including a bare <c>&lt;div&gt;</c>, where HTML says
/// −1 for anything without the content attribute. Focusability is therefore computed from the element's own
/// kind and its <c>tabindex</c> content attribute, and the focused element is held on
/// <see cref="BrowserEventRealm"/>.
/// </para>
/// </remarks>
internal static class FocusController
{
    private static readonly ConditionalWeakTable<BrowserEventRealm, FocusUpdateState> Updates = new();

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#dom-document-activeelement — the focused
    /// element, falling back to the body element, and to <see langword="null"/> for a document with no body.
    /// </summary>
    internal static Element? ActiveElement(BrowserEventRealm realm, Document document)
    {
        if (PageRuntime.Find(realm.Engine, document) is null)
        {
            return DomDocumentElements.Body(document);
        }

        if (realm.FocusedElement is not { } focused)
        {
            return DomDocumentElements.Body(document);
        }

        // A focused element removed from the tree stops being the active element, which is what HTML's
        // "if the element is no longer being rendered" clause amounts to without a rendering. Its own node
        // document is what it has to be connected to, so that focus held by a child navigable's document
        // survives a read of this one's active element rather than being cleared by it.
        if (focused.OwnerDocument is not { } owner || !IsConnectedTo(focused, owner)
            || !ReferenceEquals(BrowserEventRealm.FocusedElementOf(owner), focused))
        {
            realm.FocusedElement = null;
            return DomDocumentElements.Body(document);
        }

        return ReferenceEquals(owner, document) ? RetargetToDocument(focused, document) : DomDocumentElements.Body(document);
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
    internal static void Focus(DomRealm dom, Element element)
    {
        if (!IsInAPageDocument(dom, element) || element.OwnerDocument is not { } document
            || !IsConnectedTo(element, document) || !IsFocusable(dom, element))
        {
            return;
        }

        var realm = BrowserEventRealm.Of(dom.Engine);
        var previous = realm.FocusedElement;
        if (previous is not null && (previous.OwnerDocument is not { } owner
            || !IsConnectedTo(previous, owner)
            || !ReferenceEquals(BrowserEventRealm.FocusedElementOf(owner), previous)))
        {
            realm.FocusedElement = null;
            previous = null;
        }

        if (ReferenceEquals(previous, element))
        {
            return;
        }

        realm.FocusedElement = element;
        RunFocusUpdateSteps(dom, previous, element, ++Updates.GetOrCreateValue(realm).Revision);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#dom-blur — the unfocusing steps. HTML notes
    /// that "the <c>blur()</c> method... historically... move[s] focus to the viewport", which is what leaving
    /// nothing focused means here.
    /// </summary>
    internal static void Blur(DomRealm dom, Element element)
    {
        if (!IsInAPageDocument(dom, element))
        {
            return;
        }

        var realm = BrowserEventRealm.Of(dom.Engine);

        if (!ReferenceEquals(realm.FocusedElement, element))
        {
            return;
        }

        realm.FocusedElement = null;
        RunFocusUpdateSteps(dom, element, next: null, ++Updates.GetOrCreateValue(realm).Revision);
    }

    /// <summary>
    /// Whether <paramref name="document"/> is the displayed document and its viewport has focus.
    /// </summary>
    internal static bool HasFocus(BrowserEventRealm realm, Document document)
        => PageRuntime.Find(realm.Engine, document) is not null && realm.DocumentHasFocus;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#focus-update-steps, reduced to the two chains a
    /// document with no nested browsing context has: one element each.
    /// </summary>
    private static void RunFocusUpdateSteps(DomRealm dom, Element? previous, Element? next, long revision)
    {
        var state = Updates.GetOrCreateValue(BrowserEventRealm.Of(dom.Engine));
        if (previous is not null)
        {
            // HTML step 3's first clause: a control whose value the user changed since it was focused fires
            // `change` on the way out. TextEditing records the value at focus time; nothing recorded means
            // nothing was edited.
            TextEditing.FireChangeIfEdited(dom, previous);
            if (state.Revision != revision) return;

            var losing = dom.WrapNode(previous);
            Fire(dom, losing, "blur", bubbles: false, related: next);
            if (state.Revision != revision) return;
            Fire(dom, losing, "focusout", bubbles: true, related: next);
            if (state.Revision != revision) return;
        }

        if (next is not null)
        {
            TextEditing.RememberValueAtFocus(next);

            var gaining = dom.WrapNode(next);
            Fire(dom, gaining, "focus", bubbles: false, related: previous);
            if (state.Revision != revision) return;
            Fire(dom, gaining, "focusin", bubbles: true, related: previous);
        }
    }

    private static void Fire(DomRealm dom, DomNodeObject target, string type, bool bubbles, Element? related)
    {
        var realm = BrowserEventRealm.Of(dom.Engine, target.DomRealm.OwningRealm);

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
    internal static bool IsFocusable(DomRealm dom, Element element)
    {
        if (EventDom.Disabled(dom, element) || element.HasAttribute("hidden") || element.HasAttribute("inert"))
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
    internal static bool IsTabbable(DomRealm dom, Element element)
    {
        if (!IsFocusable(dom, element))
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
    internal static Element? NextInTabOrder(DomRealm dom, Document document, Element? from, bool backwards)
    {
        var order = TabOrder(dom, document);
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

    private static List<Element> TabOrder(DomRealm dom, Document document)
    {
        var positive = new List<(int TabIndex, int Position, Element Element)>();
        var zero = new List<Element>();
        var position = 0;

        foreach (var element in NodeTraversal.DescendantElements(document, () => dom.NativeReadCheckpoint(256), dom.CancellationToken))
        {
            if (!IsTabbable(dom, element))
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

        var order = new List<Element>(positive.Count + zero.Count);
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
    internal static void FlushAutofocus(DomRealm dom, Document document)
    {
        if (BrowserEventRealm.Of(dom.Engine).FocusedElement is not null)
        {
            return;
        }

        foreach (var element in NodeTraversal.DescendantElements(document, () => dom.NativeReadCheckpoint(256), dom.CancellationToken))
        {
            if (element.HasAttribute("autofocus") && IsFocusable(dom, element))
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
    private static int? TabIndexAttribute(Element element)
    {
        var raw = element.GetAttribute("tabindex");
        return raw is not null && int.TryParse(raw.Trim(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static bool IsInherentlyFocusable(Element element)
    {
        if (element.NamespaceUri != Namespaces.Html) return false;
        return element.LocalName switch
        {
            "a" or "area" => element.HasAttribute("href"),
            "button" or "select" or "textarea" or "iframe" => true,
            "input" => HtmlInputTypes.Get(element) != HtmlInputType.Hidden,
            _ => element.LocalName is "summary" || ReferenceEquals(ContentEditing.HostOf(element), element),
        };
    }

    /// <summary>
    /// Whether <paramref name="element"/> belongs to a document this page is showing — its own, or one of a
    /// child navigable's.
    /// </summary>
    /// <remarks>
    /// The browsing-context tree rather than the displayed document alone, because HTML's focusing steps do
    /// not stop at a frame boundary: focusing an element inside an <c>iframe</c> takes focus away from the
    /// element that had it, and a page whose focus could not leave its own document would keep answering
    /// <c>:focus</c> for an element a browser has already blurred. A document with no browsing context —
    /// <c>DOMParser</c>, <c>createHTMLDocument</c>, <c>new Document()</c> — is not in the tree and still
    /// neither takes focus nor moves it.
    /// </remarks>
    private static bool IsInAPageDocument(DomRealm dom, Element element)
        => PageRuntime.FindBrowsingContext(dom.Engine, element.OwnerDocument) is not null;

    // DOM retargeting keeps the real focused node in the interaction store and exposes its outer host.
    private static Element? RetargetToDocument(Element focused, Document document)
    {
        var target = focused;
        for (Node? current = focused; current is not null; current = current.ParentNode)
        {
            if (current is ShadowRoot { Host: { } host })
            {
                target = host;
                current = host;
            }
            if (ReferenceEquals(current, document)) return target;
        }
        return null;
    }

    private sealed class FocusUpdateState
    {
        internal long Revision;
    }

    private static bool IsConnectedTo(Element element, Document document)
    {
        for (Node? node = element; node is not null; node = node.ParentNode ?? (node as ShadowRoot)?.Host)
        {
            if (ReferenceEquals(node, document))
            {
                return true;
            }
        }

        return false;
    }
}
