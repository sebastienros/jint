using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Xml.Dom;
using Jint.Browser.Observers;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// The bodies of the members <c>overrides.json</c>'s <c>additions</c> list adds to a generated interface.
/// </summary>
/// <remarks>
/// <para>
/// Every one of them is a member the DOM standard puts on an interface the generator emits, and that the
/// generator could not project from AngleSharp: a filter parameter is a CLR delegate, a stringifier has no
/// <c>[DomName]</c>, and one member AngleSharp simply spells by its Shadow DOM v0 name. Adding them here
/// rather than hand-writing the whole interface keeps the prototype shaped and keeps the other two hundred
/// members generated.
/// </para>
/// <para>
/// They are static and take the brand-checked receiver, exactly like a generated body, so nothing about the
/// call site differs from the member beside it.
/// </para>
/// </remarks>
internal static class DomViewMembers
{
    /// <summary>
    /// The <c>NodeFilter</c> each traversal object was created with, so that <c>walker.filter</c> answers the
    /// value the page passed rather than the delegate it was converted into.
    /// </summary>
    /// <remarks>
    /// Keyed on the AngleSharp traversal object, which belongs to one engine and one document, so the stored
    /// value can never be read by an engine it does not belong to. A <see cref="ConditionalWeakTable{TKey,TValue}"/>
    /// rather than a field on the wrapper, because the wrapper is created lazily by the cache and the filter
    /// is known here, at creation.
    /// </remarks>
    private static readonly ConditionalWeakTable<object, JsValue> _filters = new();

    private sealed record TraversalCallback(HtmlParser.TraversalFilter? Filter);
    private static readonly ConditionalWeakTable<object, TraversalCallback> _callbacks = new();

    internal static JsValue CreateTreeWalker(DomRealm realm, HtmlParser.Document document, JsValue[] arguments)
    {
        var root = DomBindings.IdentityArgument(arguments, 0, "Document.createTreeWalker");
        var settings = DomConvert.OptionalUInt32(arguments, 1, uint.MaxValue);
        var filter = arguments.At(2);
        var callback = NodeFilters.From(realm, filter, "createTreeWalker");
        var walker = new HtmlParser.DomTreeWalker(root, settings);
        _filters.Add(walker, filter.IsNullOrUndefined() ? JsValue.Null : filter);
        _callbacks.Add(walker, new(callback));
        return realm.Wrap(walker);
    }

    internal static JsValue CreateNodeIterator(DomRealm realm, HtmlParser.Document document, JsValue[] arguments)
    {
        var root = DomBindings.IdentityArgument(arguments, 0, "Document.createNodeIterator");
        var settings = DomConvert.OptionalUInt32(arguments, 1, uint.MaxValue);
        var filter = arguments.At(2);
        var callback = NodeFilters.From(realm, filter, "createNodeIterator");
        var iterator = new HtmlParser.DomNodeIterator(root, settings);
        _filters.Add(iterator, filter.IsNullOrUndefined() ? JsValue.Null : filter);
        _callbacks.Add(iterator, new(callback));
        return realm.Wrap(iterator);
    }

    internal static HtmlParser.TraversalFilter? Callback(object traversal)
        => _callbacks.TryGetValue(traversal, out var callback) ? callback.Filter : null;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-treewalker-filter and its <c>NodeIterator</c> twin: the value the page
    /// passed, or <see langword="null"/>.
    /// </summary>
    internal static JsValue Filter(object traversal)
        => _filters.TryGetValue(traversal, out var filter) ? filter : JsValue.Null;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-range-stringifier — the text of every text node the range covers.
    /// </summary>
    /// <remarks>
    /// AngleSharp implements it as <c>Range.ToString()</c> and puts no <c>[DomName]</c> on it, so nothing in
    /// the metadata says it is the interface's stringifier.
    /// </remarks>
    internal static JsValue RangeToString(DomRealm realm, HtmlParser.DomRange range) => JsString.Create(range.GetText(realm.NativeReadCheckpoint, realm.CancellationToken));

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-range-getboundingclientrect, at the only size a range can be
    /// told.
    /// </summary>
    /// <remarks>
    /// <b>Zeros, and still zeros with the flat box model in place.</b> That model gives every <i>element</i>
    /// a row; a range is a pair of positions inside the text of one, and nothing here measures text. A range
    /// covering half a paragraph has no honest rectangle, so it keeps the empty one.
    /// </remarks>
    internal static JsValue RangeRect(DomRealm realm) => Layout.DomRects.Zero(realm.Engine);

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-range-getclientrects — empty, because a range with no layout
    /// covers no boxes.
    /// </summary>
    internal static JsValue RangeRects(DomRealm realm)
        => realm.OwningRealm.Intrinsics.Array.ConstructFast(System.Array.Empty<JsValue>());

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-slot-assignednodes.
    /// </summary>
    /// <remarks>
    /// AngleSharp names it <c>getDistributedNodes</c>, which is the Shadow DOM v0 spelling, and that is the
    /// name its <c>[DomName]</c> carries — so the generated interface has the old name and not the standard
    /// one. Both are present: the generated one because it is what the metadata says, this one because it is
    /// what a page calls.
    /// </remarks>
    internal static JsValue AssignedNodes(DomRealm realm, HtmlParser.Element slot, JsValue[] arguments)
        => DomConvert.NodeSequence(realm, HtmlParser.SlotAssignment.AssignedNodes(slot,
            Flatten(realm, arguments, "HTMLSlotElement.assignedNodes"), realm.CancellationToken));

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-slot-assignedelements — the assigned nodes that are elements.
    /// </summary>
    internal static JsValue AssignedElements(DomRealm realm, HtmlParser.Element slot, JsValue[] arguments)
        => DomConvert.NodeSequence(realm, HtmlParser.SlotAssignment.AssignedElements(slot,
            Flatten(realm, arguments, "HTMLSlotElement.assignedElements"), realm.CancellationToken));

