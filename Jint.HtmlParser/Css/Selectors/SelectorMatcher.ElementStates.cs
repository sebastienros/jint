using static Jint.HtmlParser.Css.Selectors.CompiledSelector;

namespace Jint.HtmlParser.Css.Selectors;

internal static partial class SelectorMatcher
{
    private const string XLinkNamespace = "http://www.w3.org/1999/xlink";
    // HTML §4.16.3: https://html.spec.whatwg.org/multipage/semantics-other.html#pseudo-classes
    private static bool MatchElementState(PredicateKind kind, Element element, ref Work work) => kind switch
    {
        PredicateKind.Open => element is { NamespaceUri: Namespaces.Html, LocalName: "details" or "dialog" }
            && StateAttribute(element, "open", null, ref work) is not null,
        PredicateKind.Closed => MatchesLegacyClosed(element, ref work),
        PredicateKind.Link or PredicateKind.AnyLink => MatchesHyperlink(element, ref work),
        // Selectors §8.2 permits treating all hyperlinks as unvisited. There is no history model,
        // and selector answers never expose a host's visited history.
        // https://drafts.csswg.org/selectors/#link
        PredicateKind.Visited => false,
        _ => false
    };

    // Compatibility extension preserving Jint.Browser's established :closed surface.
    // This is not a current Selectors Level 4 pseudo-class, nor the complement of :open.
    private static bool MatchesLegacyClosed(Element element, ref Work work)
    {
        if (element.NamespaceUri != Namespaces.Html) return false;
        switch (element.LocalName)
        {
            case "details":
            case "dialog":
                return StateAttribute(element, "open", null, ref work) is null;
            case "select":
                var metadata = work.Shared.SelectMetadata(element);
                return !metadata.Multiple && (metadata.Size ?? 1) == 1;
            case "input":
                return HtmlInputTypes.Parse(StateAttribute(element, "type", null, ref work)) == HtmlInputType.File;
            default:
                return false;
        }
    }

    // SVG 2 §16.2: nested SVG anchors are inactive below either an HTML or SVG hyperlink.
    // https://svgwg.org/svg2-draft/linking.html#AElement
    private static bool MatchesHyperlink(Element element, ref Work work)
    {
        if (!HasHyperlinkAttribute(element, ref work)) return false;
        if (element.NamespaceUri != Namespaces.Svg) return true;
        for (var parent = element.ParentNode; parent is not null; parent = parent.ParentNode)
        {
            work.Step();
            if (parent is Element ancestor && HasHyperlinkAttribute(ancestor, ref work)) return false;
        }
        return true;
    }

    private static bool HasHyperlinkAttribute(Element element, ref Work work)
    {
        if (element.NamespaceUri == Namespaces.Html && element.LocalName is "a" or "area")
            return StateAttribute(element, "href", null, ref work) is not null;
        if (element.NamespaceUri == Namespaces.Svg && element.LocalName == "a")
            return StateAttribute(element, "href", null, ref work) is not null ||
                   StateAttribute(element, "href", XLinkNamespace, ref work) is not null;
        return false;
    }

    // Attribute namespace/name checks are exact, including on HTML elements in XML documents.
    private static string? StateAttribute(Element element, string name, string? namespaceUri, ref Work work)
    {
        for (uint i = 0; element.GetAttributeAt(i) is { } attribute; i++)
        {
            work.Step();
            if (attribute.NamespaceUri == namespaceUri && attribute.LocalName == name) return attribute.Value;
        }
        return null;
    }
}
