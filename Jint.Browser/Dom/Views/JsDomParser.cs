using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Xhtml;
using AngleSharp.Xml.Parser;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// <c>DOMParser</c>: markup in, a document out, with nothing in it allowed to run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scripting is off for the document this produces</b>, which
/// <a href="https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-domparser-parsefromstring">
/// DOM Parsing</a> requires: the parsed document is "not a browsing context" and its scripts never run. That
/// falls out of two things here rather than being enforced by one — the parser is given a browsing context of
/// its own that carries no scripting service, and <c>IsScripting</c> is false so the parse itself treats
/// <c>&lt;noscript&gt;</c> as markup. A <c>&lt;script&gt;</c> in the input becomes an element in the tree,
/// with its text, and nothing else happens.
/// </para>
/// <para>
/// <b>The XML half is AngleSharp's XML parser</b>, which is a separate package (<c>AngleSharp.Xml</c>, MIT,
/// same project) referenced for exactly this. Writing an XML parser here instead would be the one thing this
/// package is not for. A parse that fails answers the
/// <a href="https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-domparser-parsefromstring">
/// <c>parsererror</c> document</a> the standard prescribes, which is what a page tests for, rather than
/// throwing.
/// </para>
/// <para>
/// Each parse gets a fresh browsing context. The context is what a document's services hang off, and sharing
/// one across parses would make every document this ever produced reachable from the last one.
/// </para>
/// </remarks>
internal sealed class JsDomParser : ObjectInstance
{
    /// <summary>
    /// The namespace a <c>parsererror</c> element is in. It is Mozilla's, which every engine copied and
    /// which the HTML standard now names.
    /// </summary>
    private const string ParserErrorNamespace = "http://www.mozilla.org/newlayout/xml/parsererror.xml";

    private readonly PageRuntime _runtime;

    internal JsDomParser(PageRuntime runtime, ObjectInstance prototype) : base(runtime.Engine)
    {
        _runtime = runtime;
        Prototype = prototype;
    }

    /// <inheritdoc />
    public override string ToString() => "[object DOMParser]";

    /// <summary>The receiver check the one member starts with.</summary>
    internal static JsDomParser Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsDomParser parser)
        {
            return parser;
        }

        var message = "Failed to execute '" + member + "' on 'DOMParser': Illegal invocation";

        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return null!;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-domparser-parsefromstring
    /// </summary>
    internal JsValue ParseFromString(JsValue[] arguments)
    {
        var source = DomConvert.RequiredText(arguments, 0, "DOMParser.parseFromString");
        var type = DomConvert.RequiredText(arguments, 1, "DOMParser.parseFromString");

        var document = type switch
        {
            "text/html" => ParseHtml(source),
            "text/xml" or "application/xml" or "application/xhtml+xml" or "image/svg+xml" => ParseXml(source),
            _ => Unsupported(_runtime.Engine, type),
        };

        return _runtime.Dom.WrapNode(document);
    }

    private static IDocument ParseHtml(string source)
        => new HtmlParser(new HtmlParserOptions { IsScripting = false }, NewContext()).ParseDocument(source);

    private static IDocument ParseXml(string source)
    {
        try
        {
            return new XmlParser(default, NewContext()).ParseDocument(source);
        }
        catch (Exception exception) when (exception is not JavaScriptException)
        {
            return ErrorDocument(exception.Message);
        }
    }

    /// <summary>
    /// The document a failed XML parse answers: a <c>parsererror</c> element carrying the message, which is
    /// what a page looks for with <c>doc.querySelector('parsererror')</c>.
    /// </summary>
    private static IDocument ErrorDocument(string message)
    {
        var text = message
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

        var markup =
            "<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><parsererror xmlns=\"" + ParserErrorNamespace + "\">"
            + text
            + "</parsererror></body></html>";

        return new XmlParser(new XmlParserOptions { IsSuppressingErrors = true }, NewContext()).ParseDocument(markup);
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-enumeration — a value outside the enumeration is a
    /// <c>TypeError</c>, built in the page's own realm.
    /// </summary>
    private static IDocument Unsupported(Engine engine, string type)
    {
        Throw.TypeError(
            engine._mainRealm,
            "Failed to execute 'parseFromString' on 'DOMParser': The provided value '" + type
            + "' is not a valid enum value of type SupportedType.");
        return null!;
    }

    /// <summary>
    /// A browsing context with the CSS services and nothing else: no requester, so a parsed document reaches
    /// no network, and no scripting service, so its scripts are inert.
    /// </summary>
    private static IBrowsingContext NewContext() => BrowsingContext.New(ViewInstaller.ParserConfiguration);
}

/// <summary>
/// <c>XMLSerializer</c>: a node out as XML-shaped markup.
/// </summary>
/// <remarks>
/// AngleSharp's <c>XhtmlMarkupFormatter</c> is the
/// <a href="https://w3c.github.io/DOM-Parsing/#dfn-xml-serialization">XML serialization</a> algorithm's
/// practical equivalent: every element is closed, an empty element is self-closed, and text is escaped for
/// XML rather than for HTML. What it does not do is the standard's namespace-prefix invention for a tree
/// whose prefixes conflict, so a document built by hand out of two namespaces with one prefix serializes to
/// markup that does not round-trip. Every document this package can produce — parsed, not hand-assembled —
/// serializes correctly.
/// </remarks>
internal sealed class JsXmlSerializer : ObjectInstance
{
    internal JsXmlSerializer(PageRuntime runtime, ObjectInstance prototype) : base(runtime.Engine)
    {
        Prototype = prototype;
    }

    /// <inheritdoc />
    public override string ToString() => "[object XMLSerializer]";

    /// <summary>The receiver check the one member starts with.</summary>
    internal static JsXmlSerializer Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsXmlSerializer serializer)
        {
            return serializer;
        }

        var message = "Failed to execute '" + member + "' on 'XMLSerializer': Illegal invocation";

        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return null!;
    }

    /// <summary>https://w3c.github.io/DOM-Parsing/#dom-xmlserializer-serializetostring.</summary>
    internal static JsValue SerializeToString(JsValue[] arguments)
    {
        var node = DomBindings.Argument<INode>(arguments, 0, "XMLSerializer.serializeToString");
        return JsString.Create(node.ToHtml(XhtmlMarkupFormatter.Instance));
    }
}
