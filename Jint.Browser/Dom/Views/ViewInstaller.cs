using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// Installs Browser-owned DOM views and their script-visible shapes.
/// </summary>
/// <remarks>
/// Selection and view operations use native nodes and ranges while Browser owns callbacks, brand checks
/// and the JavaScript-facing interfaces. Traversal filters run on the owning engine thread.
/// </remarks>
internal static class ViewInstaller
{
    private static readonly JsObjectShape _domParser = BuildDomParserShape();
    private static readonly JsObjectShape _xmlSerializer = BuildXmlSerializerShape();
    private static readonly JsObjectShape _selection = BuildSelectionShape();
    private static readonly JsObjectShape _nodeFilter = BuildNodeFilterShape();
    private static readonly JsObjectShape _mediaQueryListEvent = BuildMediaQueryListEventShape();
    private static readonly JsObjectShape _geolocation = BuildGeolocationShape();
    private static readonly JsObjectShape _xPathEvaluator = BuildXPathEvaluatorShape();
    private static readonly JsObjectShape _xPathExpression = BuildXPathExpressionShape();
    private static readonly JsObjectShape _xPathResult = BuildXPathResultShape();
    private static readonly JsObjectShape _cssNamespace = BuildCssNamespaceShape();
    private static readonly JsObjectShape _sanitizer = BuildSanitizerShape();

    /// <summary>Installs the globals on <paramref name="runtime"/>'s engine. Called once, at construction.</summary>
    internal static void Install(PageRuntime runtime)
    {
        var engine = runtime.Engine;

        Add(engine, "DOMParser", static realm => realm.DomParser);
        Add(engine, "XMLSerializer", static realm => realm.XmlSerializer);
        Add(engine, "Selection", static realm => realm.SelectionInterface);
        Add(engine, "NodeFilter", static realm => realm.NodeFilter);
        Add(engine, "MediaQueryListEvent", static realm => realm.MediaQueryListEvent);
        Add(engine, "Geolocation", static realm => realm.GeolocationInterface);
        Add(engine, "XPathEvaluator", static realm => realm.XPathEvaluator);
        Add(engine, "XPathExpression", static realm => realm.XPathExpressionInterface);
        Add(engine, "XPathResult", static realm => realm.XPathResultInterface);
        Add(engine, "CSS", static realm => realm.CssNamespace);
        Add(engine, "Sanitizer", static realm => realm.Sanitizer);
    }

    private static void Add(Engine engine, string name, Func<ViewRealm, JsValue> factory)
        => engine.AddLazyGlobal(
            name,
            factory,
            static (e, f) => f(PageRuntime.Find(e)!.Views),
            PropertyFlag.NonEnumerable);

    internal static JsObjectShape DomParserShape => _domParser;

    internal static JsObjectShape XmlSerializerShape => _xmlSerializer;

    internal static JsObjectShape SelectionShape => _selection;

    internal static JsObjectShape NodeFilterShape => _nodeFilter;

    internal static JsObjectShape MediaQueryListEventShape => _mediaQueryListEvent;

    internal static JsObjectShape GeolocationShape => _geolocation;

    internal static JsObjectShape XPathEvaluatorShape => _xPathEvaluator;

    internal static JsObjectShape XPathExpressionShape => _xPathExpression;

    internal static JsObjectShape XPathResultShape => _xPathResult;

    internal static JsObjectShape CssNamespaceShape => _cssNamespace;

