using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Svg.Dom;

namespace Jint.Browser.Dom;

/// <summary>https://dom.spec.whatwg.org/#concept-create-element</summary>
internal sealed class CaseSensitiveSvgFactory(IElementFactory<Document, SvgElement> native) : IElementFactory<Document, SvgElement>
{
    internal static IConfiguration Configure(IConfiguration configuration)
    {
        // Resolve the configured factory through the public service API. The adapter retains only that
        // factory, not the temporary context or a document, and is also used by native cloning.
        using var context = BrowsingContext.New(configuration);
        var factory = context.GetFactory<IElementFactory<Document, SvgElement>>();
        return configuration.WithOnly<IElementFactory<Document, SvgElement>>(new CaseSensitiveSvgFactory(factory));
    }

    public SvgElement Create(Document document, string localName, string? prefix = null, NodeFlags flags = NodeFlags.None)
    {
        var element = native.Create(document, localName, prefix, flags);
        // The native factory's case-insensitive known-name lookup can turn SVG into svg and select
        // SVGSVGElement. A differently cased name has the generic SVGElement interface instead.
        return element.LocalName == localName ? element : new SvgElement(document, localName, prefix, flags);
    }
}
