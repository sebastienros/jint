using System.Buffers;
using AdjacentPosition = Jint.Browser.Dom.DomAdjacentPosition;
using Element = Jint.HtmlParser.Element;
using Document = Jint.HtmlParser.Document;
using Namespaces = Jint.HtmlParser.Namespaces;
using Jint.Browser.Dom.Collections;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>
/// Browser policy for native DOM members that need page state or script scheduling.
/// </summary>
/// <remarks>
/// Generated bindings call these hooks for markup insertion, form state, navigation and other page-owned
/// operations. A binding-only engine uses the native DOM defaults. Page-specific answers apply only to the
/// runtime's current document; secondary documents retain their own state.
/// </remarks>
internal class DomHostHooks
{
    /// <summary>The native DOM defaults for a binding with no page runtime.</summary>
    internal static readonly DomHostHooks Default = new();

    /// <summary>
    /// A wrapper has just been created and is the one this engine will keep for its object; the runtime may
    /// add members the generator could not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cost is one virtual call per wrapper creation, not per member access.
    /// </para>
    /// </remarks>
    /// <param name="realm">The DOM state of the engine the wrapper belongs to.</param>
    /// <param name="target">The the native DOM object being wrapped.</param>
    /// <param name="wrapper">The wrapper, before anything has read a property off it.</param>
    internal virtual void WrapperCreated(DomRealm realm, object target, ObjectInstance wrapper)
    {
        // The handler content attributes an element's markup declares, registered the moment its wrapper wins
        // the cache — which is before anything can dispatch through it or add a listener of its own, and is
        // what puts a markup handler ahead of a script's in the listener list. It is here rather than in
        // DomNodeObject's constructor because this hook fires exactly once for the wrapper that won, and a
        // re-entrant member that built a second one would otherwise have scanned the loser too.
        // https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-content-attributes step 3:
        // "if scripting is disabled for element's node document, return" — which is what
        // Emulation.setScriptExecutionDisabled turns off.
        if (wrapper is DomNodeObject node && realm.ScriptingEnabled)
        {
            Events.EventHandlerContentAttributes.InstallFromMarkup(node);
        }
    }

    internal virtual JsValue GetInnerHtml(DomRealm realm, Jint.HtmlParser.Node node)
        => JsString.Create(DomHtmlMarkupFormatter.InnerHtml(realm, node));

    internal virtual JsValue GetOuterHtml(DomRealm realm, Jint.HtmlParser.Element element)
        => JsString.Create(DomHtmlMarkupFormatter.OuterHtml(realm, element));

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-innerhtml</summary>
    internal virtual void SetInnerHtml(DomRealm realm, Element element, string markup)
    {
        Jint.HtmlParser.Node target = element.TemplateContent ?? (Jint.HtmlParser.Node) element;
        SetInnerHtml(realm, element, target, markup);
    }

    internal virtual void SetInnerHtml(DomRealm realm, Jint.HtmlParser.ShadowRoot shadow, string markup)
        => SetInnerHtml(realm, shadow.Host, shadow, markup);