    private static bool Flatten(DomRealm realm, JsValue[] arguments, string member)
    {
        var options = arguments.At(0);
        if (options.IsNullOrUndefined()) return false;
        if (options is not Jint.Native.Object.ObjectInstance dictionary)
        {
            Throw.TypeError(realm.OwningRealm, "Failed to execute '" + member + "': parameter 1 is not a dictionary.");
            return false;
        }
        return TypeConverter.ToBoolean(dictionary.Get("flatten"));
    }

    /// <summary>https://w3c.github.io/selection-api/#dom-document-getselection.</summary>
    /// <remarks>
    /// A selection belongs to a document's browsing context. A document made by <c>DOMParser</c>,
    /// <c>new Document()</c> or <c>DOMImplementation</c> has none, so its answer is <c>null</c> rather than
    /// the selection of the displayed document that happens to share its engine.
    /// </remarks>
    internal static JsValue GetSelection(DomRealm realm, HtmlParser.Document document)
    {
        var runtime = PageRuntime.Find(realm.Engine, document);
        return runtime is null ? JsValue.Null : runtime.Views.Selection;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-document-createcdatasection.</summary>
    /// <remarks>
    /// <para>
    /// <b>Its first step is a refusal, and that refusal is the point.</b> On an HTML document — every page
    /// this browser loads — the standard's answer is a <c>NotSupportedError</c>, not the <c>TypeError</c> a
    /// missing member gives. <c>dom/common.js</c>, the fixture builder the whole of <c>dom/ranges/</c> and
    /// half of <c>dom/traversal/</c> load, reaches this member on a <c>new Document()</c> before a single
    /// <c>test()</c> runs; with the member absent it threw at file scope and thirty-one documents reported
    /// nothing at all.
    /// </para>
    /// <para>
    /// The node itself comes from AngleSharp.Xml's <c>IXmlDocument.CreateCDataSection</c>, which is the only
    /// place in either assembly a CDATA section can be made. Every non-HTML document this package can produce
    /// is one — <c>new Document()</c> and every <c>DOMParser</c> XML type go through <c>XmlParser</c> — so the
    /// last refusal is for a document shape that does not exist yet rather than one a page can reach.
    /// </para>
    /// </remarks>
    internal static JsValue CreateCDataSection(DomRealm realm, HtmlParser.Document document, JsValue[] arguments)
    {
        var data = DomConvert.RequiredText(arguments, 0, Member.CreateCDataSection);
        return realm.WrapNode(document.CreateCDataSection(data));
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-domimplementation-createhtmldocument.</summary>
    /// <remarks>
    /// <para>
    /// <b>The title is optional, and its absence is not the empty string.</b> DOM's step 6 is "<i>if title is
    /// given</i>, create a <c>title</c> element … and append": <c>createHTMLDocument()</c> makes a document
    /// whose <c>head</c> is empty, and <c>createHTMLDocument("")</c> makes one holding <c>&lt;title&gt;&lt;/title&gt;</c>
    /// with an empty text node in it. AngleSharp's <c>CreateHtmlDocument</c> takes a required string and
    /// creates the element only when that string is non-empty, so the two spellings were indistinguishable
    /// from outside and the argument could not be made optional by projecting it — which is why the member is
    /// <c>skip</c>ped and re-declared.
    /// </para>
    /// <para>
    /// Adding the element the standard asks for is the whole of what this does beyond that call. It is Web
    /// IDL semantics AngleSharp's CLR surface cannot represent rather than a behaviour worked around: there
    /// is no overload that distinguishes an absent title from an empty one, and the divergence register
    /// records that.
    /// </para>
    /// </remarks>
    internal static JsValue CreateHtmlDocument(DomRealm realm, DomImplementation implementation, JsValue[] arguments)
    {
        var given = arguments.Length > 0 && !arguments[0].IsUndefined();
        var title = DomConvert.OptionalText(arguments, 0, "")!;
        var document = HtmlParser.Document.CreateHtml();
        DomDocumentMetadata.Initialize(document, DomDocumentState.Of(implementation.Document).Origin);
        document.AppendChild(document.CreateDocumentType("html"));
        var html = document.CreateElement("html");
        document.AppendChild(html);
        var head = document.CreateElement("head");
        html.AppendChild(head);
        if (given)
        {
            var element = document.CreateElement("title");
            element.AppendChild(document.CreateTextNode(title));
            head.AppendChild(element);
        }
        html.AppendChild(document.CreateElement("body"));
        return realm.WrapNode(document);
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-domimplementation-createdocument.</summary>
    /// <remarks>
    /// <para>
    /// <b>AngleSharp's <c>IImplementation</c> has three members and this is not one of them</b> —
    /// <c>createHTMLDocument</c>, <c>createDocumentType</c> and <c>hasFeature</c> are the whole of it — so
    /// there is nothing to project and the algorithm is here. It is DOM's, over the pieces AngleSharp does
    /// have: an empty XML document from the same parse <c>DomConstructors</c> uses for <c>new Document()</c>,
    /// <c>createElementNS</c> for the document element, and DOM's own append for both children.
    /// </para>
    /// <para>
    /// <b>The content type is step 7 and is decided by the namespace</b> — <c>application/xhtml+xml</c> for
    /// the XHTML namespace, <c>image/svg+xml</c> for SVG, <c>application/xml</c> for everything else. It is
    /// declared on the browsing context the document is parsed into rather than set on the document, because
    /// AngleSharp's <c>Document.ContentType</c> setter is <see langword="protected"/>; see
    /// <see cref="DomContentType"/>.
    /// </para>
    /// </remarks>
    internal static JsValue CreateDocument(DomRealm realm, DomImplementation implementation, JsValue[] arguments)
    {
        if (arguments.Length < 2)
        {
            Throw.TypeError(
                realm.OwningRealm,
                "Failed to execute '" + Member.CreateDocument + "': 2 arguments required, but only "
                + arguments.Length + " present.");
        }

        var namespaceUri = DomConvert.NullableText(arguments, 0);

        // [LegacyNullToEmptyString]: null is the empty string here, and undefined is still "undefined" —
        // which is why this cannot be DomConvert.NullableText or OptionalText.
        var qualifiedNameValue = DomConvert.At(arguments, 1);
        var qualifiedName = qualifiedNameValue.IsNull() ? "" : TypeConverter.ToString(qualifiedNameValue);

        var doctype = DomBindings.NullableArgument<HtmlParser.DocumentType>(arguments, 2, Member.CreateDocument);

        // Step 7, taken first because the content type is what the document is parsed as rather than
        // something set on it afterwards.
        var document = DomConstructors.NewXmlDocument(ContentTypeFor(namespaceUri));
        DomDocumentMetadata.Initialize(document, DomDocumentState.Of(implementation.Document).Origin);

        // Step 3: the internal createElementNS steps. Validate-and-extract's two refusals are DomNames', which
        // this member now has a row of its own in: the creation no longer leans on AngleSharp's stricter name
        // check to make them, because that check refuses names the standard allows.
        var element = qualifiedName.Length == 0
            ? null
            : document.CreateElementNS(namespaceUri, qualifiedName);

        // Steps 4 and 5, in the standard's order: the doctype first, so a document built with both has them
        // the way a parse would. Appending adopts, which is what lets a doctype made by the page's own
        // implementation become this document's child.
        if (doctype is not null)
        {
            realm.RecordSubtree(doctype);
            document.AppendChild(doctype);
        }

        if (element is not null)
        {
            document.AppendChild(element);
        }

        return realm.WrapNode(document);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-domimplementation-createdocument step 7: the content type the new
    /// document gets, decided by the namespace it was asked for and by nothing else.
    /// </summary>
    private static string ContentTypeFor(string? namespaceUri)
    {
        if (string.Equals(namespaceUri, NamespaceNames.HtmlUri, StringComparison.Ordinal))
        {
            return DomContentType.Xhtml;
        }

        if (string.Equals(namespaceUri, NamespaceNames.SvgUri, StringComparison.Ordinal))
        {
            return DomContentType.Svg;
        }

        return DomContentType.Xml;
    }

    /// <summary>The qualified member names the refusals above wear, spelled once.</summary>
    private static class Member
    {
        internal const string CreateCDataSection = "Document.createCDATASection";

        internal const string CreateDocument = "DOMImplementation.createDocument";
    }
}
