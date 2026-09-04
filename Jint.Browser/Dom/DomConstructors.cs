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
/// <b>The shortness of this table is the point.</b> AngleSharp puts
/// <c>[DomConstructor]</c> on concrete classes and on no <c>[DomName]</c> interface, so the generator can
/// never learn that an interface is constructible; <see cref="DomInterfaceObject"/> therefore refuses every
/// <c>new</c>, which is also what a browser answers for <c>new HTMLDivElement()</c> and for all but a handful
/// of DOM's interfaces. Each row here is a decision rather than a projection:
/// <c>Document</c> (https://dom.spec.whatwg.org/#dom-document-document);
/// <c>DocumentFragment</c> (https://dom.spec.whatwg.org/#dom-documentfragment-documentfragment), which htmx 2
/// builds for every swap whose response starts with <c>&lt;html&gt;</c> or <c>&lt;body&gt;</c>;
/// and the three DOM has given a constructor since 2014 —
/// <c>Comment</c> (https://dom.spec.whatwg.org/#dom-comment-comment),
/// <c>Text</c> (https://dom.spec.whatwg.org/#dom-text-text) and
/// <c>Range</c> (https://dom.spec.whatwg.org/#dom-range-range).
/// </para>
/// <para>
/// <b>Every one of them says "the current global object's associated <c>Document</c>".</b> That is the page's
/// when there is a page runtime behind the binding, and an empty XML document of this call's own when there
/// is not — the same answer <c>DocumentFragment</c> already gave, for the same reason: a node nothing else
/// can reach still needs an owner.
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
    internal static bool TryConstruct(DomRealm realm, DomInterfaceDefinition definition, JsValue[] arguments, out ObjectInstance instance)
    {
        if (ReferenceEquals(definition, DomInterfaces.Document))
        {
            instance = (ObjectInstance) realm.WrapNode(NewXmlDocument());
            return true;
        }

        if (ReferenceEquals(definition, DomInterfaces.DocumentFragment))
        {
            instance = (ObjectInstance) realm.WrapNode(NodeDocument(realm).CreateDocumentFragment());
            return true;
        }

        // `constructor(optional DOMString data = "")` for both, so an absent argument is the empty string
        // rather than "undefined".
        if (ReferenceEquals(definition, DomInterfaces.Comment))
        {
            instance = (ObjectInstance) realm.WrapNode(NodeDocument(realm).CreateComment(Data(arguments)));
            return true;
        }

        if (ReferenceEquals(definition, DomInterfaces.Text))
        {
            instance = (ObjectInstance) realm.WrapNode(NodeDocument(realm).CreateTextNode(Data(arguments)));
            return true;
        }

        if (ReferenceEquals(definition, DomInterfaces.Range))
        {
            // The new range's start and end are (that document, 0), which is what AngleSharp's own
            // CreateRange answers.
            instance = (ObjectInstance) realm.Wrap(NodeDocument(realm).CreateRange());
            return true;
        }

        instance = null!;
        return false;
    }

    /// <summary>The current global object's associated <c>Document</c>, or an empty one when there is none.</summary>
    private static IDocument NodeDocument(DomRealm realm)
        => Runtime.PageRuntime.Find(realm.Engine)?.Document ?? NewXmlDocument();

    private static string Data(JsValue[] arguments)
        => DomConvert.OptionalText(arguments, 0, string.Empty)!;

    /// <summary>
    /// An empty XML document, and what <c>DOMImplementation.createDocument</c> starts from too. AngleSharp's
    /// <c>IImplementation</c> has no <c>createDocument</c> at all and its <c>CreateHtmlDocument</c> answers an
    /// HTML one, so the empty parse is what is left — and it is exact, because an XML document with no content
    /// has no document element, which is what the constructor promises.
    /// </summary>
    internal static IDocument NewXmlDocument()
        => new XmlParser(new XmlParserOptions { IsSuppressingErrors = true }, BrowsingContext.New(Views.ViewInstaller.ParserConfiguration))
            .ParseDocument(string.Empty);
}
