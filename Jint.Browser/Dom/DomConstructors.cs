using System.Globalization;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Dom;

/// <summary>
/// Installs the constructible DOM interfaces and validates their WebIDL constructor arguments.
/// </summary>
/// <remarks>
/// Most DOM interfaces are not constructible. The small constructor table supplies the explicit exceptions
/// and creates native objects belonging to the current realm's document.
/// </remarks>
internal static class DomConstructors
{
    // Use the same native factory and exception translation as Document.createProcessingInstruction.
    private static readonly Func<JsValue, JsValue[], JsValue> _processingInstruction =
        DomFailures.Guard("ProcessingInstruction", static (receiver, arguments) =>
        {
            var self = DomBindings.Bind<Document>(receiver, "ProcessingInstruction");
            var target = DomConvert.RequiredText(arguments, 0, "ProcessingInstruction");
            var data = DomConvert.OptionalText(arguments, 1, string.Empty)!;
            return self.Realm.WrapNode(self.Target.CreateProcessingInstruction(target, data));
        });

    /// <summary>
    /// Validates required WebIDL constructor arguments.
    /// </summary>
    internal static int LengthOf(DomInterfaceDefinition definition)
        => ReferenceEquals(definition, DomInterfaces.ProcessingInstruction) ? 1 : definition.ConstructorLength;

    /// <summary>The legacy factory functions installed beside the generated interface objects.</summary>
    internal static readonly DomLegacyFactoryDefinition[] LegacyFactories =
    [
        new("Image", DomInterfaces.HTMLImageElement, 0, ConstructImage),
    ];

    /// <summary>
    /// Builds the instance for a <c>new</c> on <paramref name="definition"/>, or answers
    /// <see langword="false"/> for an interface WebIDL gives no constructor.
    /// </summary>
    internal static bool TryConstruct(DomRealm realm, DomInterfaceDefinition definition, JsValue[] arguments, out ObjectInstance instance)
    {
        if (ReferenceEquals(definition, DomInterfaces.Document))
        {
            var document = NewXmlDocument();
            DomDocumentMetadata.Initialize(document, DomDocumentMetadata.CreatorOrigin(realm));
            instance = (ObjectInstance) realm.Wrap(document, DomInterfaces.Document);
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

        // https://dom.spec.whatwg.org/#dom-processinginstruction-processinginstruction:
        // the target is required, data defaults to empty, and the document is the associated document.
        if (ReferenceEquals(definition, DomInterfaces.ProcessingInstruction))
        {
            instance = (ObjectInstance) _processingInstruction(realm.WrapNode(NodeDocument(realm)), arguments);
            return true;
        }

        if (ReferenceEquals(definition, DomInterfaces.Range))
        {
            instance = (ObjectInstance) realm.Wrap(new DomRange(NodeDocument(realm)));
            return true;
        }

        // https://dom.spec.whatwg.org/#dom-staticrange-staticrange, and the one row here that is not a
        if (ReferenceEquals(definition, DomManualInterfaces.StaticRange))
        {
            instance = DomStaticRange.Construct(realm, arguments);
            return true;
        }

        instance = null!;
        return false;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/embedded-content.html#dom-image.</summary>
    private static DomNodeObject ConstructImage(DomRealm realm, JsValue[] arguments)
    {
        var image = NodeDocument(realm).CreateElementNS(Namespaces.Html, "img");

        if (arguments.Length > 0)
        {
            image.SetAttribute(
                "width",
                DomConvert.RequiredUInt32(arguments, 0, "Image").ToString(CultureInfo.InvariantCulture));
        }

        if (arguments.Length > 1)
        {
            image.SetAttribute(
                "height",
                DomConvert.RequiredUInt32(arguments, 1, "Image").ToString(CultureInfo.InvariantCulture));
        }

        return realm.WrapNode(image);
    }

    /// <summary>The current global object's associated <c>Document</c>, or an empty one when there is none.</summary>
    private static Document NodeDocument(DomRealm realm)
    {
        if (realm.Document is { } document) return document;
        document = NewXmlDocument();
        DomDocumentMetadata.Initialize(document, DomDocumentMetadata.CreatorOrigin(realm));
        return document;
    }

    private static string Data(JsValue[] arguments)
        => DomConvert.OptionalText(arguments, 0, string.Empty)!;

    /// <summary>
    /// Creates an empty native XML document for Document construction and DOMImplementation.createDocument.
    /// </summary>
    /// <param name="contentType">
    /// The content type DOM gives the document. <c>new Document()</c> takes the default, which is DOM §4.5's
    /// own "content type: application/xml"; <c>createDocument</c> derives one from the namespace. See
    /// <see cref="DomContentType"/> for why it cannot simply be set on the document.
    /// </param>
    internal static Document NewXmlDocument(string contentType = DomContentType.Xml)
        => Document.CreateXml(contentType);
}