    internal static JsObjectShape SanitizerShape => _sanitizer;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#the-domparser-interface
    /// </summary>
    private static JsObjectShape BuildDomParserShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("DOMParser")
        .Method("parseFromString", static (t, args) => JsDomParser.Brand(t, "parseFromString").ParseFromString(args), length: 2)
        .Build();

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#the-sanitizer-interface</summary>
    private static JsObjectShape BuildSanitizerShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("Sanitizer")
        .Method("get", static (t, _) => JsSanitizer.Brand(t, "get").Get())
        .Method("allowElement", static (t, args) => JsSanitizer.Brand(t, "allowElement").AllowElement(args), length: 1)
        .Method("removeElement", static (t, args) => JsSanitizer.Brand(t, "removeElement").RemoveElement(args), length: 1)
        .Method("replaceElementWithChildren", static (t, args) => JsSanitizer.Brand(t, "replaceElementWithChildren").ReplaceElementWithChildren(args), length: 1)
        .Method("allowProcessingInstruction", static (t, args) => JsSanitizer.Brand(t, "allowProcessingInstruction").AllowProcessingInstruction(args), length: 1)
        .Method("removeProcessingInstruction", static (t, args) => JsSanitizer.Brand(t, "removeProcessingInstruction").RemoveProcessingInstruction(args), length: 1)
        .Method("allowAttribute", static (t, args) => JsSanitizer.Brand(t, "allowAttribute").AllowAttribute(args), length: 1)
        .Method("removeAttribute", static (t, args) => JsSanitizer.Brand(t, "removeAttribute").RemoveAttribute(args), length: 1)
        .Method("setComments", static (t, args) => JsSanitizer.Brand(t, "setComments").SetComments(args), length: 1)
        .Method("setDataAttributes", static (t, args) => JsSanitizer.Brand(t, "setDataAttributes").SetDataAttributes(args), length: 1)
        .Method("setJavascriptURLs", static (t, args) => JsSanitizer.Brand(t, "setJavascriptURLs").SetJavascriptUrls(args), length: 1)
        .Method("removeUnsafe", static (t, _) => JsSanitizer.Brand(t, "removeUnsafe").RemoveUnsafe())
        .Build();

    /// <summary>https://w3c.github.io/DOM-Parsing/#the-xmlserializer-interface</summary>
    private static JsObjectShape BuildXmlSerializerShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("XMLSerializer")
        .Method("serializeToString", static (t, args) =>
        {
            JsXmlSerializer.Brand(t, "serializeToString");
            return JsXmlSerializer.SerializeToString(args);
        }, length: 1)
        .Build();

    /// <summary>https://w3c.github.io/selection-api/#selection-interface</summary>
    private static JsObjectShape BuildSelectionShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("Selection")
        .Accessor("anchorNode", Selection("anchorNode", static (t, _) => JsSelection.Brand(t, "anchorNode").AnchorNode))
        .Accessor("anchorOffset", Selection("anchorOffset", static (t, _) => JsNumber.Create(JsSelection.Brand(t, "anchorOffset").AnchorOffset)))
        .Accessor("focusNode", Selection("focusNode", static (t, _) => JsSelection.Brand(t, "focusNode").FocusNode))
        .Accessor("focusOffset", Selection("focusOffset", static (t, _) => JsNumber.Create(JsSelection.Brand(t, "focusOffset").FocusOffset)))
        .Accessor("isCollapsed", Selection("isCollapsed", static (t, _) => JsSelection.Brand(t, "isCollapsed").IsCollapsed ? JsBoolean.True : JsBoolean.False))
        .Accessor("rangeCount", Selection("rangeCount", static (t, _) => JsNumber.Create(JsSelection.Brand(t, "rangeCount").RangeCount)))
        .Accessor("type", Selection("type", static (t, _) => JsString.Create(JsSelection.Brand(t, "type").SelectionType)))
        .Method("getRangeAt", Selection("getRangeAt", static (t, args) => JsSelection.Brand(t, "getRangeAt").GetRangeAt(args)), length: 1)
        .Method("addRange", Selection("addRange", static (t, args) => JsSelection.Brand(t, "addRange").AddRange(args)), length: 1)
        .Method("removeRange", Selection("removeRange", static (t, args) => JsSelection.Brand(t, "removeRange").RemoveRange(args)), length: 1)
        .Method("removeAllRanges", Selection("removeAllRanges", static (t, _) => JsSelection.Brand(t, "removeAllRanges").RemoveAllRanges()))
        .Method("empty", Selection("empty", static (t, _) => JsSelection.Brand(t, "empty").RemoveAllRanges()))
        .Method("collapse", Selection("collapse", static (t, args) => JsSelection.Brand(t, "collapse").Collapse(args)), length: 1)
        .Method("setPosition", Selection("setPosition", static (t, args) => JsSelection.Brand(t, "setPosition").Collapse(args)), length: 1)
        .Method("collapseToStart", Selection("collapseToStart", static (t, _) => JsSelection.Brand(t, "collapseToStart").CollapseTo(true, "collapseToStart")))
        .Method("collapseToEnd", Selection("collapseToEnd", static (t, _) => JsSelection.Brand(t, "collapseToEnd").CollapseTo(false, "collapseToEnd")))
        .Method("selectAllChildren", Selection("selectAllChildren", static (t, args) => JsSelection.Brand(t, "selectAllChildren").SelectAllChildren(args)), length: 1)
        .Method("containsNode", Selection("containsNode", static (t, args) => JsSelection.Brand(t, "containsNode").ContainsNode(args)), length: 1)
        .Method("deleteFromDocument", Selection("deleteFromDocument", static (t, _) => JsSelection.Brand(t, "deleteFromDocument").DeleteFromDocument()))
        .Method("toString", Selection("toString", static (t, _) => JsString.Create(JsSelection.Brand(t, "toString").ToString())))
        .Build();

