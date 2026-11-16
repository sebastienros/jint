using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// Owns focus transitions and their script-visible events for a Browser realm.
/// </summary>
/// <remarks>
/// Focus state and event dispatch share the owning engine thread. Native elements provide the targets;
/// Browser policy decides focusability and updates the active element before listeners observe it.
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
        var work = FocusReadWork(realm);
        var focused = realm.FocusedElement;
        var result = ActiveElement(realm, document, work, out var clearFocus);
        work.Check();
        if (clearFocus && ReferenceEquals(realm.FocusedElement, focused)) realm.FocusedElement = null;
        return result;
    }

    private static DomReadWork FocusReadWork(BrowserEventRealm realm)
    {
        var dom = DomRealm.Of(realm.Engine);
        var work = new DomReadWork(dom.NativeReadCheckpoint, dom.CancellationToken);
        work.Check();
        return work;
    }

    private static Element? ActiveElement(BrowserEventRealm realm, Document document, DomReadWork work, out bool clearFocus)
    {
        clearFocus = false;
        if (PageRuntime.Find(realm.Engine, document) is null)
        {
            return Body(document, work);
        }

        if (realm.FocusedElement is not { } focused)
        {
            return Body(document, work);
        }

        // A focused element removed from the tree stops being the active element, which is what HTML's
        // "if the element is no longer being rendered" clause amounts to without a rendering. Its own node
        // document is what it has to be connected to, so that focus held by a child navigable's document
        // survives a read of this one's active element rather than being cleared by it.
        if (focused.OwnerDocument is not { } owner || !IsConnectedTo(focused, owner, work)
            || !ReferenceEquals(BrowserEventRealm.FocusedElementOf(owner), focused))
        {
            clearFocus = true;
            return Body(document, work);
        }

        return ReferenceEquals(owner, document) ? RetargetToDocument(focused, document, work) : Body(document, work);
    }

    private static Element? Body(Document document, DomReadWork work)
    {
        Element? html = null;
        for (var child = document.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is not Element element) continue;
            if (element is { NamespaceUri: Namespaces.Html, LocalName: "html" }) html = element;
            break;
        }
        if (html is null) return null;
        for (var child = html.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is Element { NamespaceUri: Namespaces.Html, LocalName: "body" or "frameset" } body) return body;
        }
        return null;
    }

    /// <summary>HTML DocumentOrShadowRoot.activeElement, retargeted within this shadow tree.</summary>
    internal static Element? ActiveElement(BrowserEventRealm realm, ShadowRoot root)
    {
        var work = FocusReadWork(realm);
        var focused = realm.FocusedElement;
        var result = ShadowActiveElement(realm, root, work, out var clearFocus);
        work.Check();
        if (clearFocus && ReferenceEquals(realm.FocusedElement, focused)) realm.FocusedElement = null;
        return result;
    }

    private static Element? ShadowActiveElement(BrowserEventRealm realm, ShadowRoot root, DomReadWork work, out bool clearFocus)
    {
        clearFocus = false;
        var document = root.OwnerDocument!;
        if (PageRuntime.Find(realm.Engine, document) is null) return null;
        // Reuse document validation, but commit stale-focus clearing only after the whole read succeeds.
        ActiveElement(realm, document, work, out clearFocus);
        if (clearFocus || realm.FocusedElement is not { } focused || !ReferenceEquals(focused.OwnerDocument, document)) return null;
        Element candidate = focused;
        while (true)
        {
            Node tree = candidate;
            while (tree.ParentNode is { } parent) { work.Step(); tree = parent; }
            if (ReferenceEquals(tree, root)) return candidate;
            if (tree is not ShadowRoot shadow) return null;
            work.Step();
            candidate = shadow.Host;
        }
    }

    // User input reaches the actual focused control; document.activeElement is a retargeted exposure.
    internal static Element? InteractionTarget(BrowserEventRealm realm, Document document)
    {
        var exposed = ActiveElement(realm, document);
        return realm.FocusedElement is { } focused
            && PageRuntime.FindBrowsingContext(realm.Engine, focused.OwnerDocument) is not null
                ? focused : exposed;
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
    /// <returns>
    /// <see langword="true"/> when the focusing steps ran — <paramref name="element"/> passed their guard and
    /// was given focus or already had it — whatever a listener then did with focus; <see langword="false"/>
    /// when the element is not a focusable area of a page document and nothing happened.
    /// </returns>
    internal static bool Focus(DomRealm dom, Element element, FocusOrigin origin = FocusOrigin.Script, bool? focusVisible = null)
    {
        if (!CanFocus(dom, element))
        {
            return false;
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
            // HTML's focus() indicates focus after the focusing steps, which return early here; a user's
            // click or key on the focused element leaves its indication as it was, as Chromium does.
            if (focusVisible is { } requested) Indicate(realm, requested, requestedByScript: true);
            return true;
        }

        IndicateFocus(realm, previous is not null, origin, focusVisible);
        realm.FocusedElement = element;
        RunFocusUpdateSteps(dom, previous, element, ++Updates.GetOrCreateValue(realm).Revision);
        return true;
    }

    /// <summary>
    /// https://drafts.csswg.org/selectors-4/#the-focus-visible-pseudo — the heuristics Selectors 4 §9.4
    /// recommends, as Chromium applies them when focus moves: a key indicates focus and a pointer does not;
    /// a script indicates it when the element it takes focus from did, or when no pointer has moved focus
    /// since the last key; and <c>focus({ focusVisible })</c> decides outright.
    /// </summary>
    private static void IndicateFocus(BrowserEventRealm realm, bool hadFocus, FocusOrigin origin, bool? focusVisible)
    {
        switch (origin)
        {
            case FocusOrigin.Keyboard:
                realm.LastFocusByPointer = false;
                Indicate(realm, true, requestedByScript: false);
                return;
            case FocusOrigin.Pointer:
                realm.LastFocusByPointer = true;
                Indicate(realm, false, requestedByScript: false);
                return;
            default:
                if (focusVisible is { } requested) Indicate(realm, requested, requestedByScript: true);
                else Indicate(realm, hadFocus && realm.FocusIndicated || !realm.LastFocusByPointer, requestedByScript: false);
                return;
        }
    }

    private static void Indicate(BrowserEventRealm realm, bool indicated, bool requestedByScript)
    {
        realm.FocusIndicated = indicated;
        realm.FocusIndicationRequested = requestedByScript;
    }

    /// <summary>
    /// A key press, which Selectors 4 §9.4 says makes the focused element indicate its focus — unless a
    /// script asked for it not to with <c>focus({ focusVisible: false })</c> — and which a later script
    /// <c>focus()</c> inherits.
    /// </summary>
    internal static void NoteKeyPress(BrowserEventRealm realm)
    {
        realm.LastFocusByPointer = false;
        if (!realm.FocusIndicationRequested) realm.FocusIndicated = true;
    }

    /// <summary>
    /// Whether <paramref name="element"/>, the focused element of its document, matches <c>:focus-visible</c>:
    /// its focus is indicated, or it is a control a user types into — a text field, a <c>&lt;textarea&gt;</c>
    /// or an editing host — which always indicates focus, readonly or not, as in Chromium.
    /// </summary>
    internal static bool IsFocusVisible(DomRealm dom, BrowserEventRealm realm, Element element)
    {
        if (realm.FocusIndicated) return true;
        if (element.NamespaceUri == Namespaces.Html)
        {
            if (element.LocalName == "textarea") return true;
            if (element.LocalName == "input")
                return element.GetHtmlState()?.GetInputValueState(dom.NativeReadCheckpoint, dom.CancellationToken)
                    is { HasEditingBuffer: true };
        }
        return ContentEditing.HostOf(element) is not null;
    }

    /// <summary>
    /// The FocusOptions dictionary's <c>focusVisible</c> member, or <see langword="null"/> when it is absent.
    /// </summary>
    /// <remarks>
    /// https://html.spec.whatwg.org/multipage/interaction.html#focusoptions — WebIDL reads a dictionary's
    /// members in lexicographic order, so <c>focusVisible</c> before <c>preventScroll</c>, which nothing here
    /// could honour or violate and whose getter still runs.
    /// </remarks>
    internal static bool? FocusVisibleOption(DomRealm dom, JsValue[] arguments, string interfaceName)
    {
        var options = arguments.Length > 0 ? arguments[0] : JsValue.Undefined;
        if (options.IsNullOrUndefined()) return null;
        if (options is not ObjectInstance dictionary)
        {
            Throw.TypeError(dom.Engine.Realm,
                "Failed to execute 'focus' on '" + interfaceName + "': parameter 1 is not of type 'FocusOptions'.");
            return null;
        }
        var focusVisible = dictionary.Get("focusVisible");
        _ = dictionary.Get("preventScroll");
        return focusVisible.IsUndefined() ? null : TypeConverter.ToBoolean(focusVisible);
    }

    /// <summary>
    /// <c>HTMLElement.focus(options)</c> and <c>SVGElement.focus(options)</c>: the focusing steps for a
    /// script, with FocusOptions' <c>focusVisible</c>.
    /// </summary>
    internal static void FocusFromScript(DomRealm dom, Element element, JsValue[] arguments, string interfaceName)
        => Focus(dom, element, FocusOrigin.Script, FocusVisibleOption(dom, arguments, interfaceName));

    /// <summary>
    /// Whether <see cref="Focus"/> would move focus to <paramref name="element"/>: it is connected to a
    /// document this page shows, and it is a focusable area.
    /// </summary>
    /// <remarks>
    /// The focusing steps' own guard. A caller that has to answer whether it focused something reads
    /// <see cref="Focus"/>'s result instead of asking this first and paying for the question twice.
    /// </remarks>
    private static bool CanFocus(DomRealm dom, Element element)
        => IsInAPageDocument(dom, element) && element.OwnerDocument is { } document
            && IsConnectedTo(element, document) && IsFocusable(dom, element);

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
            TextEditing.RememberValueAtFocus(dom, next);

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
    /// https://html.spec.whatwg.org/multipage/interaction.html#focusable-area, decided without a layout: an
    /// element is focusable when its kind makes it so or when it carries a valid <c>tabindex</c>, when it is
    /// neither disabled nor inert, and when it is <see cref="IsBeingRendered(DomRealm, Element)">being
    /// rendered</see>.
    /// </summary>
    internal static bool IsFocusable(DomRealm dom, Element element) => IsFocusableCore(dom, element, null, null, null);

    /// <summary>The same focus classification with bounded native reads for a caller's traversal.</summary>
    /// <remarks>
    /// <paramref name="scope"/> is an ancestor whose own rendering the caller is about to change, so the
    /// rendering walk stops below it: a <c>&lt;dialog&gt;</c> looks for its focus delegate before it gains
    /// <c>open</c>, while <c>dialog:not([open])</c> still says <c>display: none</c>. Whatever is above the
    /// scope is checked again when <see cref="Focus"/> runs.
    /// </remarks>
    internal static bool IsFocusable(DomRealm dom, Element element, DomReadWork work, Element? scope = null)
    {
        work.Check();
        var focusable = IsFocusableCore(dom, element, work, new RenderingQuery(dom, work, scope), inert: null);
        work.Check();
        return focusable;
    }

    private static bool IsFocusableCore(DomRealm dom, Element element, DomReadWork? work, RenderingQuery? rendering, InertQuery? inert)
    {
        if (EventDom.Disabled(dom, element) || Attribute(element, "inert", work) is not null) return false;
        if (TabIndexAttribute(element, work) is null && !IsInherentlyFocusable(element, work)) return false;
        if ((inert ?? new InertQuery(work)).IsInert(element)) return false;
        // The cascade is asked last, and only for an element every cheaper test already admitted.
        return (rendering ?? new RenderingQuery(dom, work, scope: null)).IsRendered(element);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#the-inert-attribute — whether
    /// <paramref name="element"/> is inert because it or a flat-tree ancestor carries the attribute, or
    /// because the <c>&lt;iframe&gt;</c> holding its document is inert.
    /// </summary>
    internal static bool IsInInertSubtree(Element element) => new InertQuery(work: null).IsInert(element);

    /// <summary>
    /// The inert half of a focus question, remembered per node the way <see cref="RenderingQuery"/>
    /// remembers its containers, so a <kbd>Tab</kbd> walk reads each ancestor's <c>inert</c> attribute once.
    /// </summary>
    /// <remarks>
    /// The <c>inert</c> attribute makes the element and every flat-tree descendant inert, and an inert node is
    /// not a focusable area. The walk is the flat tree's — a shadow root continues at its host, a shadow
    /// host's child at its assigned slot — and a child navigable's document continues at its container, so
    /// an inert <c>&lt;iframe&gt;</c> makes the document inside it inert too. A shadow host's child no slot
    /// takes has no flat-tree parent; it is not being rendered either, so its answer does not matter here.
    /// </remarks>
    private sealed class InertQuery(DomReadWork? work)
    {
        private readonly Dictionary<Node, bool> _known = new();
        private readonly List<Node> _path = new();

        internal bool IsInert(Node node)
        {
            var inert = false;
            _path.Clear();
            for (Node? current = node; current is not null;)
            {
                if (_known.TryGetValue(current, out inert))
                {
                    break;
                }

                work?.Step();
                _path.Add(current);
                if (current is Element element && Attribute(element, "inert", work) is not null)
                {
                    inert = true;
                    break;
                }

                current = current is Document document
                    ? DomBrowsingContext.Of(document)?.FrameElement
                    : FlatParent(current);
            }

            foreach (var visited in _path)
            {
                _known[visited] = inert;
            }

            _path.Clear();
            return inert;
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/rendering.html#being-rendered, answered from the cascade: an
    /// element whose box CSS removes is not a focusable area, and neither is one <c>visibility</c> hides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// HTML's focusable-area table admits only elements that are "being rendered" or that "delegate their
    /// rendering to their children". So <c>display: none</c> on the element or on any flat-tree ancestor
    /// removes it — a light-DOM child of a shadow host is reached through its assigned slot, and one no slot
    /// takes is not rendered at all — as does a metadata ancestor. <c>display: contents</c> delegates and
    /// keeps the element, except on the replaced elements and form controls CSS Display §2.8 makes compute
    /// it to <c>none</c>. The <c>hidden</c> attribute is the UA sheet's <c>display: none</c>, so an author
    /// rule overriding it is honoured, while <c>hidden=until-found</c> is <c>content-visibility: hidden</c>,
    /// which the cascade does not compute and which hides what it contains. <c>visibility: hidden|collapse</c>
    /// is the element's own computed value, because CSS inherits it and a <c>visibility: visible</c>
    /// descendant escapes it; every engine skips such an element in sequential navigation and refuses it to
    /// <c>focus()</c>. An <c>&lt;area&gt;</c> is the one element whose own <c>display: none</c> does not
    /// count: its focusable area is a shape of the image that uses its map, not a box of its own.
    /// </para>
    /// <para>
    /// It asks the same <see cref="ElementVisibility"/> questions as <c>LayoutMembers.IsCheckVisible</c>,
    /// which answers CSSOM View's different question — whether the element itself generates a box, so its
    /// own <c>display: contents</c> fails there and passes here. Nothing here knows that an element is clipped
    /// or off-screen, and neither matters to focus.
    /// </para>
    /// </remarks>
    internal static bool IsBeingRendered(DomRealm dom, Element element) => new RenderingQuery(dom, work: null, scope: null).IsRendered(element);

    /// <summary>
    /// One cascade read shared by every candidate of a single focus question: the document's traversal is
    /// taken once, and each ancestor's verdict is remembered, so a <kbd>Tab</kbd> walk costs one style read
    /// per element rather than one per element per level of nesting.
    /// </summary>
    private sealed class RenderingQuery(DomRealm dom, DomReadWork? work, Element? scope)
    {
        private readonly ElementVisibility _visibility = new(useComputedStyle: true, work);
        private readonly Dictionary<Node, Container> _containers = new();
        private readonly List<Node> _path = new();
        private CssCascade.Traversal? _traversal;
        private Document? _document;

        /// <summary>Whether descendants of a node can be rendered, and the nearest <c>visibility</c> above them.</summary>
        /// <remarks>
        /// <see cref="Visibility"/> only matters when the cascade cannot answer and inline styles are all
        /// there is: a computed value is already inherited.
        /// </remarks>
        private readonly record struct Container(bool Rendered, string? Visibility);

        internal bool IsRendered(Element element)
        {
            if (element.OwnerDocument is not { } document)
            {
                return false;
            }

            if (!ReferenceEquals(_document, document))
            {
                _document = document;
                _containers.Clear();
                _traversal = _visibility.CascadeAvailable ? CssCascade.Traversal.Current(document) : null;
            }

            Step();
            if (!Admits(element, self: true, out var visibility))
            {
                return false;
            }

            var container = ContainerOf(FlatParent(element));
            if (!container.Rendered)
            {
                return false;
            }

            return !IsHiddenVisibility(visibility ?? container.Visibility);
        }

        private Container ContainerOf(Node? parent)
        {
            var state = new Container(Rendered: false, Visibility: null);
            _path.Clear();
            for (var node = parent; ;)
            {
                if (node is null)
                {
                    // Detached, or a shadow host's child that no slot takes.
                    break;
                }

                if (ReferenceEquals(node, scope))
                {
                    state = new Container(Rendered: true, Visibility: null);
                    break;
                }

                if (node is Document owner)
                {
                    state = new Container(ReferenceEquals(owner, _document), Visibility: null);
                    break;
                }

                if (_containers.TryGetValue(node, out state))
                {
                    break;
                }

                Step();
                _path.Add(node);
                node = FlatParent(node);
            }

            for (var i = _path.Count - 1; i >= 0; i--)
            {
                var node = _path[i];
                if (state.Rendered && node is Element element)
                {
                    state = Admits(element, self: false, out var visibility)
                        ? state with { Visibility = visibility ?? state.Visibility }
                        : new Container(Rendered: false, Visibility: null);
                }

                _containers[node] = state;
            }

            _path.Clear();
            return state;
        }

        /// <summary>The element's own share of the verdict: its box, and its specified <c>visibility</c>.</summary>
        private bool Admits(Element element, bool self, out string? visibility)
        {
            visibility = null;
            if (FlatLayout.IsMetadata(element))
            {
                return false;
            }

            var hidden = Attribute(element, "hidden", work);
            if (hidden is not null && string.Equals(hidden.Trim(), "until-found", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var (display, specified) = _visibility.Style(element, _traversal);
            // The UA sheet's [hidden] rule says display: none; only an inline-only read cannot see it.
            if (hidden is not null && !(_traversal is not null && _visibility.CascadeAvailable))
            {
                return false;
            }

            var isHtml = element.NamespaceUri == Namespaces.Html;
            if (string.Equals(display, "none", StringComparison.OrdinalIgnoreCase)
                && !(self && isHtml && element.LocalName == "area"))
            {
                return false;
            }

            if (string.Equals(display, "contents", StringComparison.OrdinalIgnoreCase) && isHtml
                && element.LocalName is "input" or "textarea" or "select" or "iframe" or "img" or "video" or "audio"
                    or "canvas" or "embed" or "object" or "meter" or "progress" or "br" or "wbr" or "frame" or "frameset")
            {
                return false;
            }

            visibility = string.IsNullOrEmpty(specified) ? null : specified;
            return true;
        }

        private void Step()
        {
            if (work is not null) work.Step();
            else dom.CancellationToken.ThrowIfCancellationRequested();
        }

        private static bool IsHiddenVisibility(string? visibility)
            => string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase)
                || string.Equals(visibility, "collapse", StringComparison.OrdinalIgnoreCase);

    }

    // The flat tree's parent: a shadow root's host, a shadow host's child's assigned slot (null when no
    // slot takes it), and otherwise the tree parent.
    private static Node? FlatParent(Node node)
    {
        if (node is ShadowRoot root)
        {
            return root.Host;
        }

        var parent = node.ParentNode;
        return parent is Element { AttachedShadowRoot: not null } ? SlotAssignment.GetAssignedSlot(node) : parent;
    }

    private static string? Attribute(Element element, string name, DomReadWork? work)
        => work is null ? element.GetAttributeNS(null, name) : work.Attribute(element, name);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#sequential-focus-navigation — whether the
    /// element takes part in <kbd>Tab</kbd> traversal, which a negative <c>tabindex</c> opts out of while
    /// leaving the element focusable by other means.
    /// </summary>
    internal static bool IsTabbable(DomRealm dom, Element element) => IsTabbable(dom, element, rendering: null, inert: null);

    private static bool IsTabbable(DomRealm dom, Element element, RenderingQuery? rendering, InertQuery? inert)
    {
        if (!IsFocusableCore(dom, element, null, rendering, inert))
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
    /// <para>
    /// The order is HTML's, reduced to what a document without a layout can decide: every element with a
    /// positive <c>tabindex</c> first, ordered by that value and then by tree order, and then everything else
    /// in tree order. Only elements that are <see cref="IsBeingRendered(DomRealm, Element)">being rendered</see> take part.
    /// </para>
    /// <para>
    /// Past either end it answers <see langword="null"/>, which is HTML's "the user agent may transfer focus
    /// to its own controls": Chromium takes focus out of the document there, leaving <c>activeElement</c> at
    /// the body, and only the next <kbd>Tab</kbd> wraps round to the first element — which is what starting
    /// from nothing focused already gives.
    /// </para>
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
        return (uint) next < (uint) order.Count ? order[next] : null;
    }

    private static List<Element> TabOrder(DomRealm dom, Document document)
    {
        var positive = new List<(int TabIndex, int Position, Element Element)>();
        var zero = new List<Element>();
        var position = 0;
        var rendering = new RenderingQuery(dom, work: null, scope: null);
        var inert = new InertQuery(work: null);

        foreach (var element in NodeTraversal.DescendantElements(document, () => dom.NativeReadCheckpoint(256), dom.CancellationToken))
        {
            if (!IsTabbable(dom, element, rendering, inert))
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
            if (element.HasContentAttribute("autofocus") && IsFocusable(dom, element))
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
    private static int? TabIndexAttribute(Element element, DomReadWork? work = null)
    {
        if (work is not null) return BoundedTabIndexAttribute(element, work);
        var raw = element.GetAttributeNS(null, "tabindex");
        return raw is not null && int.TryParse(raw.Trim(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static int? BoundedTabIndexAttribute(Element element, DomReadWork work)
    {
        var raw = work.Attribute(element, "tabindex");
        if (raw is null) return null;
        var start = 0;
        var end = raw.Length;
        while (start < end)
        {
            work.Step();
            if (!char.IsWhiteSpace(raw[start])) break;
            start++;
        }
        while (end > start)
        {
            work.Step();
            if (!char.IsWhiteSpace(raw[end - 1])) break;
            end--;
        }
        if (start == end) return null;
        var negative = raw[start] == '-';
        if (negative || raw[start] == '+') { work.Step(); start++; }
        var limit = negative ? 2147483648u : int.MaxValue;
        uint value = 0;
        var digits = 0;
        for (; start < end; start++)
        {
            work.Step();
            // Int32.TryParse, used by the existing classifier, permits terminating NUL characters.
            if (raw[start] == '\0')
            {
                for (; start < end; start++) { work.Step(); if (raw[start] != '\0') return null; }
                break;
            }
            var digit = (uint) (raw[start] - '0');
            if (digit > 9 || value > (limit - digit) / 10) return null;
            value = value * 10 + digit;
            digits++;
        }
        return digits == 0 ? null : (int) (negative ? -(long) value : value);
    }

    private static bool IsInherentlyFocusable(Element element, DomReadWork? work = null)
    {
        // HTML §6.8.1: an HTML element directly under a document is an editing host with designMode on.
        // A document has at most one element child, so this parent link identifies it without a root scan.
        work?.Step();
        if (element.NamespaceUri == Namespaces.Html && element.ParentNode is Document document
            && DomDocumentState.IsDesignModeEnabled(document)) return true;
        if (element.NamespaceUri != Namespaces.Html) return ReferenceEquals(ContentEditing.HostOf(element, work), element);
        return element.LocalName switch
        {
            "a" or "area" => Attribute(element, "href", work) is not null,
            "button" or "select" or "textarea" or "iframe" => true,
            "input" => (work is null ? HtmlInputTypes.Get(element) : HtmlInputTypes.Parse(work.Attribute(element, "type"))) != HtmlInputType.Hidden,
            _ => element.LocalName is "summary" || ReferenceEquals(ContentEditing.HostOf(element, work), element),
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
    private static Element? RetargetToDocument(Element focused, Document document, DomReadWork work)
    {
        var target = focused;
        for (Node? current = focused; current is not null; current = current.ParentNode)
        {
            work.Step();
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

    private static bool IsConnectedTo(Element element, Document document, DomReadWork? work = null)
    {
        for (Node? node = element; node is not null; node = node.ParentNode ?? (node as ShadowRoot)?.Host)
        {
            work?.Step();
            if (ReferenceEquals(node, document))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>What moved focus, which is what Selectors 4 §9.4's <c>:focus-visible</c> heuristics ask.</summary>
internal enum FocusOrigin
{
    /// <summary><c>element.focus()</c>, autofocus, a dialog, or a host API standing in for one.</summary>
    Script,

    /// <summary>Sequential focus navigation with a key.</summary>
    Keyboard,

    /// <summary>A trusted mouse press or tap.</summary>
    Pointer,
}
