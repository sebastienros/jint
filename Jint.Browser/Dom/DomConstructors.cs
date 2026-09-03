using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Xml.Parser;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Dom;

/// <summary>
/// The generated interfaces a script may really call <c>new</c> on.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is exactly one, and the emptiness of this table is the point.</b> AngleSharp puts
/// <c>[DomConstructor]</c> on concrete classes and on no <c>[DomName]</c> interface, so the generator can
/// never learn that an interface is constructible; <see cref="DomInterfaceObject"/> therefore refuses every
/// <c>new</c>, which is also what a browser answers for <c>new HTMLDivElement()</c> and for all but a handful
/// of DOM's interfaces. <c>Document</c> is the one this corpus reaches for —
/// https://dom.spec.whatwg.org/#dom-document-document, "the Document() constructor" — and it is a decision
/// rather than a projection, so it is written here by name.
/// </para>
/// <para>
/// The document it makes is DOM's: an <b>XML</b> document with no doctype, no document element and no
/// browsing context. Its parser gets the same configuration a <c>DOMParser</c> document gets — the CSS
/// services and nothing else — so it reaches no network and runs no script, which is the whole of what "no
/// browsing context" costs a page that then builds a tree in it.
/// </para>
/// </remarks>
internal static class DomConstructors
{
    /// <summary>
    /// Builds the instance for a <c>new</c> on <paramref name="definition"/>, or answers
    /// <see langword="false"/> for an interface WebIDL gives no constructor.
    /// </summary>
    internal static bool TryConstruct(DomRealm realm, DomInterfaceDefinition definition, out ObjectInstance instance)
    {
        if (ReferenceEquals(definition, DomInterfaces.Document))
        {
            instance = (ObjectInstance) realm.WrapNode(NewXmlDocument());
            return true;
        }

        instance = null!;
        return false;
    }

    /// <summary>
    /// An empty XML document. AngleSharp's <c>IImplementation</c> has no <c>createDocument</c> at all and its
    /// <c>CreateHtmlDocument</c> answers an HTML one, so the empty parse is what is left — and it is exact,
    /// because an XML document with no content has no document element, which is what the constructor
    /// promises.
    /// </summary>
    private static IDocument NewXmlDocument()
        => new XmlParser(new XmlParserOptions { IsSuppressingErrors = true }, BrowsingContext.New(Views.ViewInstaller.ParserConfiguration))
            .ParseDocument(string.Empty);
}