    /// <summary>
    /// Wraps a Selection operation so native DOM failures become script-visible DOMException values.
    /// </summary>
    private static Func<JsValue, JsValue[], JsValue> Selection(
        string member,
        Func<JsValue, JsValue[], JsValue> implementation)
        => DomFailures.Guard("Selection." + member, implementation);

    /// <summary>
    /// https://dom.spec.whatwg.org/#interface-nodefilter — a callback interface, so its interface object is
    /// a plain object carrying the constants and nothing callable.
    /// </summary>
    private static JsObjectShape BuildNodeFilterShape() => new JsObjectShape.Builder()
        .ToStringTag("NodeFilter")
        .Constant("FILTER_ACCEPT", JsNumber.Create(NodeFilters.Accept))
        .Constant("FILTER_REJECT", JsNumber.Create(NodeFilters.Reject))
        .Constant("FILTER_SKIP", JsNumber.Create(NodeFilters.Skip))
        .Constant("SHOW_ALL", JsNumber.Create(4294967295))
        .Constant("SHOW_ATTRIBUTE", JsNumber.Create(2))
        .Constant("SHOW_CDATA_SECTION", JsNumber.Create(8))
        .Constant("SHOW_COMMENT", JsNumber.Create(128))
        .Constant("SHOW_DOCUMENT", JsNumber.Create(256))
        .Constant("SHOW_DOCUMENT_FRAGMENT", JsNumber.Create(1024))
        .Constant("SHOW_DOCUMENT_TYPE", JsNumber.Create(512))
        .Constant("SHOW_ELEMENT", JsNumber.Create(1))
        .Constant("SHOW_ENTITY", JsNumber.Create(32))
        .Constant("SHOW_ENTITY_REFERENCE", JsNumber.Create(16))
        .Constant("SHOW_NOTATION", JsNumber.Create(2048))
        .Constant("SHOW_PROCESSING_INSTRUCTION", JsNumber.Create(64))
        .Constant("SHOW_TEXT", JsNumber.Create(4))
        .Build();

    /// <summary>
    /// https://drafts.csswg.org/cssom/#namespacedef-css — a namespace object, so no constructor and no
    /// prototype.
    /// </summary>
    private static JsObjectShape BuildCssNamespaceShape() => new JsObjectShape.Builder()
        .ToStringTag("CSS")
        .Method("escape", static (_, args) => JsCssNamespace.Escape(args), length: 1)
        .PerRealmSlot("supports", static owner =>
        {
            var realm = DomRealm.Of(owner.Engine, owner.CreationRealm);
            return new ClrFunction(owner.Engine, realm.OwningRealm, "supports",
                (_, args) => JsCssNamespace.Supports(realm, args), 1);
        }, enumerable: true)
        .Build();

    /// <summary>https://w3c.github.io/geolocation/#geolocation_interface</summary>
    /// <remarks>
    /// The whole interface, and it is three operations: what a page has no way to observe is that the fix
    /// never moves, so <c>watchPosition</c> delivers once. <see cref="JsGeolocation"/> says why.
    /// </remarks>
    private static JsObjectShape BuildGeolocationShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("Geolocation")
        .Method("getCurrentPosition", static (t, args) => JsGeolocation.Brand(t, "getCurrentPosition").GetCurrentPosition(args), length: 1)
        .Method("watchPosition", static (t, args) => JsGeolocation.Brand(t, "watchPosition").WatchPosition(args), length: 1)
        .Method("clearWatch", static (t, args) => JsGeolocation.Brand(t, "clearWatch").ClearWatch(args), length: 1)
        .Build();

    /// <summary>https://dom.spec.whatwg.org/#interface-xpathevaluator — the three members of the mixin.</summary>
    private static JsObjectShape BuildXPathEvaluatorShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("XPathEvaluator")
        .Method("createExpression", static (t, args) => JsXPathEvaluator.Brand(t, "createExpression").CreateExpression(args), length: 1)
        .Method("createNSResolver", static (t, args) =>
        {
            JsXPathEvaluator.Brand(t, "createNSResolver");
            return XPathEvaluation.CreateNSResolver(args, "XPathEvaluator.createNSResolver");
        }, length: 1)
        .Method("evaluate", static (t, args) => JsXPathEvaluator.Brand(t, "evaluate").Evaluate(args), length: 2)
        .Build();

