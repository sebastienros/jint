using Jint.HtmlParser;
using Jint.Browser.Dom.Collections;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// Declares Browser-owned WebIDL interfaces that supplement the generated native binding contract.
/// </summary>
/// <remarks>
/// Interface selection preserves HTML, SVG and XML brands. Hand-written shapes provide browser semantics
/// that cannot be expressed as direct native member forwarding; they use the same wrapper cache and realm.
/// </remarks>
internal static class DomManualInterfaces
{
    /// <summary>
    /// <c>HTMLDListElement.compact</c> — the one member
    /// <a href="https://html.spec.whatwg.org/multipage/obsolete.html#HTMLDListElement-partial">HTML §16.3.3</a>
    /// adds to <a href="https://html.spec.whatwg.org/multipage/grouping-content.html#the-dl-element">§4.4.9</a>'s
    /// interface.
    /// </summary>
    private static readonly ReflectedAttribute[] _dListMembers =
    [
        ReflectedAttribute.Boolean("HTMLDListElement.compact", "compact"),
    ];

    /// <summary>
    /// <c>HTMLDirectoryElement.compact</c>
    /// (<a href="https://html.spec.whatwg.org/multipage/obsolete.html#htmldirectoryelement">HTML §16.3.3</a>).
    /// </summary>
    private static readonly ReflectedAttribute[] _directoryMembers =
    [
        ReflectedAttribute.Boolean("HTMLDirectoryElement.compact", "compact"),
    ];

    /// <summary>
    /// <c>HTMLFontElement</c>'s three
    /// (<a href="https://html.spec.whatwg.org/multipage/obsolete.html#htmlfontelement">HTML §16.3.3</a>). Only
    /// <c>color</c> is <c>[LegacyNullToEmptyString]</c>, so <c>font.face = null</c> writes <c>"null"</c> where
    /// <c>font.color = null</c> writes the empty string.
    /// </summary>
    private static readonly ReflectedAttribute[] _fontMembers =
    [
        ReflectedAttribute.Text("HTMLFontElement.color", "color", legacyNullToEmptyString: true),
        ReflectedAttribute.Text("HTMLFontElement.face", "face"),
        ReflectedAttribute.Text("HTMLFontElement.size", "size"),
    ];

    /// <summary>
    /// <c>HTMLFrameElement</c>'s eight reflected members, in the IDL's own order
    /// (<a href="https://html.spec.whatwg.org/multipage/obsolete.html#htmlframeelement">HTML §16.3.3</a>).
    /// <c>src</c> and <c>longDesc</c> are <c>USVString</c>s whose content attribute contains a URL, so both
    /// answer the absolute URL the attribute resolves to; the two margins are
    /// <c>[LegacyNullToEmptyString]</c>, exactly as <c>HTMLIFrameElement</c>'s pair already is.
    /// </summary>
    private static readonly ReflectedAttribute[] _frameMembers =
    [
        ReflectedAttribute.Text("HTMLFrameElement.name", "name"),
        ReflectedAttribute.Text("HTMLFrameElement.scrolling", "scrolling"),
        ReflectedAttribute.Url("HTMLFrameElement.src", "src"),
        ReflectedAttribute.Text("HTMLFrameElement.frameBorder", "frameborder"),
        ReflectedAttribute.Url("HTMLFrameElement.longDesc", "longdesc"),
        ReflectedAttribute.Boolean("HTMLFrameElement.noResize", "noresize"),
        ReflectedAttribute.Text("HTMLFrameElement.marginHeight", "marginheight", legacyNullToEmptyString: true),
        ReflectedAttribute.Text("HTMLFrameElement.marginWidth", "marginwidth", legacyNullToEmptyString: true),
    ];

    /// <summary>
    /// <c>HTMLFrameSetElement</c>'s two
    /// (<a href="https://html.spec.whatwg.org/multipage/obsolete.html#htmlframesetelement">HTML §16.3.3</a>).
    /// Its <c>WindowEventHandlers</c> are on <c>HTMLElement</c> here along with <c>GlobalEventHandlers</c>, so
    /// the shape carries only these.
    /// </summary>
    private static readonly ReflectedAttribute[] _frameSetMembers =
    [
        ReflectedAttribute.Text("HTMLFrameSetElement.cols", "cols"),
        ReflectedAttribute.Text("HTMLFrameSetElement.rows", "rows"),
    ];

    /// <summary>
    /// <c>SVGAElement.rel</c>
    /// (<a href="https://svgwg.org/svg2-draft/linking.html#InterfaceSVGAElement">SVG 2 §16.2</a>), the
    /// content attribute <c>relList</c> reflects. Plain <c>DOMString</c> reflection, the same algorithm
    /// <c>HTMLAnchorElement.rel</c> takes.
    /// </summary>
    private static readonly ReflectedAttribute[] _svgAnchorMembers =
    [
        ReflectedAttribute.Text("SVGAElement.rel", "rel"),
    ];

    /// <summary>https://html.spec.whatwg.org/multipage/obsolete.html#htmldirectoryelement.</summary>
    internal static readonly DomInterfaceDefinition HTMLDirectoryElement =
        ElementInterface("HTMLDirectoryElement", _directoryMembers);

