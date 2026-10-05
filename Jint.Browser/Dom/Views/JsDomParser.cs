using Jint.HtmlParser;
using Jint.HtmlParser.Html;
using Jint.HtmlParser.Serialization;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// <c>DOMParser</c>: markup in, a document out, with nothing in it allowed to run.
/// </summary>
/// <remarks>
/// The native parser produces the same objects the binding projects. Scripting is disabled for
/// HTML parsing and XML syntax failures produce a parsererror document; cancellation and native
/// resource failures remain failures rather than being converted to malformed-XML results.
/// </remarks>
internal sealed class JsDomParser : ObjectInstance
{
    /// <summary>
    /// The namespace a <c>parsererror</c> element is in. It is Mozilla's, which every engine copied and
    /// which the HTML standard now names.
    /// </summary>
    private const string ParserErrorNamespace = "http://www.mozilla.org/newlayout/xml/parsererror.xml";

    private readonly PageRuntime _runtime;
    private readonly DomRealm _realm;

    internal JsDomParser(PageRuntime runtime, ObjectInstance prototype) : base(runtime.Engine)
    {
        _runtime = runtime;
        _realm = runtime.Dom;
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

        // https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-domparser-parsefromstring
        // step 2.2 and step 3.2 both end "set document's content type to type", so the type the caller named
        // is the document's own — AngleSharp's XML parser answers `text/xml` for all four of them.
        //
        // `SupportedType` is a closed WebIDL enumeration: these five and no others, however XML-shaped a
        // sixth may be. `DomContentType.IsXml` is the open rule HTML's *read XML* uses for a document a page
        // navigates to or a frame is served, and it is deliberately not asked here — the two share the
        // constants and not the decision.
        var document = type switch
        {
            DomContentType.Html => ParseHtml(source),
            DomContentType.TextXml or DomContentType.Xml or DomContentType.Xhtml or DomContentType.Svg
                => ParseXml(source, type),
            _ => Unsupported(_runtime.Engine, type),
        };

        return _realm.WrapNode(document);
    }

    private Document ParseHtml(string source)
    {
        var document = CreateDocument(DomContentType.Html);
        var session = new HtmlParserSession(document, new HtmlParseOptions { ScriptingEnabled = false });
        session.AppendInput(source, isFinal: true);
        var cancellationToken = _runtime.Cancellation?.Token ?? CancellationToken.None;
        while (true)
        {
            _runtime.Engine.Constraints.Check();
            var step = session.Drive(4096, cancellationToken);
            if (step.Kind == HtmlParseStepKind.Complete)
            {
                return document;
            }
            if (step.Kind != HtmlParseStepKind.Yielded)
            {
                throw new InvalidOperationException("The native HTML parser could not complete the supplied input: " + step.Kind + ".");
            }
        }
    }

    private Document ParseXml(string source, string type)
    {
        try
        {
            return BrowserXmlParsing.ParseDocument(_runtime.Engine, source, CreateDocument(type), _runtime.Options, _runtime.Cancellation?.Token ?? CancellationToken.None);
        }
        catch (MarkupParseException exception)
        {
            return ErrorDocument(exception.Message, type);
        }
    }

    private Document CreateDocument(string type)
    {
        var document = type == DomContentType.Html ? Document.CreateHtml() : Document.CreateXml(type);
        DomDocumentMetadata.Initialize(document, DomDocumentMetadata.CreatorOrigin(_realm));
        DomDocumentState.Of(document).Url = _realm.Document is { } associated
            ? DomDocumentState.Of(associated).Url : _runtime.DocumentUrl;
        return document;
    }

    /// <summary>The parsererror document HTML's parseFromString algorithm requires on XML syntax failure.</summary>
    private Document ErrorDocument(string message, string type)
    {
        var document = CreateDocument(type);
        var html = document.CreateElementNS(Namespaces.Html, "html");
        var body = document.CreateElementNS(Namespaces.Html, "body");
        var error = document.CreateElementNS(ParserErrorNamespace, "parsererror");
        document.AppendChild(html);
        html.AppendChild(body);
        body.AppendChild(error);
        error.AppendChild(document.CreateTextNode(message));
        return document;
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-enumeration — a value outside the enumeration is a
    /// <c>TypeError</c>, built in the page's own realm.
    /// </summary>
    private static Document Unsupported(Engine engine, string type)
    {
        Throw.TypeError(
            engine._mainRealm,
            "Failed to execute 'parseFromString' on 'DOMParser': The provided value '" + type
            + "' is not a valid enum value of type SupportedType.");
        return null!;
    }


}

/// <summary>
/// <c>XMLSerializer</c>: a node out as XML-shaped markup.
/// </summary>
/// <remarks>The native serializer implements namespace fixup directly over the associated tree.</remarks>
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
        var node = DomBindings.NodeArgument(arguments, 0, "XMLSerializer.serializeToString");
        return JsString.Create(node.Attribute is { } attribute
            ? XmlMarkupSerializer.Serialize(attribute, checkpoint: _ => node.DomRealm.Engine.Constraints.Check(), cancellationToken: node.DomRealm.CancellationToken)
            : XmlMarkupSerializer.Serialize(node.Node!, checkpoint: _ => node.DomRealm.Engine.Constraints.Check(), cancellationToken: node.DomRealm.CancellationToken));
    }
}
