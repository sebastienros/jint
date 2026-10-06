using System.Runtime.CompilerServices;
using Jint.Browser.Observers;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Namespaces = Jint.HtmlParser.Namespaces;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// Supplies DOM view members whose WebIDL behavior needs a hand-written Browser adapter.
/// </summary>
/// <remarks>
/// These members bridge script filters, range stringification and document creation to native DOM operations.
/// </remarks>
internal static class DomViewMembers
{
    /// <summary>
    /// The <c>NodeFilter</c> each traversal object was created with, so that <c>walker.filter</c> answers the
    /// value the page passed rather than the delegate it was converted into.
    /// </summary>
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
    internal static JsValue RangeRect(DomRealm realm) => Layout.DomRects.Zero(realm);

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-range-getclientrects — empty, because a range with no layout
    /// covers no boxes.
    /// </summary>
    internal static JsValue RangeRects(DomRealm realm)
        => realm.OwningRealm.Intrinsics.Array.ConstructFast(System.Array.Empty<JsValue>());

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-slot-assignednodes.
    /// </summary>
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
    /// </remarks>
    internal static JsValue CreateCDataSection(DomRealm realm, HtmlParser.Document document, JsValue[] arguments)
    {
        var data = DomConvert.RequiredText(arguments, 0, Member.CreateCDataSection);
        return realm.WrapNode(document.CreateCDataSection(data));
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-domimplementation-createhtmldocument.</summary>
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
        if (string.Equals(namespaceUri, Namespaces.Html, StringComparison.Ordinal))
        {
            return DomContentType.Xhtml;
        }

        if (string.Equals(namespaceUri, Namespaces.Svg, StringComparison.Ordinal))
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
