using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Dom.Collections;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>
/// The seam for the DOM members whose behaviour depends on whether a page runtime is behind the binding:
/// the ones that parse markup into the tree and so have to reach the script scheduler, the three whose
/// answer is an object the host made rather than one AngleSharp did, and the handful whose value the host
/// simply <em>has</em> and AngleSharp does not.
/// </summary>
/// <remarks>
/// <para>
/// The generated members call these instead of AngleSharp directly. There is one instance and no subclass —
/// <see cref="DomRealm.Hooks"/> is a seam nothing currently replaces — so a member whose answer differs with
/// a page asks for one here, through <c>PageRuntime.Find</c>, and uses it only when the target belongs to the
/// document that runtime is showing. A secondary document in the same engine and a binding-only engine both
/// fall through to AngleSharp's own state.
/// </para>
/// <para>
/// Two of them the parser driver settled rather than replaced. A <c>&lt;script&gt;</c> inserted through
/// <c>innerHTML</c> needs nothing here: AngleSharp's fragment parser marks it "already started", so adopting
/// it into the tree never runs it, which is HTML's own rule. And <c>document.write</c> <i>during</i> a parse
/// is AngleSharp's own call and is correct — its writable text source inserts at the parser's index while the
/// baton has the parser parked. Every write that is <i>not</i> into a document a parser is reading needed a
/// decision, and which decision depends on the document it targets rather than on the engine it was made
/// from; <see cref="RefusedWrite"/> below is that decision.
/// </para>
/// </remarks>
internal class DomHostHooks
{
    /// <summary>The behaviour a binding with no runtime behind it has: AngleSharp, called directly.</summary>
    internal static readonly DomHostHooks Default = new();