    /// <summary>https://html.spec.whatwg.org/multipage/grouping-content.html#the-dl-element.</summary>
    internal static readonly DomInterfaceDefinition HTMLDListElement =
        ElementInterface("HTMLDListElement", _dListMembers);

    /// <summary>https://html.spec.whatwg.org/multipage/obsolete.html#htmlfontelement.</summary>
    internal static readonly DomInterfaceDefinition HTMLFontElement =
        ElementInterface("HTMLFontElement", _fontMembers);

    /// <summary>https://html.spec.whatwg.org/multipage/obsolete.html#htmlframeelement.</summary>
    internal static readonly DomInterfaceDefinition HTMLFrameElement =
        ElementInterface("HTMLFrameElement", _frameMembers);

    /// <summary>https://html.spec.whatwg.org/multipage/obsolete.html#htmlframesetelement.</summary>
    internal static readonly DomInterfaceDefinition HTMLFrameSetElement =
        ElementInterface("HTMLFrameSetElement", _frameSetMembers);

    /// <summary>
    /// <a href="https://svgwg.org/svg2-draft/linking.html#InterfaceSVGAElement">SVG 2 §16.2</a>'s
    /// <c>SVGAElement</c>, selected by namespace and case-sensitive local name over the native element.
    /// </summary>
    /// <remarks>
    /// Its parent is SVGGraphicsElement. Animated href/target use the same weak, per-realm attribute
    /// identities as generated SVG elements; relList retains the existing DOMTokenList implementation.
    /// </remarks>
    internal static readonly DomInterfaceDefinition SVGAElement = new(
        "SVGAElement",
        typeof(Element),
        SvgAnchorShape,
        DomInterfaces.SVGGraphicsElement,
        rootsAtEventTarget: true,
        hasInterfaceObject: true,
        DomWrapperKind.Node);

    /// <summary>
    /// https://dom.spec.whatwg.org/#xmldocument — selects the XMLDocument interface, while new Document() retains the Document brand.
    /// </summary>
    internal static readonly DomInterfaceDefinition XMLDocument = new(
        "XMLDocument",
        typeof(Document),
        static () => new JsObjectShape.Builder()
            .PerRealmSlot("constructor", enumerable: false)
            .ToStringTag("XMLDocument")
            .Build(),
        DomInterfaces.Document,
        rootsAtEventTarget: true,
        hasInterfaceObject: true,
        DomWrapperKind.Node);

    /// <summary>https://dom.spec.whatwg.org/#interface-cdatasection</summary>
    internal static readonly DomInterfaceDefinition CDATASection = new(
        "CDATASection",
        typeof(CDataSection),
        static () => new JsObjectShape.Builder()
            .PerRealmSlot("constructor", enumerable: false)
            .ToStringTag("CDATASection")
            .Build(),
        DomInterfaces.Text,
        rootsAtEventTarget: true,
        hasInterfaceObject: true,
        DomWrapperKind.Node);

    /// <summary>
    /// https://dom.spec.whatwg.org/#staticrange — declares the immutable range shape backed by DomStaticRange.State.
    /// </summary>
    internal static readonly DomInterfaceDefinition StaticRange = new(
        "StaticRange",
        typeof(DomStaticRange.State),
        DomStaticRange.Shape,
        parent: null,
        rootsAtEventTarget: false,
        hasInterfaceObject: true,
        DomWrapperKind.Object,
        constructorLength: DomStaticRange.ConstructorLength);

    internal static readonly DomInterfaceDefinition RadioNodeList = new(
        "RadioNodeList",
        typeof(DomRadioNodeList),
        DomRadioNodeList.Shape,
        DomInterfaces.NodeList,
        rootsAtEventTarget: false,
        hasInterfaceObject: true,
        DomWrapperKind.Collection,
        collectionAccessor: DomAccessorNodeList.Instance);

    /// <summary>Every manual interface, in index order.</summary>
    internal static readonly DomInterfaceDefinition[] All =
    [
        HTMLDirectoryElement,
        HTMLDListElement,
        HTMLFontElement,
        HTMLFrameElement,
        HTMLFrameSetElement,
        SVGAElement,
        XMLDocument,
        StaticRange,
        RadioNodeList,
        CDATASection,
    ];

    /// <summary>
    /// Continues <c>DomInterfaces</c>' dense numbering, which is what <see cref="DomRealm"/> indexes its
    /// per-engine prototype and interface-object arrays by. It is done here rather than in each declaration
    /// so that adding a row cannot silently collide with one already numbered.
    /// </summary>
    static DomManualInterfaces()
    {
        for (var i = 0; i < All.Length; i++)
        {
            All[i].Index = DomInterfaces.All.Length + i;
        }
    }

    /// <summary>
    /// Declares an HTML element interface over a native Element and its reflected members.
    /// </summary>
    private static DomInterfaceDefinition ElementInterface(string name, ReflectedAttribute[] members)
        => new(
            name,
            typeof(Element),
            () => Shape(name, members),
            DomInterfaces.HTMLElement,
            rootsAtEventTarget: true,
            hasInterfaceObject: true,
            DomWrapperKind.Node);

