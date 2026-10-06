namespace Jint.HtmlParser.Sanitization;

/// <summary>
/// The constants of https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#sanitization-constants.
/// </summary>
/// <remarks>
/// The HTML rows come from each element's own "Sanitization" entry in the HTML Standard (elements
/// categorized <i>Default</i> with their listed attributes; <i>Unsafe</i> for the baseline); the MathML and
/// SVG rows are §8.6.5's tables.
/// </remarks>
internal static class SanitizerBuiltins
{
    internal const string XLinkNamespace = "http://www.w3.org/1999/xlink";

    private static readonly SanitizerConfiguration _safeDefault = BuildSafeDefault();

    /// <summary>
    /// The built-in safe baseline configuration's <c>removeElements</c>: every HTML element categorized
    /// <i>Unsafe</i>, the obsolete <c>frame</c>, and SVG's <c>script</c> and <c>use</c>.
    /// </summary>
    internal static readonly SanitizerName[] SafeBaselineRemoveElements =
    [
        SanitizerName.Html("base"),
        SanitizerName.Html("embed"),
        SanitizerName.Html("frame"),
        SanitizerName.Html("iframe"),
        SanitizerName.Html("object"),
        SanitizerName.Html("script"),
        new("script", Namespaces.Svg),
        new("use", Namespaces.Svg),
    ];

    /// <summary>
    /// Every event handler content attribute an element can carry: HTML's <c>GlobalEventHandlers</c>,
    /// <c>WindowEventHandlers</c> (the <c>&lt;body&gt;</c> and <c>&lt;frameset&gt;</c> forms) and
    /// <c>DocumentAndElementEventHandlers</c>, plus Touch Events' four, which a browser that compiles them
    /// from markup must remove as well.
    /// </summary>
    /// <remarks>
    /// <c>remove unsafe</c> removes each of these as a no-namespace attribute. A host whose event handler
    /// content attributes are a larger set must keep this list a superset of it; Jint.Browser has a test
    /// asserting exactly that.
    /// </remarks>
    internal static readonly string[] EventHandlerContentAttributes =
    [
        "onabort", "onafterprint", "onauxclick", "onbeforeinput", "onbeforematch", "onbeforeprint",
        "onbeforetoggle", "onbeforeunload", "onblur", "oncancel", "oncanplay", "oncanplaythrough", "onchange",
        "onclick", "onclose", "oncommand", "oncontextlost", "oncontextmenu", "oncontextrestored", "oncopy",
        "oncuechange", "oncut", "ondblclick", "ondrag", "ondragend", "ondragenter", "ondragleave", "ondragover",
        "ondragstart", "ondrop", "ondurationchange", "onemptied", "onended", "onerror", "onfocus", "onformdata",
        "onhashchange", "oninput", "oninvalid", "onkeydown", "onkeypress", "onkeyup", "onlanguagechange",
        "onload", "onloadeddata", "onloadedmetadata", "onloadstart", "onmessage", "onmessageerror",
        "onmousedown", "onmouseenter", "onmouseleave", "onmousemove", "onmouseout", "onmouseover", "onmouseup",
        "onoffline", "ononline", "onpagehide", "onpagereveal", "onpageshow", "onpageswap", "onpaste", "onpause",
        "onplay", "onplaying", "onpointercancel", "onpointerdown", "onpointerenter", "onpointerleave",
        "onpointermove", "onpointerout", "onpointerover", "onpointerup", "onpopstate", "onprogress",
        "onratechange", "onrejectionhandled", "onreset", "onresize", "onscroll", "onscrollend",
        "onsecuritypolicyviolation", "onseeked", "onseeking", "onselect", "onselectionchange", "onselectstart",
        "onslotchange", "onstalled", "onstorage", "onsubmit", "onsuspend", "ontimeupdate", "ontoggle",
        "ontouchcancel", "ontouchend", "ontouchmove", "ontouchstart", "ontransitioncancel", "ontransitionend",
        "ontransitionrun", "ontransitionstart", "onunhandledrejection", "onunload", "onvolumechange",
        "onwaiting", "onwheel",
    ];

    private static readonly SanitizerName[] _nonReplaceable =
    [
        SanitizerName.Html("html"),
        new("svg", Namespaces.Svg),
        new("math", Namespaces.MathMl),
    ];

    /// <summary>A fresh copy of the built-in safe default configuration, already canonical.</summary>
    internal static SanitizerConfiguration SafeDefault() => _safeDefault.Clone();

    /// <summary>The built-in non-replaceable elements list.</summary>
    internal static bool IsNonReplaceable(SanitizerName element) => Array.IndexOf(_nonReplaceable, element) >= 0;

    /// <summary>
    /// The built-in navigating URL attributes list: HTML's elements with navigating URL attributes, and SVG
    /// <c>a</c>'s <c>href</c> with no namespace or the XLink namespace.
    /// </summary>
    internal static bool IsNavigatingUrlAttribute(string? elementNamespace, string element, string? attributeNamespace, string attribute)
    {
        if (elementNamespace == Namespaces.Html && attributeNamespace is null)
        {
            return (element, attribute) switch
            {
                ("a", "href") or ("area", "href") or ("form", "action") or ("input", "formaction") or ("button", "formaction") => true,
                _ => false,
            };
        }

        return elementNamespace == Namespaces.Svg && element == "a" && attribute == "href"
            && attributeNamespace is null or XLinkNamespace;
    }