    private static void SetInnerHtml(DomRealm realm, Element context, Jint.HtmlParser.Node target, string markup)
    {
        var fragment = DomFragmentParser.Parse(realm, markup, context, target);
        realm.RecordSubtree(fragment);
        target.ReplaceChildren(fragment);
        CustomElements.CustomElementRegistry.SubtreeCreated(realm, target);
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-outerhtml.</summary>
    /// <remarks>
    /// The layout mutation scope is this algorithm's own because <c>DOM.setOuterHTML</c> reaches it without
    /// the generated setter's guard; inside that guard it only nests (#4138).
    /// </remarks>
    internal virtual void SetOuterHtml(DomRealm realm, Element element, string markup)
    {
        using var mutation = realm.MutateLayout();
        if (element.ParentNode is not { } parent) return;
        if (parent is Document)
        {
            DomFailures.Refuse(realm, "Element.outerHTML", DomExceptionNames.NoModificationAllowed,
                "the element's parent is a Document.");
        }
        var fragment = DomFragmentParser.Parse(realm, markup, DomFragmentParser.ContextFor(parent), parent);
        realm.RecordSubtree(fragment);
        parent.ReplaceChild(fragment, element);
        CustomElements.CustomElementRegistry.SubtreeCreated(realm, parent);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-element-setattribute, hooked so that a handler content attribute a
    /// script writes activates its handler <i>then</i> — which is what fixes the handler's position in the
    /// element's listener list. See <c>Events.EventHandlerContentAttributes.AttributeChanged</c>.
    /// </summary>
    internal virtual void SetAttribute(DomRealm realm, Jint.HtmlParser.Element element, JsValue[] arguments)
    {
        var name = DomConvert.RequiredText(arguments, 0, "Element.setAttribute");
        var value = DomConvert.RequiredText(arguments, 1, "Element.setAttribute");
        var wasOpen = DetailsOpen(element);
        element.SetAttribute(name, value);
        NotifyDetailsOpenChanged(realm, element, wasOpen);
        if (element.NamespaceUri == Jint.HtmlParser.Namespaces.Html && element.OwnerDocument?.Kind == Jint.HtmlParser.DocumentKind.Html)
            name = AsciiLowercase(name);
        Events.EventHandlerContentAttributes.AttributeChanged(realm, element, name);
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-element-setattributens</summary>
    internal virtual void SetAttributeNS(DomRealm realm, Jint.HtmlParser.Element element, JsValue[] arguments)
    {
        var namespaceUri = DomConvert.NullableText(arguments, 0);
        var name = DomConvert.RequiredText(arguments, 1, "Element.setAttributeNS");
        var value = DomConvert.RequiredText(arguments, 2, "Element.setAttributeNS");
        var wasOpen = DetailsOpen(element);
        element.SetAttributeNS(namespaceUri, name, value);
        NotifyDetailsOpenChanged(realm, element, wasOpen);
        if (string.IsNullOrEmpty(namespaceUri))
        {
            var colon = name.IndexOf(':', StringComparison.Ordinal);
            Events.EventHandlerContentAttributes.AttributeChanged(realm, element, colon < 0 ? name : name[(colon + 1)..]);
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-element-removeattribute, the other half: removing the attribute
    /// deactivates the handler, and the listener goes with it.
    /// </summary>
    internal virtual void RemoveAttribute(DomRealm realm, Jint.HtmlParser.Element element, JsValue[] arguments)
    {
        var name = DomConvert.RequiredText(arguments, 0, "Element.removeAttribute");
        var wasOpen = DetailsOpen(element);
        element.RemoveAttribute(name);
        NotifyDetailsOpenChanged(realm, element, wasOpen);
        Events.EventHandlerContentAttributes.AttributeChanged(realm, element, name);
    }

    internal static bool DetailsOpen(Element element)
        => element is { NamespaceUri: Namespaces.Html, LocalName: "details" } && element.GetAttributeNS(null, "open") is not null;

    internal static void NotifyDetailsOpenChanged(DomRealm realm, Element element, bool wasOpen)
    {
        if (element is { NamespaceUri: Namespaces.Html, LocalName: "details" } && wasOpen != (element.GetAttributeNS(null, "open") is not null))
            Events.ActivationBehaviors.ScheduleToggle(realm, element);
    }

    /// <summary>HTML's <c>DOMStringMap</c> view over an element's <c>data-*</c> attributes.</summary>
    internal virtual JsValue Dataset(DomRealm realm, Element element)
        => realm.WrapStringMap(element);

    /// <summary>https://html.spec.whatwg.org/multipage/forms.html#dom-lfe-labels</summary>
    internal virtual JsValue Labels(DomRealm realm, Element element)
        => HtmlLabelAssociation.IsLabelable(element, realm.NativeReadCheckpoint, realm.CancellationToken) ? realm.WrapLabels(element) : JsValue.Null;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#dom-fae-form — the
    /// <c>form</c> IDL attribute of every element that has one, from HTML's own ownership rule.
    /// </summary>
    internal virtual JsValue FormOwner(DomRealm realm, Element element)
        => realm.WrapNodeValue(HtmlFormOwner.FormIdlOf(element, realm.NativeReadCheckpoint, realm.CancellationToken));

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-elements.html#dom-option-selected — the setter's three
    /// steps: set the selectedness, set the dirtiness, and then cause the element to ask for a reset.
    /// </summary>
    internal virtual void SetOptionSelected(DomRealm realm, Element option, bool selected)
    {
        realm.Engine.Constraints.Check();
        option.GetHtmlState()!.GetOptionState(realm.CancellationToken)!.SetSelected(selected, realm.CancellationToken);
        realm.Engine.Constraints.Check();
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-range-comparepoint</summary>
    internal virtual JsValue ComparePoint(DomRealm realm, Jint.HtmlParser.DomRange range, JsValue[] arguments)
        => DomRangeMembers.ComparePoint(realm, range, arguments);

    /// <summary>https://dom.spec.whatwg.org/#dom-range-ispointinrange</summary>
    internal virtual JsValue IsPointInRange(DomRealm realm, Jint.HtmlParser.DomRange range, JsValue[] arguments)
        => DomRangeMembers.IsPointInRange(realm, range, arguments);

    /// <summary>https://dom.spec.whatwg.org/#dom-range-clonecontents</summary>
    internal virtual JsValue CloneContents(DomRealm realm, Jint.HtmlParser.DomRange range, JsValue[] arguments)
        => DomRangeMembers.CloneContents(realm, range);

    /// <summary>https://dom.spec.whatwg.org/#dom-range-extractcontents</summary>
    internal virtual JsValue ExtractContents(DomRealm realm, Jint.HtmlParser.DomRange range, JsValue[] arguments)
        => DomRangeMembers.ExtractContents(realm, range);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-nonelementparentnode-getelementbyid - and DOM §4.9's definition of an
    /// element's ID, which is what makes the empty string answer null.
    /// </summary>
    internal virtual JsValue GetElementById(DomRealm realm, Jint.HtmlParser.Node root, JsValue[] arguments)
    {
        var elementId = DomConvert.RequiredText(arguments, 0, NativeMember(root, "getElementById"));

        if (elementId.Length == 0)
        {
            return JsValue.Null;
        }

        return realm.WrapNodeValue(DomDocumentReads.ById(realm, root, elementId));
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-childnode-before, whose viable-sibling step runs before the argument
    /// conversion that can move the receiver out of its own parent. See <see cref="DomChildNodeMembers"/>.
    /// </summary>
    internal virtual void Before(DomRealm realm, Jint.HtmlParser.Node node, JsValue[] arguments)
        => DomChildNodeMembers.Before(realm, node, arguments);

    /// <summary>https://dom.spec.whatwg.org/#dom-childnode-after</summary>
    internal virtual void After(DomRealm realm, Jint.HtmlParser.Node node, JsValue[] arguments)
        => DomChildNodeMembers.After(realm, node, arguments);

    /// <summary>https://dom.spec.whatwg.org/#dom-childnode-replacewith</summary>
    internal virtual void ReplaceWith(DomRealm realm, Jint.HtmlParser.Node node, JsValue[] arguments)
        => DomChildNodeMembers.ReplaceWith(realm, node, arguments);

    /// <summary>https://dom.spec.whatwg.org/#concept-getelementsbyclassname</summary>
    /// <remarks>
    /// <para>
    /// The mode is read <i>inside</i> the filter, with everything else the filter reads, because the
    /// collection is live and a root adopted into another document takes that document's mode with it.
    /// "ASCII case-insensitive" is spelled out rather than taken from <c>OrdinalIgnoreCase</c>, which folds
    /// the whole of Unicode's simple case mapping and would make <c>class="ı"</c> match <c>"I"</c>.
    /// </para>
    /// </remarks>
    internal virtual JsValue GetElementsByClassName(DomRealm realm, Jint.HtmlParser.Node root, JsValue[] arguments)
    {
        var classes = AsciiWhitespaceSplit(DomConvert.RequiredText(arguments, 0, NativeMember(root, "getElementsByClassName")));

        if (classes.Length == 0)
        {
            // "If classes is the empty set, return an empty HTMLCollection" - and an empty one that is still
            // a collection, because a page holds it and reads its length.
            return realm.WrapCollection<Jint.HtmlParser.Element>(new DomLiveHtmlCollection(root, DomElementFilter.None));
        }

        return realm.WrapCollection<Jint.HtmlParser.Element>(new DomLiveHtmlCollection(root, new ClassNameFilter(root, classes)));
    }

    /// <summary>
    /// The filter of https://dom.spec.whatwg.org/#concept-getelementsbyclassname. A <see cref="DomElementFilter"/>
    /// rather than a lambda so that a read allocates neither a closure nor an iterator; see that type.
    /// </summary>
    private sealed class ClassNameFilter(Jint.HtmlParser.Node root, string[] classes) : DomElementFilter
    {
        private bool _quirks;

        internal override void BeginRead()
            => _quirks = string.Equals((root as Jint.HtmlParser.Document ?? root.OwnerDocument)?.Mode.ToString(), "Quirks", StringComparison.Ordinal);

        internal override bool Matches(Jint.HtmlParser.Element element) => HasEveryClass(element, classes, _quirks);

        internal override bool Matches(Element element, DomReadWork work)
        {
            var declared = work.Attribute(element, "class");
            if (string.IsNullOrEmpty(declared)) return false;
            foreach (var expected in classes)
            {
                var matched = false;
                var offset = 0;
                while (offset < declared.Length)
                {
                    offset = TokenBoundary(declared, offset, whitespace: true, work);
                    var start = offset;
                    offset = TokenBoundary(declared, offset, whitespace: false, work);
                    if (offset - start != expected.Length) continue;
                    if (!_quirks)
                    {
                        matched = work.EqualSpan(declared.AsSpan(start, expected.Length), expected.AsSpan());
                        if (matched) break;
                        continue;
                    }
                    matched = true;
                    for (var i = 0; i < expected.Length; i += 256)
                    {
                        var length = Math.Min(256, expected.Length - i);
                        work.Account(length);
                        if (TokenEqualsFolded(declared.AsSpan(start + i, length), expected.AsSpan(i, length))) continue;
                        matched = false;
                        break;
                    }
                    if (matched) break;
                }
                if (!matched) return false;
            }
            return true;
        }
    }

    private static int TokenBoundary(string text, int offset, bool whitespace, DomReadWork work)
    {
        while (offset < text.Length)
        {
            var length = Math.Min(256, text.Length - offset);
            var part = text.AsSpan(offset, length);
            var found = whitespace ? part.IndexOfAnyExcept(AsciiWhitespaceValues) : part.IndexOfAny(AsciiWhitespaceValues);
            work.Account(found < 0 ? length : found + 1);
            if (found >= 0) return offset + found;
            offset += length;
        }
        return offset;
    }

    /// <summary>https://dom.spec.whatwg.org/#concept-getelementsbytagname</summary>
    internal virtual JsValue GetElementsByTagName(DomRealm realm, Jint.HtmlParser.Node root, JsValue[] arguments)
    {
        var qualifiedName = DomConvert.RequiredText(arguments, 0, NativeMember(root, "getElementsByTagName"));
        var htmlDocument = (root as Jint.HtmlParser.Document ?? root.OwnerDocument)?.Kind == Jint.HtmlParser.DocumentKind.Html;

        return realm.WrapCollection<Jint.HtmlParser.Element>(
            new DomLiveHtmlCollection(root, new TagNameFilter(qualifiedName, AsciiLowercase(qualifiedName), htmlDocument)));
    }

    /// <summary>The filter of https://dom.spec.whatwg.org/#concept-getelementsbytagname.</summary>
    private sealed class TagNameFilter(string qualifiedName, string htmlName, bool htmlDocument) : DomElementFilter
    {
        internal override bool Matches(Jint.HtmlParser.Element element)
        {
            if (qualifiedName == "*")
            {
                return true;
            }

            var candidate = element.TagName;
            return htmlDocument && string.Equals(element.NamespaceUri, Namespaces.Html, StringComparison.Ordinal)
                ? string.Equals(candidate, htmlName, StringComparison.Ordinal)
                : string.Equals(candidate, qualifiedName, StringComparison.Ordinal);
        }
        internal override bool Matches(Element element, DomReadWork work)
            => qualifiedName == "*" || work.EqualSpan(element.TagName.AsSpan(),
                htmlDocument && element.NamespaceUri == Namespaces.Html ? htmlName : qualifiedName);
    }

    /// <summary>https://dom.spec.whatwg.org/#concept-getelementsbynamespacename</summary>
    internal virtual JsValue GetElementsByTagNameNS(DomRealm realm, Jint.HtmlParser.Node root, JsValue[] arguments)
    {
        var member = NativeMember(root, "getElementsByTagNameNS");
        var namespaceUri = DomConvert.NullableText(arguments, 0);
        if (namespaceUri is { Length: 0 })
        {
            namespaceUri = null;
        }

        var localName = DomConvert.RequiredText(arguments, 1, member);

        return realm.WrapCollection<Jint.HtmlParser.Element>(new DomLiveHtmlCollection(root, new TagNameNSFilter(namespaceUri, localName)));
    }

    /// <summary>The filter of https://dom.spec.whatwg.org/#concept-getelementsbynamespacename.</summary>
    private sealed class TagNameNSFilter(string? namespaceUri, string localName) : DomElementFilter
    {
        internal override bool Matches(Jint.HtmlParser.Element element)
            => (namespaceUri == "*" || string.Equals(element.NamespaceUri, namespaceUri, StringComparison.Ordinal))
               && (localName == "*" || string.Equals(element.LocalName, localName, StringComparison.Ordinal));

        internal override bool Matches(Element element, DomReadWork work)
            => (namespaceUri == "*" || (namespaceUri is null ? element.NamespaceUri is null : work.EqualSpan(element.NamespaceUri.AsSpan(), namespaceUri.AsSpan())))
                && (localName == "*" || work.EqualSpan(element.LocalName.AsSpan(), localName.AsSpan()));
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-parentnode-queryselectorall — "the <b>static</b> result of running
    /// scope-match a selectors string".
    /// </summary>
    internal virtual JsValue QuerySelectorAll(DomRealm realm, Jint.HtmlParser.Node root, JsValue[] arguments)
    {
        var selectors = DomConvert.RequiredText(arguments, 0, NativeMember(root, "querySelectorAll"));
        return realm.WrapStaticNodeList(DomSelectors.QuerySelectorAll(realm, root, selectors));
    }

    internal virtual JsValue QuerySelector(DomRealm realm, Jint.HtmlParser.Node root, JsValue[] arguments)
        => realm.WrapNodeValue(DomSelectors.QuerySelector(realm, root, DomConvert.RequiredText(arguments, 0, NativeMember(root, "querySelector"))));

    internal virtual JsValue Matches(DomRealm realm, Jint.HtmlParser.Element element, JsValue[] arguments)
        => DomConvert.Bool(DomSelectors.Matches(realm, element, DomConvert.RequiredText(arguments, 0, "Element.matches")));

    internal virtual JsValue Closest(DomRealm realm, Jint.HtmlParser.Element element, JsValue[] arguments)
        => realm.WrapNodeValue(DomSelectors.Closest(realm, element, DomConvert.RequiredText(arguments, 0, "Element.closest")));

    private static string NativeMember(Jint.HtmlParser.Node root, string operation)
        => (root is Jint.HtmlParser.Document ? "Document." : root is Jint.HtmlParser.DocumentFragment ? "DocumentFragment." : "Element.") + operation;


    /// <summary>https://infra.spec.whatwg.org/#ascii-whitespace: TAB, LF, FF, CR and SPACE, and nothing else.</summary>
    private static readonly char[] AsciiWhitespace = ['\t', '\n', '\f', '\r', ' '];

    /// <summary>
    /// The same five characters as a set a search can be vectorised over, for the two scans of
    /// <see cref="HasClassFolded"/>. <see cref="SearchValues{T}"/> chooses the strategy once, when it is
    /// built, and both <c>IndexOfAny</c> and <c>IndexOfAnyExcept</c> over it then read a vector of
    /// characters per step where the scan they replaced read one.
    /// </summary>
    private static readonly SearchValues<char> AsciiWhitespaceValues = SearchValues.Create(AsciiWhitespace);

    /// <summary>https://infra.spec.whatwg.org/#split-on-ascii-whitespace, which is how a class list is parsed.</summary>
    private static string[] AsciiWhitespaceSplit(string value)
        => value.Split(AsciiWhitespace, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Whether <paramref name="element"/>'s classes contain every one of <paramref name="classes"/>.</summary>
    /// <remarks>
    /// <para>
    /// The element's own token set is scanned in place rather than split: this runs once per descendant per
    /// read of a live collection, and a page that keeps one and reads its <c>length</c> in a loop would
    /// otherwise allocate an array per element per read.
    /// </para>
    /// <para>
    /// <b>The attribute is read by namespace and local name, not by qualified name.</b> DOM's
    /// <a href="https://dom.spec.whatwg.org/#concept-class">classes</a> are the token set of
    /// <c>classList</c>, and DOM §7.1 reads that attribute by "getting an attribute value given null
    /// namespace and the local name <c>class</c>" — whereas <c>getAttribute("class")</c> is the
    /// <i>qualified</i>-name lookup, which also finds an attribute someone put in a namespace under that
    /// spelling and would answer its value in preference to the real one when an element carries both.
    /// <c>className</c> and <c>classList</c> already read the content attribute, so the qualified-name form
    /// made this algorithm the one reader of an element's classes that disagreed with them. It is also the
    /// cheaper of the two on the hot path, because the qualified form ASCII-folds the name it is given on
    /// every call for an element in the HTML namespace.
    /// </para>
    /// </remarks>
    private static bool HasEveryClass(Jint.HtmlParser.Element element, string[] classes, bool quirks)
    {
        var declared = element.GetAttributeNS(null, "class");

        if (string.IsNullOrEmpty(declared))
        {
            return false;
        }

        foreach (var candidate in classes)
        {
            if (!HasClass(declared.AsSpan(), candidate, quirks))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether the ASCII-whitespace-separated <paramref name="declared"/> set holds one token.</summary>
    /// <remarks>
    /// <para>
    /// <b>Outside quirks mode the comparison is exact, so the boundaries are the whole question and the
    /// tokens themselves never have to be produced.</b> An ordinal substring search over the attribute value
    /// finds every place the candidate's characters occur — a vector of characters per step rather than one —
    /// and such a hit is a class exactly when ASCII whitespace or an end of the value stands on both sides of
    /// it. That replaces a scan that read the value one character at a time to cut it into tokens and then
    /// compared each token one character at a time: a profile of a <c>getElementsByClassName</c> loop over a
    /// 5,000-element document put that scan at ~27% of the page-loop thread, against ~2% for the attribute
    /// read it stands on.
    /// </para>
    /// <para>
    /// <b>Two properties of a candidate are what make the search equivalent to the scan, and both come from
    /// <see cref="AsciiWhitespaceSplit"/>:</b> it is never empty — <c>RemoveEmptyEntries</c> drops the empty
    /// string, and an empty set of classes never reaches a filter at all — and it contains no ASCII
    /// whitespace. The second is what makes "this hit is bounded by whitespace" the same statement as "some
    /// whole token equals the candidate", and it is also what lets a rejected hit be skipped <i>past</i>
    /// rather than re-entered at its second character: a hit beginning inside an earlier one would need a
    /// whitespace character somewhere inside the candidate to be bounded on its left.
    /// </para>
    /// </remarks>
    private static bool HasClass(ReadOnlySpan<char> declared, string candidate, bool quirks)
    {
        if (quirks)
        {
            return HasClassFolded(declared, candidate);
        }

        var index = 0;

        while (index < declared.Length)
        {
            var hit = declared[index..].IndexOf(candidate.AsSpan());

            if (hit < 0)
            {
                return false;
            }

            hit += index;
            var end = hit + candidate.Length;

            if ((hit == 0 || IsAsciiWhitespace(declared[hit - 1]))
                && (end == declared.Length || IsAsciiWhitespace(declared[end])))
            {
                return true;
            }

            index = end;
        }

        return false;
    }

    /// <summary>
    /// The quirks half of <see cref="HasClass"/>, where the tokens do have to be produced: the comparison is
    /// https://infra.spec.whatwg.org/#ascii-case-insensitive and no substring search can be. An ordinal one
    /// would miss <c>BTN</c> for <c>btn</c>, and an <c>OrdinalIgnoreCase</c> one folds the whole of Unicode's
    /// simple case mapping — the fold this algorithm must not have, for the reason
    /// <see cref="GetElementsByClassName"/> gives. So the scan stays and what is vectorised is the two
    /// searches it is made of: one for the end of a token, one for the start of the next.
    /// </summary>
    private static bool HasClassFolded(ReadOnlySpan<char> declared, string candidate)
    {
        while (true)
        {
            var start = declared.IndexOfAnyExcept(AsciiWhitespaceValues);

            if (start < 0)
            {
                return false;
            }

            declared = declared[start..];
            var end = declared.IndexOfAny(AsciiWhitespaceValues);

            if (TokenEqualsFolded(end < 0 ? declared : declared[..end], candidate))
            {
                return true;
            }

            if (end < 0)
            {
                return false;
            }

            declared = declared[(end + 1)..];
        }
    }

    /// <summary>
    /// https://infra.spec.whatwg.org/#ascii-case-insensitive - the ASCII range alone, so the Kelvin sign and
    /// the dotless i keep their own identity where <c>OrdinalIgnoreCase</c> would not. Only quirks mode
    /// compares this way; the exact comparison is the substring search in <see cref="HasClass"/> and never
    /// reaches here.
    /// </summary>
    private static bool TokenEqualsFolded(ReadOnlySpan<char> token, ReadOnlySpan<char> candidate)
    {
        if (token.Length != candidate.Length)
        {
            return false;
        }

        for (var i = 0; i < token.Length; i++)
        {
            if (token[i] != candidate[i] && AsciiLowercase(token[i]) != AsciiLowercase(candidate[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The token-boundary predicate of <see cref="HasClass"/>.</summary>
    /// <remarks>
    /// It deliberately carries no <c>[MethodImpl(AggressiveInlining)]</c>, and the profile above is not an
    /// argument for one: what put this method in that profile as a frame of its own — 4.6% of the page-loop
    /// thread, so genuinely called rather than inlined — was being called once per character of every class
    /// attribute in the document. Above it is now called at most twice per hit, so inlining it could move at
    /// most a fraction of a per cent of what is left, and the attribute would be an unmeasured claim about a
    /// call shape that no longer exists. If it ever matters again it will show up the way it did this time.
    /// </remarks>
    private static bool IsAsciiWhitespace(char character)
        => character is '\t' or '\n' or '\f' or '\r' or ' ';

    private static char AsciiLowercase(char character)
        => character is >= 'A' and <= 'Z' ? (char) (character | 0x20) : character;


    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-element-tagname — the element's
    /// <a href="https://dom.spec.whatwg.org/#element-html-uppercased-qualified-name">HTML-uppercased
    /// qualified name</a>, which is ASCII-uppercased only when the element is in the HTML namespace
    /// <b>and</b> its node document is an HTML document.
    /// </summary>
    internal virtual JsValue TagName(DomRealm realm, Jint.HtmlParser.Element element)
    {
        var qualified = element.TagName;
        return element.NamespaceUri == Jint.HtmlParser.Namespaces.Html
            && element.OwnerDocument?.Kind == Jint.HtmlParser.DocumentKind.Html
                // Memoized per realm: the transform is a pure function of the qualified name, and this is
                // the branch every repeated read of an HTML element's tagName/nodeName takes.
                ? realm.HtmlUppercasedTagName(qualified)
                : JsString.Create(qualified);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-element-namespaceuri — "this's namespace", the namespace the element
    /// was <b>created</b> with.
    /// </summary>
    internal virtual JsValue NamespaceUri(DomRealm realm, Jint.HtmlParser.Element element)
        => DomConvert.NullableText(element.NamespaceUri);

    /// <summary>
    /// ASCII-uppercases <paramref name="value"/>: only the bytes <c>a</c>-<c>z</c> move, deliberately not
    /// <see cref="string.ToUpperInvariant"/>'s culture-aware casing, because
    /// <a href="https://infra.spec.whatwg.org/#ascii-uppercase">HTML's ASCII-uppercase</a> is what
    /// <see cref="TagName"/> and <see cref="DomRealm.HtmlUppercasedTagName"/> need. Internal rather than
    /// private so the per-realm memo can call it on a cache miss without duplicating it.
    /// </summary>
    internal static string AsciiUppercase(string value)
    {
        char[]? copy = null;
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (character is < 'a' or > 'z')
            {
                continue;
            }

            copy ??= value.ToCharArray();
            copy[i] = (char) (character & ~0x20);
        }

        return copy is null ? value : new string(copy);
    }

    private static string AsciiLowercase(string value)
    {
        char[]? copy = null;
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (character is < 'A' or > 'Z')
            {
                continue;
            }

            copy ??= value.ToCharArray();
            copy[i] = (char) (character | 0x20);
        }

        return copy is null ? value : new string(copy);
    }

    /// <summary>https://html.spec.whatwg.org/multipage/forms.html#dom-label-control</summary>
    internal virtual JsValue LabelControl(DomRealm realm, Element label)
        => realm.WrapNodeValue(HtmlLabelAssociation.ControlFor(label, realm.NativeReadCheckpoint, realm.CancellationToken));

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-insertadjacenthtml</summary>
    /// <remarks>
    /// <para>
    /// Two of the four positions insert into the element's parent, so that is what is walked when there
    /// is one; see <see cref="SetInnerHtml(DomRealm, Element, string)"/> for why the walk is here at all.
    /// </para>
    /// </remarks>
    internal virtual void InsertAdjacentHtml(DomRealm realm, Element element, JsValue[] arguments)
    {
        var position = DomEnums.ToAdjacentPosition(DomConvert.At(arguments, 0), "Element.insertAdjacentHTML");
        var markup = DomConvert.RequiredText(arguments, 1, "Element.insertAdjacentHTML");
        var outside = position is AdjacentPosition.BeforeBegin or AdjacentPosition.AfterEnd;
        var parent = outside ? element.ParentNode : element;
        if (parent is null or Document)
        {
            DomFailures.Refuse(realm, "Element.insertAdjacentHTML", DomExceptionNames.NoModificationAllowed,
                "the element has no insertion parent, or its parent is a Document.");
        }
        var context = DomFragmentParser.ContextFor(parent!);
        var fragment = DomFragmentParser.Parse(realm, markup, context, parent!);
        realm.RecordSubtree(fragment);
        var next = position switch
        {
            AdjacentPosition.BeforeBegin => element,
            AdjacentPosition.AfterEnd => element.NextSibling,
            AdjacentPosition.AfterBegin => element.FirstChild,
            _ => null,
        };
        parent!.InsertBefore(fragment, next);
        CustomElements.CustomElementRegistry.SubtreeCreated(realm, parent);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-adoptnode — adopts the native node and queues adoptedCallback reactions.
    /// </summary>
    /// <remarks>
    /// It is the member's own door because a mutation record cannot be one: the removal a connected node's
    /// adoption performs is delivered <i>before</i> the node's owner changes, so the old document has to be
    /// read before the call. What that leaves — the adoption DOM's pre-insert performs on the way into a
    /// parent in another document — is argued in <c>CustomElements/CustomElementRegistry.Tree.cs</c>.
    /// </remarks>
    internal virtual JsValue AdoptNode(DomRealm realm, Document document, JsValue[] arguments)
    {
        var source = DomBindings.NodeArgument(arguments, 0, "Document.adoptNode");
        if (source.Node is { } nativeSource) realm.RecordSubtree(nativeSource);
        if (source.Attribute is { } attribute)
        {
            attribute.OwnerElement?.RemoveAttributeNode(attribute);
            attribute.Rehome(document);
            return realm.Wrap(attribute);
        }
        return realm.WrapNodeValue(CustomElements.CustomElementRegistry.Adopt(realm, document, source.Node!));
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-write</summary>
    internal virtual void Write(DomRealm realm, Document document, JsValue[] arguments)
    {
        switch (TargetOf(realm, document, "write"))
        {
            case MarkupInsertion.Parser:
                Runtime.Parsing.ParserDriver.Write(document, Join(arguments));
                break;
            case MarkupInsertion.Local:
                DynamicMarkupInsertion.Write(realm, document, Join(arguments));
                break;
            default:
                RecordDisplayedRefusal(realm, document, "write");
                break;
        }
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-writeln</summary>
    internal virtual void WriteLine(DomRealm realm, Document document, JsValue[] arguments)
    {
        switch (TargetOf(realm, document, "writeln"))
        {
            case MarkupInsertion.Parser:
                Runtime.Parsing.ParserDriver.Write(document, Join(arguments) + "\n");
                break;
            case MarkupInsertion.Local:
                DynamicMarkupInsertion.Write(realm, document, Join(arguments) + "\n");
                break;
            default:
                RecordDisplayedRefusal(realm, document, "writeln");
                break;
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-open — the
    /// two-argument form, whose arguments HTML ignores (it names them "unused1" and "unused2"), answering
    /// the document it was called on.
    /// </summary>
    internal virtual JsValue Open(DomRealm realm, Document document, JsValue[] arguments)
    {
        switch (TargetOf(realm, document, "open"))
        {
            case MarkupInsertion.Parser:
                // Open steps step 4: "if document has an active parser whose script nesting level is greater
                // than zero, then return document" — which is exactly the call that got here, one made by a
                // script of the parse the page is running. Nothing happens, and nothing is recorded.
                break;
            case MarkupInsertion.Local:
                // https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-open-window: three
                // arguments select window.open()'s overload, whose first step throws an InvalidAccessError
                // when the document it is called on is not fully active — and one with no browsing context
                // never is.
                if (arguments.Length >= 3)
                {
                    DomFailures.Refuse(
                        realm,
                        "Document.open",
                        DomExceptionNames.InvalidAccess,
                        "the three-argument form is window.open(), and this document has no browsing context.");
                }

                DynamicMarkupInsertion.Open(realm, document);
                break;
            default:
                RecordDisplayedRefusal(realm, document, "open");
                break;
        }

        return realm.WrapNodeValue(document);
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-close</summary>
    /// <remarks>
    /// Close steps step 3 is "if there is no script-created parser associated with the document, then
    /// return", and the parser reading a page is never script-created — so a page's <c>close()</c> is the
    /// standard's own no-op rather than a refusal, and nothing is recorded against it.
    /// </remarks>
    internal virtual void Close(DomRealm realm, Document document, JsValue[] arguments)
    {
        if (TargetOf(realm, document, "close") == MarkupInsertion.Local)
        {
            DynamicMarkupInsertion.Close(realm, document);
        }
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-domimplementation-createdocumenttype.</summary>
    internal virtual JsValue CreateDocumentType(DomRealm realm, DomImplementation implementation, JsValue[] arguments)
        => DomDocumentTypeFactory.Create(realm, implementation, arguments);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-createelement, and its namespaced and cloning siblings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A document with no definition at all therefore behaves exactly as the generated member did before
    /// there was a registry, which is what keeps the binding usable on its own.
    /// </para>
    /// </remarks>
    internal virtual JsValue CreateElement(DomRealm realm, Document document, JsValue[] arguments)
        => CustomElements.CustomElementCreation.CreateElement(realm, document, arguments);

    /// <summary>https://dom.spec.whatwg.org/#dom-document-createprocessinginstruction.</summary>
    internal virtual JsValue CreateProcessingInstruction(DomRealm realm, Document document, JsValue[] arguments)
    {
        var target = DomConvert.RequiredText(arguments, 0, "Document.createProcessingInstruction");
        var data = DomConvert.RequiredText(arguments, 1, "Document.createProcessingInstruction");
        return realm.WrapNode(DomProcessingInstructions.Create(document, target, data));
    }

    /// <inheritdoc cref="CreateElement" />
    internal virtual JsValue CreateElementNS(DomRealm realm, Document document, JsValue[] arguments)
        => CustomElements.CustomElementCreation.CreateElementNS(realm, document, arguments);

    /// <summary>https://dom.spec.whatwg.org/#dom-document-createattribute</summary>
    internal virtual JsValue CreateAttribute(DomRealm realm, Document document, JsValue[] arguments)
        => realm.Wrap(document.CreateAttribute(DomConvert.RequiredText(arguments, 0, "Document.createAttribute")));

    /// <summary>https://dom.spec.whatwg.org/#dom-document-createattributens.</summary>
    internal virtual JsValue CreateAttributeNS(DomRealm realm, Document document, JsValue[] arguments)
    {
        var namespaceUri = DomConvert.NullableText(arguments, 0);
        var name = DomConvert.RequiredText(arguments, 1, "Document.createAttributeNS");
        return realm.Wrap(document.CreateAttributeNS(namespaceUri, name));
    }

    /// <inheritdoc cref="CreateElement" />
    internal virtual JsValue CloneNode(DomRealm realm, Jint.HtmlParser.Node node, JsValue[] arguments)
        => CustomElements.CustomElementCreation.CloneNode(realm, node, arguments);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-node-isequalnode — compares the node data and descendants required by DOM.
    /// </summary>
    internal virtual JsValue IsEqualNode(DomRealm realm, object node, JsValue[] arguments)
        => DomConvert.Bool(DomNodeEquality.AreEqual(node, arguments.At(0).IsNullOrUndefined()
            ? null : DomBindings.NodeArgument(arguments, 0, "Node.isEqualNode").DomTarget));

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-importnode — clones into the target document with the WebIDL deep default and Browser import policy.
    /// </summary>
    internal virtual JsValue ImportNode(DomRealm realm, Document document, JsValue[] arguments)
    {
        var source = DomBindings.NodeArgument(arguments, 0, "Document.importNode");
        var deep = DomConvert.OptionalBool(arguments, 1, false);
        if (source.Attribute is { } attribute)
        {
            return realm.Wrap(document.ImportAttribute(attribute));
        }
        var imported = document.ImportNode(source.Node!, deep);
        realm.RecordSubtree(imported);
        CustomElements.CustomElementRegistry.Cloned(realm, source.Node!, imported);
        return realm.WrapNodeValue(imported);
    }


    /// <summary>
    /// https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-document-defaultview and
    /// <c>iframe.contentWindow</c>: the <c>WindowProxy</c> a member answers with.
    /// </summary>
    /// <remarks>
    /// The global object of an engine <em>is</em> its window, so the only window this can answer is the one
    /// this engine stands for; any other browsing context is <c>null</c>, which is what a browser answers for
    /// a frame that has none yet. A binding with no page runtime has no window at all.
    /// </remarks>

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#the-body-element — the first <c>body</c> or
    /// <c>frameset</c> child of the document's <i>html element</i>, and <see langword="null"/> when there is
    /// no html element.
    /// </summary>
    internal virtual JsValue Body(DomRealm realm, Document document)
        => realm.WrapNodeValue(DomDocumentElements.Body(document));

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#dom-document-currentscript — reads the parser driver's current-script scope.
    /// </summary>
    internal virtual JsValue CurrentScript(DomRealm realm, Document document)
    {
        if (PageRuntime.FindBrowsingContext(realm.Engine, document) is null)
        {
            return JsValue.Null;
        }
        var owner = realm.RealmOfDocument(document);
        return ReferenceEquals(owner.Document, document) && owner.CurrentScript is { } script
            ? realm.WrapNode(script) : JsValue.Null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#current-document-readiness — reads the readiness transitions owned by the page parser driver.
    /// </summary>
    internal virtual JsValue ReadyState(DomRealm realm, Document document)
        => JsString.Create(PageRuntime.Find(realm.Engine, document) is { } runtime
            ? runtime.ReadyState
            : realm.TryGetDocumentRealm(document, out var owner) && ReferenceEquals(owner!.Document, document) && owner.ReadyState is { } state
                ? state : DomDocumentState.Of(document).ReadyState);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-url — reads the page URL, including same-document history changes.
    /// </summary>
    internal virtual JsValue DocumentUrl(DomRealm realm, Document document)
        => JsString.Create(PageRuntime.Find(realm.Engine, document)?.DocumentUrl ?? DomDocumentState.Of(document).Url);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-node-baseuri — resolves the current document base URL, including base element changes.
    /// </summary>
    internal virtual JsValue BaseUri(DomRealm realm, Jint.HtmlParser.Node node)
    {
        var document = node as Document ?? node.OwnerDocument!;
        return JsString.Create(DomDocumentState.BaseUri(document, realm.Engine.Constraints.Check, realm.CancellationToken));
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-document-location — "return this's
    /// relevant global object's <c>Location</c> object, if this is fully active, and null otherwise". A
    /// document made by <c>createDocument</c>, <c>createHTMLDocument</c>, <c>new Document()</c> or
    /// <c>DOMParser</c> has no browsing context at all, so it is never fully active and its
    /// <c>location</c> is <see langword="null"/>.
    /// </summary>
    internal virtual JsValue Location(DomRealm realm, Document document)
    {
        if (DomBrowsingContext.Of(document) is null || PageRuntime.FindBrowsingContext(realm.Engine, document) is not { } runtime)
            return JsValue.Null;
        return ReferenceEquals(document, runtime.Document)
            ? runtime.Location
            : FrameWindows.ForDocument(runtime, document).Get("location");
    }

    /// <summary>
    /// The <c>[PutForwards=href]</c> half of the same attribute. WebIDL's setter steps read the attribute
    /// and then set <c>href</c> on what came back, so a document with no browsing context — whose value is
    /// <see langword="null"/> — is a <c>TypeError</c> and never a silent navigation of a document nobody
    /// can see. https://webidl.spec.whatwg.org/#PutForwards
    /// </summary>
    internal virtual void SetLocation(DomRealm realm, Document document, string href)
    {
        var location = Location(realm, document);
        if (location is not ObjectInstance instance)
        {
            Throw.TypeError(realm.OwningRealm, "Cannot set property 'href' of null");
            return;
        }
        instance.Set("href", JsString.Create(href), throwOnError: true);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-characterset — returns the Encoding Standard name, including the charset and inputEncoding aliases.
    /// </summary>
    internal virtual JsValue CharacterSet(DomRealm realm, Document document)
        => JsString.Create(DomDocumentState.Of(document).CharacterSet);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-contenttype — the content type the algorithm that created
    /// the document gave it. <see cref="DomContentType"/> says why it cannot be set on the document itself.
    /// </summary>
    internal virtual JsValue ContentType(DomRealm realm, Document document)
        => JsString.Create(document.ContentType);

    /// <summary>
    /// Whether <paramref name="document"/> is the active document of a browsing context, which is what HTML
    /// asks before answering with a <c>Location</c>, a <c>defaultView</c> or anything else a document only
    /// has while something is showing it.
    /// </summary>
    /// <remarks>
    /// <see cref="DomBrowsingContext"/> is the one definition of it, because HTML §4.13.4's
    /// look-up-a-custom-element-definition asks the same question of the same documents.
    /// </remarks>
    private static bool HasBrowsingContext(Document document) => DomBrowsingContext.Of(document) is not null;


    /// <summary>
    /// https://html.spec.whatwg.org/multipage/embedded-content.html#dom-img-complete — true when there is
    /// nothing to wait for: no source at all, or a current request that has finished either way.
    /// </summary>
    internal virtual JsValue ImageComplete(DomRealm realm, Element image)
    {
        var source = image.GetAttributeNS(null, "src");

        if (image.GetAttributeNS(null, "srcset") is null && string.IsNullOrEmpty(source))
        {
            return JsBoolean.True;
        }

        if (PageRuntime.Find(realm.Engine, image.OwnerDocument) is not { } runtime)
        {
            return JsBoolean.False;
        }

        // There is no pending request here (Media/PageImages says why), so "and its pending request is
        // null" is satisfied by every state this reaches.
        return DomConvert.Bool(runtime.ImagesIfLoaded?.Find(image)
            is { State: Media.ImageAvailability.CompletelyAvailable or Media.ImageAvailability.Broken });
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/embedded-content.html#dom-img-currentsrc — the current
    /// request's current URL: the selected source, resolved, and not where a redirect the fetch followed
    /// ended up. It answers for a broken request too, which is what makes it usable for saying <i>which</i>
    /// candidate of a source set a page settled on.
    /// </summary>
    internal virtual JsValue ImageCurrentSrc(DomRealm realm, Element image)
        => JsString.Create(PageRuntime.Find(realm.Engine, image.OwnerDocument) is { } runtime
            ? runtime.ImagesIfLoaded?.Find(image)?.CurrentSrc ?? ""
            : "");

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/embedded-content.html#dom-img-naturalwidth — the intrinsic
    /// width of an available image, and 0 for one that is not available or states no size.
    /// </summary>
    internal virtual JsValue ImageNaturalWidth(DomRealm realm, Element image)
        => JsNumber.Create(PageRuntime.Find(realm.Engine, image.OwnerDocument) is { } runtime
            ? Available(runtime, image)?.NaturalWidth ?? 0
            : 0);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/embedded-content.html#dom-img-naturalheight — the intrinsic
    /// height, on the same terms.
    /// </summary>
    internal virtual JsValue ImageNaturalHeight(DomRealm realm, Element image)
        => JsNumber.Create(PageRuntime.Find(realm.Engine, image.OwnerDocument) is { } runtime
            ? Available(runtime, image)?.NaturalHeight ?? 0
            : 0);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/embedded-content.html#dom-dim-width — the <c>width</c>
    /// content attribute when it has one, and otherwise the intrinsic width of an available image.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is what an image submit button's coordinates depend on
    /// (<a href="https://github.com/sebastienros/jint/issues/3933">#3933</a>): HTML selects a coordinate only
    /// within an available image the user agent displays, and until there was a size there was nothing to
    /// select within.
    /// </para>
    /// </remarks>
    internal virtual JsValue ImageWidth(DomRealm realm, Element image)
        => Dimension(realm, image, "width", DomReflected.HTMLImageElementWidth, intrinsicWidth: true);

    /// <inheritdoc cref="ImageWidth" />
    internal virtual JsValue ImageHeight(DomRealm realm, Element image)
        => Dimension(realm, image, "height", DomReflected.HTMLImageElementHeight, intrinsicWidth: false);

    private static JsNumber Dimension(
        DomRealm realm,
        Element image,
        string attribute,
        ReflectedAttribute reflected,
        bool intrinsicWidth)
    {
        // The content attribute is what the presentational hint maps to, so where it is present it is the
        // box, and HTML's own parsing rules for it are the reflected entry's.
        if (image.GetAttributeNS(null, attribute) is not null)
        {
            return (JsNumber) reflected.Get(image);
        }

        if (PageRuntime.Find(realm.Engine, image.OwnerDocument) is not { } runtime)
        {
            return JsNumber.Create(intrinsicWidth ? 0 : 0);
        }

        var request = Available(runtime, image);
        return JsNumber.Create(request is null ? 0 : intrinsicWidth ? request.NaturalWidth : request.NaturalHeight);
    }

    /// <summary>
    /// <paramref name="image"/>'s current request when it is completely available, and <see langword="null"/>
    /// otherwise — which is the one condition every dimension member above is guarded by.
    /// </summary>
    private static Media.ImageRequest? Available(PageRuntime runtime, Element image)
        => runtime.ImagesIfLoaded?.Find(image) is { State: Media.ImageAvailability.CompletelyAvailable } request
            ? request
            : null;

    /// <summary>https://html.spec.whatwg.org/multipage/dom.html#dom-document-referrer</summary>
    internal virtual JsValue Referrer(DomRealm realm, Document document)
        => JsString.Create(PageRuntime.Find(realm.Engine, document)?.Referrer ?? DomDocumentState.Of(document).Referrer);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#dom-document-cookie — reads the browsing context's shared request cookie jar.
    /// </summary>
    internal virtual JsValue Cookie(DomRealm realm, Document document)
        => JsString.Create(PageRuntime.FindBrowsingContext(realm.Engine, document) is { } runtime
            ? DocumentCookies.Read(runtime, document)
            : "");

    /// <inheritdoc cref="Cookie" />
    internal virtual void SetCookie(DomRealm realm, Document document, string value)
    {
        if (PageRuntime.FindBrowsingContext(realm.Engine, document) is { } runtime)
        {
            DocumentCookies.Write(runtime, document, value);
            return;
        }

        // A manufactured document without a browsing context is cookie-averse.
    }

    /// <summary>Who performs a dynamic-markup-insertion call, once the document it targets is known.</summary>
    private enum MarkupInsertion
    {
        /// <summary>
        /// A parser is reading this document, so a write enters its current insertion point.
        /// </summary>
        Parser,

        /// <summary>
        /// No parser is reading it and no browsing context is showing it, so
        /// <see cref="DynamicMarkupInsertion"/> runs HTML's steps against it.
        /// </summary>
        Local,

        /// <summary>The document the page is displaying, or a frame of it: the call does nothing.</summary>
        Displayed,
    }

    /// <summary>
    /// Which document a dynamic-markup-insertion call targets, which decides who performs it — a question
    /// about the <em>document</em> and not only about when it stopped parsing.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>it reads <c>_context?.Parent!.Active</c>, so any context <c>BrowsingContext.New</c> built —
    /// the page's own included — raises <see cref="NullReferenceException"/> out of a member a script
    /// called;</item>
    /// <item>past that it blocks on <c>PromptToUnloadAsync().Result</c> and <c>Unload(recycle: true).Wait()</c>
    /// on whatever thread the script ran on, and rebuilds the document behind the page's back — leaving the
    /// page's wrapper table, its frame tree and its runtime pointing at a document that no longer exists;</item>
    /// <item>and even where it completes it empties the document (<c>ReplaceAll(null)</c>), puts the ready
    /// state back to <c>loading</c> and creates <b>no parser</b>, so the re-entrant <c>Write</c> it makes
    /// inserts into a text source nothing will ever read. The markup is silently lost.</item>
    /// </list>
    /// <para>
    /// So the steps are owned here, and how far they go depends on the target. An XML document is HTML's own
    /// first step and an <c>InvalidStateError</c>. The <b>displayed</b> document — the page's, or a frame's,
    /// which is why the browsing-context tree and not the one document decides — is the one case left
    /// unimplemented: replacing it means unloading a document, swapping the engine the page runs on and
    /// re-committing a navigation, so the call does nothing and the host is told why, because a throw would
    /// break scripts that write into a document they think is still parsing. Anything else is a
    /// <b>secondary</b> document — <c>DOMParser</c>'s, <c>createHTMLDocument</c>'s — which has no page loop,
    /// no engine to swap and no navigation gate, and there
    /// <see cref="DynamicMarkupInsertion"/> performs HTML's steps in place.
    /// </para>
    /// </remarks>
    private static MarkupInsertion TargetOf(DomRealm realm, Document document, string member)
    {
        // https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-write-steps step 1,
        if (document.Kind != Jint.HtmlParser.DocumentKind.Html)
        {
            DomFailures.Refuse(
                realm,
                "Document." + member,
                DomExceptionNames.InvalidState,
                "the document is an XML document, which has no dynamic markup insertion.");
        }

        if (Runtime.PageRuntime.FindBrowsingContext(realm.Engine, document) is null)
        {
            return MarkupInsertion.Local;
        }

        return Runtime.Parsing.ParserDriver.HasInsertionPoint(document)
            ? MarkupInsertion.Parser
            : MarkupInsertion.Displayed;
    }

    /// <summary>The page error the one unimplemented case records, and the no-op that goes with it.</summary>
    private static void RecordDisplayedRefusal(DomRealm realm, Document document, string member)
    {
        var lead = string.Equals(member, "open", StringComparison.Ordinal)
            ? "document.open() on the document the page is showing would replace it"
            : "document." + member + "() after the document finished parsing implies document.open(), which "
              + "would replace the document";

        Runtime.PageRuntime.FindBrowsingContext(realm.Engine, document)!.Recorder.Add(
            PageErrorKind.ReportedError,
            lead + "; Jint.Browser does not implement replacing the displayed document, so the call did "
            + "nothing. Build the markup with the DOM, or set the page's content again.",
            DomDocumentState.Of(document).Url);
    }

    /// <summary>
    /// Concatenates the variadic DOMString arguments for document.write before native insertion.
    /// </summary>
    private static string Join(JsValue[] arguments)
    {
        if (arguments.Length == 0)
        {
            return "";
        }

        if (arguments.Length == 1)
        {
            return TypeConverter.ToString(arguments[0]);
        }

        var builder = new System.Text.StringBuilder();
        foreach (var argument in arguments)
        {
            builder.Append(TypeConverter.ToString(argument));
        }

        return builder.ToString();
    }
}