    /// <summary>Builds one such interface's prototype shape, on the first engine that asks for it.</summary>
    private static JsObjectShape Shape(string name, ReflectedAttribute[] members)
    {
        var builder = new JsObjectShape.Builder()
            .PerRealmSlot("constructor", enumerable: false)
            .ToStringTag(name);

        foreach (var member in members)
        {
            builder.Accessor(MemberNameOf(member), Reflected(member), ReflectedSetter(member));
        }

        return builder.Build();
    }

    /// <summary>
    /// Declares SVGAElement's rel and SameObject relList members over its native rel attribute.
    /// </summary>
    private static JsObjectShape SvgAnchorShape()
    {
        const string RelList = "SVGAElement.relList";

        return new JsObjectShape.Builder()
            .PerRealmSlot("constructor", enumerable: false)
            .ToStringTag("SVGAElement")
            .Accessor("href", DomFailures.Guard("SVGAElement.href", static (thisObject, _) =>
            {
                var self = DomBindings.Bind<Element>(thisObject, "SVGAElement.href");
                return Svg.SvgElements.Animated(self.Realm, self.Target, "href", Svg.SvgValueKind.String);
            }))
            .Accessor("target", DomFailures.Guard("SVGAElement.target", static (thisObject, _) =>
            {
                var self = DomBindings.Bind<Element>(thisObject, "SVGAElement.target");
                return Svg.SvgElements.Animated(self.Realm, self.Target, "target", Svg.SvgValueKind.String);
            }))
            .Accessor(
                MemberNameOf(_svgAnchorMembers[0]),
                Reflected(_svgAnchorMembers[0]),
                ReflectedSetter(_svgAnchorMembers[0]))
            .Accessor(
                "relList",
                DomFailures.Guard(RelList, static (thisObject, _) =>
                {
                    var self = DomBindings.Bind<Element>(thisObject, RelList);
                    return DomTokenListMembers.Project(self.Realm, self.Target, "rel");
                }),
                DomFailures.Guard(RelList, static (thisObject, arguments) =>
                {
                    var self = DomBindings.Bind<Element>(thisObject, RelList);
                    return DomTokenListMembers.PutForwards(self.Realm, self.Target, "rel", arguments);
                }))
            .Build();
    }

    /// <summary>
    /// The IDL attribute's own name, taken from the qualified one the descriptor already carries so that the
    /// property a page reads and the name a refusal prints can never disagree.
    /// </summary>
    private static string MemberNameOf(ReflectedAttribute attribute)
        => attribute.Member[(attribute.Member.LastIndexOf('.') + 1)..];

    /// <summary>
    /// A reflected attribute's getter, wrapped in the same failure guard and the same receiver check every
    /// generated member body is.
    /// </summary>
    /// <remarks>
    /// The receiver is bound as <see cref="Element"/> and not as the element's own type, because there is
    /// no such type to bind: that absence is the whole reason these interfaces are declared by local name. A
    /// receiver of any other interface therefore reaches the descriptor, which is exactly what a
    /// <c>Function.prototype.call</c> onto a <c>&lt;div&gt;</c> does in a browser — the member reads that
    /// element's own content attribute rather than raising.
    /// </remarks>
    private static Func<JsValue, JsValue[], JsValue> Reflected(ReflectedAttribute attribute)
    {
        return DomFailures.Guard(attribute.Member, (thisObject, _) =>
        {
            var self = DomBindings.Bind<Element>(thisObject, attribute.Member);
            return attribute.Get(self.Realm, self.Target);
        });
    }

    /// <summary>The same member's setter.</summary>
    private static Func<JsValue, JsValue[], JsValue> ReflectedSetter(ReflectedAttribute attribute)
        => DomFailures.GuardMutation(attribute.Member, (thisObject, arguments) =>
        {
            var self = DomBindings.Bind<Element>(thisObject, attribute.Member);
            return attribute.Set(self.Realm, self.Target, arguments);
        });

    /// <summary>
    /// The interface a node takes when its CLR type does not decide it, or <see langword="null"/> when
    /// <c>DomTypeMap</c> is the answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three node kinds need an answer here rather than from generated CLR metadata. XML documents have an
    /// unannotated CLR interface; the other two are HTML's rules for the five elements above and for a custom
    /// element name.
    /// <a href="https://html.spec.whatwg.org/multipage/dom.html#htmlunknownelement">The element interface
    /// for a name in the HTML namespace</a> is <c>HTMLElement</c> when the name is a valid custom element
    /// name and <c>HTMLUnknownElement</c> otherwise — so an undefined <c>&lt;my-el&gt;</c> is an
    /// <c>HTMLElement</c>, which is what a page tests before anything is defined and what the element
    /// keeps until an upgrade swaps its wrapper's prototype for the constructor's.
    /// </para>
    /// </remarks>
    internal static DomInterfaceDefinition For(Node node) => DomTypeMap.For(node);
}
