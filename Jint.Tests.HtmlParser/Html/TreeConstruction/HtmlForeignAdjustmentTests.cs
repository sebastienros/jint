#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    // Finite rows transcribed from WHATWG HTML source
    // 2f441941fc523877bd9d5cd7de3b91a81a00ca2e, §13.2.6.1 and §13.2.6.5.
    [TestCase("altglyph", "altGlyph")]
    [TestCase("altglyphdef", "altGlyphDef")]
    [TestCase("altglyphitem", "altGlyphItem")]
    [TestCase("animatecolor", "animateColor")]
    [TestCase("animatemotion", "animateMotion")]
    [TestCase("animatetransform", "animateTransform")]
    [TestCase("clippath", "clipPath")]
    [TestCase("feblend", "feBlend")]
    [TestCase("fecolormatrix", "feColorMatrix")]
    [TestCase("fecomponenttransfer", "feComponentTransfer")]
    [TestCase("fecomposite", "feComposite")]
    [TestCase("feconvolvematrix", "feConvolveMatrix")]
    [TestCase("fediffuselighting", "feDiffuseLighting")]
    [TestCase("fedisplacementmap", "feDisplacementMap")]
    [TestCase("fedistantlight", "feDistantLight")]
    [TestCase("fedropshadow", "feDropShadow")]
    [TestCase("feflood", "feFlood")]
    [TestCase("fefunca", "feFuncA")]
    [TestCase("fefuncb", "feFuncB")]
    [TestCase("fefuncg", "feFuncG")]
    [TestCase("fefuncr", "feFuncR")]
    [TestCase("fegaussianblur", "feGaussianBlur")]
    [TestCase("feimage", "feImage")]
    [TestCase("femerge", "feMerge")]
    [TestCase("femergenode", "feMergeNode")]
    [TestCase("femorphology", "feMorphology")]
    [TestCase("feoffset", "feOffset")]
    [TestCase("fepointlight", "fePointLight")]
    [TestCase("fespecularlighting", "feSpecularLighting")]
    [TestCase("fespotlight", "feSpotLight")]
    [TestCase("fetile", "feTile")]
    [TestCase("feturbulence", "feTurbulence")]
    [TestCase("foreignobject", "foreignObject")]
    [TestCase("glyphref", "glyphRef")]
    [TestCase("lineargradient", "linearGradient")]
    [TestCase("radialgradient", "radialGradient")]
    [TestCase("textpath", "textPath")]
    public void EverySpecifiedSvgTagAdjustment(string tokenName, string localName)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse($"<svg><{tokenName}/></svg>", quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            var element = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!.FirstChild!;
            element.NamespaceUri.Should().Be(Namespaces.Svg);
            element.LocalName.Should().Be(localName);
        }
    }

    [TestCase("attributename", "attributeName")]
    [TestCase("attributetype", "attributeType")]
    [TestCase("basefrequency", "baseFrequency")]
    [TestCase("baseprofile", "baseProfile")]
    [TestCase("calcmode", "calcMode")]
    [TestCase("clippathunits", "clipPathUnits")]
    [TestCase("diffuseconstant", "diffuseConstant")]
    [TestCase("edgemode", "edgeMode")]
    [TestCase("filterunits", "filterUnits")]
    [TestCase("glyphref", "glyphRef")]
    [TestCase("gradienttransform", "gradientTransform")]
    [TestCase("gradientunits", "gradientUnits")]
    [TestCase("kernelmatrix", "kernelMatrix")]
    [TestCase("kernelunitlength", "kernelUnitLength")]
    [TestCase("keypoints", "keyPoints")]
    [TestCase("keysplines", "keySplines")]
    [TestCase("keytimes", "keyTimes")]
    [TestCase("lengthadjust", "lengthAdjust")]
    [TestCase("limitingconeangle", "limitingConeAngle")]
    [TestCase("markerheight", "markerHeight")]
    [TestCase("markerunits", "markerUnits")]
    [TestCase("markerwidth", "markerWidth")]
    [TestCase("maskcontentunits", "maskContentUnits")]
    [TestCase("maskunits", "maskUnits")]
    [TestCase("numoctaves", "numOctaves")]
    [TestCase("pathlength", "pathLength")]
    [TestCase("patterncontentunits", "patternContentUnits")]
    [TestCase("patterntransform", "patternTransform")]
    [TestCase("patternunits", "patternUnits")]
    [TestCase("pointsatx", "pointsAtX")]
    [TestCase("pointsaty", "pointsAtY")]
    [TestCase("pointsatz", "pointsAtZ")]
    [TestCase("preservealpha", "preserveAlpha")]
    [TestCase("preserveaspectratio", "preserveAspectRatio")]
    [TestCase("primitiveunits", "primitiveUnits")]
    [TestCase("refx", "refX")]
    [TestCase("refy", "refY")]
    [TestCase("repeatcount", "repeatCount")]
    [TestCase("repeatdur", "repeatDur")]
    [TestCase("requiredextensions", "requiredExtensions")]
    [TestCase("requiredfeatures", "requiredFeatures")]
    [TestCase("specularconstant", "specularConstant")]
    [TestCase("specularexponent", "specularExponent")]
    [TestCase("spreadmethod", "spreadMethod")]
    [TestCase("startoffset", "startOffset")]
    [TestCase("stddeviation", "stdDeviation")]
    [TestCase("stitchtiles", "stitchTiles")]
    [TestCase("surfacescale", "surfaceScale")]
    [TestCase("systemlanguage", "systemLanguage")]
    [TestCase("tablevalues", "tableValues")]
    [TestCase("targetx", "targetX")]
    [TestCase("targety", "targetY")]
    [TestCase("textlength", "textLength")]
    [TestCase("viewbox", "viewBox")]
    [TestCase("viewtarget", "viewTarget")]
    [TestCase("xchannelselector", "xChannelSelector")]
    [TestCase("ychannelselector", "yChannelSelector")]
    [TestCase("zoomandpan", "zoomAndPan")]
    public void EverySpecifiedSvgAttributeAdjustmentOnRootsAndChildren(string tokenName, string localName)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse($"<svg {tokenName}=first><g {tokenName}=second /></svg>", quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            var svg = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
            svg.Attributes.Single().LocalName.Should().Be(localName);
            svg.GetAttribute(localName).Should().Be("first");
            ((Element) svg.FirstChild!).GetAttribute(localName).Should().Be("second");
        }
    }

    [TestCase("xlink:actuate", "xlink", "actuate", "http://www.w3.org/1999/xlink")]
    [TestCase("xlink:arcrole", "xlink", "arcrole", "http://www.w3.org/1999/xlink")]
    [TestCase("xlink:href", "xlink", "href", "http://www.w3.org/1999/xlink")]
    [TestCase("xlink:role", "xlink", "role", "http://www.w3.org/1999/xlink")]
    [TestCase("xlink:show", "xlink", "show", "http://www.w3.org/1999/xlink")]
    [TestCase("xlink:title", "xlink", "title", "http://www.w3.org/1999/xlink")]
    [TestCase("xlink:type", "xlink", "type", "http://www.w3.org/1999/xlink")]
    [TestCase("xml:lang", "xml", "lang", Namespaces.Xml)]
    [TestCase("xml:space", "xml", "space", Namespaces.Xml)]
    [TestCase("xmlns", null, "xmlns", Namespaces.Xmlns)]
    [TestCase("xmlns:xlink", "xmlns", "xlink", Namespaces.Xmlns)]
    public void EverySpecifiedForeignNamespaceAdjustment(string tokenName, string? prefix, string localName, string namespaceUri)
    {
        foreach (var root in new[] { "svg", "math" })
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse($"<{root} {tokenName}=v><x {tokenName}=w /></{root}>", quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            var element = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
            foreach (var candidate in new[] { element, (Element) element.FirstChild! })
            {
                var attribute = candidate.Attributes.Single();
                attribute.LocalName.Should().Be(localName);
                attribute.Prefix.Should().Be(prefix);
                attribute.NamespaceUri.Should().Be(namespaceUri);
            }
        }
    }
}
