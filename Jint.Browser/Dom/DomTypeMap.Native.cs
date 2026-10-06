using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>Chooses a Web IDL brand from a native node's kind, namespace and local name.</summary>
/// <remarks>
/// HTML's element interface algorithm (HTML §4.2.1) cannot use a CLR type map: every native
/// element has the same <see cref="Element"/> type. Namespace and local name are immutable, so
/// the choice remains stable when the node is adopted by another document.
/// </remarks>
internal static partial class DomTypeMap
{
    internal static DomInterfaceDefinition For(Node node) => node switch
    {
        Document { Kind: DocumentKind.Html } => DomInterfaces.HTMLDocument,
        Document => DomManualInterfaces.XMLDocument,
        Element element => ForElement(element),
        ShadowRoot => DomInterfaces.ShadowRoot,
        DocumentFragment => DomInterfaces.DocumentFragment,
        DocumentType => DomInterfaces.DocumentType,
        ProcessingInstruction => DomInterfaces.ProcessingInstruction,
        Comment => DomInterfaces.Comment,
        CDataSection => DomManualInterfaces.CDATASection,
        Text => DomInterfaces.Text,
        _ => DomInterfaces.Node,
    };

    private static DomInterfaceDefinition ForElement(Element element)
    {
        if (element.NamespaceUri == Namespaces.Svg)
        {
            return element.LocalName switch
            {
                "a" => DomManualInterfaces.SVGAElement,
                "g" => DomInterfaces.SVGGElement,
                "defs" => DomInterfaces.SVGDefsElement,
                "symbol" => DomInterfaces.SVGSymbolElement,
                "use" => DomInterfaces.SVGUseElement,
                "image" => DomInterfaces.SVGImageElement,
                "switch" => DomInterfaces.SVGSwitchElement,
                "rect" => DomInterfaces.SVGRectElement,
                "circle" => DomInterfaces.SVGCircleElement,
                "ellipse" => DomInterfaces.SVGEllipseElement,
                "line" => DomInterfaces.SVGLineElement,
                "polyline" => DomInterfaces.SVGPolylineElement,
                "polygon" => DomInterfaces.SVGPolygonElement,
                "path" => DomInterfaces.SVGPathElement,
                "text" => DomInterfaces.SVGTextElement,
                "tspan" => DomInterfaces.SVGTSpanElement,
                "textPath" => DomInterfaces.SVGTextPathElement,
                "linearGradient" => DomInterfaces.SVGLinearGradientElement,
                "radialGradient" => DomInterfaces.SVGRadialGradientElement,
                "stop" => DomInterfaces.SVGStopElement,
                "pattern" => DomInterfaces.SVGPatternElement,
                "clipPath" => DomInterfaces.SVGClipPathElement,
                "mask" => DomInterfaces.SVGMaskElement,
                "marker" => DomInterfaces.SVGMarkerElement,
                "metadata" => DomInterfaces.SVGMetadataElement,
                "script" => DomInterfaces.SVGScriptElement,
                "view" => DomInterfaces.SVGViewElement,
                "filter" => DomInterfaces.SVGFilterElement,
                "desc" => DomInterfaces.SVGDescElement,
                "foreignObject" => DomInterfaces.SVGForeignObjectElement,
                "svg" => DomInterfaces.SVGSVGElement,
                "style" => DomInterfaces.SVGStyleElement,
                "title" => DomInterfaces.SVGTitleElement,
                _ => DomInterfaces.SVGElement,
            };
        }

        if (element.NamespaceUri != Namespaces.Html)
        {
            return DomInterfaces.Element;
        }

        // HTML §4.2.1 assigns the interface by local name even in an XML document. SVG
        // retains its case-sensitive local-name matching, including foreignObject above.
        return element.LocalName switch
        {
            "a" => DomInterfaces.HTMLAnchorElement,
            "applet" => DomInterfaces.HTMLUnknownElement,
            "area" => DomInterfaces.HTMLAreaElement,
            "audio" => DomInterfaces.HTMLAudioElement,
            "base" => DomInterfaces.HTMLBaseElement,
            "body" => DomInterfaces.HTMLBodyElement,
            "br" => DomInterfaces.HTMLBRElement,
            "button" => DomInterfaces.HTMLButtonElement,
            "canvas" => DomInterfaces.HTMLCanvasElement,
            "caption" => DomInterfaces.HTMLTableCaptionElement,
            "col" or "colgroup" => DomInterfaces.HTMLTableColElement,
            "command" => DomInterfaces.HTMLCommandElement,
            "data" => DomInterfaces.HTMLDataElement,
            "datalist" => DomInterfaces.HTMLDataListElement,
            "del" or "ins" => DomInterfaces.HTMLModElement,
            "details" => DomInterfaces.HTMLDetailsElement,
            "dialog" => DomInterfaces.HTMLDialogElement,
            "dir" => DomManualInterfaces.HTMLDirectoryElement,
            "div" => DomInterfaces.HTMLDivElement,
            "dl" => DomManualInterfaces.HTMLDListElement,
            "embed" => DomInterfaces.HTMLEmbedElement,
            "fieldset" => DomInterfaces.HTMLFieldSetElement,
            "font" => DomManualInterfaces.HTMLFontElement,
            "form" => DomInterfaces.HTMLFormElement,
            "frame" => DomManualInterfaces.HTMLFrameElement,
            "frameset" => DomManualInterfaces.HTMLFrameSetElement,
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => DomInterfaces.HTMLHeadingElement,
            "head" => DomInterfaces.HTMLHeadElement,
            "hr" => DomInterfaces.HTMLHRElement,
            "html" => DomInterfaces.HTMLHtmlElement,
            "iframe" => DomInterfaces.HTMLIFrameElement,
            "img" => DomInterfaces.HTMLImageElement,
            "input" => DomInterfaces.HTMLInputElement,
            "keygen" => DomInterfaces.HTMLUnknownElement,
            "label" => DomInterfaces.HTMLLabelElement,
            "legend" => DomInterfaces.HTMLLegendElement,
            "li" => DomInterfaces.HTMLLIElement,
            "link" => DomInterfaces.HTMLLinkElement,
            "map" => DomInterfaces.HTMLMapElement,
            "marquee" => DomInterfaces.HTMLMarqueeElement,
            "menu" => DomInterfaces.HTMLMenuElement,
            "menuitem" => DomInterfaces.HTMLMenuItemElement,
            "meta" => DomInterfaces.HTMLMetaElement,
            "meter" => DomInterfaces.HTMLMeterElement,
            "object" => DomInterfaces.HTMLObjectElement,
            "ol" => DomInterfaces.HTMLOListElement,
            "optgroup" => DomInterfaces.HTMLOptGroupElement,
            "option" => DomInterfaces.HTMLOptionElement,
            "output" => DomInterfaces.HTMLOutputElement,
            "p" => DomInterfaces.HTMLParagraphElement,
            "param" => DomInterfaces.HTMLParamElement,
            "picture" => DomInterfaces.HTMLPictureElement,
            "pre" or "listing" or "xmp" => DomInterfaces.HTMLPreElement,
            "progress" => DomInterfaces.HTMLProgressElement,
            "q" or "blockquote" => DomInterfaces.HTMLQuoteElement,
            "script" => DomInterfaces.HTMLScriptElement,
            "select" => DomInterfaces.HTMLSelectElement,
            "slot" => DomInterfaces.HTMLSlotElement,
            "source" => DomInterfaces.HTMLSourceElement,
            "span" => DomInterfaces.HTMLSpanElement,
            "style" => DomInterfaces.HTMLStyleElement,
            "table" => DomInterfaces.HTMLTableElement,
            "tbody" or "tfoot" or "thead" => DomInterfaces.HTMLTableSectionElement,
            "td" or "th" => DomInterfaces.HTMLTableCellElement,
            "template" => DomInterfaces.HTMLTemplateElement,
            "textarea" => DomInterfaces.HTMLTextAreaElement,
            "time" => DomInterfaces.HTMLTimeElement,
            "title" => DomInterfaces.HTMLTitleElement,
            "tr" => DomInterfaces.HTMLTableRowElement,
            "track" => DomInterfaces.HTMLTrackElement,
            "ul" => DomInterfaces.HTMLUListElement,
            "video" => DomInterfaces.HTMLVideoElement,
            "abbr" or "address" or "article" or "aside" or "b" or "bdi" or "bdo" or "cite" or
            "code" or "dd" or "dfn" or "dt" or "em" or "figcaption" or "figure" or "footer" or
            "header" or "hgroup" or "i" or "kbd" or "main" or "mark" or "nav" or "noscript" or
            "rp" or "rt" or "ruby" or "s" or "samp" or "search" or "section" or "small" or
            "strong" or "sub" or "summary" or "sup" or "u" or "var" or "wbr" or "acronym" or
            "big" or "center" or "nobr" or "noembed" or "noframes" or "plaintext" or "strike" or
            "tt" or "basefont" or "rb" or "rtc" => DomInterfaces.HTMLElement,
            _ when CustomElements.CustomElementNames.IsValid(element.LocalName) => DomInterfaces.HTMLElement,
            _ => DomInterfaces.HTMLUnknownElement,
        };
    }
}