    /// <summary>https://dom.spec.whatwg.org/#interface-xpathexpression</summary>
    private static JsObjectShape BuildXPathExpressionShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("XPathExpression")
        .Method("evaluate", static (t, args) => JsXPathExpression.Brand(t, "evaluate").Evaluate(args), length: 1)
        .Build();

    /// <summary>https://dom.spec.whatwg.org/#interface-xpathresult</summary>
    private static JsObjectShape BuildXPathResultShape()
    {
        var builder = new JsObjectShape.Builder()
            .PerRealmSlot("constructor")
            .ToStringTag("XPathResult");

        foreach (var (name, value) in XPathEvaluation.ResultConstants)
        {
            builder = builder.Constant(name, JsNumber.Create(value));
        }

        return builder
        .Accessor("resultType", static (t, _) => JsXPathResult.Brand(t, "resultType").ResultType)
        .Accessor("numberValue", static (t, _) => JsXPathResult.Brand(t, "numberValue").NumberValue)
        .Accessor("stringValue", static (t, _) => JsXPathResult.Brand(t, "stringValue").StringValue)
        .Accessor("booleanValue", static (t, _) => JsXPathResult.Brand(t, "booleanValue").BooleanValue)
        .Accessor("singleNodeValue", static (t, _) => JsXPathResult.Brand(t, "singleNodeValue").SingleNodeValue)
        .Accessor("invalidIteratorState", static (t, _) =>
        {
            JsXPathResult.Brand(t, "invalidIteratorState");
            return JsXPathResult.InvalidIteratorState;
        })
        .Accessor("snapshotLength", static (t, _) => JsXPathResult.Brand(t, "snapshotLength").SnapshotLength)
        .Method("iterateNext", static (t, _) => JsXPathResult.Brand(t, "iterateNext").IterateNext())
        .Method("snapshotItem", static (t, args) => JsXPathResult.Brand(t, "snapshotItem").SnapshotItem(args), length: 1)
        .Build();
    }

    /// <summary>https://drafts.csswg.org/cssom-view/#mediaquerylistevent</summary>
    private static JsObjectShape BuildMediaQueryListEventShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("MediaQueryListEvent")
        .Accessor("media", static (t, _) => JsString.Create(JsMediaQueryListEvent.Brand(t, "media").Media))
        .Accessor("matches", static (t, _) => JsMediaQueryListEvent.Brand(t, "matches").Matches ? JsBoolean.True : JsBoolean.False)
        .Build();

    /// <summary>
    /// Builds an interface prototype and its interface object together, filling the per-realm
    /// <c>constructor</c> slot the way every shaped prototype in this package does.
    /// </summary>
    internal static ObjectInstance Instantiate(
        Engine engine,
        JsObjectShape shape,
        string name,
        int length,
        Func<JsValue[], ObjectInstance>? construct,
        ObjectInstance? parentPrototype,
        ObjectInstance? parentInterface,
        out HostInterfaceObject interfaceObject,
        (string Name, int Value)[]? constants = null)
    {
        var realm = engine._mainRealm;
        var prototype = shape.Instantiate(engine, parentPrototype ?? realm.Intrinsics.Object.PrototypeObject);
        interfaceObject = new HostInterfaceObject(engine, realm, name, prototype, length, construct, parentInterface, constants);

        prototype.DefineOwnPropertyUnchecked(
            "constructor",
            new PropertyDescriptor(interfaceObject, PropertyFlag.NonEnumerable));

        return prototype;
    }

    /// <summary>The <c>Event.prototype</c> a <c>MediaQueryListEvent</c> inherits from.</summary>
    internal static ObjectInstance EventPrototype(Engine engine) => engine._mainRealm.Intrinsics.Event.PrototypeObject;

    /// <summary>The <c>Event</c> interface object a <c>MediaQueryListEvent</c>'s own inherits from.</summary>
    internal static ObjectInstance EventInterface(Engine engine) => engine._mainRealm.Intrinsics.Event;

    /// <summary>The timestamp an event the runtime fires carries.</summary>
    internal static double TimeStamp(Engine engine) => EventConstructor.TimeStampNow(engine);
}
