using System;
using System.Collections.Generic;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.6, §13.2.6.5 (2026-09-25).
    // The tokenizer supplies lowercase token names; these tables are the exact
    // case and namespace adjustments required by the HTML tree constructor.
    private readonly HashSet<Element> _annotationXmlHtmlIntegration = new(ReferenceEqualityComparer.Instance);
    private bool _foreignHtmlReprocess;
    private bool _foreignBreakout;
    private int _foreignEndScan = -1;
    private bool _foreignEndStarted;
    private bool _foreignInitialEndComparison;
    private int _foreignNameCursor;
    private int _foreignAttributeIndex;
    private bool _foreignFontBreakout;
    private bool _foreignAnnotationEncoding;

    private static bool IsMathTextIntegrationPoint(Element element) => element.NamespaceUri == Namespaces.MathMl &&
        element.LocalName is "mi" or "mo" or "mn" or "ms" or "mtext";

    private bool IsHtmlIntegrationPoint(Element element) => element.NamespaceUri switch
    {
        Namespaces.Svg => element.LocalName is "foreignObject" or "desc" or "title",
        Namespaces.MathMl => _annotationXmlHtmlIntegration.Contains(element),
        _ => false
    };

    private bool ShouldUseForeignRules(HtmlToken token)
    {
        if (_open.Count == 0 || token.Kind == HtmlTokenKind.EndOfFile) return false;
        var node = AdjustedCurrent;
        if (node.NamespaceUri == Namespaces.Html) return false;
        if (token.Kind == HtmlTokenKind.StartTag)
        {
            if (IsMathTextIntegrationPoint(node) && token.Name is not ("mglyph" or "malignmark")) return false;
            if (node.NamespaceUri == Namespaces.MathMl && node.LocalName == "annotation-xml" && token.Name == "svg") return false;
            if (IsHtmlIntegrationPoint(node)) return false;
        }
        else if (token.Kind == HtmlTokenKind.Text && (IsMathTextIntegrationPoint(node) || IsHtmlIntegrationPoint(node))) return false;
        return true;
    }

    private bool InForeign()
    {
        if (_foreignBreakout)
        {
            if (Current.NamespaceUri != Namespaces.Html && !IsMathTextIntegrationPoint(Current) && !IsHtmlIntegrationPoint(Current))
            {
                Pop();
                return true;
            }
            _foreignBreakout = false;
            _foreignHtmlReprocess = true;
            return true;
        }

        switch (_token.Kind)
        {
            case HtmlTokenKind.Comment: InsertComment(); return false;
            case HtmlTokenKind.ProcessingInstruction: InsertProcessingInstruction(); return false;
            case HtmlTokenKind.Doctype: Error("unexpected-doctype"); return false;
            case HtmlTokenKind.StartTag:
                if (IsForeignBreakout(_token.Name!))
                {
                    Error("html-start-tag-in-foreign-content");
                    _foreignBreakout = true;
                    return true;
                }
                if (!TryInsertForeignTokenElement(AdjustedCurrent.NamespaceUri!)) return true;
                if (_token.SelfClosing)
                {
                    _acknowledgedSelfClosing = true;
                    CompleteForeignScriptOrPop();
                }
                return false;
            case HtmlTokenKind.EndTag:
                if (_token.Name is "br" or "p")
                {
                    Error("html-end-tag-in-foreign-content");
                    _foreignBreakout = true;
                    return true;
                }
                if (IsHtmlElement(Current, "html"))
                {
                    _foreignHtmlReprocess = true;
                    return true;
                }
                if (!_foreignEndStarted)
                {
                    if (Current.NamespaceUri == Namespaces.Svg && Current.LocalName == "script" && _token.Name == "script")
                    {
                        CompleteForeignScriptOrPop();
                        return false;
                    }
                    _foreignEndStarted = true;
                    _foreignInitialEndComparison = true;
                    _foreignEndScan = _open.Count - 1;
                }
                if (_foreignEndScan == 0) { _foreignEndScan = -1; return false; }
                var candidate = _open[_foreignEndScan];
                if (!TryCompareForeignEndName(candidate, out var matches)) return true;
                if (_foreignInitialEndComparison)
                {
                    if (!matches) Error("misnested-foreign-end-tag");
                    _foreignInitialEndComparison = false;
                }
                if (matches)
                {
                    SchedulePopTo(_foreignEndScan, reprocess: false);
                    _foreignEndScan = -1;
                    return false;
                }
                _foreignEndScan--;
                Charge(1);
                if (_open[_foreignEndScan].NamespaceUri == Namespaces.Html)
                {
                    _foreignEndScan = -1;
                    _foreignHtmlReprocess = true;
                }
                return true;
            default: throw new InvalidOperationException("Unexpected foreign token.");
        }
    }

    private void CompleteForeignScriptOrPop()
    {
        if (Current.NamespaceUri == Namespaces.Svg && Current.LocalName == "script" &&
            ScriptRequestsEnabled &&
            _scriptingMode is not (HtmlParserScriptingMode.Inert or HtmlParserScriptingMode.Fragment))
        {
            ClosedScript = Current;
            ClosedScriptIsSvg = true;
        }
        Pop();
    }

    private bool IsForeignBreakout(string name)
    {
        if (name is "b" or "big" or "blockquote" or "body" or "br" or "center" or "code" or "dd" or "div" or
            "dl" or "dt" or "em" or "embed" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "head" or
            "hr" or "i" or "img" or "li" or "listing" or "menu" or "meta" or "nobr" or "ol" or "p" or
            "pre" or "ruby" or "s" or "small" or "span" or "strong" or "strike" or "sub" or "sup" or
            "table" or "tt" or "u" or "ul" or "var") return true;
        return name == "font" && _foreignFontBreakout;
    }

    private bool TryInsertForeignTokenElement(string namespaceUri)
    {
        CheckDepth();
        var name = namespaceUri == Namespaces.Svg ? AdjustSvgTagName(_token.Name!) : _token.Name!;
        var attributes = _preparedAttributes;
        if (attributes is not null)
        {
            var adjusted = false;
            while (_foreignAttributeIndex < attributes.Length)
            {
                if (_remaining <= 0 && adjusted) return false;
                adjusted = true;
                var index = _foreignAttributeIndex++;
                var attribute = attributes[index];
                var localName = namespaceUri switch
                {
                    Namespaces.Svg => AdjustSvgAttributeName(attribute.LocalName),
                    Namespaces.MathMl when attribute.LocalName == "definitionurl" => "definitionURL",
                    _ => attribute.LocalName
                };
                attributes[index] = AdjustForeignAttribute(localName, attribute.Value);
                if (localName == "xmlns" && attribute.Value != namespaceUri ||
                    localName == "xmlns:xlink" && attribute.Value != "http://www.w3.org/1999/xlink")
                    Error("unexpected-namespace-declaration");
                Charge(1);
            }
            if (_foreignAttributeIndex < attributes.Length || adjusted && _remaining <= 0) return false;
        }
        var location = FindAdjustedInsertionLocation();
        var owner = location.Parent as Document ?? location.Parent.OwnerDocument!;
        var element = owner.CreateParsedElement(namespaceUri, name, null, _preparedIsValue);
        if (attributes is { Length: > 0 })
        {
            element.InitializeParsedAttributes(attributes, _cancellationToken);
            Charge(_preparedAttributeWork);
        }
        InsertAt(location, element);
        Push(element);
        if (namespaceUri == Namespaces.MathMl && name == "annotation-xml" && _foreignAnnotationEncoding)
            _annotationXmlHtmlIntegration.Add(element);
        return true;
    }

    private static bool AsciiEquals(string left, string right)
    {
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++)
        {
            var c = left[i];
            if (c is >= 'A' and <= 'Z') c = (char) (c + ('a' - 'A'));
            var d = right[i];
            if (d is >= 'A' and <= 'Z') d = (char) (d + ('a' - 'A'));
            if (c != d) return false;
        }
        return true;
    }

    private bool TryCompareForeignEndName(Element element, out bool matches)
    {
        var name = element.LocalName;
        var tokenName = _token.Name!;
        matches = false;
        if (name.Length != tokenName.Length)
        {
            _foreignNameCursor = 0;
            Charge(1);
            return true;
        }
        var advanced = false;
        while (_foreignNameCursor < name.Length)
        {
            if (_remaining <= 0 && advanced) return false;
            var c = name[_foreignNameCursor];
            if (c is >= 'A' and <= 'Z') c = (char) (c + ('a' - 'A'));
            var equal = c == tokenName[_foreignNameCursor++];
            Charge(1);
            advanced = true;
            if (equal) continue;
            _foreignNameCursor = 0;
            return true;
        }
        _foreignNameCursor = 0;
        matches = true;
        return true;
    }

    private static ParserAttribute AdjustForeignAttribute(string name, string value) => name switch
    {
        "xlink:actuate" => new ParserAttribute("http://www.w3.org/1999/xlink", "actuate", "xlink", value),
        "xlink:arcrole" => new ParserAttribute("http://www.w3.org/1999/xlink", "arcrole", "xlink", value),
        "xlink:href" => new ParserAttribute("http://www.w3.org/1999/xlink", "href", "xlink", value),
        "xlink:role" => new ParserAttribute("http://www.w3.org/1999/xlink", "role", "xlink", value),
        "xlink:show" => new ParserAttribute("http://www.w3.org/1999/xlink", "show", "xlink", value),
        "xlink:title" => new ParserAttribute("http://www.w3.org/1999/xlink", "title", "xlink", value),
        "xlink:type" => new ParserAttribute("http://www.w3.org/1999/xlink", "type", "xlink", value),
        "xml:lang" => new ParserAttribute(Namespaces.Xml, "lang", "xml", value),
        "xml:space" => new ParserAttribute(Namespaces.Xml, "space", "xml", value),
        "xmlns" => new ParserAttribute(Namespaces.Xmlns, "xmlns", null, value),
        "xmlns:xlink" => new ParserAttribute(Namespaces.Xmlns, "xlink", "xmlns", value),
        _ => new ParserAttribute(null, name, null, value)
    };

    private static string AdjustSvgTagName(string name) => name switch
    {
        "altglyph" => "altGlyph",
        "altglyphdef" => "altGlyphDef",
        "altglyphitem" => "altGlyphItem",
        "animatecolor" => "animateColor",
        "animatemotion" => "animateMotion",
        "animatetransform" => "animateTransform",
        "clippath" => "clipPath",
        "feblend" => "feBlend",
        "fecolormatrix" => "feColorMatrix",
        "fecomponenttransfer" => "feComponentTransfer",
        "fecomposite" => "feComposite",
        "feconvolvematrix" => "feConvolveMatrix",
        "fediffuselighting" => "feDiffuseLighting",
        "fedisplacementmap" => "feDisplacementMap",
        "fedistantlight" => "feDistantLight",
        "fedropshadow" => "feDropShadow",
        "feflood" => "feFlood",
        "fefunca" => "feFuncA",
        "fefuncb" => "feFuncB",
        "fefuncg" => "feFuncG",
        "fefuncr" => "feFuncR",
        "fegaussianblur" => "feGaussianBlur",
        "feimage" => "feImage",
        "femerge" => "feMerge",
        "femergenode" => "feMergeNode",
        "femorphology" => "feMorphology",
        "feoffset" => "feOffset",
        "fepointlight" => "fePointLight",
        "fespecularlighting" => "feSpecularLighting",
        "fespotlight" => "feSpotLight",
        "fetile" => "feTile",
        "feturbulence" => "feTurbulence",
        "foreignobject" => "foreignObject",
        "glyphref" => "glyphRef",
        "lineargradient" => "linearGradient",
        "radialgradient" => "radialGradient",
        "textpath" => "textPath",
        _ => name
    };

    private static string AdjustSvgAttributeName(string name) => name switch
    {
        "attributename" => "attributeName",
        "attributetype" => "attributeType",
        "basefrequency" => "baseFrequency",
        "baseprofile" => "baseProfile",
        "calcmode" => "calcMode",
        "clippathunits" => "clipPathUnits",
        "diffuseconstant" => "diffuseConstant",
        "edgemode" => "edgeMode",
        "filterunits" => "filterUnits",
        "glyphref" => "glyphRef",
        "gradienttransform" => "gradientTransform",
        "gradientunits" => "gradientUnits",
        "kernelmatrix" => "kernelMatrix",
        "kernelunitlength" => "kernelUnitLength",
        "keypoints" => "keyPoints",
        "keysplines" => "keySplines",
        "keytimes" => "keyTimes",
        "lengthadjust" => "lengthAdjust",
        "limitingconeangle" => "limitingConeAngle",
        "markerheight" => "markerHeight",
        "markerunits" => "markerUnits",
        "markerwidth" => "markerWidth",
        "maskcontentunits" => "maskContentUnits",
        "maskunits" => "maskUnits",
        "numoctaves" => "numOctaves",
        "pathlength" => "pathLength",
        "patterncontentunits" => "patternContentUnits",
        "patterntransform" => "patternTransform",
        "patternunits" => "patternUnits",
        "pointsatx" => "pointsAtX",
        "pointsaty" => "pointsAtY",
        "pointsatz" => "pointsAtZ",
        "preservealpha" => "preserveAlpha",
        "preserveaspectratio" => "preserveAspectRatio",
        "primitiveunits" => "primitiveUnits",
        "refx" => "refX",
        "refy" => "refY",
        "repeatcount" => "repeatCount",
        "repeatdur" => "repeatDur",
        "requiredextensions" => "requiredExtensions",
        "requiredfeatures" => "requiredFeatures",
        "specularconstant" => "specularConstant",
        "specularexponent" => "specularExponent",
        "spreadmethod" => "spreadMethod",
        "startoffset" => "startOffset",
        "stddeviation" => "stdDeviation",
        "stitchtiles" => "stitchTiles",
        "surfacescale" => "surfaceScale",
        "systemlanguage" => "systemLanguage",
        "tablevalues" => "tableValues",
        "targetx" => "targetX",
        "targety" => "targetY",
        "textlength" => "textLength",
        "viewbox" => "viewBox",
        "viewtarget" => "viewTarget",
        "xchannelselector" => "xChannelSelector",
        "ychannelselector" => "yChannelSelector",
        "zoomandpan" => "zoomAndPan",
        _ => name
    };
}
