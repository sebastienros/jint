using AngleSharp;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// The interfaces the runtime owns rather than the generator: <c>DOMParser</c>, <c>XMLSerializer</c>,
/// <c>NodeFilter</c>, <c>Selection</c> and <c>MediaQueryListEvent</c>.
/// </summary>
/// <remarks>
/// <para>
/// None of the five exists in AngleSharp — three of them are host objects a browser supplies rather than DOM
/// objects, <c>NodeFilter</c> is a callback interface with constants and no instances, and
/// <c>MediaQueryListEvent</c> is CSSOM View's. Everything else in this folder is a <em>view</em> onto the DOM
/// that AngleSharp does have and that the generator already emits: <c>Range</c>, <c>TreeWalker</c> and
/// <c>NodeIterator</c> are generated, and only the members whose signatures the conversion table could not
/// cross arrive from <c>overrides.json</c>'s additions.
/// </para>
/// <para>
/// Each global is lazy and non-clobbering, and the shapes are process-shared with the prototypes per engine,
/// which is what every other installer in this package does.
/// </para>
/// </remarks>
internal static class ViewInstaller
{
    private static readonly JsObjectShape _domParser = BuildDomParserShape();
    private static readonly JsObjectShape _xmlSerializer = BuildXmlSerializerShape();
    private static readonly JsObjectShape _selection = BuildSelectionShape();
    private static readonly JsObjectShape _nodeFilter = BuildNodeFilterShape();
    private static readonly JsObjectShape _mediaQueryListEvent = BuildMediaQueryListEventShape();
    private static readonly JsObjectShape _geolocation = BuildGeolocationShape();

    /// <summary>
    /// The configuration a <c>DOMParser</c> document is parsed with: the CSS services, so that
    /// <c>element.style</c> answers on the result, and nothing else — no requester, no scripting.
    /// </summary>
    internal static IConfiguration ParserConfiguration { get; } = Configuration.Default.WithCss();

    /// <summary>Installs the five globals on <paramref name="runtime"/>'s engine. Called once, at construction.</summary>
    internal static void Install(PageRuntime runtime)
    {
        var engine = runtime.Engine;

        Add(engine, "DOMParser", static realm => realm.DomParser);
        Add(engine, "XMLSerializer", static realm => realm.XmlSerializer);
        Add(engine, "Selection", static realm => realm.SelectionInterface);
        Add(engine, "NodeFilter", static realm => realm.NodeFilter);
        Add(engine, "MediaQueryListEvent", static realm => realm.MediaQueryListEvent);
        Add(engine, "Geolocation", static realm => realm.GeolocationInterface);
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

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#the-domparser-interface
    /// </summary>
    private static JsObjectShape BuildDomParserShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("DOMParser")
        .Method("parseFromString", static (t, args) => JsDomParser.Brand(t, "parseFromString").ParseFromString(args), length: 2)
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
        .Accessor("anchorNode", static (t, _) => JsSelection.Brand(t, "anchorNode").AnchorNode)
        .Accessor("anchorOffset", static (t, _) => JsNumber.Create(JsSelection.Brand(t, "anchorOffset").AnchorOffset))
        .Accessor("focusNode", static (t, _) => JsSelection.Brand(t, "focusNode").FocusNode)
        .Accessor("focusOffset", static (t, _) => JsNumber.Create(JsSelection.Brand(t, "focusOffset").FocusOffset))
        .Accessor("isCollapsed", static (t, _) => JsSelection.Brand(t, "isCollapsed").IsCollapsed ? JsBoolean.True : JsBoolean.False)
        .Accessor("rangeCount", static (t, _) => JsNumber.Create(JsSelection.Brand(t, "rangeCount").RangeCount))
        .Accessor("type", static (t, _) => JsString.Create(JsSelection.Brand(t, "type").SelectionType))
        .Method("getRangeAt", static (t, args) => JsSelection.Brand(t, "getRangeAt").GetRangeAt(args), length: 1)
        .Method("addRange", static (t, args) => JsSelection.Brand(t, "addRange").AddRange(args), length: 1)
        .Method("removeRange", static (t, args) => JsSelection.Brand(t, "removeRange").RemoveRange(args), length: 1)
        .Method("removeAllRanges", static (t, _) => JsSelection.Brand(t, "removeAllRanges").RemoveAllRanges())
        .Method("empty", static (t, _) => JsSelection.Brand(t, "empty").RemoveAllRanges())
        .Method("collapse", static (t, args) => JsSelection.Brand(t, "collapse").Collapse(args), length: 1)
        .Method("setPosition", static (t, args) => JsSelection.Brand(t, "setPosition").Collapse(args), length: 1)
        .Method("collapseToStart", static (t, _) => JsSelection.Brand(t, "collapseToStart").CollapseTo(true, "collapseToStart"))
        .Method("collapseToEnd", static (t, _) => JsSelection.Brand(t, "collapseToEnd").CollapseTo(false, "collapseToEnd"))
        .Method("selectAllChildren", static (t, args) => JsSelection.Brand(t, "selectAllChildren").SelectAllChildren(args), length: 1)
        .Method("containsNode", static (t, args) => JsSelection.Brand(t, "containsNode").ContainsNode(args), length: 1)
        .Method("deleteFromDocument", static (t, _) => JsSelection.Brand(t, "deleteFromDocument").DeleteFromDocument())
        .Method("toString", static (t, _) => JsString.Create(JsSelection.Brand(t, "toString").ToString()))
        .Build();

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
        out HostInterfaceObject interfaceObject)
    {
        var realm = engine._mainRealm;
        var prototype = shape.Instantiate(engine, parentPrototype ?? realm.Intrinsics.Object.PrototypeObject);
        interfaceObject = new HostInterfaceObject(engine, realm, name, prototype, length, construct, parentInterface);

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
