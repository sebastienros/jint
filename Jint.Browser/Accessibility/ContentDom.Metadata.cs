using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.HtmlParser;

namespace Jint.Browser.Accessibility;

internal static partial class ContentDom
{
    // HTML §2.4.3 and §2.6.1: use the Browser's live base URL and WHATWG URL parser.
    internal static string? Url(Element element, string attribute)
    {
        if (element.GetAttributeNS(null, attribute) is not { } value) return null;
        return PageUrl.Resolve(value, DomDocumentState.BaseUri(element.OwnerDocument!)) ?? value;
    }

}