    /// <summary>
    /// A wrapper has just been created and is the one this engine will keep for its object; the runtime may
    /// add members the generator could not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called once per object per engine, from the wrapper cache, and never again — so a member added here is
    /// an own property of a wrapper rather than of a prototype, which is what keeps every generated prototype
    /// shaped. It is the seam for a member whose <i>whole</i> body belongs to the runtime rather than one
    /// whose body is replaced: <c>form.submit()</c> is a navigation and <c>form.requestSubmit()</c> does not
    /// exist in AngleSharp at all, so neither is generated and neither can be.
    /// </para>
    /// <para>
    /// The cost is one virtual call per wrapper creation, not per member access.
    /// </para>
    /// </remarks>
    /// <param name="realm">The DOM state of the engine the wrapper belongs to.</param>
    /// <param name="target">The AngleSharp object being wrapped.</param>
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

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-innerhtml</summary>
    /// <remarks>
    /// The <c>[CEReactions]</c> half is the second line, and it is only the <i>detached</i> case: a
    /// connected element's assignment produced a mutation record, which upgraded and connected what it
    /// parsed before AngleSharp's own call returned. A detached one produces no record, and HTML
    /// upgrades there too. See <c>CustomElements/CustomElementRegistry.Tree.cs</c>.
    /// </remarks>
    internal virtual void SetInnerHtml(DomRealm realm, IElement element, string markup)
    {
        element.InnerHtml = markup;
        CustomElements.CustomElementRegistry.SubtreeCreated(realm, element);
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-outerhtml</summary>
    /// <remarks>
    /// The markup replaces the element, so what is walked afterwards is the parent it was in — the
    /// element itself is no longer in the tree the new content went into.
    /// </remarks>
    internal virtual void SetOuterHtml(DomRealm realm, IElement element, string markup)
    {
        var parent = element.Parent;
        element.OuterHtml = markup;
        CustomElements.CustomElementRegistry.SubtreeCreated(realm, parent ?? element);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-element-setattribute, hooked so that a handler content attribute a
    /// script writes activates its handler <i>then</i> — which is what fixes the handler's position in the
    /// element's listener list. See <c>Events.EventHandlerContentAttributes.AttributeChanged</c>.
    /// </summary>
    internal virtual void SetAttribute(DomRealm realm, IElement element, JsValue[] arguments)
    {
        var name = DomConvert.RequiredText(arguments, 0, "Element.setAttribute");
        element.SetAttribute(name, DomConvert.RequiredText(arguments, 1, "Element.setAttribute"));
        Events.EventHandlerContentAttributes.AttributeChanged(realm, element, name);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-element-removeattribute, the other half: removing the attribute
    /// deactivates the handler, and the listener goes with it.
    /// </summary>
    internal virtual void RemoveAttribute(DomRealm realm, IElement element, JsValue[] arguments)
    {
        var name = DomConvert.RequiredText(arguments, 0, "Element.removeAttribute");
        element.RemoveAttribute(name);
        Events.EventHandlerContentAttributes.AttributeChanged(realm, element, name);
    }

    /// <summary>HTML's <c>DOMStringMap</c> view over an element's <c>data-*</c> attributes.</summary>
    internal virtual JsValue Dataset(DomRealm realm, IHtmlElement element)
        => realm.WrapStringMap(element, element.Dataset);

    /// <summary>https://html.spec.whatwg.org/multipage/forms.html#dom-lfe-labels</summary>
    internal virtual JsValue Labels(DomRealm realm, IHtmlElement element)
        => HtmlLabelAssociation.IsLabelable(element) ? realm.WrapLabels(element) : JsValue.Null;

    /// <summary>https://dom.spec.whatwg.org/#dom-range-comparepoint</summary>
    internal virtual JsValue ComparePoint(DomRealm realm, IRange range, JsValue[] arguments)
        => DomRangeMembers.ComparePoint(realm, range, arguments);

    /// <summary>https://dom.spec.whatwg.org/#dom-range-ispointinrange</summary>
    internal virtual JsValue IsPointInRange(DomRealm realm, IRange range, JsValue[] arguments)
        => DomRangeMembers.IsPointInRange(realm, range, arguments);

    /// <summary>https://dom.spec.whatwg.org/#dom-range-clonecontents</summary>
    internal virtual JsValue CloneContents(DomRealm realm, IRange range, JsValue[] arguments)
        => DomRangeMembers.CloneContents(realm, range);

    /// <summary>https://dom.spec.whatwg.org/#dom-range-extractcontents</summary>
    internal virtual JsValue ExtractContents(DomRealm realm, IRange range, JsValue[] arguments)
        => DomRangeMembers.ExtractContents(realm, range);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-nonelementparentnode-getelementbyid - and DOM §4.9's definition of an
    /// element's ID, which is what makes the empty string answer null.
    /// </summary>
    /// <remarks>
    /// An element's ID is <i>unset</i> while its <c>id</c> content attribute is absent or the empty string, so
    /// no element in any tree has the empty string as its ID and the lookup can never match. AngleSharp
    /// compares the attribute's value instead, and answers the first element it walks for
    /// <c>getElementById("")</c> - the document element of an ordinary page. See Dom/divergences.md.
    /// </remarks>
    internal virtual JsValue GetElementById(DomRealm realm, INode root, JsValue[] arguments)
    {
        var elementId = DomConvert.RequiredText(arguments, 0, Member(root, "getElementById"));

        if (elementId.Length == 0)
        {
            return JsValue.Null;
        }

        return realm.WrapNodeValue(root is INonElementParentNode parent ? parent.GetElementById(elementId) : null);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-childnode-before, whose viable-sibling step runs before the argument
    /// conversion that can move the receiver out of its own parent. See <see cref="DomChildNodeMembers"/>.
    /// </summary>
    internal virtual void Before(DomRealm realm, INode node, JsValue[] arguments)
        => DomChildNodeMembers.Before(realm, node, arguments);

    /// <summary>https://dom.spec.whatwg.org/#dom-childnode-after</summary>
    internal virtual void After(DomRealm realm, INode node, JsValue[] arguments)
        => DomChildNodeMembers.After(realm, node, arguments);

    /// <summary>https://dom.spec.whatwg.org/#dom-childnode-replacewith</summary>
    internal virtual void ReplaceWith(DomRealm realm, INode node, JsValue[] arguments)
        => DomChildNodeMembers.ReplaceWith(realm, node, arguments);

    /// <summary>https://dom.spec.whatwg.org/#concept-getelementsbyclassname</summary>
    /// <remarks>
    /// <para>
    /// The whole algorithm is here rather than only its liveness, because its comparison is
    /// <b>ASCII case-insensitive when the root's node document is in quirks mode</b> and AngleSharp offers no
    /// seam for that: <c>ITokenList.Contains</c> is the case-sensitive token store, and the class selector the
    /// query engine uses calls the same operation. Jint owns this behaviour by the decision recorded on
    /// <a href="https://github.com/sebastienros/jint/issues/3899">#3899</a>, after the upstream change was
    /// declined as out of scope; the divergence register keeps the reproduction.
    /// </para>
    /// <para>
    /// The mode is read <i>inside</i> the filter, with everything else the filter reads, because the
    /// collection is live and a root adopted into another document takes that document's mode with it.
    /// "ASCII case-insensitive" is spelled out rather than taken from <c>OrdinalIgnoreCase</c>, which folds
    /// the whole of Unicode's simple case mapping and would make <c>class="ı"</c> match <c>"I"</c>.
    /// </para>
    /// </remarks>
    internal virtual JsValue GetElementsByClassName(DomRealm realm, INode root, JsValue[] arguments)
    {
        var classes = AsciiWhitespaceSplit(DomConvert.RequiredText(arguments, 0, Member(root, "getElementsByClassName")));

        if (classes.Length == 0)
        {
            // "If classes is the empty set, return an empty HTMLCollection" - and an empty one that is still
            // a collection, because a page holds it and reads its length.
            return realm.WrapCollection<IElement>(new DomLiveHtmlCollection(static () => []));
        }

        return realm.WrapCollection<IElement>(new DomLiveHtmlCollection(() =>
        {
            // DOM §4.5: compatMode is "BackCompat" exactly while the node document's mode is "quirks".
            var quirks = string.Equals((root as IDocument ?? root.Owner)?.CompatMode, "BackCompat", StringComparison.Ordinal);
            return root.Descendants<IElement>().Where(element => HasEveryClass(element, classes, quirks));
        }));
    }

    /// <summary>https://dom.spec.whatwg.org/#concept-getelementsbytagname</summary>
    internal virtual JsValue GetElementsByTagName(DomRealm realm, INode root, JsValue[] arguments)
    {
        var qualifiedName = DomConvert.RequiredText(arguments, 0, Member(root, "getElementsByTagName"));
        var htmlDocument = (root as IDocument ?? root.Owner) is IHtmlDocument;
        var htmlName = AsciiLowercase(qualifiedName);

        return realm.WrapCollection<IElement>(new DomLiveHtmlCollection(() =>
            root.Descendants<IElement>().Where(element =>
            {
                if (qualifiedName == "*")
                {
                    return true;
                }

                var candidate = QualifiedName(element);
                return htmlDocument && string.Equals(element.NamespaceUri, NamespaceNames.HtmlUri, StringComparison.Ordinal)
                    ? string.Equals(candidate, htmlName, StringComparison.Ordinal)
                    : string.Equals(candidate, qualifiedName, StringComparison.Ordinal);
            })));
    }

    /// <summary>https://dom.spec.whatwg.org/#concept-getelementsbynamespacename</summary>
    internal virtual JsValue GetElementsByTagNameNS(DomRealm realm, INode root, JsValue[] arguments)
    {
        var member = Member(root, "getElementsByTagNameNS");
        var namespaceUri = DomConvert.NullableText(arguments, 0);
        if (namespaceUri is { Length: 0 })
        {
            namespaceUri = null;
        }

        var localName = DomConvert.RequiredText(arguments, 1, member);
        IEnumerable<IElement> Current()
        {
            // AngleSharp preserves information unavailable through IElement for exact names it created in
            // the HTML namespace. Re-running that query keeps its answer live. Other namespaces use the
            // case-sensitive traversal below, which also owns the wildcard and null-namespace cases.
            if (string.Equals(namespaceUri, NamespaceNames.HtmlUri, StringComparison.Ordinal) && localName != "*")
            {
                return root switch
                {
                    IDocument document => document.GetElementsByTagName(namespaceUri, localName),
                    IElement element => element.GetElementsByTagNameNS(namespaceUri, localName),
                    _ => [],
                };
            }

            return root.Descendants<IElement>().Where(element =>
                (namespaceUri == "*" || string.Equals(NullIfEmpty(element.NamespaceUri), namespaceUri, StringComparison.Ordinal))
                && (localName == "*" || string.Equals(element.LocalName, localName, StringComparison.Ordinal)));
        }

        return realm.WrapCollection<IElement>(new DomLiveHtmlCollection(Current));
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-parentnode-queryselectorall — "the <b>static</b> result of running
    /// scope-match a selectors string".
    /// </summary>
    /// <remarks>
    /// AngleSharp's answer is already a snapshot, so this hook corrects no behaviour. What it does is put the
    /// standard's word "static" where the binding can act on it: the result is projected through
    /// <c>DomRealm.WrapStaticNodeList</c>, whose <see cref="Collections.DomStaticNodeList"/> makes staticness
    /// a property of the type rather than a guess about an <c>INodeList</c> — which is what lets that
    /// wrapper keep one element wrapper per index. Every other <c>NodeList</c> in the surface
    /// (<c>childNodes</c>, <c>labels</c>) is live and keeps the ordinary accessor-driven wrapper.
    /// </remarks>
    internal virtual JsValue QuerySelectorAll(DomRealm realm, INode root, JsValue[] arguments)
    {
        var selectors = DomConvert.RequiredText(arguments, 0, Member(root, "querySelectorAll"));
        return realm.WrapStaticNodeList(((IParentNode) root).QuerySelectorAll(selectors));
    }

    private static string Member(INode root, string operation)
        => root switch
        {
            IDocument => "Document.",
            IDocumentFragment => "DocumentFragment.",
            _ => "Element.",
        } + operation;

    /// <summary>https://infra.spec.whatwg.org/#ascii-whitespace: TAB, LF, FF, CR and SPACE, and nothing else.</summary>
    private static readonly char[] AsciiWhitespace = ['\t', '\n', '\f', '\r', ' '];

    /// <summary>https://infra.spec.whatwg.org/#split-on-ascii-whitespace, which is how a class list is parsed.</summary>
    private static string[] AsciiWhitespaceSplit(string value)
        => value.Split(AsciiWhitespace, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Whether <paramref name="element"/>'s classes contain every one of <paramref name="classes"/>.</summary>
    /// <remarks>
    /// The element's own token set is scanned in place rather than split: this runs once per descendant per
    /// read of a live collection, and a page that keeps one and reads its <c>length</c> in a loop would
    /// otherwise allocate an array per element per read.
    /// </remarks>
    private static bool HasEveryClass(IElement element, string[] classes, bool quirks)
    {
        var declared = element.GetAttribute("class");

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
    private static bool HasClass(ReadOnlySpan<char> declared, string candidate, bool quirks)
    {
        var index = 0;

        while (index < declared.Length)
        {
            while (index < declared.Length && IsAsciiWhitespace(declared[index]))
            {
                index++;
            }

            var start = index;

            while (index < declared.Length && !IsAsciiWhitespace(declared[index]))
            {
                index++;
            }

            if (index > start && TokenEquals(declared.Slice(start, index - start), candidate, quirks))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// https://infra.spec.whatwg.org/#ascii-case-insensitive - the ASCII range alone when the document is in
    /// quirks mode, so the Kelvin sign and the dotless i keep their own identity where
    /// <c>OrdinalIgnoreCase</c> would not, and an exact comparison otherwise.
    /// </summary>
    private static bool TokenEquals(ReadOnlySpan<char> token, string candidate, bool quirks)
    {
        if (token.Length != candidate.Length)
        {
            return false;
        }

        for (var i = 0; i < token.Length; i++)
        {
            if (token[i] == candidate[i])
            {
                continue;
            }

            if (!quirks || AsciiLowercase(token[i]) != AsciiLowercase(candidate[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiWhitespace(char character)
        => character is '\t' or '\n' or '\f' or '\r' or ' ';

    private static char AsciiLowercase(char character)
        => character is >= 'A' and <= 'Z' ? (char) (character | 0x20) : character;

    private static string QualifiedName(IElement element)
        => string.IsNullOrEmpty(element.Prefix) ? element.LocalName : element.Prefix + ":" + element.LocalName;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-element-tagname — the element's
    /// <a href="https://dom.spec.whatwg.org/#element-html-uppercased-qualified-name">HTML-uppercased
    /// qualified name</a>, which is ASCII-uppercased only when the element is in the HTML namespace
    /// <b>and</b> its node document is an HTML document.
    /// </summary>
    /// <remarks>
    /// AngleSharp decides on the namespace alone, so an element created in the page and then adopted into
    /// an XML document went on answering <c>DIV</c> where DOM says <c>div</c>: the name is not a property of
    /// the element, it is a question about the document the element is in at the moment it is asked. The
    /// divergence table records it.
    /// </remarks>
    internal virtual JsValue TagName(DomRealm realm, IElement element)
    {
        var qualified = QualifiedName(element);
        return JsString.Create(
            string.Equals(element.NamespaceUri, NamespaceNames.HtmlUri, StringComparison.Ordinal)
            && element.Owner is IHtmlDocument
                ? AsciiUppercase(qualified)
                : qualified);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-node-nodename — for an element, the same
    /// <a href="https://dom.spec.whatwg.org/#element-html-uppercased-qualified-name">HTML-uppercased
    /// qualified name</a> <see cref="TagName"/> answers, and AngleSharp's own answer for everything else.
    /// </summary>
    /// <remarks>
    /// DOM defines the two in terms of one name, so they cannot disagree; AngleSharp decides both on the
    /// namespace alone, so hooking only <c>tagName</c> would have left an element in an XML document
    /// answering <c>div</c> from one member and <c>DIV</c> from the other. It delegates rather than repeats,
    /// which is what keeps a host that overrides <see cref="TagName"/> answering one name from both.
    /// </remarks>
    internal virtual JsValue NodeName(DomRealm realm, INode node)
        => node is IElement element ? TagName(realm, element) : JsString.Create(node.NodeName);

    private static string AsciiUppercase(string value)
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

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

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
    internal virtual JsValue LabelControl(DomRealm realm, IHtmlLabelElement label)
        => realm.WrapNodeValue(HtmlLabelAssociation.ControlFor(label));

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-insertadjacenthtml</summary>
    /// <remarks>
    /// <para>
    /// Two of the four positions insert into the element's parent, so that is what is walked when there
    /// is one; see <see cref="SetInnerHtml"/> for why the walk is here at all.
    /// </para>
    /// <para>
    /// Step 2's refusal is made here rather than left to AngleSharp, which is the one place in the pinned
    /// assembly that raises a <c>DomException</c> carrying a sentence instead of a
    /// <c>DomError</c> — so <see cref="DomFailures.NameOf"/> could only ever guess at its name, and the
    /// standard's is <c>NoModificationAllowedError</c>. Recorded in <c>AGENTS.md</c>'s divergence table.
    /// </para>
    /// </remarks>
    internal virtual void InsertAdjacentHtml(DomRealm realm, IElement element, JsValue[] arguments)
    {
        var position = DomEnums.ToAdjacentPosition(DomConvert.At(arguments, 0), "Element.insertAdjacentHTML");

        // "If position is 'beforebegin' or 'afterend' … If context is null or a Document, throw a
        // NoModificationAllowedError DOMException." A parent that is the document is not an IElement, so the
        // one test covers both halves — and it is the very test AngleSharp's own `Parent as Element` makes.
        if (position is AdjacentPosition.BeforeBegin or AdjacentPosition.AfterEnd && element.Parent is not IElement)
        {
            DomFailures.Refuse(
                realm.Engine,
                "Element.insertAdjacentHTML",
                DomExceptionNames.NoModificationAllowed,
                "the element has no parent element to insert " + (position == AdjacentPosition.BeforeBegin ? "before" : "after") + ".");
        }

        element.Insert(position, DomConvert.RequiredText(arguments, 1, "Element.insertAdjacentHTML"));
        CustomElements.CustomElementRegistry.SubtreeCreated(realm, element.Parent ?? element);
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-write</summary>
    internal virtual void Write(DomRealm realm, IDocument document, JsValue[] arguments)
    {
        if (RefusedWrite(realm, document, "write"))
        {
            return;
        }

        document.Write(Join(arguments));
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-writeln</summary>
    internal virtual void WriteLine(DomRealm realm, IDocument document, JsValue[] arguments)
    {
        if (RefusedWrite(realm, document, "writeln"))
        {
            return;
        }

        document.WriteLine(Join(arguments));
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-createelement, and its namespaced and cloning siblings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These three <em>answer</em> rather than act, which is what the override table's value-returning hook
    /// form is for. Their answer belongs to the host because with the synchronous custom elements flag set,
    /// DOM's "create an element" runs the definition's <b>constructor</b> and hands back whatever it made
    /// — an object AngleSharp never saw. Everything else about the members stays AngleSharp's call: the
    /// name validation, the lower-casing, the namespace, the clone itself.
    /// </para>
    /// <para>
    /// A document with no definition at all therefore behaves exactly as the generated member did before
    /// there was a registry, which is what keeps the binding usable on its own.
    /// </para>
    /// </remarks>
    internal virtual JsValue CreateElement(DomRealm realm, IDocument document, JsValue[] arguments)
        => CustomElements.CustomElementCreation.CreateElement(realm, document, arguments);

    /// <inheritdoc cref="CreateElement" />
    internal virtual JsValue CreateElementNS(DomRealm realm, IDocument document, JsValue[] arguments)
        => CustomElements.CustomElementCreation.CreateElementNS(realm, document, arguments);

    /// <inheritdoc cref="CreateElement" />
    internal virtual JsValue CloneNode(DomRealm realm, INode node, JsValue[] arguments)
        => CustomElements.CustomElementCreation.CloneNode(realm, node, arguments);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-node-isequalnode — DOM §4.4's node equality, which
    /// <see cref="DomNodeEquality"/> states over the same tree because AngleSharp's <c>Node.Equals</c>
    /// compares a base URL the standard never mentions and leaves out data the standard requires.
    /// </summary>
    internal virtual JsValue IsEqualNode(DomRealm realm, INode node, JsValue[] arguments)
        => DomConvert.Bool(
            DomNodeEquality.AreEqual(node, DomBindings.NullableArgument<INode>(arguments, 0, "Node.isEqualNode")));

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-importnode — "return the result of cloning a node given
    /// node with <b>document set to this</b>". Three things AngleSharp's <c>Import</c> leaves out: DOM's
    /// import steps do not copy a file input's selected files, the clone's node document is the
    /// <i>source</i> document rather than this one, and the IDL default for <c>deep</c> is
    /// <see langword="false"/> where <c>Import</c>'s own is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// The second one is why the clone is adopted afterwards, which is the step DOM folds into "cloning a
    /// node given a document": an imported element went on belonging to the document it came from, so
    /// <c>importNode(x).ownerDocument === document</c> was false and every member that asks its node
    /// document a question — <c>tagName</c> among them — answered about the wrong document. AngleSharp
    /// refuses to adopt an <c>IAttr</c> where DOM adopts any node, so an imported attribute is the one node
    /// this cannot correct; the divergence table records both halves.
    /// </remarks>
    internal virtual JsValue ImportNode(DomRealm realm, IDocument document, JsValue[] arguments)
    {
        var imported = document.Import(
            DomBindings.Argument<INode>(arguments, 0, "Document.importNode"),
            DomConvert.OptionalBool(arguments, 1, false));

        if (imported is not IAttr && !ReferenceEquals(imported.Owner, document))
        {
            document.Adopt(imported);
        }

        Files.FileTransferRealm.ResetCopiedInputs(imported);
        return realm.WrapNodeValue(imported);
    }

    // ------------------------------------------------------------------------------------------------
    // The members whose value the host has and AngleSharp does not. Every one of them used to be an own
    // property written onto the document wrapper, because a getter could not be hooked; they are accessors
    // on Document.prototype now, which is where a browser has them, and `Object.getOwnPropertyNames(document)`
    // is empty as a result. The defaults below are AngleSharp's own answers, so a binding used without a
    // page runtime behaves exactly as it did.
    // ------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-document-defaultview and
    /// <c>iframe.contentWindow</c>: the <c>WindowProxy</c> a member answers with.
    /// </summary>
    /// <remarks>
    /// The global object of an engine <em>is</em> its window, so the only window this can answer is the one
    /// this engine stands for; any other browsing context is <c>null</c>, which is what a browser answers for
    /// a frame that has none yet. A binding with no page runtime has no window at all.
    /// </remarks>
    internal virtual JsValue Window(DomRealm realm, IWindow window)
    {
        if (PageRuntime.Find(realm.Engine) is not { } runtime || runtime.Document is not { } document)
        {
            return JsValue.Null;
        }

        return ReferenceEquals(window, document.DefaultView)
            ? realm.Engine._mainRealm.GlobalObject
            : JsValue.Null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#the-body-element — the first <c>body</c> or
    /// <c>frameset</c> child of the document's <i>html element</i>, and <see langword="null"/> when there is
    /// no html element.
    /// </summary>
    /// <remarks>
    /// The gate is the whole of this hook, and it is <see cref="DomDocumentElements"/>' because the five
    /// obsolete colour members of <c>Document</c> reflect onto the same element and need the same answer.
    /// "The html element of a document is its document element, if it is an <c>html</c> element, and null
    /// otherwise", and an <c>html</c> element is one in the HTML namespace with that local name — so a
    /// document whose root is an XHTML <c>div</c>, or an <c>html</c> from some other namespace, has no body
    /// element however many <c>body</c> children that root has. AngleSharp's getter walks
    /// <c>DocumentElement.ChildNodes</c> without asking what the document element is, which is the standard's
    /// own counter-example (a body inserted beneath an SVG document element) answered wrongly.
    /// </remarks>
    internal virtual JsValue Body(DomRealm realm, IDocument document)
        => realm.WrapNodeValue(DomDocumentElements.Body(document));

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#dom-document-currentscript — the script whose text is
    /// running. AngleSharp 1.7.3 tracks its own execution path, but the page's parser driver also schedules
    /// and executes scripts itself, so its current-script scope remains authoritative for a page.
    /// </summary>
    internal virtual JsValue CurrentScript(DomRealm realm, IDocument document)
    {
        if (PageRuntime.Find(realm.Engine, document) is not { } runtime)
        {
            return realm.WrapNodeValue(document.CurrentScript);
        }

        return runtime.CurrentScript is { } script ? realm.WrapNode(script) : JsValue.Null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#current-document-readiness. AngleSharp advances its own
    /// readiness on its own schedule and <c>Document.ReadyState</c>'s setter is <c>protected</c>, so nothing
    /// outside its assembly can move it; the three transitions a page observes are the parser driver's.
    /// </summary>
    internal virtual JsValue ReadyState(DomRealm realm, IDocument document)
        => JsString.Create(PageRuntime.Find(realm.Engine, document) is { } runtime
            ? runtime.ReadyState
            : document.ReadyState.ToString().ToLowerInvariant());

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-url and its <c>documentURI</c> twin. The page's URL, not
    /// AngleSharp's document address: <c>pushState</c> and a fragment navigation move the URL without
    /// reloading, and AngleSharp's address cannot follow without raising a navigation of its own.
    /// </summary>
    internal virtual JsValue DocumentUrl(DomRealm realm, IDocument document)
        => JsString.Create(PageRuntime.Find(realm.Engine, document)?.DocumentUrl ?? document.Url ?? "");

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-node-baseuri — the node document's base URL, which
    /// <c>&lt;base href&gt;</c> moves. The displayed document resolves against the page URL; a secondary HTML
    /// document is recomputed here so removing its first base element cannot leave AngleSharp's cached value.
    /// </summary>
    internal virtual JsValue BaseUri(DomRealm realm, INode node)
    {
        if (PageRuntime.Find(realm.Engine, node) is not { } runtime)
        {
            return JsString.Create(CurrentBaseUri(node));
        }

        return JsString.Create(runtime.BaseUri);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-document-location — "return this's
    /// relevant global object's <c>Location</c> object, if this is fully active, and null otherwise". A
    /// document made by <c>createDocument</c>, <c>createHTMLDocument</c>, <c>new Document()</c> or
    /// <c>DOMParser</c> has no browsing context at all, so it is never fully active and its
    /// <c>location</c> is <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// AngleSharp gives every document it builds a <c>Location</c> over <c>about:blank</c>, including the
    /// ones nothing is displaying, so the question this answers is AngleSharp's own: a document is the
    /// active document of a browsing context, or it is not one at all. That is the same distinction
    /// <c>PageRuntime.FindBrowsingContext</c> makes for the page's own tree, asked in a way that does not
    /// need a page runtime — a binding installed on its own still tells a parsed document from a
    /// manufactured one.
    /// </remarks>
    internal virtual JsValue Location(DomRealm realm, IDocument document)
        => HasBrowsingContext(document) ? realm.Wrap(document.Location) : JsValue.Null;

    /// <summary>
    /// The <c>[PutForwards=href]</c> half of the same attribute. WebIDL's setter steps read the attribute
    /// and then set <c>href</c> on what came back, so a document with no browsing context — whose value is
    /// <see langword="null"/> — is a <c>TypeError</c> and never a silent navigation of a document nobody
    /// can see. https://webidl.spec.whatwg.org/#PutForwards
    /// </summary>
    internal virtual void SetLocation(DomRealm realm, IDocument document, string href)
    {
        if (!HasBrowsingContext(document) || document.Location is not { } location)
        {
            Throw.TypeError(realm.PrincipalRealm, "Cannot set property 'href' of null");
            return;
        }

        location.Href = href;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-characterset, and the <c>charset</c> and
    /// <c>inputEncoding</c> aliases DOM keeps beside it: the document's encoding's <b>name</b>, as
    /// https://encoding.spec.whatwg.org/#names-and-labels spells it — <c>UTF-8</c>, not the ASCII-lowercased
    /// label AngleSharp hands back from .NET's <c>Encoding.WebName</c>. The two spellings are one table's two
    /// columns; <c>TextDecoder.encoding</c> reports the other one because its own definition says so.
    /// </summary>
    /// <remarks>
    /// A label the Encoding Standard does not know is answered as AngleSharp gave it, rather than as UTF-8:
    /// there is no name for it, and inventing one would hide the encoding a document really carries.
    /// </remarks>
    internal virtual JsValue CharacterSet(DomRealm realm, IDocument document)
    {
        var label = document.CharacterSet;

        if (string.IsNullOrEmpty(label))
        {
            return JsString.Create(Jint.WebApi.Encoding.EncodingLabels.Utf8Name);
        }

        return JsString.Create(
            Jint.WebApi.Encoding.EncodingLabels.TryLookup(label, out var encoding) ? encoding.Name : label);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-contenttype — the content type the algorithm that created
    /// the document gave it. <see cref="DomContentType"/> says why it cannot be set on the document itself.
    /// </summary>
    internal virtual JsValue ContentType(DomRealm realm, IDocument document)
        => JsString.Create(DomContentType.Of(document) ?? document.ContentType ?? "");

    /// <summary>
    /// Whether <paramref name="document"/> is the active document of a browsing context, which is what HTML
    /// asks before answering with a <c>Location</c>, a <c>defaultView</c> or anything else a document only
    /// has while something is showing it.
    /// </summary>
    private static bool HasBrowsingContext(IDocument document)
        => document.Context is { } context && ReferenceEquals(context.Active, document);

    // ---------------------------------------------------------------------------------------------------
    // HTML §4.8.4's image members. Every one of them answers from the page's own image lane rather than
    // from AngleSharp, whose IsCompleted/OriginalWidth/ActualSource all read an IImageInfo produced by an
    // IResourceService<IImageInfo> this browser deliberately does not register — registering one would
    // mean decoding pixels for two numbers a container header already states. See Media/PageImages.
    // A binding with no page runtime behind it keeps AngleSharp's answers, which are the ones it had before
    // there was a lane at all.
    // ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/embedded-content.html#dom-img-complete — true when there is
    /// nothing to wait for: no source at all, or a current request that has finished either way.
    /// </summary>
    /// <remarks>
    /// <b>The first two conditions are why this cannot be AngleSharp's <c>IsCompleted</c>.</b> An
    /// <c>&lt;img&gt;</c> with no <c>src</c> and no <c>srcset</c> is complete, and so is a broken one; both
    /// answered <see langword="false"/> before, and both are what a lazy-loading library tests to decide
    /// whether to wait for a <c>load</c> event that is never coming.
    /// </remarks>
    internal virtual JsValue ImageComplete(DomRealm realm, IHtmlImageElement image)
    {
        var source = image.GetAttribute(null, "src");

        if (!image.HasAttribute("srcset") && string.IsNullOrEmpty(source))
        {
            return JsBoolean.True;
        }

        if (PageRuntime.Find(realm.Engine, image.Owner) is not { } runtime)
        {
            return DomConvert.Bool(image.IsCompleted);
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
    internal virtual JsValue ImageCurrentSrc(DomRealm realm, IHtmlImageElement image)
        => JsString.Create(PageRuntime.Find(realm.Engine, image.Owner) is { } runtime
            ? runtime.ImagesIfLoaded?.Find(image)?.CurrentSrc ?? ""
            : image.ActualSource ?? "");

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/embedded-content.html#dom-img-naturalwidth — the intrinsic
    /// width of an available image, and 0 for one that is not available or states no size.
    /// </summary>
    internal virtual JsValue ImageNaturalWidth(DomRealm realm, IHtmlImageElement image)
        => JsNumber.Create(PageRuntime.Find(realm.Engine, image.Owner) is { } runtime
            ? Available(runtime, image)?.NaturalWidth ?? 0
            : image.OriginalWidth);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/embedded-content.html#dom-img-naturalheight — the intrinsic
    /// height, on the same terms.
    /// </summary>
    internal virtual JsValue ImageNaturalHeight(DomRealm realm, IHtmlImageElement image)
        => JsNumber.Create(PageRuntime.Find(realm.Engine, image.Owner) is { } runtime
            ? Available(runtime, image)?.NaturalHeight ?? 0
            : image.OriginalHeight);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/embedded-content.html#dom-dim-width — the <c>width</c>
    /// content attribute when it has one, and otherwise the intrinsic width of an available image.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This member is not reflection, and the <c>reflected</c> entry beside it is only its setter.</b>
    /// HTML makes the getter answer the element's box when it is being rendered, the density-corrected
    /// intrinsic width when it is available and not rendered, and 0 otherwise — never the content
    /// attribute. The flat box model gives every rendered element the viewport's width
    /// (<c>Runtime/AGENTS.md</c>), which would make every image 1280 wide and is worse than no answer, so
    /// the attribute stands in for the box it maps to and the intrinsic size answers when there is none.
    /// That is AngleSharp's own <c>DisplayWidth</c> rule, over an intrinsic size AngleSharp cannot produce.
    /// </para>
    /// <para>
    /// It is what an image submit button's coordinates depend on
    /// (<a href="https://github.com/sebastienros/jint/issues/3933">#3933</a>): HTML selects a coordinate only
    /// within an available image the user agent displays, and until there was a size there was nothing to
    /// select within.
    /// </para>
    /// </remarks>
    internal virtual JsValue ImageWidth(DomRealm realm, IHtmlImageElement image)
        => Dimension(realm, image, "width", DomReflected.HTMLImageElementWidth, intrinsicWidth: true);

    /// <inheritdoc cref="ImageWidth" />
    internal virtual JsValue ImageHeight(DomRealm realm, IHtmlImageElement image)
        => Dimension(realm, image, "height", DomReflected.HTMLImageElementHeight, intrinsicWidth: false);

    private static JsValue Dimension(
        DomRealm realm,
        IHtmlImageElement image,
        string attribute,
        ReflectedAttribute reflected,
        bool intrinsicWidth)
    {
        // The content attribute is what the presentational hint maps to, so where it is present it is the
        // box, and HTML's own parsing rules for it are the reflected entry's.
        if (image.HasAttribute(attribute))
        {
            return reflected.Get(image);
        }

        if (PageRuntime.Find(realm.Engine, image.Owner) is not { } runtime)
        {
            return JsNumber.Create(intrinsicWidth ? image.OriginalWidth : image.OriginalHeight);
        }

        var request = Available(runtime, image);
        return JsNumber.Create(request is null ? 0 : intrinsicWidth ? request.NaturalWidth : request.NaturalHeight);
    }

    /// <summary>
    /// <paramref name="image"/>'s current request when it is completely available, and <see langword="null"/>
    /// otherwise — which is the one condition every dimension member above is guarded by.
    /// </summary>
    private static Media.ImageRequest? Available(PageRuntime runtime, IElement image)
        => runtime.ImagesIfLoaded?.Find(image) is { State: Media.ImageAvailability.CompletelyAvailable } request
            ? request
            : null;

    /// <summary>https://html.spec.whatwg.org/multipage/dom.html#dom-document-referrer</summary>
    internal virtual JsValue Referrer(DomRealm realm, IDocument document)
        => JsString.Create(PageRuntime.Find(realm.Engine, document)?.Referrer ?? document.Referrer ?? "");

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#dom-document-cookie, over the same jar every request of
    /// the browsing context reads and writes — which is a jar AngleSharp's own document has no idea about.
    /// </summary>
    internal virtual JsValue Cookie(DomRealm realm, IDocument document)
        => JsString.Create(PageRuntime.FindBrowsingContext(realm.Engine, document) is { } runtime
            ? DocumentCookies.Read(runtime, document)
            : document.Cookie ?? "");

    /// <inheritdoc cref="Cookie" />
    internal virtual void SetCookie(DomRealm realm, IDocument document, string value)
    {
        if (PageRuntime.FindBrowsingContext(realm.Engine, document) is { } runtime)
        {
            DocumentCookies.Write(runtime, document, value);
            return;
        }

        document.Cookie = value;
    }

    /// <summary>
    /// The node document's current base URL, derived without AngleSharp's cached <see cref="INode.BaseUri"/>.
    /// </summary>
    private static string CurrentBaseUri(INode node)
    {
        var document = node as IDocument ?? node.Owner;
        if (document is null)
        {
            return node.BaseUri ?? "";
        }

        var documentUrl = document.Url ?? "";
        if (document is not IHtmlDocument)
        {
            return node.BaseUri ?? documentUrl;
        }

        var href = document.QuerySelector("base[href]")?.GetAttribute("href");
        return string.IsNullOrEmpty(href) ? documentUrl : PageUrl.Resolve(href, documentUrl) ?? documentUrl;
    }

    /// <summary>
    /// Whether a write is refused, and how — which is a question about <em>which</em> document it targets
    /// and not only about when that document stopped parsing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one write that can be delegated is one into a document a parser is still reading, because that is
    /// the only case where HTML's insertion point is defined — AngleSharp's writable text source inserts at
    /// the parser's index while the baton has the parser parked, which is the standard's own step. Every
    /// other write implies <c>document.open()</c>, and <c>Document.Open</c> cannot serve one:
    /// </para>
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
    /// So a refusal is owed in every remaining case, and which refusal depends on the target. An XML
    /// document is HTML's own first step and an <c>InvalidStateError</c>. The <b>displayed</b> document —
    /// the page's, or a frame's, which is why the browsing-context tree and not the one document decides —
    /// keeps the page error it has always recorded and the call does nothing, because that is a page a host
    /// is watching and a throw would break scripts that write into a document they think is still parsing.
    /// Anything else is a <b>secondary</b> document: <c>DOMParser</c>'s, <c>new Document()</c>'s,
    /// <c>createHTMLDocument</c>'s. The page was not involved, so no page error is recorded and nothing is
    /// swallowed either — the script asked for a capability that is missing and is told so.
    /// </para>
    /// </remarks>
    private static bool RefusedWrite(DomRealm realm, IDocument document, string member)
    {
        var qualified = "Document." + member;

        // https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-write-steps step 1,
        // before anything else and whatever the document's readiness. AngleSharp raises the same error from
        // inside Document.Open, which is reached only once the document is ready — so every XML document
        // still parsing wrote into a text source nothing reads and the call answered success.
        if (document is not IHtmlDocument)
        {
            DomFailures.Refuse(
                realm.Engine,
                qualified,
                DomExceptionNames.InvalidState,
                "the document is an XML document, which has no dynamic markup insertion.");
        }

        if (Runtime.PageRuntime.FindBrowsingContext(realm.Engine, document) is { } runtime)
        {
            // Still parsing: the insertion point is defined and AngleSharp's own write is the standard's.
            if (document.ReadyState == DocumentReadyState.Loading)
            {
                return false;
            }

            runtime.Recorder.Add(
                PageErrorKind.ReportedError,
                "document." + member + "() after the document finished parsing implies document.open(), which "
                + "would replace the document; Jint.Browser does not implement it, so the call did nothing. "
                + "Build the markup with the DOM, or set the page's content again.",
                document.Url);

            return true;
        }

        DomFailures.Refuse(
            realm.Engine,
            qualified,
            DomExceptionNames.NotSupported,
            "no parser is reading this document, so the write implies document.open(), and AngleSharp's "
            + "Document.Open cannot reopen a document that has been parsed. Build the markup with the DOM, "
            + "or parse a new document.");

        return true;
    }

    /// <summary>
    /// <c>document.write</c> takes a variadic <c>DOMString...</c> and concatenates it; AngleSharp's signature
    /// takes one string, so the concatenation happens here.
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
