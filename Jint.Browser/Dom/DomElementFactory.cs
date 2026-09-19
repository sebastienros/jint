using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Construction;
using AngleSharp.Html.Dom;
using AngleSharp.Mathml.Dom;
using AngleSharp.Svg.Dom;
using AngleSharp.Text;

namespace Jint.Browser.Dom;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#concept-create-element">Create an element</a>, over AngleSharp's own
/// element factories, for the names its <c>Document.CreateElement</c> overloads refuse or rewrite.
/// </summary>
/// <remarks>
/// <para>
/// <b>What AngleSharp refuses.</b> Both overloads gate on <c>AngleSharp.Text.XmlExtensions.IsXmlName</c>,
/// which is XML 1.0's <c>Name</c> production walked over UTF-16 <b>code units</b>: <c>IsXmlNameStart</c> ends
/// in <c>IsInRange(65536, 983039)</c>, a range no <see cref="char"/> can reach, and none of its other ranges
/// covers the surrogate block U+D800-U+DBFF. So every astral name is refused, while a BMP non-ASCII name such
/// as <c>café</c> passes. DOM stopped holding a name to the XML productions deliberately
/// (<a href="https://github.com/whatwg/dom/pull/1079">whatwg/dom#1079</a>): a
/// <a href="https://dom.spec.whatwg.org/#valid-element-local-name">valid element local name</a> is now
/// everything the HTML parser can build, which is why AngleSharp's own parser happily builds the element its
/// DOM API refuses. <see cref="DomNames"/> is where the standard's predicate is, and it has already run by
/// the time anything here is reached.
/// </para>
/// <para>
/// <b>What AngleSharp rewrites.</b> The two-argument overload runs <c>GetPrefixAndLocalName</c>, which is
/// validate-and-extract - right for <c>createElementNS</c> and wrong for <c>createElement</c>, whose argument
/// is a <i>local name</i> and never a qualified one. On a document that is not an HTML one, where the
/// one-argument overload cannot be used (it lower-cases and forces the HTML namespace),
/// <c>createElement("f:oo")</c> therefore came back as a <c>NamespaceError</c>, and
/// <c>createElement("foo:bar")</c> on an XHTML-content-type document as an element whose local name was
/// <c>bar</c> and whose prefix was <c>foo</c>. DOM asks for one element whose local name is the whole string.
/// </para>
/// <para>
/// <b>The seam, and why it is composition.</b> <c>Document.CreateElement</c> is a name check followed by
/// <c>context.GetFactory&lt;IElementFactory&lt;Document, …&gt;&gt;().Create(…)</c> and <c>SetupElement()</c>;
/// everything but the check is public, and the factory is the same service <see cref="CaseSensitiveSvgFactory"/>
/// already wraps and the page already registers. So the fallback is AngleSharp's own creation step with its
/// refusal left out, and it keeps what the factory buys: an unknown HTML name still lands on
/// <c>HtmlUnknownElement</c>, which is what <see cref="DomManualInterfaces"/>'s element-interface rule reads
/// to choose between <c>HTMLUnknownElement</c> and <c>HTMLElement</c>. A namespace with no factory is
/// AngleSharp's internal <c>AnyElement</c>, and <c>AnyNamespaceElement</c> is that one gap: a subclass of the
/// <see langword="public"/> <see langword="abstract"/> <see cref="Element"/>, whose constructor is public and
/// whose single abstract member is <c>ParseSubtree</c>. It is not a DOM store of its own - it is an AngleSharp
/// node in AngleSharp's tree, and <c>Element.Clone</c> already copies any element into an <c>AnyElement</c>,
/// so even a clone of one is AngleSharp's own type.
/// </para>
/// <para>
/// <b>The fallback runs only for a name AngleSharp would refuse or rewrite.</b> Every other call reaches the
/// very same <c>Document.CreateElement</c> overload it reached before, decided by the same public predicates
/// AngleSharp gates on rather than by catching its refusal, so nothing about the ordinary path moves.
/// <c>Dom/divergences.md</c> has the row.
/// </para>
/// </remarks>
internal static class DomElementFactory
{
    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-createelement — steps 2 to 4, with the local name taken as
    /// written.
    /// </summary>
    /// <remarks>
    /// The one-argument AngleSharp overload is <c>createElement</c> for an HTML document and for nothing
    /// else: it lower-cases unconditionally and puts the element in the HTML namespace, where step 2
    /// lower-cases only "if this is an HTML document" and step 4 chooses the namespace from the document's
    /// content type.
    /// </remarks>
    internal static IElement Create(IDocument document, string localName)
    {
        if (document is IHtmlDocument)
        {
            return localName.IsXmlName()
                ? document.CreateElement(localName)
                : Build(document, NamespaceNames.HtmlUri, localName.HtmlLower(), prefix: null);
        }

        var namespaceUri = NamespaceFor(document);

        // The two-argument overload is validate-and-extract, which createElement does not run at all, so it
        // may only be used for a name that algorithm would pass through untouched: one with no colon to
        // extract as a prefix, and not `xmlns`, which https://dom.spec.whatwg.org/#validate-and-extract
        // step 10 refuses outside the XMLNS namespace and createElement has no opinion about.
        var passesThrough = localName.IsXmlName()
            && localName.IndexOf(':', StringComparison.Ordinal) < 0
            && !string.Equals(localName, "xmlns", StringComparison.Ordinal);

        return DomNamespaces.Created(passesThrough
            ? document.CreateElement(namespaceUri, localName)
            : Build(document, namespaceUri, localName, prefix: null), namespaceUri);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#internal-createelementns-steps — validate and extract, then create.
    /// </summary>
    /// <remarks>
    /// <see cref="DomNames"/> has already made every refusal the algorithm makes, so a qualified name that
    /// arrives here and that AngleSharp's two predicates reject is one the standard accepts. Splitting it
    /// again is the standard's step 4 — the <i>first</i> colon — which is also how AngleSharp splits it.
    /// </remarks>
    internal static IElement CreateNamespaced(IDocument document, string? namespaceUri, string qualifiedName)
    {
        if (qualifiedName.IsXmlName() && qualifiedName.IsQualifiedName())
        {
            return DomNamespaces.Created(document.CreateElement(namespaceUri, qualifiedName), namespaceUri);
        }

        var colon = qualifiedName.IndexOf(':', StringComparison.Ordinal);
        var localName = colon < 0 ? qualifiedName : qualifiedName[(colon + 1)..];
        var prefix = colon < 0 ? null : qualifiedName[..colon];

        // Step 1: the empty string is the absence of a namespace, which is what AngleSharp's own
        // GetPrefixAndLocalName does with it.
        var namespaceOrNone = string.IsNullOrEmpty(namespaceUri) ? null : namespaceUri;

        return DomNamespaces.Created(Build(document, namespaceOrNone, localName, prefix), namespaceOrNone);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-createelement step 4, for a document that is not an HTML
    /// one: the HTML namespace when its content type is <c>application/xhtml+xml</c>, and no namespace
    /// otherwise.
    /// </summary>
    internal static string? NamespaceFor(IDocument document)
        => string.Equals(
            DomContentType.Of(document) ?? document.ContentType,
            DomContentType.Xhtml,
            StringComparison.Ordinal)
            ? NamespaceNames.HtmlUri
            : null;

    /// <summary>
    /// The half of <c>Document.CreateElement</c> that is not the name check: the namespace's own factory, or
    /// the generic element for a namespace that has none, and then the element's setup.
    /// </summary>
    /// <remarks>
    /// <c>SetupElement</c> is what AngleSharp calls after every factory, and it is reachable because
    /// <c>AngleSharp.Html.Construction.IConstructableElement</c> declares it. Its body notifies the
    /// document's attribute observers of each attribute the factory set, so it does nothing at all for an
    /// element that has none — which is every element created here. It is called anyway, because what makes
    /// this the ordinary creation step with one check left out is that nothing else about the step is left
    /// out.
    /// </remarks>
    private static Element Build(IDocument document, string? namespaceUri, string localName, string? prefix)
    {
        var owner = (Document) document;
        var context = document.Context;

        Element element;

        if (string.Equals(namespaceUri, NamespaceNames.HtmlUri, StringComparison.Ordinal))
        {
            element = context.GetFactory<IElementFactory<Document, HtmlElement>>().Create(owner, localName, prefix);
        }
        else if (string.Equals(namespaceUri, NamespaceNames.SvgUri, StringComparison.Ordinal))
        {
            element = context.GetFactory<IElementFactory<Document, SvgElement>>().Create(owner, localName, prefix);
        }
        else if (string.Equals(namespaceUri, NamespaceNames.MathMlUri, StringComparison.Ordinal))
        {
            element = context.GetFactory<IElementFactory<Document, MathElement>>().Create(owner, localName, prefix);
        }
        else
        {
            element = new AnyNamespaceElement(owner, localName, prefix, namespaceUri);
        }

        ((IConstructableElement) element).SetupElement();
        return element;
    }

    /// <summary>
    /// An element in a namespace no factory is registered for, which is AngleSharp's <c>AnyElement</c> — an
    /// <see langword="internal"/> <see langword="sealed"/> type this assembly cannot name.
    /// </summary>
    /// <remarks>
    /// The one abstract member is the fragment parse an <c>innerHTML</c> or <c>insertAdjacentHTML</c> write
    /// runs, and it is answered the way AngleSharp's own public <c>HtmlParser.ParseFragment(string,
    /// IElement)</c> answers it for an arbitrary context element: by asking the owning document to build the
    /// element of that name and prefix and parsing in <i>its</i> context. Only the context element's local
    /// name reaches the tokenizer, so the two contexts are the same one.
    /// </remarks>
    private sealed class AnyNamespaceElement(Document owner, string localName, string? prefix, string? namespaceUri)
        : Element(owner, localName, prefix, namespaceUri)
    {
        public override IElement ParseSubtree(string source)
            => ((Document) ((INode) this).Owner!).CreateElementFrom(LocalName, Prefix!).ParseSubtree(source);
    }
}