    /// <summary>The built-in animating URL attributes list: SVG's three animation elements' <c>attributeName</c>.</summary>
    internal static bool IsAnimatingUrlAttribute(string? elementNamespace, string element, string? attributeNamespace, string attribute)
        => elementNamespace == Namespaces.Svg && attributeNamespace is null && attribute == "attributeName"
            && element is "animate" or "animateTransform" or "set";

    private static SanitizerConfiguration BuildSafeDefault()
    {
        var elements = new List<SanitizerElementRule>();

        void Html(string attributes, params string[] names)
        {
            foreach (var name in names)
            {
                elements.Add(Rule(SanitizerName.Html(name), attributes));
            }
        }

        Html("", "html", "head", "title", "body", "article", "section", "nav", "aside", "h1", "h2", "h3", "h4",
            "h5", "h6", "hgroup", "header", "footer", "address", "p", "hr", "pre", "ul", "menu", "dl", "dt", "dd",
            "figure", "figcaption", "main", "search", "div", "em", "strong", "small", "s", "cite", "q", "dfn", "abbr",
            "ruby", "rt", "rp", "code", "var", "samp", "kbd", "sub", "sup", "i", "b", "u", "mark", "bdi", "bdo", "span",
            "br", "wbr", "table", "caption", "tbody", "thead", "tfoot", "tr");
        Html("cite", "blockquote");
        Html("reversed start type", "ol");
        Html("value", "li", "data");
        Html("href hreflang type", "a");
        Html("datetime", "time");
        Html("cite datetime", "ins", "del");
        Html("span", "colgroup", "col");
        Html("colspan headers rowspan", "td");
        Html("abbr colspan headers rowspan scope", "th");

        void Foreign(string ns, string name, string attributes) => elements.Add(Rule(new SanitizerName(name, ns), attributes));

        foreach (var name in new[] { "math", "merror", "mfrac", "mi", "mmultiscripts", "mn", "mphantom", "mprescripts",
                     "mroot", "mrow", "ms", "msqrt", "mstyle", "msub", "msubsup", "msup", "mtext", "mtr", "semantics" })
        {
            Foreign(Namespaces.MathMl, name, "");
        }

        Foreign(Namespaces.MathMl, "mo", "fence form largeop lspace maxsize minsize movablelimits rspace separator stretchy symmetric");
        Foreign(Namespaces.MathMl, "mover", "accent");
        Foreign(Namespaces.MathMl, "mpadded", "depth height lspace voffset width");
        Foreign(Namespaces.MathMl, "mspace", "depth height width");
        Foreign(Namespaces.MathMl, "mtable", "");
        Foreign(Namespaces.MathMl, "mtd", "columnspan rowspan");
        Foreign(Namespaces.MathMl, "munder", "accentunder");
        Foreign(Namespaces.MathMl, "munderover", "accent accentunder");

        Foreign(Namespaces.Svg, "a", "href hreflang type");
        Foreign(Namespaces.Svg, "circle", "cx cy pathLength r");
        Foreign(Namespaces.Svg, "defs", "");
        Foreign(Namespaces.Svg, "desc", "");
        Foreign(Namespaces.Svg, "ellipse", "cx cy pathLength rx ry");
        Foreign(Namespaces.Svg, "foreignObject", "height width x y");
        Foreign(Namespaces.Svg, "g", "");
        Foreign(Namespaces.Svg, "line", "pathLength x1 x2 y1 y2");
        Foreign(Namespaces.Svg, "marker", "markerHeight markerUnits markerWidth orient preserveAspectRatio refX refY viewBox");
        Foreign(Namespaces.Svg, "metadata", "");
        Foreign(Namespaces.Svg, "path", "d pathLength");
        Foreign(Namespaces.Svg, "polygon", "pathLength points");
        Foreign(Namespaces.Svg, "polyline", "pathLength points");
        Foreign(Namespaces.Svg, "rect", "height pathLength rx ry width x y");
        Foreign(Namespaces.Svg, "svg", "height preserveAspectRatio viewBox width x y");
        Foreign(Namespaces.Svg, "text", "dx dy lengthAdjust rotate textLength x y");
        Foreign(Namespaces.Svg, "textPath", "lengthAdjust method path side spacing startOffset textLength");
        Foreign(Namespaces.Svg, "title", "");
        Foreign(Namespaces.Svg, "tspan", "dx dy lengthAdjust rotate textLength x y");

        return new SanitizerConfiguration
        {
            Elements = elements,
            ProcessingInstructions = [],
            Attributes = Names("dir lang title alignment-baseline baseline-shift clip-path clip-rule color "
                + "color-interpolation cursor direction display displaystyle dominant-baseline fill fill-opacity "
                + "fill-rule font-family font-size font-size-adjust font-stretch font-style font-variant font-weight "
                + "letter-spacing marker-end marker-mid marker-start mathbackground mathcolor mathsize opacity "
                + "paint-order pointer-events scriptlevel shape-rendering stop-color stop-opacity stroke "
                + "stroke-dasharray stroke-dashoffset stroke-linecap stroke-linejoin stroke-miterlimit stroke-opacity "
                + "stroke-width text-anchor text-decoration text-overflow text-rendering transform transform-origin "
                + "unicode-bidi vector-effect visibility white-space word-spacing writing-mode"),
            Comments = false,
            DataAttributes = false,
            JavascriptUrls = false,
        };
    }

    // An element whose definition lists no attributes still carries an (empty) local allow-list.
    private static SanitizerElementRule Rule(SanitizerName name, string attributes)
        => new(name, attributes: Names(attributes));

    private static List<SanitizerName> Names(string names)
        => [.. names.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(SanitizerName.Attribute)];
}
